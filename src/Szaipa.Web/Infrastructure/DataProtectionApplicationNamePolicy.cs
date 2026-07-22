using Microsoft.Extensions.Hosting;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// Prevents an environment typo from silently changing the cookie isolation discriminator.
/// Production owns the canonical name; side-by-side non-Development sites must use a distinct name.
/// </summary>
public static class DataProtectionApplicationNamePolicy
{
    public static string Resolve(string configuredApplicationName, string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredApplicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var applicationName = configuredApplicationName.Trim();
        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(
                applicationName,
                PersistentDataProtectionOptions.DefaultApplicationName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Production DataProtection:ApplicationName must be exactly "
                    + $"'{PersistentDataProtectionOptions.DefaultApplicationName}'.");
            }
        }
        else if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                applicationName,
                PersistentDataProtectionOptions.DefaultApplicationName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A non-Production deployment must use a distinct DataProtection:ApplicationName "
                + "so it cannot decrypt or mint production Staff cookies.");
        }

        return applicationName;
    }
}
