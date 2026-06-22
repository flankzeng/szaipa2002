namespace Szaipa.Data.Scaffolding;

public static class LegacyRepositoryPlans
{
    public static IReadOnlyList<LegacyRepositoryPlan> Plans { get; } =
    [
        new LegacyRepositoryPlan
        {
            RepositoryName = "INewsReadRepository",
            Source = "Szaipa",
            Methods =
            [
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetHomePageSnapshotAsync",
                    Purpose = "Support Home/newIndex with latest news plus featured publication summaries.",
                    Entities = ["News", "Publication"],
                    QuerySteps =
                    [
                        "Load latest news ordered by Date descending and cap to landing-page count.",
                        "Project to NewsSummaryModel before leaving IQueryable.",
                        "Load publication summaries ordered by StartDate descending for the exhibition section.",
                        "Return HomePageSnapshotModel with both lists populated."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "LatestNewsCount",
                            Value = "6",
                            Reason = "The legacy Home/newIndex page takes the latest 6 news rows."
                        },
                        new LegacyQueryParameterPlan
                        {
                            Name = "PublicationOrder",
                            Value = "StartDate DESC",
                            Reason = "The legacy landing page orders exhibition content by StartDate descending."
                        }
                    ],
                    TargetModels = ["HomePageSnapshotModel", "NewsSummaryModel", "PublicationSummaryModel"],
                    Guardrails =
                    [
                        "Do not call SaveChanges.",
                        "Do not create Diary rows or visit logs.",
                        "Keep date formatting in Razor, not in the repository."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetLatestNewsAsync",
                    Purpose = "Support Home/newnews with the public descending news list.",
                    Entities = ["News"],
                    QuerySteps =
                    [
                        "Load News rows ordered by Date descending.",
                        "Cap the result set with the supplied count when needed.",
                        "Project rows to NewsSummaryModel before returning."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "Order",
                            Value = "Date DESC",
                            Reason = "The legacy Home/newnews page orders public news rows by Date descending."
                        }
                    ],
                    TargetModels = ["NewsSummaryModel"],
                    Guardrails =
                    [
                        "Do not increment ReadCount.",
                        "Do not write analytics rows.",
                        "Keep public list behavior independent from session or staff state."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetNewsByIdAsync",
                    Purpose = "Load the primary Home/newnewsread detail payload.",
                    Entities = ["News"],
                    QuerySteps =
                    [
                        "Load one News row by primary key.",
                        "Project the row directly to NewsDetailModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "DetailLookup",
                            Value = "Primary key Id",
                            Reason = "The old controller fetches one News row by Id before rendering the detail page."
                        }
                    ],
                    TargetModels = ["NewsDetailModel"],
                    Guardrails =
                    [
                        "Do not increment ReadCount.",
                        "Do not update Diary.NewsVisit.",
                        "Keep detail projection read-only."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetRelatedNewsAsync",
                    Purpose = "Load the Home/newnewsread sidebar summaries.",
                    Entities = ["News"],
                    QuerySteps =
                    [
                        "Load latest News rows ordered by Date descending for sidebar navigation.",
                        "Cap to the supplied count.",
                        "Project rows to NewsSummaryModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "RelatedNewsCount",
                            Value = "5",
                            Reason = "The legacy Home/newnewsread page loads 5 recent news items for the sidebar."
                        }
                    ],
                    TargetModels = ["NewsSummaryModel"],
                    Guardrails =
                    [
                        "Do not increment ReadCount.",
                        "Do not update Diary.NewsVisit.",
                        "Do not derive related rows from session or analytics state."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "SearchNewsAsync",
                    Purpose = "Support keyword filtering once the new news page is wired.",
                    Entities = ["News"],
                    QuerySteps =
                    [
                        "Filter by Title or Content using the supplied keyword.",
                        "Order by Date descending.",
                        "Project matches to NewsSummaryModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "SearchFields",
                            Value = "Title, Content",
                            Reason = "The legacy Search action filters against both title and content."
                        }
                    ],
                    TargetModels = ["NewsSummaryModel"],
                    Guardrails =
                    [
                        "Treat empty keywords as a caller concern or short-circuit safely.",
                        "No analytics writes during search."
                    ]
                }
            ]
        },
        new LegacyRepositoryPlan
        {
            RepositoryName = "IArtistReadRepository",
            Source = "Szaipa",
            Methods =
            [
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetArtistsAsync",
                    Purpose = "Support Home/newvip list pages with stable artist ordering.",
                    Entities = ["Artist"],
                    QuerySteps =
                    [
                        "Order Artist rows by Id ascending or descending based on newestFirst.",
                        "Project the list to ArtistSummaryModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "NewestFirstOrder",
                            Value = "Id DESC",
                            Reason = "The legacy Home/newvip page orders artists by descending Id."
                        },
                        new LegacyQueryParameterPlan
                        {
                            Name = "LegacyVipOrder",
                            Value = "Id ASC",
                            Reason = "The older Home/vip page used ascending Id, so the flag preserves both behaviors."
                        }
                    ],
                    TargetModels = ["ArtistSummaryModel"],
                    Guardrails =
                    [
                        "Do not derive ordering from visit metrics.",
                        "No session-based behavior."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetArtistByIdAsync",
                    Purpose = "Load the primary artist identity payload used by the Home/newArt header.",
                    Entities = ["Artist"],
                    QuerySteps =
                    [
                        "Load one Artist row by primary key.",
                        "Project the row directly to ArtistDetailModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "ArtistLookup",
                            Value = "Primary key Id",
                            Reason = "The legacy Home/newArt action starts from Artist.Id before composing the rest of the page."
                        }
                    ],
                    TargetModels = ["ArtistDetailModel"],
                    Guardrails =
                    [
                        "Do not increment Artist.VisitCount.",
                        "Do not update Diary.ArtVisit.",
                        "Keep the identity projection read-only."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetArtistWorksAsync",
                    Purpose = "Load the read-only works panel for one artist on Home/newArt.",
                    Entities = ["Works"],
                    QuerySteps =
                    [
                        "Load Works rows filtered by ArtistId.",
                        "Project each row to WorkSummaryModel before leaving IQueryable."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "WorksFilter",
                            Value = "ArtistId == id",
                            Reason = "The legacy newArt page loads every Works row for the selected artist (no Take cap, unlike the old Art page)."
                        }
                    ],
                    TargetModels = ["WorkSummaryModel"],
                    Guardrails =
                    [
                        "Do not increment Works.VisitCount.",
                        "Keep the works projection read-only and independent of session state."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetArtistProfileAsync",
                    Purpose = "Support Home/newArt with artist details, works, side-panel content, and the per-artist exhibition feed.",
                    Entities = ["Artist", "Works", "ArtNews", "Fav", "Auction", "Exhibition"],
                    QuerySteps =
                    [
                        "Load one Artist row by primary key and project to ArtistDetailModel.",
                        "Load Works rows by ArtistId and project to WorkSummaryModel.",
                        "Take the first ArtNews row by ArtistId with an UNORDERED Take(1) (legacy does NOT OrderBy) and project to ArtistSidePanelItemModel.",
                        "Take the first Fav row by ArtistId with an UNORDERED Take(1) and project to ArtistSidePanelItemModel.",
                        "Take the first Auction row by ArtistId with an UNORDERED Take(1) and project to ArtistSidePanelItemModel.",
                        "Keep the legacy ArtNews-backed publication placeholder (the 'publication' slot reads ArtNews again) as an explicit compatibility decision.",
                        "Load Exhibition rows by ArtistId and project to ArtistExhibitionItemModel for the '相关展览 / EXHIBITION' swiper (StartDate/EndDate are legacy strings)."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "ArtistLookup",
                            Value = "Primary key Id",
                            Reason = "The legacy Home/newArt action starts from Artist.Id."
                        },
                        new LegacyQueryParameterPlan
                        {
                            Name = "SidePanelCountPerFeed",
                            Value = "1 (unordered)",
                            Reason = "The legacy side panel uses an unordered Take(1) per feed — the FIRST row in default/PK order, NOT OrderByDescending(Date). The Chinese comment says 最新 (latest) but the code does not order; reproduce the unordered row for parity."
                        }
                    ],
                    TargetModels = ["ArtistProfileSnapshotModel", "ArtistDetailModel", "WorkSummaryModel", "ArtistSidePanelItemModel", "ArtistExhibitionItemModel"],
                    Guardrails =
                    [
                        "Do not increment Artist.VisitCount.",
                        "Do not update Diary.ArtVisit.",
                        "Do not call SaveChanges.",
                        "Do not add OrderByDescending to the single-row side-panel feeds; legacy uses an unordered Take(1)."
                    ]
                }
            ]
        },
        new LegacyRepositoryPlan
        {
            RepositoryName = "IPublicationReadRepository",
            Source = "Szaipa",
            Methods =
            [
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetLatestPublicationsAsync",
                    Purpose = "Support landing-page exhibition lists and publication index pages.",
                    Entities = ["Publication"],
                    QuerySteps =
                    [
                        "Order Publication rows by StartDate descending or Id descending based on page need.",
                        "Project list items to PublicationSummaryModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "LandingPageOrder",
                            Value = "StartDate DESC",
                            Reason = "Home/newIndex uses StartDate descending for exhibition content."
                        },
                        new LegacyQueryParameterPlan
                        {
                            Name = "PublicationListOrder",
                            Value = "Id DESC",
                            Reason = "Home/PublicationList uses descending Id for the list page."
                        }
                    ],
                    TargetModels = ["PublicationSummaryModel"],
                    Guardrails =
                    [
                        "Do not mutate ReadCount.",
                        "Keep raw date values in the model."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetPublicationByIdAsync",
                    Purpose = "Resolve one publication summary by primary key for lightweight by-id lookups.",
                    Entities = ["Publication"],
                    QuerySteps =
                    [
                        "Load one Publication row by primary key.",
                        "Project the row directly to PublicationSummaryModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "DetailLookup",
                            Value = "Primary key Id",
                            Reason = "Dedicated/neighbor publication lookups resolve a single Publication row by Id."
                        }
                    ],
                    TargetModels = ["PublicationSummaryModel"],
                    Guardrails =
                    [
                        "Do not increment Publication.ReadCount.",
                        "Keep raw date values and avoid SaveChanges."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetPublicationDetailSnapshotAsync",
                    Purpose = "Support Home/Publication with detail content plus a related list.",
                    Entities = ["Publication"],
                    QuerySteps =
                    [
                        "Load one Publication row by primary key and project to PublicationDetailModel.",
                        "Load related Publication summaries for sidebar or below-fold navigation.",
                        "Return PublicationDetailSnapshotModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "DetailLookup",
                            Value = "Primary key Id",
                            Reason = "The legacy Home/Publication action fetches one Publication row by Id."
                        },
                        new LegacyQueryParameterPlan
                        {
                            Name = "RelatedSource",
                            Value = "Publication list",
                            Reason = "The legacy detail page also loads a publication list for surrounding navigation."
                        }
                    ],
                    TargetModels = ["PublicationDetailSnapshotModel", "PublicationDetailModel", "PublicationSummaryModel"],
                    Guardrails =
                    [
                        "Do not increment Publication.ReadCount.",
                        "Do not call SaveChanges."
                    ]
                }
            ]
        },
        new LegacyRepositoryPlan
        {
            RepositoryName = "ITongouReadRepository",
            Source = "Tongou",
            Methods =
            [
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetWorkByIdAsync",
                    Purpose = "Support Project_Tongou/Work detail views.",
                    Entities = ["TongouWorks"],
                    QuerySteps =
                    [
                        "Load one TongouWorks row by primary key.",
                        "Project to TongouWorkModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "DefaultId",
                            Value = "1",
                            Reason = "The legacy Project_Tongou/Work action defaults to id 1 when no route value is supplied."
                        }
                    ],
                    TargetModels = ["TongouWorkModel"],
                    Guardrails =
                    [
                        "Do not increment VisityCount.",
                        "Preserve special redirect behavior in the controller layer, not the repository."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetArtistByIdAsync",
                    Purpose = "Support dedicated Tongou artist routes that only need one artist payload.",
                    Entities = ["TongouAtrist"],
                    QuerySteps =
                    [
                        "Load one TongouAtrist row by primary key.",
                        "Project directly to TongouArtistModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "ArtistLookup",
                            Value = "Primary key Id",
                            Reason = "Dedicated artist routes such as beiniTD resolve from one TongouAtrist row."
                        }
                    ],
                    TargetModels = ["TongouArtistModel"],
                    Guardrails =
                    [
                        "Do not write hot counts or view counts.",
                        "Keep dedicated route branching in the controller layer."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetArtistProfileAsync",
                    Purpose = "Support Project_Tongou/WorkList with artist plus works (the generic Atrist route uses GetArtistByIdAsync — single artist, no works).",
                    Entities = ["TongouAtrist", "TongouWorks"],
                    QuerySteps =
                    [
                        "Load one TongouAtrist row by primary key and project to TongouArtistModel.",
                        "Load TongouWorks rows by Atristid and project to TongouWorkModel.",
                        "Return TongouArtistProfileSnapshotModel."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "DefaultArtistId",
                            Value = "1",
                            Reason = "The legacy Tongou artist and work-list flows default to id 1 when the route value is missing."
                        },
                        new LegacyQueryParameterPlan
                        {
                            Name = "WorksFilter",
                            Value = "Atristid == id",
                            Reason = "The legacy WorkList action filters TongouWorks by Atristid."
                        }
                    ],
                    TargetModels = ["TongouArtistProfileSnapshotModel", "TongouArtistModel", "TongouWorkModel"],
                    Guardrails =
                    [
                        "Do not write hot counts or view counts.",
                        "Keep controller-only redirects outside repository code."
                    ]
                },
                new LegacyRepositoryMethodPlan
                {
                    MethodName = "GetWorksByArtistIdAsync",
                    Purpose = "Load the read-only Tongou work list for one artist on Project_Tongou/WorkList.",
                    Entities = ["TongouWorks"],
                    QuerySteps =
                    [
                        "Load TongouWorks rows filtered by Atristid.",
                        "Project each row to TongouWorkModel before returning."
                    ],
                    Parameters =
                    [
                        new LegacyQueryParameterPlan
                        {
                            Name = "WorksFilter",
                            Value = "Atristid == id",
                            Reason = "The legacy WorkList action filters TongouWorks by Atristid for the selected artist."
                        }
                    ],
                    TargetModels = ["TongouWorkModel"],
                    Guardrails =
                    [
                        "Do not increment VisityCount.",
                        "Keep the work list projection read-only and free of hot-count writes."
                    ]
                }
            ]
        }
    ];
}
