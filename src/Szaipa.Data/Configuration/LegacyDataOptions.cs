namespace Szaipa.Data.Configuration;

public sealed class LegacyDataOptions
{
    public const string SectionName = "LegacyData";

    public LegacyDatabaseOptions Szaipa { get; set; } = new();

    public LegacyDatabaseOptions Tongou { get; set; } = new();
}
