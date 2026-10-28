namespace Szaipa.Web.Services.Traffic;

public sealed record TrafficEvent(Guid EventId, string Kind, string PagePath, string? TargetPath, DateTimeOffset Timestamp);

public sealed record TrafficEventRequest(Guid EventId, string Kind, string PagePath, string? TargetPath);

public sealed record TrafficSummary(long Views, long Clicks, IReadOnlyList<TrafficTopPage> TopPages,
    IReadOnlyList<TrafficEvent> Recent, bool SourceTruncated);

public sealed record TrafficTopPage(string Path, long Views, long Clicks);
