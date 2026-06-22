namespace Szaipa.Data.Configuration;

public sealed class LegacyDatabaseOptions
{
    public LegacyAccessMode AccessMode { get; set; } = LegacyAccessMode.Disabled;

    public string ConnectionString { get; set; } = string.Empty;

    public string ConnectionStringSource { get; set; } = "None";
}
