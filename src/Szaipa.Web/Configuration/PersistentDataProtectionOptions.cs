namespace Szaipa.Web.Configuration;

/// <summary>
/// Stable Data Protection identity and key storage used by the staff authentication cookie.
/// Production keys must live outside immutable release directories so a release switch does not
/// invalidate existing sessions.
/// </summary>
public sealed class PersistentDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public const string DefaultApplicationName = "Szaipa.Web";

    /// <summary>
    /// Isolates this application's protected payloads from other applications sharing the key ring.
    /// The default is deliberately independent of the physical release path.
    /// </summary>
    public string ApplicationName { get; set; } = DefaultApplicationName;

    /// <summary>
    /// Persistent key directory. Development may leave this empty and use the project-local fallback;
    /// every other environment must supply an absolute directory outside the application content root.
    /// </summary>
    public string KeysPath { get; set; } = string.Empty;

    public string EffectiveApplicationName =>
        string.IsNullOrWhiteSpace(ApplicationName)
            ? DefaultApplicationName
            : ApplicationName.Trim();
}
