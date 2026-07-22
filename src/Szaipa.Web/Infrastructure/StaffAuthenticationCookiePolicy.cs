using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Szaipa.Web.Infrastructure;

public sealed record StaffAuthenticationCookieSettings(
    string Name,
    CookieSecurePolicy SecurePolicy);

/// <summary>
/// Keeps staging/development cookies from overwriting the production Staff cookie. Cookie scope does
/// not include the TCP port, so deployment still requires a dedicated hostname for every test site.
/// </summary>
public static class StaffAuthenticationCookiePolicy
{
    public const string ProductionCookieName = "Szaipa.Admin";

    public static StaffAuthenticationCookieSettings Resolve(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            return new StaffAuthenticationCookieSettings(
                ProductionCookieName,
                CookieSecurePolicy.Always);
        }

        var normalizedEnvironmentName = environmentName.Trim();
        return new StaffAuthenticationCookieSettings(
            $"{ProductionCookieName}.{normalizedEnvironmentName}",
            string.Equals(normalizedEnvironmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always);
    }
}
