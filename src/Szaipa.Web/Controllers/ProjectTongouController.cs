using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contracts.Tongou;
using Szaipa.Web.Configuration;
using Szaipa.Web.Models;
using Szaipa.Web.Services;

namespace Szaipa.Web.Controllers;

[Route("Project_Tongou")]
public sealed class ProjectTongouController : Controller
{
    private static readonly IReadOnlyDictionary<int, string> SpecialWorkRoutes = new Dictionary<int, string>
    {
        [36] = nameof(Mengboshen),
        [84] = nameof(Beini),
        [115] = nameof(Mouliqiao),
        [162] = nameof(Mouliqiao2),
        [163] = nameof(Gaodaqing2)
    };

    private static readonly IReadOnlyDictionary<int, string> SpecialArtistRoutes = new Dictionary<int, string>
    {
        [96] = nameof(BeiniTd)
    };

    private readonly ReadOnlyMigrationOptions _readOnlyMigration;
    private readonly IMigrationWorkspaceDiagnosticsService _migrationWorkspaceDiagnosticsService;

    public ProjectTongouController(
        IOptions<ReadOnlyMigrationOptions> readOnlyMigration,
        IMigrationWorkspaceDiagnosticsService migrationWorkspaceDiagnosticsService)
    {
        _readOnlyMigration = readOnlyMigration.Value;
        _migrationWorkspaceDiagnosticsService = migrationWorkspaceDiagnosticsService;
    }

    /// <summary>
    /// A Tongou public read route can render real data only when the Tongou read-model flag is on AND the
    /// Tongou source has a (read-only) connection. Otherwise the route shows its migration skeleton. This keeps
    /// the default (DB-off) startup safe and makes activation a deliberate, config-driven step.
    /// </summary>
    private bool TongouReadModelsActive()
    {
        if (!_readOnlyMigration.EnableTongouReadModels)
        {
            return false;
        }

        var diagnostics = _migrationWorkspaceDiagnosticsService.GetDiagnostics();
        return diagnostics.LegacySources.TryGetValue(nameof(LegacyDataSource.Tongou), out var source)
            && source.HasConnectionString
            && source.AccessMode != nameof(LegacyAccessMode.Disabled);
    }

