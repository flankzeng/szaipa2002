using System.Text;
using Szaipa.Web.Infrastructure;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// Writes one-line request files into the queue directories for the SYSTEM task to consume.
/// File names are either a normalized IPv4 (unblock) or a GUID (whitelist edit); the body is
/// pipe-delimited and never contains untrusted text beyond the already-validated IP.
/// </summary>
public sealed class SecurityActionQueue : ISecurityActionQueue
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly SecurityMonitoringPaths _paths;

    public SecurityActionQueue(SecurityMonitoringPaths paths)
    {
        _paths = paths;
    }

    public bool WriteUnblock(string ip, string requestedBy, DateTimeOffset requestedAt)
    {
        var directory = _paths.UnblockQueueDirectory;
        var fileName = $"{ip}.txt";
        var body = $"{requestedAt:O}|{Sanitize(requestedBy)}";
        return WriteRequest(directory, fileName, body);
    }

    public bool WriteWhitelistEdit(string ip, bool add, string requestedBy, DateTimeOffset requestedAt)
    {
        var directory = _paths.WhitelistEditQueueDirectory;
        var fileName = $"{Guid.NewGuid():N}.txt";
        var action = add ? "add" : "remove";
        var body = $"{requestedAt:O}|{Sanitize(requestedBy)}|{action}|{ip}";
        return WriteRequest(directory, fileName, body);
    }

    private static bool WriteRequest(string directory, string fileName, string body)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, body + Environment.NewLine, Utf8NoBom);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Sanitize(string value) =>
        value.Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
}
