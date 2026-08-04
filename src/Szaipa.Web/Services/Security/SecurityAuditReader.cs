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
    {
        var lines = ReadLinesInRange("requests", sinceUtc.UtcDateTime.Date, maxLines);
        var parsed = new List<RequestLogLine>(lines.Count);
        foreach (var line in lines)
        {
            var item = AuditLogParser.ParseRequestLine(line);
            if (item is not null)
            {
                parsed.Add(item);
            }
        }

        return parsed;
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

    private IReadOnlyList<string> ReadLinesInRange(string prefix, DateTime fromUtc, int maxLines)
    {
        var files = EnumerateAuditFiles(prefix);
        var lines = new List<string>();
        foreach (var file in files.OrderBy(FileDateFromName))
        {
            if (lines.Count >= maxLines)
            {
                break;
            }

            if (FileDateFromName(file) < fromUtc)
            {
                continue;
            }

            lines.AddRange(ReadFileLines(file, maxLines - lines.Count));
        }

        return lines;
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

    private List<string> ReadFileLines(string path, int maxLines)
    {
        var result = new List<string>();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaxBytesPerFile)
            {
                return result;
            }

            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                result.Add(line);
                if (result.Count >= maxLines)
                {
                    break;
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
