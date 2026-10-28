using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class TrafficInsightsServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 4, 0, 0, TimeSpan.Zero);
    private readonly string _auditDir = Path.Combine(Path.GetTempPath(), $"traffic-audit-{Guid.NewGuid():N}");

    public TrafficInsightsServiceTests() => Directory.CreateDirectory(_auditDir);

    public void Dispose()
    {
        try { Directory.Delete(_auditDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void ClassifyUsesExplicitAutomationAndProbeSignalsAndNeverClaimsHuman()
    {
        var bot = TrafficInsightsService.Classify(Request("/news", "Mozilla/5.0 Googlebot/2.1"));
        var suspectedMissingAgent = TrafficInsightsService.Classify(Request("/news", null));
        var suspectedProbe = TrafficInsightsService.Classify(Request("/.env", "Mozilla/5.0"));
        var unknown = TrafficInsightsService.Classify(Request("/news", "Mozilla/5.0"));

        Assert.Equal("bot", bot.Category);
        Assert.Contains(bot.Reasons, reason => reason.Contains("googlebot", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("suspectedBot", suspectedMissingAgent.Category);
        Assert.Contains(suspectedMissingAgent.Reasons, reason => reason.Contains("未提供"));
        Assert.Equal("suspectedBot", suspectedProbe.Category);
        Assert.Equal("unknown", unknown.Category);
        Assert.Contains(unknown.Reasons, reason => reason.Contains("无法确认"));
        Assert.DoesNotContain(new[] { bot, suspectedMissingAgent, suspectedProbe, unknown }, x => x.Category == "human");
    }

    [Fact]
    public void BuildAggregatesPagesAndAppliesFiltersAndPagination()
    {
        var source = new[]
        {
            Request("/Home", "Googlebot", Now.AddMinutes(-3)),
            Request("/Home", "Mozilla/5.0", Now.AddMinutes(-2)),
            Request("/Home", null, Now.AddMinutes(-1)),
            Request("/About", "curl/8.0", Now),
        };

        var result = TrafficInsightsService.Build(source, Now.AddDays(-1), Now, page: 1, limit: 2);

        Assert.Equal(4, result.Counts.Total);
        Assert.Equal(2, result.Counts.Bot);
        Assert.Equal(1, result.Counts.SuspectedBot);
        Assert.Equal(1, result.Counts.Unknown);
        Assert.Equal("/Home", result.TopPages[0].Path);
        Assert.Equal(3, result.TopPages[0].Requests);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.HasMore);
        Assert.False(result.SourceTruncated);

        var filtered = TrafficInsightsService.Build(source, Now.AddDays(-1), Now, category: "unknown", path: "home", limit: 5);
        Assert.Equal(1, filtered.Counts.Total);
        Assert.Equal("unknown", filtered.Items[0].Category);
        Assert.Equal("/Home", filtered.Items[0].Path);
    }

    [Fact]
    public void BuildReportsSourceCapAndRejectsUnknownCategory()
    {
        var source = Enumerable.Repeat(Request("/Home", "Mozilla/5.0"), TrafficInsightsService.SourceLimit).ToArray();
        var result = TrafficInsightsService.Build(source, Now.AddDays(-1), Now, limit: 1);

        Assert.True(result.SourceTruncated);
        Assert.Single(result.Items);
        Assert.True(result.HasMore);
        Assert.Throws<ArgumentException>(() => TrafficInsightsService.Build(
            Array.Empty<RequestLogLine>(), Now.AddDays(-1), Now, category: "human"));
    }

    [Fact]
    public void BuildRemovesQueryAndHashAndExcludesNonPagePathsFromTopPages()
    {
        var source = new[]
        {
            Request("/News?token=private#section", "Mozilla/5.0"),
            Request("/Staff/SecurityMonitoring?secret=yes", "Mozilla/5.0"),
            Request("/Admin", "Mozilla/5.0"),
            Request("/api/traffic?days=7", "Mozilla/5.0"),
            Request("/api/items", "Mozilla/5.0"),
            Request("/assets/site.css?v=1", "Mozilla/5.0"),
            new(Now, "203.0.113.8", "POST", "/Contact", 200, 12, "Mozilla/5.0"),
            Request("/future", "Mozilla/5.0", Now.AddMinutes(5)),
        };

        var result = TrafficInsightsService.Build(source, Now.AddDays(-1), Now);

        Assert.Equal(4, result.Counts.Total);
        Assert.All(result.Items, item => Assert.DoesNotContain("?", item.Path));
        Assert.Single(result.TopPages);
        Assert.Equal("/News", result.TopPages[0].Path);
        Assert.Contains(result.Items, item => item.Path == "/api/items");
        Assert.DoesNotContain(result.Items, item => item.Path?.StartsWith("/api/traffic", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void UtcCalendarStartIncludesCurrentUtcDateAndPreviousCalendarDays()
    {
        var now = new DateTimeOffset(2026, 10, 28, 0, 5, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero),
            TrafficInsightsService.UtcCalendarStart(now, 7));
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
            TrafficInsightsService.UtcCalendarStart(now, 0));
    }

    [Fact]
    public void BuildFlagsHighRateOnlyForSameIpAndUserAgent()
    {
        var requests = Enumerable.Range(0, 60)
            .Select(i => Request("/News", "Mozilla/5.0", Now.AddSeconds(i)))
            .Append(new RequestLogLine(Now, "203.0.113.8", "GET", "/About", 200, 1, "OtherAgent"))
            .ToArray();

        var result = TrafficInsightsService.Build(requests, Now.AddDays(-1), Now.AddMinutes(2));

        Assert.Equal(60, result.Counts.SuspectedBot);
        Assert.Equal(1, result.Counts.Unknown);
        Assert.Contains(result.Items[0].Reasons, reason => reason.Contains("阈值 60"));
    }

    [Fact]
    public void ReaderReportsLineAndOversizedFileCoverageLimits()
    {
        var reader = CreateReader();
        var requestFile = Path.Combine(_auditDir, $"requests-{Now:yyyyMMdd}.log");
        File.WriteAllText(requestFile,
            $"{Now:O}|203.0.113.8|GET|/a|200|1|Mozilla/5.0\n" +
            $"{Now:O}|203.0.113.8|GET|/b|200|1|Mozilla/5.0\n");

        var lineCapped = reader.ReadRequestsWithMetadata(Now.AddDays(-1), maxLines: 1);
        Assert.True(lineCapped.IsPartial);
        Assert.Contains(lineCapped.PartialReasons, reason => reason.Contains("line limit"));

        using (var stream = new FileStream(requestFile, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(50L * 1024 * 1024 + 1);
        }

        var oversized = reader.ReadRequestsWithMetadata(Now.AddDays(-1), maxLines: 10);
        Assert.True(oversized.IsPartial);
        Assert.Contains(oversized.PartialReasons, reason => reason.Contains("exceeds"));
    }

    [Fact]
    public void ReaderFiltersByDateBeforeApplyingTheRequestFileCap()
    {
        var reader = CreateReader();
        var historicalStart = new DateOnly(2024, 1, 1);
        for (var i = 0; i < 401; i++)
        {
            var date = historicalStart.AddDays(i);
            File.WriteAllText(Path.Combine(_auditDir, $"requests-{date:yyyyMMdd}.log"), "old\n");
        }

        File.WriteAllText(Path.Combine(_auditDir, "requests-20261027.log"),
            $"{Now.AddDays(-1):O}|203.0.113.8|GET|/recent-one|200|1|Mozilla/5.0\n");
        File.WriteAllText(Path.Combine(_auditDir, "requests-20261028.log"),
            $"{Now:O}|203.0.113.8|GET|/recent-two|200|1|Mozilla/5.0\n");

        var result = reader.ReadRequestsWithMetadata(Now.AddDays(-1), 100);

        Assert.False(result.IsPartial);
        Assert.Equal(2, result.Lines.Count);
        Assert.Contains(result.Lines, item => item.Path == "/recent-two");
    }

    private static RequestLogLine Request(string path, string? userAgent, DateTimeOffset? timestamp = null) =>
        new(timestamp ?? Now, "203.0.113.8", "GET", path, 200, 12, userAgent);

    private SecurityAuditReader CreateReader() => new(new Szaipa.Web.Infrastructure.SecurityMonitoringPaths(
        string.Empty, _auditDir, string.Empty, string.Empty, true, 30, 512, Array.Empty<string>()));
}
