using System.Net;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// Strict IPv4 validation/normalization for security actions. IPv4 only: IPv6 addresses contain ':',
/// which is invalid in Windows file names (queue files are named after the IP) and the firewall rules
/// are IPv4-based. Also blocks trailing junk, whitespace, relative-path tricks and CSV metacharacters.
/// </summary>
public static class SecurityIpPolicy
{
    /// <summary>
    /// Returns the normalized IPv4 string, or <c>null</c> when the input is not a plain IPv4 address.
    /// </summary>
    public static string? NormalizeIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        // Reject anything that is not a single dotted-quad address (defeats "..", commas, colons, spaces
        // hidden inside, and IPv6). IPAddress.TryParse would accept forms like "1" or hex dotted quads.
        if (!IsPlainIpv4(trimmed))
        {
            return null;
        }

        if (!IPAddress.TryParse(trimmed, out var parsed))
        {
            return null;
        }

        if (parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return null;
        }

        var normalized = parsed.ToString();
        // Round-trip check: the canonical form must equal the trimmed input so we never accept "203.0.113.5 ",
        // leading zeros, or address-family aliasing.
        return string.Equals(trimmed, normalized, StringComparison.Ordinal) ? normalized : null;
    }

    private static bool IsPlainIpv4(string candidate)
    {
        if (candidate.Length is < 7 or > 15)
        {
            return false;
        }

        var segments = candidate.Split('.');
        if (segments.Length != 4)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length is < 1 or > 3)
            {
                return false;
            }

            foreach (var ch in segment)
            {
                if (ch is < '0' or > '9')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
