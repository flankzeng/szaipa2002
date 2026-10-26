using System.Text.RegularExpressions;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Models.Home;
using Szaipa.Data.Services;

namespace Szaipa.Web.Models;

/// <summary>One rendered block of a 专题 page: its type plus the already-normalised string fields.</summary>
public sealed class TopicBlockModel
{
    public required string Type { get; init; }

    public required IReadOnlyDictionary<string, string> Fields { get; init; }

    public string Get(string key) => PublicationBlockContent.Get(Fields, key);

    /// <summary>Plain-text field split into paragraphs (one per non-empty line).</summary>
    public IReadOnlyList<string> Paragraphs(string key) =>
        Get(key).Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// View model of the <c>_ExhibitionTopic</c> partial (Publication.Type == 2). Built defensively from stored rows:
/// every block is re-normalised (so even a hand-edited DB row cannot inject markup), unknown types are dropped and
/// only the first <c>works</c> / <c>gallery</c> block is kept (they bind to the single catalog / photo folder).
/// </summary>
public sealed class ExhibitionTopicModel
{
    public const string DefaultAccentColor = "#bf272d";

    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public required PublicationDetailModel Publication { get; init; }

    /// <summary>Validated #RRGGBB accent colour (title rules, quote marks, headings).</summary>
    public required string AccentColor { get; init; }

    /// <summary>Header hero image URL (may be a not-yet-uploaded convention path; the hero has a dark fallback colour).</summary>
    public required string BannerUrl { get; init; }

    public required IReadOnlyList<TopicBlockModel> Blocks { get; init; }

    /// <summary>Gallery image URLs for the (single) gallery block: explicit list, else the numbered folder convention.</summary>
    public required IReadOnlyList<string> MainImages { get; init; }

    public required IReadOnlyList<string> ThumbnailImages { get; init; }

    public static ExhibitionTopicModel Create(PublicationDetailModel publication, IReadOnlyList<string> explicitImagePaths)
    {
        var blocks = new List<TopicBlockModel>();
        var hasWorks = false;
        var hasGallery = false;

        foreach (var block in publication.Blocks)
        {
            var normalized = PublicationBlockContent.Normalize(block.BlockType, block.Content);
            if (normalized is null)
            {
                continue;
            }

            if (block.BlockType == PublicationBlockTypes.Works)
            {
                if (hasWorks) { continue; }
                hasWorks = true;
            }
            else if (block.BlockType == PublicationBlockTypes.Gallery)
            {
                if (hasGallery) { continue; }
                hasGallery = true;
            }

            blocks.Add(new TopicBlockModel
            {
                Type = block.BlockType,
                Fields = PublicationBlockContent.Parse(normalized)
            });
        }

        var accent = HexColor.IsMatch(publication.ThemeColor ?? string.Empty) ? publication.ThemeColor : DefaultAccentColor;
        var banner = PublicationBlockContent.IsSafeLocalPath(publication.BannerPath)
            ? publication.BannerPath
            : $"/Content/images/{publication.FolderName}/10000.jpg";

        var main = explicitImagePaths.Count > 0
            ? explicitImagePaths
            : Enumerable.Range(10001, Math.Max(0, publication.MaxImg) + 1)
                .Select(number => $"/Content/images/{publication.FolderName}/{number}.jpg")
                .ToArray();
        var thumbs = explicitImagePaths.Count > 0
            ? explicitImagePaths
            : Enumerable.Range(10000, Math.Max(0, publication.MaxImg) + 1)
                .Select(number => $"/Content/images/{publication.FolderName}/{number}.jpg")
                .ToArray();

        return new ExhibitionTopicModel
        {
            Publication = publication,
            AccentColor = accent,
            BannerUrl = banner,
            Blocks = blocks,
            MainImages = main,
            ThumbnailImages = thumbs
        };
    }
}
