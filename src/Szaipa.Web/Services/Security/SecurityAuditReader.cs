using Szaipa.Web.Infrastructure;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// File-backed audit reader. Picks the per-day files that intersect the requested range (files are named
/// requests-YYYYMMDD.log / logins-YYYYMMDD.log), reads them with shared read/write so concurrent writes
/// never throw, and stops at the line cap. Malformed lines are skipped.
/// </summary>
public sealed class SecurityAuditReader : ISecurityAuditReader
{
    private const int MaxBytesPerFile = 50 * 1024 * 1024;

    private readonly SecurityMonitoringPaths _paths;

    public SecurityAuditReader(SecurityMonitoringPaths paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<RequestLogLine> ReadRequests(DateTimeOffset sinceUtc, int maxLines = 100_000)
        => ReadRequestsWithMetadata(sinceUtc, maxLines).Lines;

    public RequestAuditReadResult ReadRequestsWithMetadata(DateTimeOffset sinceUtc, int maxLines = 100_000)
    {
        var reasons = new List<string>();
        var files = EnumerateRequestFiles(sinceUtc.UtcDateTime.Date, DateTime.UtcNow.Date, reasons);
        var lines = new List<string>();
        foreach (var file in files)
        {
            if (lines.Count >= maxLines)
            {
                reasons.Add($"Request line limit ({maxLines}) reached.");
                break;
            }

            var read = ReadFileLines(file, maxLines - lines.Count, out var partialReason);
            lines.AddRange(read);
            if (partialReason is not null)
            {
                reasons.Add($"{Path.GetFileName(file)}: {partialReason}");
            }

            if (lines.Count >= maxLines)
            {
                reasons.Add($"Request line limit ({maxLines}) reached.");
                break;
            }
        }

        var parsed = new List<RequestLogLine>(lines.Count);
        foreach (var line in lines)
        {
            var item = AuditLogParser.ParseRequestLine(line);
            if (item is not null)
            {
                parsed.Add(item);
            }
        }

        return new RequestAuditReadResult(parsed, reasons.Count > 0, reasons.Distinct().ToArray());
    }

    public IReadOnlyList<LoginLogLine> ReadLogins(int maxLines = 500)
    {
        // Newest first: read files from newest to oldest until the cap is reached. Within a file the lines
        // are chronological (oldest at the top), so reverse each file's lines to put the newest attempt first.
        var files = EnumerateAuditFiles("logins");
        var lines = new List<string>();
        foreach (var file in files.OrderByDescending(FileDateFromName))
        {
            if (lines.Count >= maxLines)
            {
                break;
            }

            var remaining = maxLines - lines.Count;
            var read = ReadFileLines(file, remaining);
            read.Reverse();
            lines.AddRange(read);
        }

        var parsed = new List<LoginLogLine>(lines.Count);
        foreach (var line in lines)
        {
            var item = AuditLogParser.ParseLoginLine(line);
            if (item is not null)
            {
                parsed.Add(item);
            }
        }

        return parsed;
    }

    private IEnumerable<string> EnumerateAuditFiles(string prefix)
    {
        if (!Directory.Exists(_paths.AuditDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            var pattern = $"{prefix}-*.log";
            return Directory.EnumerateFiles(_paths.AuditDirectory, pattern).Take(400);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private string[] EnumerateRequestFiles(DateTime sinceUtcDate, DateTime todayUtcDate, List<string> partialReasons)
    {
        if (!Directory.Exists(_paths.AuditDirectory)) return Array.Empty<string>();
        try
        {
            var files = Directory.EnumerateFiles(_paths.AuditDirectory, "requests-*.log")
                .Where(path => FileDateFromName(path) >= sinceUtcDate && FileDateFromName(path) <= todayUtcDate)
                .OrderByDescending(FileDateFromName)
                .Take(401)
                .ToArray();
            if (files.Length > 400)
            {
                partialReasons.Add("Request file enumeration limit (400 files) reached.");
                return files[..400].OrderBy(FileDateFromName).ToArray();
            }

            return files.OrderBy(FileDateFromName).ToArray();
        }
        catch (IOException)
        {
            partialReasons.Add("Unable to enumerate request audit files because of an I/O error.");
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            partialReasons.Add("Unable to enumerate request audit files because access was denied.");
            return Array.Empty<string>();
        }
    }

    private List<string> ReadFileLines(string path, int maxLines) => ReadFileLines(path, maxLines, out _);

    private List<string> ReadFileLines(string path, int maxLines, out string? partialReason)
    {
        var result = new List<string>();
        partialReason = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaxBytesPerFile)
            {
                partialReason = $"file exceeds {MaxBytesPerFile} byte read limit";
                return result;
            }

            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                result.Add(line);
                if (result.Count >= maxLines)
                {
                    partialReason = $"file reached line limit ({maxLines})";
                    break;
                }
            }
        }
        catch (IOException)
        {
            partialReason = "I/O error while reading file";
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            partialReason = "access denied while reading file";
            return result;
        }

        return result;
    }

    private static DateTime FileDateFromName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        // "requests-20260804" -> last 8 chars are the yyyyMMdd date.
        if (name.Length >= 8 && DateTime.TryParseExact(
                name[^8..],
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }
}
