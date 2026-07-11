using Szaipa.Data.Composition;
using Szaipa.Data.Scaffolding;
using Szaipa.Web.Models;

namespace Szaipa.Web.Services;

public static class PublicReadRouteContractCatalog
{
    public static LandingPreviewViewModel CreateLandingPreview()
    {
        return new LandingPreviewViewModel
        {
            Eyebrow = "Home / newIndex",
            Title = "Landing page migration preview",
            Description = "This preview mirrors the first public homepage sections we will migrate first: news and exhibition content driven by the new read-only repositories.",
            Sections =
            [
                new LandingPreviewSectionViewModel
                {
                    AnchorId = "section06",
                    Kind = LandingPreviewSectionKind.News,
                    Title = "新闻动态",
                    Subtitle = "NEWS",
                    Description = $"The first public read slice. Renders the latest {LegacyDisplayRules.LandingPageNewsCount} news rows into HomePageSnapshotModel, applies the {LegacyDisplayRules.LandingPageNewsSubtitleMaxLength}-character subtitle trim, preserves the Important-vs-normal split (HomePageSnapshotModel.ImportantNews / NormalNews), and routes into the dedicated news list and detail pages.",
                    TargetRoute = "/Home/newnews",
                    Dependencies =
                    [
                        "INewsReadRepository.GetHomePageSnapshotAsync",
                        "INewsReadRepository.GetLatestNewsAsync",
                        "legacy/content/newsImg",
                        "legacy/content/icon"
                    ],
                    Items =
                    [
                        new LandingPreviewItemViewModel
                        {
                            Label = "Important row",
                            Marker = "01",
                            Meta = "2026.06.17",
                            MetaAccent = "Top story",
                            Subline = "Homepage featured story with subtitle trim and image treatment",
                            CallToAction = "查看全部 VIEW MORE",
                            SecondaryAction = "进入新闻列表",
                            Title = "Featured headline placeholder",
                            Detail = "Represents one Important news row with image, subtitle, and CTA treatment.",
                            IsFeatured = true,
                            Kind = LandingPreviewItemKind.FeaturedHeadline
                        },
                        new LandingPreviewItemViewModel
                        {
                            Label = "Standard row",
                            Marker = "02",
                            Meta = "2026.06.16",
                            MetaAccent = "List rail",
                            CallToAction = "阅读全文",
                            Title = "Date + title list placeholder",
                            Detail = "Represents the normal descending news entries from the latest 6-row homepage slice.",
                            IsFeatured = false,
                            Kind = LandingPreviewItemKind.StandardListRow
                        }
                    ]
                },
                new LandingPreviewSectionViewModel
                {
                    AnchorId = "section07",
                    Kind = LandingPreviewSectionKind.Exhibition,
                    Title = "展会活动",
                    Subtitle = "EXHIBITION",
                    Description = "The second homepage read slice. Surfaces publication summaries ordered by StartDate descending into HomePageSnapshotModel.FeaturedPublications; the view then partitions by Status (true = active section, false = ended section, null = dropped from both). Cards require FolderName (image carousel path) plus zhuban/chengban/xieban organizer lines, then lead into publication list/detail flows.",
                    TargetRoute = "/Home/PublicationList",
                    Dependencies =
                    [
                        "IPublicationReadRepository.GetLatestPublicationsAsync",
                        "legacy/content/123",
                        "legacy/content/icon"
                    ],
                    Items =
                    [
                        new LandingPreviewItemViewModel
                        {
                            Label = "Exhibition card",
                            Marker = "A1",
                            Meta = "Primary card",
                            MetaAccent = "Lead programme",
                            Subline = "StartDate-descending publication summary with strong lead visual",
                            CallToAction = "进入展会预览",
                            SecondaryAction = "查看活动列表",
                            Title = "Publication summary placeholder",
                            Detail = "Represents a StartDate-descending exhibition/publication summary card.",
                            IsFeatured = true,
                            Kind = LandingPreviewItemKind.FeaturedCard
                        },
                        new LandingPreviewItemViewModel
                        {
                            Label = "Navigation rail",
                            Marker = "A2",
                            Meta = "Secondary navigation",
                            MetaAccent = "Next item",
                            CallToAction = "进入详情",
                            TertiaryAction = "->",
                            Title = "Supporting exhibition items",
                            Detail = "Represents the secondary publication summaries that will lead into detail pages.",
                            IsFeatured = false,
                            Kind = LandingPreviewItemKind.SupportingCard
                        }
                    ]
                }
            ]
        };
    }

