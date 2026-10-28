using System.Text;
using System.Text.Json;
using Szaipa.Web.Infrastructure;

namespace Szaipa.Web.Services.Traffic;

/// <summary>Bounded daily JSONL storage. Abuse controls are process-local; the log remains useful across restarts.</summary>
public sealed class TrafficEventStore : ITrafficEventStore
{
    private const int MaxDailyBytes = 5 * 1024 * 1024;
    private const int MaxDailyEvents = 50_000;
    private const int MaxRememberedEventIds = MaxDailyEvents * 2;
    private const int MaxRateKeys = 2_000;
    private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);
    private const int RateLimit = 30;

    private readonly object _gate = new();
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private readonly string _directory;
    private readonly int _keepDays;
    private readonly Dictionary<Guid, DateTimeOffset> _eventIds = new();
    private readonly Dictionary<string, (DateTimeOffset Start, int Count)> _rates = new(StringComparer.Ordinal);
    private int _todayCount;
    private DateOnly _countDate;

    public TrafficEventStore(SecurityMonitoringPaths paths)
    {
        _directory = paths.AuditDirectory;
        _keepDays = Math.Clamp(paths.AuditKeepDays, 1, 365);
    }

    public async Task<bool> RecordAsync(TrafficEventRequest request, string clientKey, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (_countDate != today)
            {
                _countDate = today;
                await LoadRecentEventIdsAsync(today.AddDays(-1), now, cancellationToken);
                await LoadRecentEventIdsAsync(today, now, cancellationToken);
                _todayCount = await CountTodayAsync(today, cancellationToken);
                PruneFiles(today);
            }
            lock (_gate)
            {
                PruneMemory(now);
                if (request.EventId == Guid.Empty || _eventIds.ContainsKey(request.EventId) || !AllowRate(clientKey, now)) return false;
                _eventIds[request.EventId] = now;
                if (_eventIds.Count > MaxRememberedEventIds)
                    _eventIds.Remove(_eventIds.MinBy(pair => pair.Value).Key);
            }
            if (_todayCount >= MaxDailyEvents) return false;

            var item = new TrafficEvent(request.EventId, request.Kind, request.PagePath, request.TargetPath, now);
            var line = JsonSerializer.Serialize(item) + "\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            var file = GetFile(today);
            var currentLength = File.Exists(file) ? new FileInfo(file).Length : 0;
            if (currentLength + bytes.Length > MaxDailyBytes) return false;

            try
            {
                Directory.CreateDirectory(_directory);
                await using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read,
                    4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await stream.WriteAsync(bytes, cancellationToken);
                _todayCount++;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        finally { _fileGate.Release(); }
    }

    public async Task<TrafficSummary> ReadSummaryAsync(int days, int limit, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = today.AddDays(-(days - 1));
        var events = new List<TrafficEvent>();
        var truncated = false;
        for (var date = today; date >= since; date = date.AddDays(-1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = GetFile(date);
            if (!File.Exists(file)) continue;
            try
            {
                if (new FileInfo(file).Length > MaxDailyBytes) { truncated = true; continue; }
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                    8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                using var reader = new StreamReader(stream);
                var rawLines = 0;
                while (rawLines < MaxDailyEvents && await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    rawLines++;
                    if (events.Count >= 100_000) { truncated = true; break; }
                    try
                    {
                        var item = JsonSerializer.Deserialize<TrafficEvent>(line);
                        if (item is not null && TrafficEventPolicy.IsValidKind(item.Kind)
                            && TrafficEventPolicy.TryNormalizePath(item.PagePath, out var safePage))
                        {
                            var safeTarget = item.TargetPath is not null
                                && TrafficEventPolicy.TryNormalizePath(item.TargetPath, out var normalizedTarget)
                                ? normalizedTarget
                                : null;
                            events.Add(item with { PagePath = safePage, TargetPath = safeTarget });
                        }
                        else truncated = true;
                    }
                    catch (JsonException) { truncated = true; }
                }
                if (rawLines == MaxDailyEvents && await reader.ReadLineAsync(cancellationToken) is not null) truncated = true;
                if (rawLines >= MaxDailyEvents || new FileInfo(file).Length >= MaxDailyBytes * 0.95) truncated = true;
            }
            catch (IOException) { truncated = true; }
            catch (UnauthorizedAccessException) { truncated = true; }
            if (events.Count >= 100_000) break;
        }
        var top = events.GroupBy(e => e.PagePath, StringComparer.Ordinal)
            .Select(g => new TrafficTopPage(g.Key, g.LongCount(e => e.Kind == "view"), g.LongCount(e => e.Kind == "click")))
            .OrderByDescending(x => x.Views + x.Clicks).ThenBy(x => x.Path, StringComparer.Ordinal).Take(limit).ToArray();
        return new TrafficSummary(events.LongCount(e => e.Kind == "view"), events.LongCount(e => e.Kind == "click"),
            top, events.OrderByDescending(e => e.Timestamp).Take(50).ToArray(), truncated);
    }

    private bool AllowRate(string key, DateTimeOffset now)
    {
        if (!_rates.TryGetValue(key, out var entry) || now - entry.Start >= RateWindow)
        {
            if (!_rates.ContainsKey(key) && _rates.Count >= MaxRateKeys) return false;
            _rates[key] = (now, 1);
            return true;
        }
        if (entry.Count >= RateLimit) return false;
        _rates[key] = (entry.Start, entry.Count + 1);
        return true;
    }

    private void PruneMemory(DateTimeOffset now)
    {
        foreach (var id in _eventIds.Where(x => now - x.Value > DedupWindow).Select(x => x.Key).ToArray()) _eventIds.Remove(id);
        foreach (var key in _rates.Where(x => now - x.Value.Start > RateWindow).Select(x => x.Key).ToArray()) _rates.Remove(key);
    }

    private async Task<int> CountTodayAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var file = GetFile(date);
        if (!File.Exists(file)) return 0;
        var count = 0;
        try
        {
            if (new FileInfo(file).Length > MaxDailyBytes) return MaxDailyEvents;
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(stream);
            while (count < MaxDailyEvents && await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                count++;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return count;
    }

    private async Task LoadRecentEventIdsAsync(DateOnly date, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var file = GetFile(date);
        if (!File.Exists(file)) return;
        try
        {
            if (new FileInfo(file).Length > MaxDailyBytes) return;
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(stream);
            var lines = 0;
            while (lines++ < MaxDailyEvents && await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                try
                {
                    var item = JsonSerializer.Deserialize<TrafficEvent>(line);
                    if (item is not null && item.Timestamp >= now.Subtract(DedupWindow))
                        RememberEventId(item.EventId, item.Timestamp);
                }
                catch (JsonException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void RememberEventId(Guid eventId, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            if (_eventIds.Count >= MaxRememberedEventIds && !_eventIds.ContainsKey(eventId))
                _eventIds.Remove(_eventIds.MinBy(pair => pair.Value).Key);
            _eventIds[eventId] = timestamp;
        }
    }

    private void PruneFiles(DateOnly today)
    {
        try
        {
            if (!Directory.Exists(_directory)) return;
            var cutoff = today.AddDays(-_keepDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "traffic-????????.jsonl"))
                if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file).AsSpan("traffic-".Length), "yyyyMMdd", out var date) && date < cutoff)
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string GetFile(DateOnly date) => Path.Combine(_directory, $"traffic-{date:yyyyMMdd}.jsonl");
}
