namespace Szaipa.Data.Services;

/// <summary>
/// Parsing / validation shared by the home-page featured-exhibition read model and its admin repository.
/// Links and image addresses must be site-local (<c>/…</c>); anything else is dropped so a bad row can never
/// inject an external URL or script into the home page.
/// </summary>
public static class FeaturedExhibitionContent
{
    /// <summary>Home cards use at most this many carousel images.</summary>
    public const int MaxImages = 12;

    public static IReadOnlyList<string> SplitLines(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlyList<string> SplitPaths(string? value) =>
        SplitLines(value).Where(PublicationBlockContent.IsSafeLocalPath).Take(MaxImages).ToArray();

    public static string SafePathOrEmpty(string? value) =>
        PublicationBlockContent.IsSafeLocalPath(value) ? value!.Trim() : string.Empty;

    /// <summary>A link target: site-local path (query/hash allowed).</summary>
    public static bool IsSafeLink(string? value) => PublicationBlockContent.IsSafeLocalPath(value);

    /// <summary>Normalises a multi-line text field (trim lines, drop blanks).</summary>
    public static string? JoinLines(string? value)
    {
        var lines = SplitLines(value);
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    public static string? JoinPaths(string? value)
    {
        var paths = SplitPaths(value);
        return paths.Count == 0 ? null : string.Join("\n", paths);
    }
}
