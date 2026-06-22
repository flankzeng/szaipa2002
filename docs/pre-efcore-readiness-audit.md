# Pre-EF-Core Readiness Audit

This is the Stage 3 ("pre-EF-Core single-source consolidation") sign-off. It verifies that every
public read-only route has a complete contract before any EF Core scaffolding starts, and records the
legacy behaviors that are the source of truth for activation parity.

## Verdict

All 12 public read routes are **ready for scaffold/implementation**. Each has a route contract, query
parameters, target read models, a removed-write list, route-level guardrails, a repository blueprint,
an asset dependency mapping, captured compatibility quirks, and dashboard visibility.

`dotnet build Szaipa.Modernization.slnx` succeeds with 0 warnings / 0 errors, and `/healthz` reports
`legacyDataSources:false`, `liveDatabase:false`, `hasReadWriteLegacySource:false`.

## How readiness is computed (single source, not hand-maintained)

The dashboard **Public read-only link readiness** table is generated at request time from:

- `src/Szaipa.Web/Services/PublicReadRouteContractCatalog.cs` — route contracts (skeleton definitions,
  query parameters, target read models, removed writes, guardrails, asset dependencies, blueprint links).
- `src/Szaipa.Data/Scaffolding/LegacyRepositoryPlans.cs` — repository blueprints, now one entry per
  interface method.
- `src/Szaipa.Data/Scaffolding/LegacyReadBatches.cs` — batch-level write-removal for `newIndex`.
- `src/Szaipa.Data/Composition/LegacyDisplayRules.cs` — the legacy magic numbers (6, 90, 5, 1).

A route is `✓ Ready` only when **contract complete AND blueprint aligned AND asset mapping listed AND
write-removal defined**. Because the audit and the activation gate read the same objects, this document
cannot drift from the code.

## Coverage matrix

Legend: C = contract, P = query params, M = target read models, W = removed writes, G = guardrails,
B = repository blueprint, A = asset mapping, Q = compatibility quirk captured, D = dashboard visible.

### Home routes

| Route | Target method | C | P | M | W | G | B | A | Q | D |
| --- | --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| `/Home/newIndex` | `INewsReadRepository.GetHomePageSnapshotAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/Home/newnews` | `INewsReadRepository.GetLatestNewsAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | n/a | ✓ |
| `/Home/newnewsread/{id}` | `INewsReadRepository.GetNewsByIdAsync` (+ `GetRelatedNewsAsync`) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | n/a | ✓ |
| `/Home/newvip` | `IArtistReadRepository.GetArtistsAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/Home/newArt/{id}` | `IArtistReadRepository.GetArtistProfileAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/Home/PublicationList` | `IPublicationReadRepository.GetLatestPublicationsAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | n/a | ✓ |
| `/Home/Publication/{id}` | `IPublicationReadRepository.GetPublicationDetailSnapshotAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | n/a | ✓ |

### Tongou routes

| Route | Target method | C | P | M | W | G | B | A | Q | D |
| --- | --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| `/Project_Tongou/Atrist/{id}` | `ITongouReadRepository.GetArtistByIdAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/Project_Tongou/WorkList/{id}` | `ITongouReadRepository.GetArtistProfileAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | n/a | ✓ |
| `/Project_Tongou/Work/{id}` | `ITongouReadRepository.GetWorkByIdAsync` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/Project_Tongou/beiniTD` | `ITongouReadRepository.GetArtistByIdAsync(96)` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/Project_Tongou/{beini,mouliqiao,mouliqiao2,mengboshen,gaodaqing2}` | `ITongouReadRepository.GetWorkByIdAsync({84,115,162,36,163})` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

## Repository contract ↔ blueprint alignment

Every interface method now has exactly one `LegacyRepositoryPlans` entry (the composite
"GetNewsByIdAsync and GetRelatedNewsAsync" entry was removed; `GetArtistByIdAsync`, `GetArtistWorksAsync`,
`GetPublicationByIdAsync`, and `GetWorksByArtistIdAsync` were added):

| Repository | Methods (interface == plan) |
| --- | --- |
| `INewsReadRepository` | `GetHomePageSnapshotAsync`, `GetLatestNewsAsync`, `GetNewsByIdAsync`, `GetRelatedNewsAsync`, `SearchNewsAsync` |
| `IArtistReadRepository` | `GetArtistsAsync`, `GetArtistByIdAsync`, `GetArtistWorksAsync`, `GetArtistProfileAsync` |
| `IPublicationReadRepository` | `GetLatestPublicationsAsync`, `GetPublicationByIdAsync`, `GetPublicationDetailSnapshotAsync` |
| `ITongouReadRepository` | `GetArtistByIdAsync`, `GetArtistProfileAsync`, `GetWorkByIdAsync`, `GetWorksByArtistIdAsync` |

## Legacy behavior source of truth

These are the exact values activation must reproduce, taken from `szaipa2022/Controllers/HomeController.cs`
and `szaipa2022/Controllers/Project_TongouController.cs`.

