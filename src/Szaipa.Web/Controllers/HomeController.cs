using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Scaffolding;
using Szaipa.Web.Configuration;
using Szaipa.Web.Models;
using Szaipa.Web.Services;

namespace Szaipa.Web.Controllers;

[Route("Home")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DatabaseSafetyOptions _databaseSafety;
    private readonly ReadOnlyMigrationOptions _readOnlyMigration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILegacyScaffoldCommandService _legacyScaffoldCommandService;
    private readonly IMigrationWorkspaceDiagnosticsService _migrationWorkspaceDiagnosticsService;

    public HomeController(
        ILogger<HomeController> logger,
        IOptions<DatabaseSafetyOptions> databaseSafety,
        IOptions<ReadOnlyMigrationOptions> readOnlyMigration,
        IWebHostEnvironment environment,
        ILegacyScaffoldCommandService legacyScaffoldCommandService,
        IMigrationWorkspaceDiagnosticsService migrationWorkspaceDiagnosticsService)
    {
        _logger = logger;
        _databaseSafety = databaseSafety.Value;
        _readOnlyMigration = readOnlyMigration.Value;
        _environment = environment;
        _legacyScaffoldCommandService = legacyScaffoldCommandService;
        _migrationWorkspaceDiagnosticsService = migrationWorkspaceDiagnosticsService;
    }

    /// <summary>
    /// A Szaipa public read route can render real data only when the read-model flag is on AND the Szaipa
    /// source has a (read-only) connection. Otherwise the route shows its migration skeleton. This keeps the
    /// default (DB-off) startup safe and makes activation a deliberate, config-driven step.
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
    [HttpGet("newIndex")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            // Legacy newIndex fed ViewBag.n (top-6 news, subtitle trimmed) and ViewBag.ExhibitionList
            // (all publications ordered by StartDate desc); the snapshot reproduces both read-only.
            var newsRepository = HttpContext.RequestServices.GetRequiredService<INewsReadRepository>();
            var snapshot = await newsRepository.GetHomePageSnapshotAsync(cancellationToken);
            return View("NewIndex", snapshot);
        }

        var diagnostics = _migrationWorkspaceDiagnosticsService.GetDiagnostics();

        return View(new MigrationDashboardViewModel
        {
            LandingPreview = PublicReadRouteContractCatalog.CreateLandingPreview(),
            HasReadWriteLegacySource = diagnostics.HasReadWriteLegacySource,
            TargetFramework = "net10.0 LTS",
            UseLegacyDataSources = _databaseSafety.UseLegacyDataSources,
            AllowLiveDatabase = _databaseSafety.AllowLiveDatabase,
            ActiveEnvironment = _environment.EnvironmentName,
            LegacySources = diagnostics.LegacySources
                .ToDictionary(item => item.Key, item => item.Value.AccessMode),
            LegacyConnectionSources = diagnostics.LegacySources
                .ToDictionary(
                    item => item.Key,
                    item => $"{item.Value.ConnectionStringSource} | HasConnectionString={item.Value.HasConnectionString}"),
            ScaffoldReadiness = _legacyScaffoldCommandService.GetSuggestions()
                .ToDictionary(item => item.Source.ToString(), item => item.IsReady),
            ReadModelFlags = diagnostics.ReadModelFlags,
            ScaffoldSuggestions = _legacyScaffoldCommandService.GetSuggestions()
                .Select(item => new ScaffoldSuggestionViewModel
                {
                    Source = item.Source.ToString(),
                    ContextName = item.ContextName,
                    AccessMode = item.AccessMode,
                    IsReady = item.IsReady,
                    EnvironmentVariableName = item.EnvironmentVariableName,
                    Command = item.Command,
                    Notes = item.Notes
                })
                .ToArray(),
            Guardrails = new[]
            {
                new MigrationStepViewModel
                {
                    Title = "Keep legacy sources disabled by default",
                    Detail = "Do not enable RuntimeSafety.UseLegacyDataSources until a verified read-only connection string is available locally."
                },
                new MigrationStepViewModel
                {
                    Title = "Never allow live writes during first-pass migration",
                    Detail = "RuntimeSafety.AllowLiveDatabase must stay false while we scaffold contexts, validate queries, and compare output against the existing publish snapshot."
                },
                new MigrationStepViewModel
                {
                    Title = "Use environment variables for connection strings",
                    Detail = "Keep real credentials out of source control and only inject read-only strings through local environment configuration."
                }
            },
            Batches = LegacyReadBatches.Batches
                .Select(batch => new MigrationBatchViewModel
                {
                    Name = batch.Name,
                    Source = batch.Source.ToString(),
                    Goal = batch.Goal,
                    Entities = batch.Entities,
                    Actions = batch.Actions
                        .Select(action => new MigrationActionViewModel
                        {
                            ActionName = action.ActionName,
                            Purpose = action.Purpose,
                            Queries = action.Queries,
                            WriteSideBehaviorsToRemove = action.WriteSideBehaviorsToRemove
                        })
                        .ToArray()
                })
                .ToArray(),
            RepositoryPlans = LegacyRepositoryPlans.Plans
                .Select(plan => new RepositoryPlanViewModel
                {
                    RepositoryName = plan.RepositoryName,
                    Source = plan.Source,
                    Methods = plan.Methods
                        .Select(method => new RepositoryMethodPlanViewModel
                        {
                            MethodName = method.MethodName,
                            Purpose = method.Purpose,
                            Entities = method.Entities,
                            QuerySteps = method.QuerySteps,
                            Parameters = method.Parameters
                                .Select(parameter => new RepositoryMethodParameterViewModel
                                {
                                    Name = parameter.Name,
                                    Value = parameter.Value,
                                    Reason = parameter.Reason
                                })
                                .ToArray(),
                            TargetModels = method.TargetModels,
                            Guardrails = method.Guardrails
                        })
                        .ToArray()
                })
                .ToArray(),
            RoutePreviewGroups = PublicReadRouteContractCatalog.CreateRoutePreviewGroups(),
            PublicReadLinkReadiness = PublicReadRouteContractCatalog.CreatePublicReadRouteReadiness(),
            Workstreams = new[]
            {
                "Replace ASP.NET MVC 5 hosting with ASP.NET Core MVC hosting",
                "Migrate EF6 Database First models to EF Core in a separate data project",
                "Move static assets and Razor views in small batches against the latest publish output",
                "Keep live database access disabled until read-only verification rules are in place"
            }
        });
    }

    [HttpGet("newnews")]
    public async Task<IActionResult> NewNews(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            // Resolve the repository only when activated: constructing it builds the read context, whose lazy
            // connection resolution throws while the Szaipa source is Disabled. Keeping it out of the
            // constructor means the default (DB-off) path never touches it.
            var newsRepository = HttpContext.RequestServices.GetRequiredService<INewsReadRepository>();

            // Legacy newnews lists all news ordered by Date descending (count 0 = no cap).
            var news = await newsRepository.GetLatestNewsAsync(0, cancellationToken);
            return View("NewNews", news);
        }

        return View("NewNewsSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateHomeNewsListDefinition()));
    }

    [HttpGet("newnewsread/{id:int}")]
    public async Task<IActionResult> NewNewsRead(int id, CancellationToken cancellationToken)
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
            return View("NewNewsRead", new NewsDetailPageViewModel { Article = article, Related = related });
        }

        return View("NewNewsReadSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateHomeNewsDetailDefinition(id)));
    }

    [HttpGet("vip")]
    [HttpGet("newvip")]
    public async Task<IActionResult> NewVip(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var artistRepository = HttpContext.RequestServices.GetRequiredService<IArtistReadRepository>();
            var artists = await artistRepository.GetArtistsAsync(newestFirst: true, cancellationToken);
            return View("NewVip", artists);
        }

        return View("NewVipSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateHomeArtistListDefinition()));
    }

    [HttpGet("newabout")]
    public IActionResult NewAbout() => View("NewAbout");

    [HttpGet("newArt/{id:int}")]
    public async Task<IActionResult> NewArt(int id, CancellationToken cancellationToken)
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

            return View("NewArt", profile);
        }

        return View("NewArtSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateHomeArtistProfileDefinition(id)));
    }

    [HttpGet("PublicationList")]
    public async Task<IActionResult> PublicationList(CancellationToken cancellationToken)
    {
        if (SzaipaReadModelsActive())
        {
            var publicationRepository = HttpContext.RequestServices.GetRequiredService<IPublicationReadRepository>();
            var publications = await publicationRepository.GetLatestPublicationsAsync(0, cancellationToken);
            return View("PublicationList", publications);
        }

        return View("PublicationListSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateHomePublicationListDefinition()));
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

        return View("PublicationSkeleton", PageSkeletonFactory.Create(PublicReadRouteContractCatalog.CreateHomePublicationDetailDefinition(id)));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}
