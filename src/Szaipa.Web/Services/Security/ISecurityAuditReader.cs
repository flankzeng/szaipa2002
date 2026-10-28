namespace Szaipa.Web.Services.Security;

/// <summary>
/// Read-only access to the rolling audit files written by <see cref="ISecurityAuditWriter"/>. Bounded reads
/// (date-range file selection + a total line cap) and tolerant of missing directories/files.
/// </summary>
public interface ISecurityAuditReader
{
    /// <summary>Request lines from files on/after <paramref name="sinceUtc"/>, oldest first, up to <paramref name="maxLines"/>.</summary>
    IReadOnlyList<RequestLogLine> ReadRequests(DateTimeOffset sinceUtc, int maxLines = 100_000);

    /// <summary>Request lines plus explicit coverage information for bounded or failed reads.</summary>
    RequestAuditReadResult ReadRequestsWithMetadata(DateTimeOffset sinceUtc, int maxLines = 100_000)
    {
        var lines = ReadRequests(sinceUtc, maxLines);
        var atLimit = lines.Count >= maxLines;
        return new(lines, atLimit, atLimit
            ? new[] { $"Request read limit ({maxLines}) may have been reached; this reader does not report file-level coverage." }
            : Array.Empty<string>());
    }

    /// <summary>Login-attempt lines, newest first, up to <paramref name="maxLines"/>.</summary>
    IReadOnlyList<LoginLogLine> ReadLogins(int maxLines = 500);
}

public sealed record RequestAuditReadResult(
    IReadOnlyList<RequestLogLine> Lines,
    bool IsPartial,
    IReadOnlyList<string> PartialReasons);