- **Landing news count = 6** (`newIndex`: `for (a = 0; a < 6; a++)` over `News` ordered by `Date` desc).
- **Landing subtitle trim = 90 + "..."** (`if (n.Subtitle.Length > 90) n.Subtitle = Substring(0,90)+"..."`).
  Captured in `LegacyDisplayRules.LandingPageNewsSubtitleMaxLength` and applied by
  `LegacyTextTransform.TrimForLandingPage`.
- **Important vs normal split** (`newIndex` section06: `if (n.Important == true)` → featured block with
  cover image + subtitle + CTA, else date + title list row). Now first-class on
  `HomePageSnapshotModel.ImportantNews` / `NormalNews`, order preserved.
- **Landing exhibitions: query is uncapped `StartDate` desc, but the view partitions by `Status`.**
  The controller query is `ViewBag.ExhibitionList = db.Publication.OrderByDescending(d => d.StartDate)` (uncapped),
  but `newIndex.cshtml` renders `Status == true` rows in the active/"processing" section and `Status == false`
  rows in the ended section; `Status == null` rows are dropped from both. Cards also read `FolderName`
  (image carousel path `/Content/images/{FolderName}/{n}.jpg`) and the `zhuban`/`chengban`/`xieban` organizer
  lines, so `PublicationSummaryModel` carries `Status` (nullable), `FolderName`, `Organizer`, `Host`, `CoHost`.
- **Landing date display = `yyyy年MM月dd日`**: `newslist` pre-formats `Date` to that string, and the view's
  `{0:yyyy.MM.dd}` format on an already-formatted string is a no-op. Reproduce the visible output.
- **News list (`newnews`) order = `Date` desc**; **news detail sidebar = latest 5** (`Take(5)`).
- **Artist list (`newvip`) order = `Id` desc**; legacy `vip` used `Id` asc (both behaviors kept as params).
- **Artist profile (`newArt`) side panels = first 1 each via an UNORDERED `Take(1)`** (ArtNews/Fav/Auction
  use `.Where(ArtistId==id).ToList().Take(1)` with **no `OrderBy`**, so the first/lowest-Id row shows, not the
  newest — the `最新` comment is aspirational; reproduce the unordered row for parity, do not add
  `OrderByDescending`). The "publication" side panel is the legacy **ArtNews-backed placeholder**
  (`pub = db.ArtNews...Take(1)`) — an explicit compatibility decision, not a real Publication query.
- **Artist profile (`newArt`) also renders a per-artist Exhibition feed** (`相关展览 / EXHIBITION`) from
  `db.Exhibition.Where(e => e.ArtistId == id)`, carried by `ArtistExhibitionItemModel` (note `Exhibition`
  stores `StartDate`/`EndDate` as `string`). This is distinct from the landing exhibition list, which is
  sourced from `Publication`.
- **Publication list (`PublicationList`) order = `Id` desc**; detail also loads a publication list for
  surrounding navigation.
- **Tongou default id = 1** when the route value is missing (`Work`, `Atrist`, `WorkList`).
- **Tongou work-list filter = `Atristid == id`**.
- **Tongou controller-only redirects (must stay in controllers):**
  - Artist `id == 96` → `beiniTD`.
  - Work `id == 36 → mengboshen`, `84 → beini`, `115 → mouliqiao`, `162 → mouliqiao2`, `163 → gaodaqing2`.

## Removed write behaviors (must never appear in the public read flow)

`News.ReadCount++`, `Publication.ReadCount++`, `Artist.VisitCount++`, `Works.VisitCount++`,
`TongouWorks.VisityCount++`, `TongouWorks/TongouAtrist.HotCount` updates, `Diary.*Visit++`,
`HaveViti()`/`AccessData` logging, and any `SaveChanges()` in a public request.

## Explicitly out of scope for this phase

Staff/admin pages, QR-code generation, RAR/file export, Excel import, search write paths, and all
analytics/diary writebacks.

## What unblocks Phase 4 (real data)

Stage 3 is complete, and the read-only data layer is now implemented without any live DB contact: both
contexts (`SzaipaLegacyReadContext`, `TongouLegacyReadContext`) and all entities are hand-translated from the
EF6 model, all four repositories are implemented (`AsNoTracking`, no `SaveChanges`), and 28 SQLite-in-memory
unit tests pass. Default startup keeps both legacy sources `Disabled`.

The only remaining prerequisite before page activation is a **verified read-only SQL Server connection**
(see `docs/local-readonly-bootstrap.md`): export `SZAIPA_READONLY_CONNECTION` / `TONGOU_READONLY_CONNECTION`,
prove read-only at the SQL Server side, optionally run `dotnet ef dbcontext scaffold` (see
`docs/efcore-scaffold-checklist.md`) to validate the hand-authored entities for drift, then wire each
controller to its repository, port the Razor view, and activate + compare against
`/Users/arthur/Project/GitClone/web24.05` one route group at a time.
