using Szaipa.Data.Configuration;

namespace Szaipa.Data.Scaffolding;

public static class LegacyReadBatches
{
    public static IReadOnlyList<LegacyReadBatch> Batches { get; } =
    [
        new LegacyReadBatch
        {
            Name = "Home content",
            Source = LegacyDataSource.Szaipa,
            Entities = ["News", "Publication"],
            Controllers = ["HomeController"],
            Goal = "Support landing page, news list, publication list, and content detail pages without write-side behavior.",
            Actions =
            [
                new LegacyReadActionPlan
                {
                    ActionName = "newIndex",
                    Purpose = "Render the modern landing page with featured news and exhibition listings.",
                    Queries =
                    [
                        "Load the latest 6 rows from News ordered by Date descending.",
                        "Project each News row into the landing-page summary shape used by ViewBag.n.",
                        "Load Publication rows ordered by StartDate descending for exhibition listing blocks."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "Do not call HaveViti or create Diary rows during page load.",
                        "Do not mutate any visit counters while composing the response."
                    ]
                },
                new LegacyReadActionPlan
                {
                    ActionName = "newnews",
                    Purpose = "Render the full news listing page with important and normal rows.",
                    Queries =
                    [
                        "Load News ordered by Date descending for the main list.",
                        "Keep the Important flag available so the Razor view can branch between featured and normal layouts."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "No visit logging or SaveChanges calls."
                    ]
                },
                new LegacyReadActionPlan
                {
                    ActionName = "newnewsread",
                    Purpose = "Render one news detail record plus the latest 5 related news items.",
                    Queries =
                    [
                        "Load one News row by Id for the detail page.",
                        "Load the latest 5 News rows ordered by Date descending for the sidebar list."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "Remove News.ReadCount increments.",
                        "Remove Diary.NewsVisit increments.",
                        "Do not call SaveChanges."
                    ]
                },
                new LegacyReadActionPlan
                {
                    ActionName = "PublicationList and Publication",
                    Purpose = "Render the publication list and individual publication detail pages.",
                    Queries =
                    [
                        "Load Publication rows ordered by Id descending for the list page.",
                        "Project Publication rows into publicationActiveList-compatible summary values.",
                        "Load one Publication row by Id for the detail page.",
                        "Load Publication rows for the related list shown on the detail page."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "Remove Publication.ReadCount increments.",
                        "Do not call SaveChanges."
                    ]
                }
            ]
        },
        new LegacyReadBatch
        {
            Name = "Artist browsing",
            Source = LegacyDataSource.Szaipa,
            Entities = ["Artist", "Works", "ArtNews", "Fav", "Auction", "Exhibition"],
            Controllers = ["HomeController"],
            Goal = "Support artist profile pages and artwork browsing while leaving visit-count updates disabled.",
            Actions =
            [
                new LegacyReadActionPlan
                {
                    ActionName = "newArt",
                    Purpose = "Render one artist profile with works and the latest side-panel content.",
                    Queries =
                    [
                        "Load one Artist row by Id.",
                        "Load Works rows by ArtistId for the portfolio section.",
                        "Take the first ArtNews, Fav, ArtNews-as-publication placeholder, and Auction row by ArtistId using an unordered Take(1) (legacy does not OrderBy).",
                        "Load Exhibition rows by ArtistId for the per-artist '相关展览 / EXHIBITION' swiper."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "Remove Artist.VisitCount increments.",
                        "Remove Diary.ArtVisit increments.",
                        "Do not call SaveChanges."
                    ]
                },
                new LegacyReadActionPlan
                {
                    ActionName = "works and work",
                    Purpose = "Render work listings and individual artwork pages for artist browsing.",
                    Queries =
                    [
                        "Load Works rows globally or by ArtistId for the list view.",
                        "Load one Works row by Id for the detail view.",
                        "Load WorksTag and Tag joins for detail-page labels.",
                        "Load related works for the recommendation block."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "Remove Works.VisitCount increments.",
                        "Remove Diary.WorksVisit increments.",
                        "Do not call SaveChanges."
                    ]
                }
            ]
        },
        new LegacyReadBatch
        {
            Name = "Tongou browsing",
            Source = LegacyDataSource.Tongou,
            Entities = ["TongouAtrist", "TongouWorks", "Project"],
            Controllers = ["Project_TongouController"],
            Goal = "Support Tongou artist pages, work pages, and work list pages without incrementing counters.",
            Actions =
            [
                new LegacyReadActionPlan
                {
                    ActionName = "Tongou list and detail flows",
                    Purpose = "Render Tongou project, artist, and work browsing pages in read-only mode.",
                    Queries =
                    [
                        "Load project and artist listings from Tongou tables.",
                        "Load work detail and related list data without crossing into Szaipa context tables."
                    ],
                    WriteSideBehaviorsToRemove =
                    [
                        "Remove any visit-count or audit writes tied to Tongou reads."
                    ]
                }
            ]
        }
    ];
}
