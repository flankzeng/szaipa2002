namespace Szaipa.Web.Services.Security;

/// <summary>
/// Queues staff-issued security actions for the privileged SYSTEM scheduled task to apply. The web app has
/// no elevation and never runs netsh or edits the server files directly — it only drops a request file.
/// </summary>
public interface ISecurityActionQueue
{
    /// <summary>Queues an unblock for <paramref name="ip"/> (writes &lt;ip&gt;.txt). False when the directory is unwritable.</summary>
    bool WriteUnblock(string ip, string requestedBy, DateTimeOffset requestedAt);

    /// <summary>Queues a whitelist add/remove (writes &lt;guid&gt;.txt). False when the directory is unwritable.</summary>
    bool WriteWhitelistEdit(string ip, bool add, string requestedBy, DateTimeOffset requestedAt);
}
