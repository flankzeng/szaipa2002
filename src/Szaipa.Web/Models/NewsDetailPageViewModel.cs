using Szaipa.Data.Models.Home;

namespace Szaipa.Web.Models;

/// <summary>
/// Backing model for the migrated news detail page (legacy <c>newnewsread</c>): the article plus the
/// related-news sidebar list. Replaces the legacy model+ViewBag pairing (News + ViewBag.NewsList).
/// </summary>
public sealed class NewsDetailPageViewModel
{
    public required NewsDetailModel Article { get; init; }

    public required IReadOnlyList<NewsSummaryModel> Related { get; init; }
}
