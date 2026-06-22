namespace Szaipa.Web.Configuration;

public sealed class ReadOnlyMigrationOptions
{
    public const string SectionName = "ReadOnlyMigration";

    public bool EnableSzaipaReadModels { get; set; }

    public bool EnableTongouReadModels { get; set; }
}
