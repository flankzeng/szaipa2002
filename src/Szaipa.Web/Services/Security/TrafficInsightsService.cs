namespace Szaipa.Web.Services.Security;

/// <summary>Traffic classification derived from request-audit evidence. No category asserts that a visitor is human.</summary>
public static class TrafficInsightsService
{
    public const int SourceLimit = 200_000;
    private const int TopPageLimit = 20;

    private static readonly string[] AutomatedAgentMarkers =
    [
        "googlebot", "bingbot", "yandexbot", "duckduckbot", "baiduspider", "bot", "crawler", "spider", "slurp", "curl/", "wget/", "python-requests", "python-urllib",
        "httpclient", "libwww", "go-http-client", "scrapy", "headlesschrome", "phantomjs", "selenium"
    ];

    private static readonly string[] ProbePathMarkers =
    [
        "/wp-admin", "/wp-login", "/.env", "/phpmyadmin", "/xmlrpc.php", "/.git", "/vendor/phpunit",
        "/cgi-bin/", "/actuator", "/server-status"
    ];

    public static DateTimeOffset UtcCalendarStart(DateTimeOffset nowUtc, int days)
    {
        days = Math.Clamp(days, 1, 30);
        return new DateTimeOffset(nowUtc.UtcDateTime.Date.AddDays(-(days - 1)), TimeSpan.Zero);
    }

