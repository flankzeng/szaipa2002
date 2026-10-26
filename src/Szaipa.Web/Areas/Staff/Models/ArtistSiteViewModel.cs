using Szaipa.Data.Services.Admin;
namespace Szaipa.Web.Areas.Staff.Models;
public sealed class ArtistSiteViewModel
{
 public required ArtistSiteOverview Overview { get; init; }
 public required ArtistWorkList Works { get; init; }
 public required ArtistExhibitionLinks Exhibitions { get; init; }
 public required IReadOnlyList<PublicationOption> Publications { get; init; }
}
