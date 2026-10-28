using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Traffic;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class TrafficEventPolicyTests
{
    [Theory]
    [InlineData("/Home/News?search=private", "/Home/News")]
    [InlineData("/Home/About#section02", "/Home/About")]
    public void NormalizePath_DropsQueryAndFragment(string input, string expected)
    {
        Assert.True(TrafficEventPolicy.TryNormalizePath(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("https://example.com/a")]
    [InlineData("//example.com/a")]
    [InlineData("/Admin/Users")]
    [InlineData("/staff/SecurityMonitoring")]
    [InlineData("/%2e%2e/Staff/SecurityMonitoring")]
    [InlineData("/Home/%252e%252e/Admin")]
    [InlineData("/api/traffic/events")]
    [InlineData("/bad\\path")]
    public void NormalizePath_RejectsExternalSensitiveOrMalformedPaths(string input)
    {
        Assert.False(TrafficEventPolicy.TryNormalizePath(input, out _));
    }

    [Fact]
    public async Task Store_DeduplicatesEventIdsAndAggregatesViewsAndClicks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "szaipa-traffic-test-" + Guid.NewGuid().ToString("N"));
        var paths = new SecurityMonitoringPaths("", directory, "", "", true, 30, 512, Array.Empty<string>());
        try
        {
            var store = new TrafficEventStore(paths);
            var id = Guid.NewGuid();
            Assert.True(await store.RecordAsync(new TrafficEventRequest(id, "view", "/Home/News", null), "127.0.0.1", default));
            Assert.False(await store.RecordAsync(new TrafficEventRequest(id, "view", "/Home/News", null), "127.0.0.1", default));
            Assert.True(await store.RecordAsync(new TrafficEventRequest(Guid.NewGuid(), "click", "/Home/News", "/Home/Art"), "127.0.0.2", default));

            var summary = await store.ReadSummaryAsync(7, 50, default);
            Assert.Equal(1, summary.Views);
            Assert.Equal(1, summary.Clicks);
            Assert.Contains(summary.TopPages, page => page.Path == "/Home/News" && page.Views == 1 && page.Clicks == 1);
            Assert.Equal(2, summary.Recent.Count);

            var restartedStore = new TrafficEventStore(paths);
            Assert.False(await restartedStore.RecordAsync(new TrafficEventRequest(id, "view", "/Home/News", null), "127.0.0.1", default));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
