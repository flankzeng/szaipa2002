using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SecurityMonitoringPathResolverTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-security-root-{Guid.NewGuid():N}");

    public SecurityMonitoringPathResolverTests()
    {
        Directory.CreateDirectory(_contentRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private static SecurityMonitoringOptions Options(string? serverRoot = null) => new()
    {
        ServerSecurityRoot = serverRoot ?? string.Empty,
    };

    [Fact]
    public void DevelopmentPrefersCommittedSampleDirectory()
    {
        var sample = Path.Combine(_contentRoot, "SecuritySampleData");
        Directory.CreateDirectory(sample);

        var paths = SecurityMonitoringPathResolver.Resolve(_contentRoot, Options(), isDevelopment: true);

        Assert.Equal(Path.GetFullPath(sample), paths.ServerSecurityRoot);
    }

    [Fact]
    public void DevelopmentFallsBackToAppDataWhenNoSample()
    {
        var paths = SecurityMonitoringPathResolver.Resolve(_contentRoot, Options(), isDevelopment: true);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(_contentRoot, "App_Data", "security")),
            paths.ServerSecurityRoot);
    }

    [Fact]
    public void ProductionRequiresServerSecurityRoot()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityMonitoringPathResolver.Resolve(_contentRoot, Options(), isDevelopment: false));

        Assert.Contains("ServerSecurityRoot is required", exception.Message);
    }

    [Fact]
    public void ProductionRejectsRelativeServerSecurityRoot()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityMonitoringPathResolver.Resolve(
                _contentRoot,
                Options(serverRoot: "relative\\security"),
                isDevelopment: false));

        Assert.Contains("absolute", exception.Message);
    }

    [Fact]
    public void ProductionRejectsRootInsideReleaseDirectory()
    {
        var inside = Path.Combine(_contentRoot, "Security");
        Directory.CreateDirectory(inside);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityMonitoringPathResolver.Resolve(
                _contentRoot,
                Options(serverRoot: inside),
                isDevelopment: false));

        Assert.Contains("outside the application release root", exception.Message);
    }

    [Fact]
    public void ProductionAcceptsExternalAbsoluteRoot()
    {
        var external = Path.Combine(Path.GetTempPath(), $"szaipa-security-ext-{Guid.NewGuid():N}");
        Directory.CreateDirectory(external);

        var paths = SecurityMonitoringPathResolver.Resolve(
            _contentRoot,
            Options(serverRoot: external),
            isDevelopment: false);

        Assert.Equal(Path.GetFullPath(external), paths.ServerSecurityRoot);
    }

    [Fact]
    public void DevelopmentResolvesRelativeRootUnderContentRoot()
    {
        var paths = SecurityMonitoringPathResolver.Resolve(
            _contentRoot,
            Options(serverRoot: "custom/security"),
            isDevelopment: true);

        Assert.Equal(Path.GetFullPath(Path.Combine(_contentRoot, "custom", "security")), paths.ServerSecurityRoot);
    }

    [Fact]
    public void AuditDirectoryDerivesFromServerSecurityRoot()
    {
        var external = Path.Combine(Path.GetTempPath(), $"szaipa-security-ext-{Guid.NewGuid():N}");

        var paths = SecurityMonitoringPathResolver.Resolve(
            _contentRoot,
            Options(serverRoot: external),
            isDevelopment: false);

        Assert.Equal(Path.Combine(external, "audit"), paths.AuditDirectory);
    }

    [Fact]
    public void AuditDirectoryOverrideIsRespected()
    {
        var external = Path.Combine(Path.GetTempPath(), $"szaipa-security-ext-{Guid.NewGuid():N}");
        var audit = Path.Combine(Path.GetTempPath(), $"szaipa-security-audit-{Guid.NewGuid():N}");

        var paths = SecurityMonitoringPathResolver.Resolve(
            _contentRoot,
            new SecurityMonitoringOptions { ServerSecurityRoot = external, AuditDirectory = audit },
            isDevelopment: false);

        Assert.Equal(audit, paths.AuditDirectory);
    }

    [Fact]
    public void QueueDirectoriesDeriveFromServerSecurityRoot()
    {
        var external = Path.Combine(Path.GetTempPath(), $"szaipa-security-ext-{Guid.NewGuid():N}");

        var paths = SecurityMonitoringPathResolver.Resolve(
            _contentRoot,
            Options(serverRoot: external),
            isDevelopment: false);

        Assert.Equal(Path.Combine(external, "unblock_queue"), paths.UnblockQueueDirectory);
        Assert.Equal(Path.Combine(external, "whitelist_edit_queue"), paths.WhitelistEditQueueDirectory);
    }

    [Fact]
    public void PassThroughSettingsArePreserved()
    {
        var external = Path.Combine(Path.GetTempPath(), $"szaipa-security-ext-{Guid.NewGuid():N}");

        var paths = SecurityMonitoringPathResolver.Resolve(
            _contentRoot,
            new SecurityMonitoringOptions
            {
                ServerSecurityRoot = external,
                RequestAuditEnabled = true,
                AuditKeepDays = 7,
                AuditMaxLineLength = 256,
                AuditSkipPaths = new[] { "/healthz" },
            },
            isDevelopment: false);

        Assert.True(paths.RequestAuditEnabled);
        Assert.Equal(7, paths.AuditKeepDays);
        Assert.Equal(256, paths.AuditMaxLineLength);
        Assert.Equal(new[] { "/healthz" }, paths.AuditSkipPaths);
    }
}
