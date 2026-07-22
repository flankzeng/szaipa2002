using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// Resolves one canonical host environment before environment-specific configuration is loaded.
/// An explicit command-line value wins; conflicting environment variables fail closed instead of
/// letting Data Protection policy and the actual web host disagree.
/// </summary>
public static class HostEnvironmentNameResolver
{
    public static string Resolve(
        string[] args,
        string? aspNetCoreEnvironment,
        string? dotNetEnvironment)
    {
        ArgumentNullException.ThrowIfNull(args);

        var commandLineEnvironment = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build()[HostDefaults.EnvironmentKey];

        var normalizedCommandLine = Normalize(commandLineEnvironment);
        var normalizedAspNetCore = Normalize(aspNetCoreEnvironment);
        var normalizedDotNet = Normalize(dotNetEnvironment);

        string environmentName;
        if (normalizedCommandLine is not null)
        {
            environmentName = normalizedCommandLine;
        }
        else if (normalizedAspNetCore is not null
            && normalizedDotNet is not null
            && !string.Equals(normalizedAspNetCore, normalizedDotNet, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT conflict. Set one value, or make both values identical.");
        }
        else
        {
            environmentName = normalizedAspNetCore
                ?? normalizedDotNet
                ?? Environments.Production;
        }

        ValidateForConfigurationFile(environmentName);
        return environmentName;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateForConfigurationFile(string environmentName)
    {
        if (environmentName is "." or ".."
            || environmentName.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '.' and not '-' and not '_'))
        {
            throw new InvalidOperationException(
                "The host environment name may contain only ASCII letters, numbers, dots, hyphens, and underscores.");
        }
    }
}
