namespace Szaipa.Web.Services.Security;

/// <summary>One parsed request-audit line. Any field may be null when malformed.</summary>
public sealed record RequestLogLine(
    DateTimeOffset? Timestamp,
    string? Ip,
    string? Method,
    string? Path,
    int? Status,
    long? DurationMs,
    string? UserAgent);

/// <summary>One parsed staff-login attempt line.</summary>
public sealed record LoginLogLine(
    DateTimeOffset? Timestamp,
    string? Username,
    string? Ip,
    bool? Success);

/// <summary>
/// Pure line parsers for the rolling audit files. Malformed lines return <c>null</c> and are skipped by
/// callers. Kept static so parsing is unit-testable without file I/O.
/// </summary>
public static class AuditLogParser
{
    public static RequestLogLine? ParseRequestLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split('|');
        if (parts.Length < 6)
        {
            return null;
        }

        return new RequestLogLine(
            TryParseTimestamp(parts[0]),
            EmptyToNull(parts[1]),
            EmptyToNull(parts[2]),
            EmptyToNull(parts[3]),
            TryParseInt(parts[4]),
            TryParseLong(parts[5]),
            parts.Length > 6 ? parts[6] : null);
    }

    public static LoginLogLine? ParseLoginLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split('|');
        if (parts.Length < 4)
        {
            return null;
        }

        return new LoginLogLine(
            TryParseTimestamp(parts[0]),
            EmptyToNull(parts[1]),
            EmptyToNull(parts[2]),
            parts[3] == "1" ? true : parts[3] == "0" ? false : null);
    }

    private static DateTimeOffset? TryParseTimestamp(string value) =>
        DateTimeOffset.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static int? TryParseInt(string value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static long? TryParseLong(string value) =>
        long.TryParse(value, out var parsed) ? parsed : null;

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