    public static IReadOnlyList<RoutePreviewGroupViewModel> CreateRoutePreviewGroups()
    {
        return
        [
            new RoutePreviewGroupViewModel
            {
                Name = "Home",
                Routes =
                [
                    new RoutePreviewLinkViewModel
                    {
                        Title = "News list skeleton",
                        Route = "/Home/newnews",
                        Status = "Dedicated skeleton with news/list layout preview"
                    },
                    new RoutePreviewLinkViewModel
                    {
                        Title = "News detail skeleton",
                        Route = "/Home/newnewsread/1000",
                        Status = "Dedicated skeleton with asset dependency panel"
                    },
                    new RoutePreviewLinkViewModel
                    {
                        Title = "Artist list skeleton",
                        Route = "/Home/newvip",
                        Status = "Dedicated skeleton with asset dependency panel"
                    },
                    new RoutePreviewLinkViewModel
                    {
                        Title = "Artist profile skeleton",
                        Route = "/Home/newArt/1000",
                        Status = "Dedicated skeleton with asset dependency panel"
                    },
                    new RoutePreviewLinkViewModel
                    {
                        Title = "Publication list skeleton",
                        Route = "/Home/PublicationList",
                        Status = "Dedicated skeleton with exhibition/card layout preview"
                    }
                ]
            }
        ];
    }

    public static IReadOnlyList<PublicReadRouteReadinessViewModel> CreatePublicReadRouteReadiness()
    {
        return
        [
            CreateLandingReadiness(),
            CreateReadiness("Home", CreateHomeNewsListDefinition()),
            CreateReadiness("Home", CreateHomeNewsDetailDefinition(1000)),
            CreateReadiness("Home", CreateHomeArtistListDefinition()),
            CreateReadiness("Home", CreateHomeArtistProfileDefinition(1000)),
            CreateReadiness("Home", CreateHomePublicationListDefinition()),
            CreateReadiness("Home", CreateHomePublicationDetailDefinition(1000))
        ];
    }

    private static PublicReadRouteReadinessViewModel CreateReadiness(string group, PageSkeletonDefinition definition)
    {
        var contractComplete =
            !string.IsNullOrWhiteSpace(definition.TargetRepository)
            && definition.QueryParameters.Count > 0
            && definition.TargetReadModels.Count > 0
            && definition.Guardrails.Count > 0;
        var blueprintAligned =
            definition.Blueprint is { } blueprint
            && !string.IsNullOrWhiteSpace(blueprint.Purpose)
            && blueprint.QuerySteps.Count > 0;
        var assetMappingListed = definition.AssetDependencies.Count > 0;
        var writeRemovalDefined = definition.RemovedWriteBehaviors.Count > 0;

        return new PublicReadRouteReadinessViewModel
        {
            Group = group,
            LegacyRoute = definition.LegacyRoute,
            TargetRepositoryMethod = definition.TargetRepository,
            ContractComplete = contractComplete,
            BlueprintAligned = blueprintAligned,
            AssetMappingListed = assetMappingListed,
            WriteRemovalDefined = writeRemovalDefined,
            ReadyForScaffold = contractComplete && blueprintAligned && assetMappingListed && writeRemovalDefined
        };
    }

