namespace Szaipa.Data.Services.Admin;

/// <summary>Headline KPI tiles for the dashboard (today / this month / content totals / recent ops count).</summary>
public sealed class DashboardKpi
{
    public int TodayVisits { get; init; }

    public long MonthVisits { get; init; }

    public long ContentAccessTotal { get; init; }

    public int RecentOperationCount { get; init; }
}

/// <summary>One day on the daily-visit trend line (date label + visit tally from Diary.VistiTotal).</summary>
public sealed record DailyVisitPoint(string Date, int Count);

/// <summary>One slice of the content-access pie (访问作品/会员/企业 + 新闻阅读数).</summary>
public sealed record ContentAccessSlice(string Name, long Value);

/// <summary>A city node under a province in the geo sunburst.</summary>
public sealed record GeoCity(string Name, long Value);

/// <summary>A province node (with its cities) in the geo sunburst.</summary>
public sealed class GeoProvince
{
    public required string Name { get; init; }

    public long Value { get; init; }

    public IReadOnlyList<GeoCity> Children { get; init; } = Array.Empty<GeoCity>();
}

/// <summary>One day's operation-record entries (Diary.OperationRecord split on '/').</summary>
public sealed class OperationRecordDay
{
    public required string Date { get; init; }

    public IReadOnlyList<string> Records { get; init; } = Array.Empty<string>();
}

/// <summary>A page of operation-record days, newest first.</summary>
public sealed class OperationRecordPage
{
    public IReadOnlyList<OperationRecordDay> Items { get; init; } = Array.Empty<OperationRecordDay>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalDays { get; init; }

    public int TotalPages => TotalDays == 0 ? 0 : (int)Math.Ceiling((double)TotalDays / PageSize);
}
