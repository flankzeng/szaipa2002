namespace Szaipa.Web.Configuration;

public sealed class DatabaseSafetyOptions
{
    public const string SectionName = "RuntimeSafety";

    public bool UseLegacyDataSources { get; set; }

    public bool AllowLiveDatabase { get; set; }

    public bool UseHttpsRedirection { get; set; }
}