    private static PublicReadRouteReadinessViewModel CreateLandingReadiness()
    {
        var landing = CreateLandingPreview();
        var snapshotPlan = LegacyRepositoryPlans.Plans
            .First(plan => plan.RepositoryName == "INewsReadRepository")
            .Methods.First(method => method.MethodName == "GetHomePageSnapshotAsync");
        var newIndexAction = LegacyReadBatches.Batches
            .SelectMany(batch => batch.Actions)
            .First(action => action.ActionName == "newIndex");

        var contractComplete =
            snapshotPlan.Parameters.Count > 0
            && snapshotPlan.TargetModels.Count > 0
            && snapshotPlan.Guardrails.Count > 0
            && landing.Sections.All(section =>
                !string.IsNullOrWhiteSpace(section.TargetRoute) && section.Dependencies.Count > 0);
        var blueprintAligned = snapshotPlan.QuerySteps.Count > 0;
        var assetMappingListed = landing.Sections
            .SelectMany(section => section.Dependencies)
            .Any(dependency => dependency.StartsWith("legacy/content", StringComparison.OrdinalIgnoreCase));
        var writeRemovalDefined = newIndexAction.WriteSideBehaviorsToRemove.Count > 0;

        return new PublicReadRouteReadinessViewModel
        {
            Group = "Home",
            LegacyRoute = "/Home/newIndex",
            TargetRepositoryMethod = "INewsReadRepository.GetHomePageSnapshotAsync",
            ContractComplete = contractComplete,
            BlueprintAligned = blueprintAligned,
            AssetMappingListed = assetMappingListed,
            WriteRemovalDefined = writeRemovalDefined,
            ReadyForScaffold = contractComplete && blueprintAligned && assetMappingListed && writeRemovalDefined
        };
    }

