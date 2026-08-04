using System.Text;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// Pure formatting/sanitization for the rolling audit logs. The '|' pipe is the field delimiter; every
/// field is sanitized (CR/LF stripped, length-capped) so a crafted user-agent or path can never forge
/// extra lines or shift the columns.
/// </summary>
public static class RequestAuditEntry
{
    private const char Separator = '|';

    /// <summary>Formats a request-audit line: ISO timestamp | client IP | method | path | status | duration ms | user-agent.</summary>
    public static string Format(
        DateTimeOffset timestamp,
        string clientIp,
        string method,
        string path,
        int status,
        long durationMs,
        string userAgent) =>
        string.Join(
            Separator.ToString(),
            Sanitize(timestamp.ToString("O"), int.MaxValue),
            Sanitize(clientIp, 64),
            Sanitize(method, 16),
            Sanitize(path, 512),
            status.ToString(),
            durationMs.ToString(),
            Sanitize(userAgent, 512));

    /// <summary>Formats a staff-login attempt line: ISO timestamp | username | client IP | success(1/0).</summary>
    public static string FormatLogin(
        DateTimeOffset timestamp,
        string username,
        string clientIp,
        bool success) =>
        string.Join(
            Separator.ToString(),
            Sanitize(timestamp.ToString("O"), int.MaxValue),
            Sanitize(username, 128),
            Sanitize(clientIp, 64),
            success ? "1" : "0");

    /// <summary>
    /// Field-level sanitization: replaces CR/LF and the '|' field delimiter with a space, then caps the
    /// length. Applied inside <see cref="Format"/>/<see cref="FormatLogin"/> so no field can inject a
    /// delimiter or forge extra lines. Pass <see cref="int.MaxValue"/> to skip the cap (used for the
    /// timestamp, which is fixed-length anyway).
    /// </summary>
    public static string Sanitize(string value, int maxLength)
    {
        var builder = new StringBuilder(value);
        builder.Replace('\r', ' ');
        builder.Replace('\n', ' ');
        builder.Replace('|', ' ');
        if (builder.Length > maxLength)
        {
            builder.Length = maxLength;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whole-line sanitization for the writer: neutralizes CR/LF only (the delimiters are already safe —
    /// every field went through <see cref="Sanitize"/>), then caps the length. Guards against a caller
    /// handing over a line with an embedded newline without destroying the pipe-separated structure.
    /// </summary>
    public static string SanitizeLine(string value, int maxLength)
    {
        var builder = new StringBuilder(value);
        builder.Replace('\r', ' ');
        builder.Replace('\n', ' ');
        if (builder.Length > maxLength)
        {
            builder.Length = maxLength;
        }

        return builder.ToString();
    }
}
