namespace Szaipa.Web.Configuration;

/// <summary>
/// Points the new app at the legacy static assets (news/exhibition/artist images, fonts, model CSS/JS)
/// without copying ~1 GB into the repo. When <see cref="ContentRoot"/> is a real directory it is served at
/// request path <c>/Content</c>, matching the legacy view asset references.
/// </summary>
public sealed class LegacyAssetsOptions
{
    public const string SectionName = "LegacyAssets";

    /// <summary>
    /// Absolute path to the legacy <c>Content</c> directory (e.g. the <c>web24.05/Content</c> publish baseline).
    /// Empty by default so production never serves an unintended local path.
    /// </summary>
    public string ContentRoot { get; set; } = string.Empty;
}
