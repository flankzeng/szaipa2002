using System.Text.Json;
using Szaipa.Web.Infrastructure;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// File-backed read repository for the server security tree. Every open uses
/// <see cref="FileShare.ReadWrite"/> + <see cref="FileShare.Delete"/> so a concurrent SYSTEM-task write
/// never causes a sharing violation; every read is byte/line bounded.
/// </summary>
public sealed class ServerSecurityRepository : IServerSecurityRepository
{
    private const long SnapshotMaxBytes = 1 * 1024 * 1024;
    private const long BlockedIpsMaxBytes = 5 * 1024 * 1024;
    private const int BlockedIpsMaxLines = 100_000;
    private const long WhitelistMaxBytes = 1 * 1024 * 1024;
    private const int WhitelistMaxLines = 10_000;
    private const long AlertErrorsMaxBytes = 5 * 1024 * 1024;
    private const int QueueMaxFilesPerDirectory = 500;

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SecurityMonitoringPaths _paths;

    public ServerSecurityRepository(SecurityMonitoringPaths paths)
    {
        _paths = paths;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_paths.ServerSecurityRoot)
        && Directory.Exists(_paths.ServerSecurityRoot);

    public SecuritySnapshot? ReadSnapshot()
    {
        if (!IsConfigured)
        {
            return null;
        }

        var path = Path.Combine(_paths.ServerSecurityRoot, "security_snapshot.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = OpenShared(path);
            if (stream is null || stream.Length > SnapshotMaxBytes)
            {
                return null;
            }

            return JsonSerializer.Deserialize<SecuritySnapshot>(stream, SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public IReadOnlyList<BlockedIp> ReadBlockedIps()
    {
        var result = new List<BlockedIp>();
        if (!IsConfigured)
        {
            return result;
        }

        var path = Path.Combine(_paths.ServerSecurityRoot, "blocked_ips.csv");
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            using var stream = OpenShared(path);
            if (stream is null || stream.Length > BlockedIpsMaxBytes)
            {
                return result;
            }

            using var reader = new StreamReader(stream);
            var lineCount = 0;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (++lineCount > BlockedIpsMaxLines)
                {
                    break;
                }

                var parts = line.Split(',');
                if (parts.Length < 2)
                {
                    continue;
                }

                var ip = parts[0].Trim();
                if (ip.Length == 0 || ip[0] == '#')
                {
                    continue;
                }

                if (long.TryParse(parts[1].Trim(), out var unixSeconds))
                {
                    result.Add(new BlockedIp(ip, DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));
                }
                else
                {
                    result.Add(new BlockedIp(ip, null));
                }
            }
        }
        catch (IOException)
        {
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return result;
        }

        return result;
    }

    public IReadOnlyList<string> ReadWhitelist()
    {
        var result = new List<string>();
        if (!IsConfigured)
        {
            return result;
        }

        var path = Path.Combine(_paths.ServerSecurityRoot, "whitelist.txt");
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            using var stream = OpenShared(path);
            if (stream is null || stream.Length > WhitelistMaxBytes)
            {
                return result;
            }

            using var reader = new StreamReader(stream);
            var lineCount = 0;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (++lineCount > WhitelistMaxLines)
                {
                    break;
                }

                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                result.Add(trimmed);
            }
        }
        catch (IOException)
        {
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return result;
        }

