namespace Szaipa.Web.Services.Security;

/// <summary>
/// Writes the rolling audit files (requests-*.log / logins-*.log) shared by the request-audit middleware
/// and the staff-login audit hook. No-op when <c>SecurityMonitoring:RequestAuditEnabled</c> is false.
/// </summary>
public interface ISecurityAuditWriter
{
    /// <summary>Appends a pre-formatted request line (see <see cref="RequestAuditEntry.Format"/>).</summary>
    void WriteRequest(string formattedLine);

    /// <summary>Appends a staff-login attempt (success or failure).</summary>
    void WriteLoginAttempt(string username, string clientIp, bool success);
}
