using Xunit;

namespace Szaipa.Web.Tests;

public sealed class TrafficMonitorCoverageContractTests
{
    [Fact]
    public void Every_public_layout_and_standalone_page_includes_the_monitor_once()
    {
        var viewsRoot = Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web", "Views");
        var entries = new[]
        {
            "Shared/_PublicLayout.cshtml",
            "Shared/_ArtistLayout.cshtml",
            "Shared/_Layout.cshtml",
            "Home/NewsRead.cshtml",
            "Publication/chunyu3.cshtml",
            "Publication/tonggou2.cshtml",
            "Publication/tonggou2024.cshtml"
        };

        foreach (var relativePath in entries)
        {
            var markup = File.ReadAllText(Path.Combine(viewsRoot, relativePath));
            Assert.Equal(1, Count(markup, "<partial name=\"_TrafficMonitor\" />"));
        }
    }

    [Fact]
    public void Partial_gates_capture_and_excludes_backend_routes()
    {
        var partial = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Szaipa.Web", "Views", "Shared", "_TrafficMonitor.cshtml"));

        Assert.Contains("RequestAuditEnabled", partial, StringComparison.Ordinal);
        Assert.Contains("IsExcluded(\"/admin\")", partial, StringComparison.Ordinal);
        Assert.Contains("IsExcluded(\"/staff\")", partial, StringComparison.Ordinal);
        Assert.Contains("IsExcluded(\"/api\")", partial, StringComparison.Ordinal);
        Assert.Contains("traffic-monitor.js", partial, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "src", "Szaipa.Web")))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Szaipa repository root.");
    }
}