        return result;
    }

    public IReadOnlyList<string> ReadAlertErrors(int maxLines = 100)
    {
        var result = new List<string>();
        if (!IsConfigured)
        {
            return result;
        }

        var path = Path.Combine(_paths.ServerSecurityRoot, "alert_errors.log");
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            using var stream = OpenShared(path);
            if (stream is null || stream.Length > AlertErrorsMaxBytes)
            {
                return result;
            }

            using var reader = new StreamReader(stream);
            var buffer = new Queue<string>();
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                buffer.Enqueue(line);
                if (buffer.Count > maxLines)
                {
                    buffer.Dequeue();
                }
            }

            result.AddRange(buffer);
        }
        catch (IOException)
        {
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return result;
        }

        return result;
    }

    public IReadOnlyList<QueueAction> ReadQueueActions()
    {
        var actions = new List<QueueAction>();

        if (Directory.Exists(_paths.UnblockQueueDirectory))
        {
            actions.AddRange(ReadQueueDirectory(
                _paths.UnblockQueueDirectory,
                QueueAction.KindUnblock,
                parseUnblock: true,
                parseWhitelist: false));
        }

        if (Directory.Exists(_paths.WhitelistEditQueueDirectory))
        {
            actions.AddRange(ReadQueueDirectory(
                _paths.WhitelistEditQueueDirectory,
                QueueAction.KindWhitelistAdd,
                parseUnblock: false,
                parseWhitelist: true));
        }

        return actions
            .OrderByDescending(a => a.ProcessedAtUtc ?? a.RequestedAtUtc ?? string.Empty)
            .ToList();
    }

    private IReadOnlyList<QueueAction> ReadQueueDirectory(
        string directory,
        string kind,
        bool parseUnblock,
        bool parseWhitelist)
    {
        var result = new List<QueueAction>();
        var files = EnumerateQueueFiles(directory);
        if (files is null)
        {
            return result;
        }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var isDone = name.EndsWith(".done.txt", StringComparison.OrdinalIgnoreCase);
            var isPending = name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && !isDone;
            if (!isDone && !isPending)
            {
                continue;
            }

            var body = ReadQueueFileBody(file);
            var parts = (body ?? string.Empty).Split('|');

            if (isDone)
            {
                result.Add(new QueueAction(
                    name,
                    kind,
                    TargetIpFromName(name),
                    QueueAction.StatusProcessed,
                    RequestedAtUtc: null,
                    RequestedBy: null,
                    Result: parts.Length >= 2 ? $"{parts[1]} {string.Join('|', parts.Skip(2))}".Trim() : parts.ElementAtOrDefault(0) ?? string.Empty,
                    ProcessedAtUtc: parts.Length >= 1 ? parts[0] : null));
            }
            else if (parseUnblock)
            {
                result.Add(new QueueAction(
                    name,
                    QueueAction.KindUnblock,
                    TargetIpFromName(name),
                    QueueAction.StatusPending,
                    RequestedAtUtc: parts.Length >= 1 ? parts[0] : null,
                    RequestedBy: parts.Length >= 2 ? parts[1] : null,
                    Result: null,
                    ProcessedAtUtc: null));
            }
            else if (parseWhitelist && parts.Length >= 3)
            {
                result.Add(new QueueAction(
                    name,
                    string.Equals(parts[2], "remove", StringComparison.OrdinalIgnoreCase)
                        ? QueueAction.KindWhitelistRemove
                        : QueueAction.KindWhitelistAdd,
                    parts.Length >= 4 ? parts[3] : null,
                    QueueAction.StatusPending,
                    RequestedAtUtc: parts.Length >= 1 ? parts[0] : null,
                    RequestedBy: parts.Length >= 2 ? parts[1] : null,
                    Result: null,
                    ProcessedAtUtc: null));
            }
        }

        return result;
    }

    private static IEnumerable<string>? EnumerateQueueFiles(string directory)
    {
        try
        {
            return Directory
                .EnumerateFiles(directory, "*.txt")
                .Take(QueueMaxFilesPerDirectory);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadQueueFileBody(string path)
    {
        try
        {
            using var stream = OpenShared(path);
            if (stream is null || stream.Length > 4 * 1024)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadLine();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? TargetIpFromName(string name)
    {
        // Pending: <ip>.txt ; Done: <ip>.done.txt
        var baseName = name.Replace(".done.txt", ".txt", StringComparison.OrdinalIgnoreCase);
        return baseName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : null;
    }

    private static FileStream? OpenShared(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