    [HttpGet("")]
    [HttpGet("Index")]
    [HttpGet("Atrist/{id?}")]
    public async Task<IActionResult> Atrist(int? id, CancellationToken cancellationToken)
    {
        // Legacy Atrist defaulted a missing id to 1 and redirected id 96 to the dedicated beiniTD route.
        var targetId = id ?? 1;
        if (TryCreateLegacyArtistRedirect(targetId, out var redirect))
        {
            return redirect;
        }

        if (TongouReadModelsActive())
        {
            var repository = HttpContext.RequestServices.GetRequiredService<ITongouReadRepository>();
            var artist = await repository.GetArtistByIdAsync(targetId, cancellationToken);
            if (artist is null)
            {
                // Legacy Atrist NRE'd on a missing id; the read-only flow returns 404 instead.
                return NotFound();
            }

            return View("Atrist", artist);
        }

        return View("AtristSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateTongouArtistDefinition(targetId)));
    }

    [HttpGet("Work/{id:int?}")]
    public async Task<IActionResult> Work(int? id, CancellationToken cancellationToken)
    {
        // Legacy Work defaulted a missing id to 1, incremented VisityCount (dropped here), and redirected the
        // five curated work ids to their dedicated routes.
        var targetId = id ?? 1;
        if (TryCreateLegacyWorkRedirect(targetId, out var redirect))
        {
            return redirect;
        }

        if (TongouReadModelsActive())
        {
            var repository = HttpContext.RequestServices.GetRequiredService<ITongouReadRepository>();
            var work = await repository.GetWorkByIdAsync(targetId, cancellationToken);
            if (work is null)
            {
                // Legacy Work NRE'd on a missing id; the read-only flow returns 404 and never writes VisityCount.
                return NotFound();
            }

            return View("Work", work);
        }

        return View("WorkSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateTongouWorkDefinition(targetId)));
    }

    [HttpGet("WorkList/{id:int?}")]
    public async Task<IActionResult> WorkList(int? id, CancellationToken cancellationToken)
    {
        var targetId = id ?? 1;
        if (TongouReadModelsActive())
        {
            var repository = HttpContext.RequestServices.GetRequiredService<ITongouReadRepository>();

            // Legacy WorkList loaded the artist (for ViewBag.Name) plus its works filtered by Atristid.
            var profile = await repository.GetArtistProfileAsync(targetId, cancellationToken);
            if (profile is null)
            {
                // Legacy WorkList NRE'd on a missing artist id; the read-only flow returns 404 instead.
                return NotFound();
            }

            ViewBag.Name = profile.Artist.Name;
            return View("WorkList", profile.Works);
        }

        return View("WorkListSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateTongouWorkListDefinition(targetId)));
    }

    [HttpGet("beini")]
    public Task<IActionResult> Beini(CancellationToken cancellationToken) => RenderSpecialWork(
        "beini",
        84,
        "Legacy work id 84 resolves into a dedicated route/view and must stay a controller-level compatibility rule during the read-only migration.",
        cancellationToken);

    [HttpGet("mouliqiao")]
    public Task<IActionResult> Mouliqiao(CancellationToken cancellationToken) => RenderSpecialWork(
        "mouliqiao",
        115,
        "Legacy work id 115 resolves into a dedicated route/view and should stay outside the repository implementation.",
        cancellationToken);

    [HttpGet("mouliqiao2")]
    public Task<IActionResult> Mouliqiao2(CancellationToken cancellationToken) => RenderSpecialWork(
        "mouliqiao2",
        162,
        "Legacy work id 162 resolves into a dedicated route/view and remains a controller compatibility concern.",
        cancellationToken);

    [HttpGet("mengboshen")]
    public Task<IActionResult> Mengboshen(CancellationToken cancellationToken) => RenderSpecialWork(
        "mengboshen",
        36,
        "Legacy work id 36 resolves into a dedicated route/view and must bypass generic detail rendering.",
        cancellationToken);

    [HttpGet("gaodaqing2")]
    public Task<IActionResult> Gaodaqing2(CancellationToken cancellationToken) => RenderSpecialWork(
        "gaodaqing2",
        163,
        "Legacy work id 163 resolves into a dedicated route/view and should remain a controller-owned redirect rule.",
        cancellationToken);

    [HttpGet("beiniTD")]
    public async Task<IActionResult> BeiniTd(CancellationToken cancellationToken)
    {
        if (TongouReadModelsActive())
        {
            var repository = HttpContext.RequestServices.GetRequiredService<ITongouReadRepository>();
            var artist = await repository.GetArtistByIdAsync(96, cancellationToken);
            if (artist is null)
            {
                return NotFound();
            }

            return View("beiniTD", artist);
        }

        return View("AtristSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateTongouSpecialArtistDefinition()));
    }

    /// <summary>
    /// The five curated work ids (84/115/162/36/163) render their own bespoke views. When the read models are
    /// active they load the fixed work id read-only; otherwise they fall back to the work skeleton.
    /// </summary>
    private async Task<IActionResult> RenderSpecialWork(
        string viewName,
        int workId,
        string compatibilityDetail,
        CancellationToken cancellationToken)
    {
        if (TongouReadModelsActive())
        {
            var repository = HttpContext.RequestServices.GetRequiredService<ITongouReadRepository>();
            var work = await repository.GetWorkByIdAsync(workId, cancellationToken);
            if (work is null)
            {
                return NotFound();
            }

            return View(viewName, work);
        }

        return View("WorkSkeleton", PageSkeletonFactory.Create(
            PublicReadRouteContractCatalog.CreateTongouSpecialWorkDefinition($"/Project_Tongou/{viewName}", workId, compatibilityDetail)));
    }

    private IActionResult? CreateCompatibilityRedirect(int id, IReadOnlyDictionary<int, string> routes)
    {
        return routes.TryGetValue(id, out var actionName)
            ? RedirectToAction(actionName)
            : null;
    }

    private bool TryCreateLegacyWorkRedirect(int id, out IActionResult redirect)
    {
        var result = CreateCompatibilityRedirect(id, SpecialWorkRoutes);
        redirect = result ?? new EmptyResult();
        return result is not null;
    }

    private bool TryCreateLegacyArtistRedirect(int id, out IActionResult redirect)
    {
        var result = CreateCompatibilityRedirect(id, SpecialArtistRoutes);
        redirect = result ?? new EmptyResult();
        return result is not null;
    }
}