    public static PageSkeletonDefinition CreateHomeNewsListDefinition()
    {
        return new PageSkeletonDefinition
        {
            Title = "News List Migration Skeleton",
            Eyebrow = "Home Content",
            Lead = "This route now has a dedicated ASP.NET Core page skeleton and is ready for the first read-only news list implementation.",
            LegacyRoute = "/Home/newnews",
            TargetRepository = "INewsReadRepository.GetLatestNewsAsync",
            TargetBatch = "Home content",
            Sections =
            [
                new PageSkeletonSectionViewModel
                {
                    Title = "Featured rows",
                    Description = "Important and standard news rows based on the legacy Important flag."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "News list stream",
                    Description = "A read-only list ordered by Date descending."
                }
            ],
            AssetDependencies = HomeNewsAssets,
            QueryParameters =
            [
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "Order",
                    Value = "Date DESC",
                    Reason = "The legacy news list orders rows by publish date descending."
                }
            ],
            Guardrails = HomeNewsGuardrails,
            TargetReadModels = NewsListTargetModels,
            RemovedWriteBehaviors = HomeNewsRemovedWrites,
            Blueprint = CreateBlueprint("INewsReadRepository", "GetLatestNewsAsync", "GetLatestNewsAsync"),
            NextSteps = CommonPageSkeletonNextSteps
        };
    }

    public static PageSkeletonDefinition CreateHomeNewsDetailDefinition(int id)
    {
        return new PageSkeletonDefinition
        {
            Title = "News Detail Migration Skeleton",
            Eyebrow = "Home Content",
            Lead = "This detail page skeleton is reserved for the first read-only news detail flow and its sidebar snapshots.",
            LegacyRoute = $"/Home/newnewsread/{id}",
            TargetRepository = "INewsReadRepository.GetNewsByIdAsync",
            TargetBatch = "Home content",
            Sections =
            [
                new PageSkeletonSectionViewModel
                {
                    Title = "Article header",
                    Description = "Title, date, cover, and author fields from NewsDetailModel."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "Article body",
                    Description = "Read-only content area with no ReadCount side effects."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "Related sidebar",
                    Description = "The latest 5 news summaries for the surrounding navigation."
                }
            ],
            AssetDependencies = HomeNewsAssets,
            QueryParameters =
            [
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "DetailLookup",
                    Value = "Id",
                    Reason = "The old route fetches one news row by primary key."
                },
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "RelatedNewsCount",
                    Value = "5",
                    Reason = "The legacy sidebar shows the latest 5 news items."
                }
            ],
            Guardrails = HomeNewsGuardrails,
            TargetReadModels = NewsDetailTargetModels,
            RemovedWriteBehaviors = HomeNewsRemovedWrites,
            Blueprint = CreateBlueprint("INewsReadRepository", "GetNewsByIdAsync", "GetNewsByIdAsync"),
            NextSteps = CommonPageSkeletonNextSteps
        };
    }

    public static PageSkeletonDefinition CreateHomeArtistListDefinition()
    {
        return new PageSkeletonDefinition
        {
            Title = "Artist List Migration Skeleton",
            Eyebrow = "Artist Browsing",
            Lead = "This artist list page skeleton is ready for the first read-only summary list once the artist repository is wired.",
            LegacyRoute = "/Home/newvip",
            TargetRepository = "IArtistReadRepository.GetArtistsAsync",
            TargetBatch = "Artist browsing",
            Sections =
            [
                new PageSkeletonSectionViewModel
                {
                    Title = "Artist grid",
                    Description = "A descending Id artist summary list aligned with the legacy newvip page."
                }
            ],
            AssetDependencies = ArtistAssets,
            QueryParameters =
            [
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "Order",
                    Value = "Id DESC",
                    Reason = "The legacy newvip page orders artists by descending Id."
                }
            ],
            Guardrails = ArtistGuardrails,
            TargetReadModels = ArtistListTargetModels,
            RemovedWriteBehaviors = ArtistRemovedWrites,
            Blueprint = CreateBlueprint("IArtistReadRepository", "GetArtistsAsync", "GetArtistsAsync"),
            NextSteps = CommonPageSkeletonNextSteps
        };
    }

    public static PageSkeletonDefinition CreateHomeArtistProfileDefinition(int id)
    {
        return new PageSkeletonDefinition
        {
            Title = "Artist Profile Migration Skeleton",
            Eyebrow = "Artist Browsing",
            Lead = "This artist profile skeleton is reserved for the first read-only snapshot implementation with works and side-panel content.",
            LegacyRoute = $"/Home/newArt/{id}",
            TargetRepository = "IArtistReadRepository.GetArtistProfileAsync",
            TargetBatch = "Artist browsing",
            Sections =
            [
                new PageSkeletonSectionViewModel
                {
                    Title = "Artist header",
                    Description = "Primary artist fields plus intro and portrait paths."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "Works panel",
                    Description = "Read-only works list by ArtistId."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "Side-panel feeds",
                    Description = "First (unordered Take(1)) news, publication placeholder, favorites, and auctions — legacy does not OrderBy."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "Related exhibitions",
                    Description = "Per-artist '相关展览 / EXHIBITION' swiper from Exhibition by ArtistId (separate from the landing Publication list)."
                }
            ],
            AssetDependencies = ArtistAssets,
            QueryParameters =
            [
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "ArtistLookup",
                    Value = "Id",
                    Reason = "The old route starts from one Artist row by primary key."
                },
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "SidePanelCountPerFeed",
                    Value = "1 (unordered)",
                    Reason = "Each side-panel feed takes the FIRST row via an unordered Take(1) (legacy does not OrderByDescending); reproduce the unordered row for parity."
                }
            ],
            Guardrails = ArtistGuardrails,
            TargetReadModels = ArtistProfileTargetModels,
            RemovedWriteBehaviors = ArtistRemovedWrites,
            Blueprint = CreateBlueprint("IArtistReadRepository", "GetArtistProfileAsync", "GetArtistProfileAsync"),
            NextSteps = CommonPageSkeletonNextSteps
        };
    }

    public static PageSkeletonDefinition CreateHomePublicationListDefinition()
    {
        return new PageSkeletonDefinition
        {
            Title = "Publication List Migration Skeleton",
            Eyebrow = "Home Content",
            Lead = "This publication list skeleton is ready for the first read-only exhibition list implementation.",
            LegacyRoute = "/Home/PublicationList",
            TargetRepository = "IPublicationReadRepository.GetLatestPublicationsAsync",
            TargetBatch = "Home content",
            Sections =
            [
                new PageSkeletonSectionViewModel
                {
                    Title = "Publication stream",
                    Description = "Descending publication list with read-only exhibition summary cards."
                }
            ],
            AssetDependencies = PublicationAssets,
            QueryParameters =
            [
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "ListOrder",
                    Value = "Id DESC",
                    Reason = "The legacy publication list page orders rows by descending Id."
                }
            ],
            Guardrails = PublicationGuardrails,
            TargetReadModels = PublicationListTargetModels,
            RemovedWriteBehaviors = PublicationRemovedWrites,
            Blueprint = CreateBlueprint("IPublicationReadRepository", "GetLatestPublicationsAsync", "GetLatestPublicationsAsync"),
            NextSteps = CommonPageSkeletonNextSteps
        };
    }

    public static PageSkeletonDefinition CreateHomePublicationDetailDefinition(int id)
    {
        return new PageSkeletonDefinition
        {
            Title = "Publication Detail Migration Skeleton",
            Eyebrow = "Home Content",
            Lead = "This publication detail skeleton is reserved for the first read-only detail snapshot and related list flow.",
            LegacyRoute = $"/Home/Publication/{id}",
            TargetRepository = "IPublicationReadRepository.GetPublicationDetailSnapshotAsync",
            TargetBatch = "Home content",
            Sections =
            [
                new PageSkeletonSectionViewModel
                {
                    Title = "Publication hero",
                    Description = "Primary title, dates, location, and branding assets."
                },
                new PageSkeletonSectionViewModel
                {
                    Title = "Related publications",
                    Description = "Read-only neighboring publication summaries for navigation."
                }
            ],
            AssetDependencies = PublicationAssets,
            QueryParameters =
            [
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "DetailLookup",
                    Value = "Id",
                    Reason = "The old publication detail route fetches one Publication by primary key."
                },
                new PageSkeletonQueryParameterViewModel
                {
                    Name = "RelatedSource",
                    Value = "Publication list",
                    Reason = "The detail page also carries neighboring publication navigation."
                }
            ],
            Guardrails = PublicationGuardrails,
            TargetReadModels = PublicationDetailTargetModels,
            RemovedWriteBehaviors = PublicationRemovedWrites,
            Blueprint = CreateBlueprint("IPublicationReadRepository", "GetPublicationDetailSnapshotAsync", "GetPublicationDetailSnapshotAsync"),
            NextSteps = CommonPageSkeletonNextSteps
        };
    }

    private static PageSkeletonBlueprintViewModel CreateBlueprint(
        string repositoryName,
        string methodLabel,
        string legacyPlanMethodName)
    {
        var repositoryPlan = LegacyRepositoryPlans.Plans.First(plan => plan.RepositoryName == repositoryName);
        var methodPlan = repositoryPlan.Methods.First(method => method.MethodName == legacyPlanMethodName);

        return new PageSkeletonBlueprintViewModel
        {
            RepositoryName = repositoryName,
            MethodName = methodLabel,
            Source = repositoryPlan.Source,
            Purpose = methodPlan.Purpose,
            Entities = methodPlan.Entities,
            QuerySteps = methodPlan.QuerySteps
        };
    }

    private static readonly string[] CommonPageSkeletonNextSteps =
    [
        "Generate or wire the read-only EF Core query path for this route.",
        "Port the legacy Razor layout and static assets in the matching batch.",
        "Keep all visit-count and read-count writes disabled.",
        "Compare the rendered output against the latest publish snapshot."
    ];

    private static readonly PageSkeletonGuardrailViewModel[] HomeNewsGuardrails =
    [
        new()
        {
            Title = "No read-count writes",
            Detail = "Do not increment News.ReadCount or recreate Diary.NewsVisit behavior anywhere in the public flow."
        },
        new()
        {
            Title = "Read-only projection only",
            Detail = "Keep repository output limited to read models and prefer raw date values over controller-side formatting."
        }
    ];

    private static readonly PageSkeletonTargetModelViewModel[] NewsListTargetModels =
    [
        new()
        {
            Name = "NewsSummaryModel",
            Purpose = "Carries list-row title, subtitle, date, cover path, and Important flag for the news list."
        }
    ];

    private static readonly PageSkeletonTargetModelViewModel[] NewsDetailTargetModels =
    [
        new()
        {
            Name = "NewsDetailModel",
            Purpose = "Carries title, subtitle, author, content, cover path, and date for the detail payload."
        },
        new()
        {
            Name = "NewsSummaryModel",
            Purpose = "Supports the related sidebar rows around the news detail view."
        }
    ];

    private static readonly PageSkeletonRemovedWriteViewModel[] HomeNewsRemovedWrites =
    [
        new()
        {
            LegacyBehavior = "News.ReadCount++",
            Reason = "Public read routes must not mutate counters during the read-only migration."
        },
        new()
        {
            LegacyBehavior = "Diary.NewsVisit++",
            Reason = "Legacy analytics writebacks stay out of the first-pass ASP.NET Core public flow."
        },
        new()
        {
            LegacyBehavior = "SaveChanges()",
            Reason = "Repository activation must remain read-only until live-db policy changes deliberately."
        }
    ];

    private static readonly PageSkeletonGuardrailViewModel[] ArtistGuardrails =
    [
        new()
        {
            Title = "No visit logging",
            Detail = "Do not increment Artist.VisitCount and do not recreate Diary.ArtVisit when activating artist pages."
        },
        new()
        {
            Title = "Side-panel compatibility stays explicit",
            Detail = "The ArtNews-backed publication placeholder should remain an explicit compatibility decision during EF Core wiring."
        }
    ];

    private static readonly PageSkeletonTargetModelViewModel[] ArtistListTargetModels =
    [
        new()
        {
            Name = "ArtistSummaryModel",
            Purpose = "Carries the artist summary grid/list fields used by the public newvip route."
        }
    ];

    private static readonly PageSkeletonTargetModelViewModel[] ArtistProfileTargetModels =
    [
        new()
        {
            Name = "ArtistProfileSnapshotModel",
            Purpose = "Wraps the full public artist page payload before the view is activated."
        },
        new()
        {
            Name = "ArtistDetailModel",
            Purpose = "Carries the primary artist identity, intro, portrait, and biography fields."
        },
        new()
        {
            Name = "WorkSummaryModel",
            Purpose = "Feeds the read-only artist works panel."
        },
        new()
        {
            Name = "ArtistSidePanelItemModel",
            Purpose = "Feeds the first-row (unordered Take(1)) news, publication placeholder, favorite, and auction side-panel rows."
        },
        new()
        {
            Name = "ArtistExhibitionItemModel",
            Purpose = "Feeds the per-artist '相关展览 / EXHIBITION' swiper sourced from Exhibition by ArtistId."
        }
    ];

    private static readonly PageSkeletonRemovedWriteViewModel[] ArtistRemovedWrites =
    [
        new()
        {
            LegacyBehavior = "Artist.VisitCount++",
            Reason = "Public artist pages must not mutate visit counters in the first migration pass."
        },
        new()
        {
            LegacyBehavior = "Diary.ArtVisit++",
            Reason = "Legacy artist analytics writes are explicitly out of scope for the read-only flow."
        },
        new()
        {
            LegacyBehavior = "SaveChanges()",
            Reason = "The activated artist read path should project only and avoid any write-capable unit of work."
        }
    ];

    private static readonly PageSkeletonGuardrailViewModel[] PublicationGuardrails =
    [
        new()
        {
            Title = "No publication writebacks",
            Detail = "Do not increment Publication.ReadCount and do not allow SaveChanges paths in public publication requests."
        },
        new()
        {
            Title = "Keep raw date values",
            Detail = "Date formatting belongs in Razor so the repository can stay focused on read-only projection."
        }
    ];

    private static readonly PageSkeletonTargetModelViewModel[] PublicationListTargetModels =
    [
        new()
        {
            Name = "PublicationSummaryModel",
            Purpose = "Carries the public exhibition list card fields for landing and publication index views."
        }
    ];

    private static readonly PageSkeletonTargetModelViewModel[] PublicationDetailTargetModels =
    [
        new()
        {
            Name = "PublicationDetailSnapshotModel",
            Purpose = "Wraps the detail payload plus neighboring publication summaries."
        },
        new()
        {
            Name = "PublicationDetailModel",
            Purpose = "Carries title, dates, location, logo, cover, and organizer fields for the public detail page."
        },
        new()
        {
            Name = "PublicationSummaryModel",
            Purpose = "Feeds the surrounding publication navigation around the detail view."
        }
    ];

    private static readonly PageSkeletonRemovedWriteViewModel[] PublicationRemovedWrites =
    [
        new()
        {
            LegacyBehavior = "Publication.ReadCount++",
            Reason = "Publication detail activation must not mutate counters during the read-only phase."
        },
        new()
        {
            LegacyBehavior = "SaveChanges()",
            Reason = "Public exhibition pages stay projection-only until a later, explicit write policy exists."
        }
    ];

    // Asset SourcePath values are display-only migration-dashboard metadata. The "<ContentRoot>" token stands
    // for the configured LegacyAssets:ContentRoot (LegacyAssetsOptions.ContentRoot) so the dashboard shows a
    // portable, machine-independent path instead of a baked-in absolute local path.
    private static readonly PageAssetDependencyViewModel[] HomeNewsAssets =
    [
        new()
        {
            SourcePath = "<ContentRoot>/newsImg",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/newsImg",
            Reason = "News covers and article imagery for landing, list, and detail pages."
        },
        new()
        {
            SourcePath = "<ContentRoot>/icon",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/icon",
            Reason = "Legacy arrows and navigation accents used across the news surfaces."
        },
        new()
        {
            SourcePath = "<ContentRoot>/fonts",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/fonts",
            Reason = "Typography parity for the public landing and news pages."
        }
    ];

    private static readonly PageAssetDependencyViewModel[] PublicationAssets =
    [
        new()
        {
            SourcePath = "<ContentRoot>/123",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/123",
            Reason = "Shared exhibition and themed imagery used by publication-related surfaces."
        },
        new()
        {
            SourcePath = "<ContentRoot>/icon",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/icon",
            Reason = "Legacy publication navigation accents."
        }
    ];

    private static readonly PageAssetDependencyViewModel[] ArtistAssets =
    [
        new()
        {
            SourcePath = "<ContentRoot>/ArtImg",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/artimg",
            Reason = "Artist portraits and works imagery for newvip and newArt."
        },
        new()
        {
            SourcePath = "<ContentRoot>/Model",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/model",
            Reason = "Legacy model CSS and related artwork-page dependencies."
        },
        new()
        {
            SourcePath = "<ContentRoot>/icon",
            TargetPath = "src/Szaipa.Web/wwwroot/legacy/content/icon",
            Reason = "Shared icons used in artist browsing surfaces."
        }
    ];

}
