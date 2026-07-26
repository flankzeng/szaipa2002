using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contracts.Home;
using Szaipa.Web.Configuration;
using Szaipa.Web.Models;
using Szaipa.Web.Services;

namespace Szaipa.Web.Controllers;

[Route("Home")]
public class HomeController : Controller
{
    private readonly ReadOnlyMigrationOptions _readOnlyMigration;
    private readonly IMigrationWorkspaceDiagnosticsService _migrationWorkspaceDiagnosticsService;

    public HomeController(
        IOptions<ReadOnlyMigrationOptions> readOnlyMigration,
        IMigrationWorkspaceDiagnosticsService migrationWorkspaceDiagnosticsService)
    {
        _readOnlyMigration = readOnlyMigration.Value;
        _migrationWorkspaceDiagnosticsService = migrationWorkspaceDiagnosticsService;
    }

    /// <summary>
    /// A Szaipa public read route can render real data only when the read-model flag is on AND the Szaipa
    /// source has a (read-only) connection. List pages otherwise render formal empty states and content
    /// detail pages return 503. This keeps the default (DB-off) startup safe without exposing migration UI.
    /// </summary>
    private bool SzaipaReadModelsActive()
    {
        if (!_readOnlyMigration.EnableSzaipaReadModels)
        {
            return false;
        }

        var diagnostics = _migrationWorkspaceDiagnosticsService.GetDiagnostics();
        return diagnostics.LegacySources.TryGetValue(nameof(LegacyDataSource.Szaipa), out var source)
            && source.HasConnectionString
            && source.AccessMode != nameof(LegacyAccessMode.Disabled);
    }

