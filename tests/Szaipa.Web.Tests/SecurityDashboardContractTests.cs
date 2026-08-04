using Xunit;

namespace Szaipa.Web.Tests;

/// <summary>
/// Deployment-contract tests for the security-monitoring dashboard: the pieces that must keep matching
/// each other (nav link, esbuild bundle, sample data, production config template) are asserted against the
/// committed source so a drift fails the build instead of silently breaking the page.
/// </summary>
public sealed class SecurityDashboardContractTests
{
    private static string WebProject =>
        Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web");

    [Fact]
    public void StaffLayoutHasSecurityMonitoringNavLink()
    {
        var layout = File.ReadAllText(
            Path.Combine(WebProject, "Areas", "Staff", "Views", "Shared", "_StaffLayout.cshtml"));

        Assert.Contains("asp-controller=\"SecurityMonitoring\"", layout, StringComparison.Ordinal);
        Assert.Contains("安全监控", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardViewRenders()
    {
        var view = File.ReadAllText(
            Path.Combine(WebProject, "Areas", "Staff", "Views", "SecurityMonitoring", "Index.cshtml"));

        Assert.Contains("security-monitoring.js", view, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
    }

    [Fact]
    public void EsbuildBundlesSecurityMonitoringScript()
    {
        var packageJson = File.ReadAllText(Path.Combine(WebProject, "package.json"));

        Assert.Contains("\"build:security-monitoring\"", packageJson, StringComparison.Ordinal);
        Assert.Contains("security-monitoring.js", packageJson, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltBundleIsCommitted()
    {
        Assert.True(
            File.Exists(Path.Combine(WebProject, "wwwroot", "admin", "security-monitoring.js")),
            "wwwroot/admin/security-monitoring.js must be committed (built via npm run build:security-monitoring).");
    }

    [Fact]
    public void ProductionTemplateEnablesAuditWithExternalRoot()
    {
        var template = File.ReadAllText(Path.Combine(WebProject, "appsettings.Production.example.json"));

        Assert.Contains("\"SecurityMonitoring\"", template, StringComparison.Ordinal);
        Assert.Contains("\"ServerSecurityRoot\": \"C:\\\\Security\"", template, StringComparison.Ordinal);
        Assert.Contains("\"RequestAuditEnabled\": true", template, StringComparison.Ordinal);
    }

    [Fact]
    public void CommittedSampleDataMatchesSnapshotContract()
    {
        var sample = Path.Combine(WebProject, "SecuritySampleData", "security_snapshot.json");
        Assert.True(File.Exists(sample));
        var snapshot = File.ReadAllText(sample);

        Assert.Contains("\"bySourceIp\"", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"lockoutsByUser\"", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"failedLogons24h\"", snapshot, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Szaipa.Web")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Szaipa repository root.");
    }
}