    public static TrafficInsightsResult Build(
        IReadOnlyList<RequestLogLine> source,
        DateTimeOffset sinceUtc,
        DateTimeOffset generatedAtUtc,
        string? category = null,
        string? path = null,
        int page = 1,
        int limit = 50,
        bool sourcePartial = false,
        IReadOnlyList<string>? sourcePartialReasons = null)
    {
        var normalizedCategory = NormalizeCategory(category);
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? null : NormalizePath(path.Trim());
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var classified = source
            .Where(x => x.Timestamp is { } timestamp
                && timestamp.ToUniversalTime() >= sinceUtc.ToUniversalTime()
                && timestamp.ToUniversalTime() <= generatedAtUtc.ToUniversalTime())
            .Where(x => !IsExcludedFromInsights(NormalizePath(x.Path)))
            .Select(Classify)
            .ToArray();
        ApplyBehaviorSignals(classified);
        var matching = classified
            .Where(x => normalizedCategory is null || x.Category == normalizedCategory)
            .Where(x => normalizedPath is null || (x.Path?.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(x => x.Timestamp)
            .ToArray();

        var topPages = matching
            .Where(x => string.Equals(x.Method, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Path) && IsPagePath(x.Path))
            .GroupBy(x => x.Path!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TrafficPageInsight(
                group.Key,
                group.LongCount(),
                group.LongCount(x => x.Category == "bot"),
                group.LongCount(x => x.Category == "suspectedBot"),
                group.LongCount(x => x.Category == "unknown"),
                group.LongCount(x => x.Status is >= 400)))
            .OrderByDescending(x => x.Requests)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Take(TopPageLimit)
            .ToArray();

        var skip = (int)Math.Min((long)(page - 1) * limit, int.MaxValue);
        var items = matching.Skip(skip).Take(limit).ToArray();
        return new TrafficInsightsResult(
            sinceUtc,
            generatedAtUtc,
            normalizedCategory,
            normalizedPath,
            new TrafficCategoryCounts(matching.LongLength,
                matching.LongCount(x => x.Category == "bot"),
                matching.LongCount(x => x.Category == "suspectedBot"),
                matching.LongCount(x => x.Category == "unknown")),
            topPages,
            items,
            page,
            limit,
            matching.LongLength,
            skip + items.Length < matching.LongLength,
            sourcePartial || source.Count >= SourceLimit,
            sourcePartialReasons ?? Array.Empty<string>());
    }

    public static TrafficRequestInsight Classify(RequestLogLine line)
    {
        var reasons = new List<string>();
        var agent = line.UserAgent?.Trim();
        var agentLower = agent?.ToLowerInvariant() ?? string.Empty;
        var matchedAgent = AutomatedAgentMarkers.FirstOrDefault(marker => agentLower.Contains(marker, StringComparison.Ordinal));
        if (matchedAgent is not null)
        {
            reasons.Add($"User-Agent 自报包含自动化标记“{matchedAgent}”；该声明未经验证。");
            return ToInsight(line, "bot", reasons);
        }

        if (string.IsNullOrWhiteSpace(agent))
        {
            reasons.Add("请求未提供 User-Agent，属于可疑信号。");
            return ToInsight(line, "suspectedBot", reasons);
        }

        var normalizedPath = line.Path?.ToLowerInvariant() ?? string.Empty;
        var matchedProbe = ProbePathMarkers.FirstOrDefault(marker => normalizedPath.Contains(marker, StringComparison.Ordinal));
        if (matchedProbe is not null)
        {
            reasons.Add($"请求路径命中常见漏洞探测特征“{matchedProbe}”。");
            return ToInsight(line, "suspectedBot", reasons);
        }

        reasons.Add("未发现已配置的自动化或探测特征，访客身份无法确认。");
        return ToInsight(line, "unknown", reasons);
    }

    private static TrafficRequestInsight ToInsight(RequestLogLine line, string category, IReadOnlyList<string> reasons) =>
        new(line.Timestamp, line.Ip, line.Method, NormalizePath(line.Path), line.Status, line.DurationMs, line.UserAgent, category, reasons);

    private static string? NormalizePath(string? path)
    {
        if (path is null) return null;
        var end = path.IndexOfAny(['?', '#']);
        return end >= 0 ? path[..end] : path;
    }

    private static bool IsPagePath(string path)
    {
        var normalized = path.Trim();
        if (normalized.Length == 0
            || normalized.StartsWith("/staff", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("/api", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(normalized);
        return extension.Length == 0 || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedFromInsights(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Trim();
        return normalized.StartsWith("/staff", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("/api/traffic", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/api/traffic/", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBehaviorSignals(TrafficRequestInsight[] requests)
    {
        var minuteCounts = requests
            .Where(x => !string.IsNullOrWhiteSpace(x.Ip) && !string.IsNullOrWhiteSpace(x.UserAgent) && x.Timestamp.HasValue)
            .GroupBy(x => (x.Ip, x.UserAgent, Minute: x.Timestamp!.Value.ToUniversalTime().ToString("yyyyMMddHHmm")))
            .ToDictionary(x => x.Key, x => x.Count());
        var scanPathCounts = requests
            .Where(x => !string.IsNullOrWhiteSpace(x.Ip) && !string.IsNullOrWhiteSpace(x.UserAgent)
                && x.Timestamp.HasValue && x.Status == 404 && !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => (x.Ip, x.UserAgent, Hour: x.Timestamp!.Value.ToUniversalTime().ToString("yyyyMMddHH")))
            .ToDictionary(x => x.Key, x => x.Select(y => y.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        for (var i = 0; i < requests.Length; i++)
        {
            var item = requests[i];
            if (string.IsNullOrWhiteSpace(item.Ip) || string.IsNullOrWhiteSpace(item.UserAgent) || item.Timestamp is not { } timestamp)
                continue;
            var minuteKey = (item.Ip, item.UserAgent, timestamp.ToUniversalTime().ToString("yyyyMMddHHmm"));
            var hourKey = (item.Ip, item.UserAgent, timestamp.ToUniversalTime().ToString("yyyyMMddHH"));
            string? signal = null;
            if (minuteCounts.TryGetValue(minuteKey, out var requestsPerMinute) && requestsPerMinute >= 60)
                signal = $"同一 IP 与 User-Agent 在该分钟内请求 {requestsPerMinute} 次（阈值 60），属于高频信号。";
            else if (scanPathCounts.TryGetValue(hourKey, out var notFoundPaths) && notFoundPaths >= 20)
                signal = $"同一 IP 与 User-Agent 在该小时内访问 {notFoundPaths} 个不同的 404 路径（阈值 20），符合路径扫描特征。";

            if (signal is not null && item.Category != "bot")
            {
                requests[i] = item with
                {
                    Category = "suspectedBot",
                    Reasons = item.Reasons.Append(signal).ToArray(),
                };
            }
        }
    }

    private static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        return category.Trim().ToLowerInvariant() switch
        {
            "bot" => "bot",
            "suspectedbot" => "suspectedBot",
            "unknown" => "unknown",
            _ => throw new ArgumentException("category must be bot, suspectedBot, or unknown", nameof(category))
        };
    }
}

public sealed record TrafficInsightsResult(
    DateTimeOffset SinceUtc,
    DateTimeOffset GeneratedAtUtc,
    string? Category,
    string? Path,
    TrafficCategoryCounts Counts,
    IReadOnlyList<TrafficPageInsight> TopPages,
    IReadOnlyList<TrafficRequestInsight> Items,
    int Page,
    int Limit,
    long Total,
    bool HasMore,
    bool SourceTruncated,
    IReadOnlyList<string> SourcePartialReasons);

public sealed record TrafficCategoryCounts(long Total, long Bot, long SuspectedBot, long Unknown);

public sealed record TrafficPageInsight(
    string Path,
    long Requests,
    long Bot,
    long SuspectedBot,
    long Unknown,
    long ErrorCount);

public sealed record TrafficRequestInsight(
    DateTimeOffset? Timestamp,
    string? Ip,
    string? Method,
    string? Path,
    int? Status,
    long? DurationMs,
    string? UserAgent,
    string Category,
    IReadOnlyList<string> Reasons);