    [HttpGet("/")]
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var newsRepository = HttpContext.RequestServices.GetRequiredService<INewsReadRepository>();
            var snapshot = await newsRepository.GetHomePageSnapshotAsync(cancellationToken);
            return View("Index", snapshot);
        }

        return View("Index", new Szaipa.Data.Models.Home.HomePageSnapshotModel
        {
            LatestNews = Array.Empty<Szaipa.Data.Models.Home.NewsSummaryModel>(),
            FeaturedPublications = Array.Empty<Szaipa.Data.Models.Home.PublicationSummaryModel>()
        });
    }

    [HttpGet("newIndex")]
    public IActionResult LegacyNewIndex() => RedirectPermanent("/");

    [HttpGet("News")]
    public async Task<IActionResult> News(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            // Resolve the repository only when activated: constructing it builds the read context, whose lazy
            // connection resolution throws while the Szaipa source is Disabled. Keeping it out of the
            // constructor means the default (DB-off) path never touches it.
            var newsRepository = HttpContext.RequestServices.GetRequiredService<INewsReadRepository>();

            var news = await newsRepository.GetLatestNewsAsync(0, cancellationToken);
            return View("News", news);
        }

        return View("News", Array.Empty<Szaipa.Data.Models.Home.NewsSummaryModel>());
    }

    [HttpGet("newnews")]
    public IActionResult LegacyNewNews() => RedirectPermanent("/Home/News");

    [HttpGet("NewsRead/{id:int}")]
    public async Task<IActionResult> NewsRead(int id, CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var newsRepository = HttpContext.RequestServices.GetRequiredService<INewsReadRepository>();
            var article = await newsRepository.GetNewsByIdAsync(id, cancellationToken);
            if (article is null)
            {
                // Legacy newnewsread NRE'd on a missing id; the read-only flow returns 404 instead.
                return NotFound();
            }

            var related = await newsRepository.GetRelatedNewsAsync(
                Szaipa.Data.Composition.LegacyDisplayRules.NewsDetailSidebarCount,
                cancellationToken);
            return View("NewsRead", new NewsDetailPageViewModel { Article = article, Related = related });
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    [HttpGet("newnewsread/{id:int}")]
    public IActionResult LegacyNewNewsRead(int id) => RedirectPermanent($"/Home/NewsRead/{id}");

    [HttpGet("Vip")]
    public async Task<IActionResult> Vip(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var artistRepository = HttpContext.RequestServices.GetRequiredService<IArtistReadRepository>();
            var artists = await artistRepository.GetArtistsAsync(newestFirst: true, cancellationToken);
            return View("Vip", artists);
        }

        return View("Vip", Array.Empty<Szaipa.Data.Models.Home.ArtistSummaryModel>());
    }

    [HttpGet("newvip")]
    public IActionResult LegacyNewVip() => RedirectPermanent("/Home/Vip");

    [HttpGet("About")]
    public IActionResult About() => View("About");

    [HttpGet("newabout")]
    public IActionResult LegacyNewAbout() => RedirectPermanent("/Home/About");

    [HttpGet("Art/{id:int}")]
    public async Task<IActionResult> Art(int id, CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var artistRepository = HttpContext.RequestServices.GetRequiredService<IArtistReadRepository>();
            var profile = await artistRepository.GetArtistProfileAsync(id, cancellationToken);
            if (profile is null)
            {
                // Legacy newArt NRE'd on a missing artist id; the read-only flow returns 404.
                return NotFound();
            }

            return View("Art", profile);
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    [HttpGet("newArt/{id:int}")]
    public IActionResult LegacyNewArt(int id) => RedirectPermanent($"/Home/Art/{id}");

    [HttpGet("Art/{id:int}/Archive")]
    public async Task<IActionResult> ArtArchive(int id, CancellationToken cancellationToken)
    {
        if (!SzaipaReadModelsActive())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var artistRepository = HttpContext.RequestServices.GetRequiredService<IArtistReadRepository>();
        var archive = await artistRepository.GetArtistArchiveAsync(id, cancellationToken);
        return archive is null ? NotFound() : View("ArtArchive", archive);
    }

    [HttpGet("Art/News/{id:int}")]
    public async Task<IActionResult> ArtArticle(int id, CancellationToken cancellationToken)
    {
        if (!SzaipaReadModelsActive())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var artistRepository = HttpContext.RequestServices.GetRequiredService<IArtistReadRepository>();
        var article = await artistRepository.GetArtistArticleAsync(id, cancellationToken);
        return article is null ? NotFound() : View("ArtArticle", article);
    }

    [HttpGet("ArtNews/{id:int}")]
    public IActionResult LegacyArtNews(int id) => RedirectPermanent($"/Home/Art/{id}/Archive");

    [HttpGet("ArtNewsRead/{id:int}")]
    public IActionResult LegacyArtNewsRead(int id) => RedirectPermanent($"/Home/Art/News/{id}");

    [HttpGet("PublicationList")]
    public async Task<IActionResult> PublicationList(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var publicationRepository = HttpContext.RequestServices.GetRequiredService<IPublicationReadRepository>();
            var publications = await publicationRepository.GetLatestPublicationsAsync(0, cancellationToken);
            return View("PublicationList", publications);
        }

        return View("PublicationList", Array.Empty<Szaipa.Data.Models.Home.PublicationSummaryModel>());
    }

    [HttpGet("Publication/{id:int}")]
    public async Task<IActionResult> Publication(int id, CancellationToken cancellationToken)
    {
        var readModelsActive = SzaipaReadModelsActive();
        if (readModelsActive)
        {
            var publicationRepository = HttpContext.RequestServices.GetRequiredService<IPublicationReadRepository>();

            // Legacy Publication(id) loaded the detail row plus the full publication list (relatedCount 0 = no cap).
            var snapshot = await publicationRepository.GetPublicationDetailSnapshotAsync(id, 0, cancellationToken);
            if (snapshot is not null)
            {
                return View("Publication", snapshot);
            }
        }

        // These fourteen retired hand-written galleries predate the database migration. Keep them available
        // without requiring a writable database; an actual database row always wins when one exists.
        var fallback = LegacyPublicationGalleryCatalog.GetFallbackPublication(id);
        if (fallback is not null)
        {
            return View("Publication", fallback);
        }

        if (readModelsActive)
        {
            // Legacy Publication NRE'd on a missing id (and incremented ReadCount); the read-only flow returns 404 and never writes.
            return NotFound();
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}
