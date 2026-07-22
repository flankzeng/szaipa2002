using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class DataProtectionDeploymentContractTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-data-protection-{Guid.NewGuid():N}");

    [Fact]
    public void ApplicationNameHasStableDefault()
    {
        var options = new PersistentDataProtectionOptions();

        Assert.Equal("Szaipa.Web", options.EffectiveApplicationName);
    }

    [Fact]
    public void BlankApplicationNameFallsBackToStableDefault()
    {
        var options = new PersistentDataProtectionOptions { ApplicationName = "  " };

        Assert.Equal("Szaipa.Web", options.EffectiveApplicationName);
    }

    [Fact]
    public void ProductionAcceptsOnlyCanonicalApplicationName()
    {
        var result = DataProtectionApplicationNamePolicy.Resolve(
            "Szaipa.Web",
            Environments.Production);

        Assert.Equal("Szaipa.Web", result);
    }

    [Fact]
    public void ProductionRejectsChangedApplicationName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionApplicationNamePolicy.Resolve(
                "Szaipa.Weeb",
                Environments.Production));

        Assert.Contains("must be exactly", exception.Message);
    }

    [Fact]
    public void StagingRejectsProductionCookieScope()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionApplicationNamePolicy.Resolve(
                "Szaipa.Web",
                Environments.Staging));

        Assert.Contains("distinct", exception.Message);
    }

    [Fact]
    public void StagingAcceptsIsolatedCookieScope()
    {
        var result = DataProtectionApplicationNamePolicy.Resolve(
            "Szaipa.Web.Test",
            Environments.Staging);

        Assert.Equal("Szaipa.Web.Test", result);
    }

    [Fact]
    public void DevelopmentWithoutConfiguredPathUsesProjectLocalFallback()
    {
        var contentRoot = Path.Combine(_root, "development-release");

        var result = DataProtectionKeyPathResolver.Resolve(
            contentRoot,
            configuredKeysPath: string.Empty,
            isDevelopment: true);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "DataProtection-Keys")),
            result);
    }

    [Fact]
    public void ProductionWithoutConfiguredPathFailsClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyPathResolver.Resolve(
                Path.Combine(_root, "release"),
                configuredKeysPath: string.Empty,
                isDevelopment: false));

        Assert.Contains("DataProtection:KeysPath is required", exception.Message);
    }

    [Fact]
    public void ProductionRejectsRelativePath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyPathResolver.Resolve(
                Path.Combine(_root, "release"),
                configuredKeysPath: Path.Combine("shared", "keys"),
                isDevelopment: false));

        Assert.Contains("must be an absolute path", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("keys")]
    [InlineData("App_Data/DataProtection-Keys")]
    public void ProductionRejectsReleaseRootAndDescendants(string relativePath)
    {
        var contentRoot = Path.Combine(_root, "releases", "commit-a");
        var configuredPath = Path.GetFullPath(Path.Combine(contentRoot, relativePath));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyPathResolver.Resolve(
                contentRoot,
                configuredPath,
                isDevelopment: false));

        Assert.Contains("outside the application release root", exception.Message);
    }

    [Fact]
    public void ProductionAcceptsExternalAbsolutePath()
    {
        var contentRoot = Path.Combine(_root, "releases", "commit-a");
        var externalKeysPath = Path.Combine(_root, "persistent", "keys");

        var result = DataProtectionKeyPathResolver.Resolve(
            contentRoot,
            externalKeysPath,
            isDevelopment: false);

        Assert.Equal(Path.GetFullPath(externalKeysPath), result);
    }

    [Fact]
    public void DevelopmentDoesNotRequireWindowsDpapi()
    {
        var result = DataProtectionKeyProtectionPolicy.ShouldUseMachineDpapi(
            isDevelopment: true,
            isWindows: false);

        Assert.False(result);
    }

    [Fact]
    public void ProductionRequiresWindowsForDpapiProtection()
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(() =>
            DataProtectionKeyProtectionPolicy.ShouldUseMachineDpapi(
                isDevelopment: false,
                isWindows: false));

        Assert.Contains("Windows DPAPI", exception.Message);
    }

    [Fact]
    public void ProductionWindowsUsesMachineDpapiProtection()
    {
        var result = DataProtectionKeyProtectionPolicy.ShouldUseMachineDpapi(
            isDevelopment: false,
            isWindows: true);

        Assert.True(result);
    }

    [Fact]
    public void TwoReleasesWithSameKeysAndApplicationNameCanUnprotectPayload()
    {
        var externalKeysPath = Path.Combine(_root, "persistent", "keys");
        var options = new PersistentDataProtectionOptions { KeysPath = externalKeysPath };
        var firstKeysPath = DataProtectionKeyPathResolver.Resolve(
            Path.Combine(_root, "releases", "commit-a"),
            options.KeysPath,
            isDevelopment: false);
        var secondKeysPath = DataProtectionKeyPathResolver.Resolve(
            Path.Combine(_root, "releases", "commit-b"),
            options.KeysPath,
            isDevelopment: false);

        using var firstServices = BuildServices(firstKeysPath, options.EffectiveApplicationName);
        var protectedPayload = firstServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("deployment-contract")
            .Protect("staff-session");

        using var secondServices = BuildServices(secondKeysPath, options.EffectiveApplicationName);
        var unprotectedPayload = secondServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("deployment-contract")
            .Unprotect(protectedPayload);

        Assert.Equal("staff-session", unprotectedPayload);
    }

    [Fact]
    public void DifferentApplicationNameCannotUnprotectPayload()
    {
        var externalKeysPath = Path.Combine(_root, "persistent", "keys");
        using var firstServices = BuildServices(externalKeysPath, "Szaipa.Web");
        var protectedPayload = firstServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("deployment-contract")
            .Protect("staff-session");

        using var secondServices = BuildServices(externalKeysPath, "Different.Application");
        var secondProtector = secondServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("deployment-contract");

        Assert.Throws<CryptographicException>(() => secondProtector.Unprotect(protectedPayload));
    }

    private static ServiceProvider BuildServices(string keysPath, string applicationName)
    {
        var services = new ServiceCollection();
        services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
