# Route Activation Map

## Goal

This map tracks which legacy public routes already have an ASP.NET Core endpoint reserved, and what still has to happen before each route can serve real content.

## Home routes

- Legacy: `/Home/newIndex`
  - New route: `/Home/Index`
  - Status: active migration dashboard placeholder
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/newIndex`
  - Preview shell: the new app now renders a dedicated landing preview for the first homepage sections before full repository activation
  - Section preview: the landing shell now includes concrete placeholder cards for news and exhibition content blocks
  - Layout roles: the preview now distinguishes featured news, list rows, featured exhibition cards, and supporting exhibition cards
  - Section layout: news now renders as headline plus list rail, and exhibition renders as main card plus side rail
  - Row anatomy: standard news rows now expose a date slot and exhibition side items expose a short navigation meta label
  - Hero anatomy: featured news and exhibition entries now expose a visual placeholder area plus CTA row to approximate first-screen emphasis
  - Hero detail lines: featured blocks now expose subline text and dual action cues so the preview better resembles a real first-screen content module
  - Rail cues: list rows and exhibition side items now expose marker tags, accent labels, and inline navigation hints to better approximate the old page's ranked list feeling
  - Next milestone: replace the dashboard with the first real landing-page read-only implementation when `INewsReadRepository` and `IPublicationReadRepository` are wired

- Legacy: `/Home/newnews`
  - New route: `/Home/NewNews`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/newnews`
  - Depends on:
    - `INewsReadRepository.GetLatestNewsAsync`
    - migrated Razor view
    - `newsImg` and `icon` assets

- Legacy: `/Home/newnewsread/{id}`
  - New route: `/Home/NewNewsRead/{id}`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/newnewsread/{id}`
  - Depends on:
    - `INewsReadRepository.GetNewsByIdAsync`
    - `INewsReadRepository.GetRelatedNewsAsync`
    - migrated detail Razor view

- Legacy: `/Home/newvip`
  - New route: `/Home/NewVip`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/newvip`
  - Depends on:
    - `IArtistReadRepository.GetArtistsAsync`
    - migrated artist list Razor view

- Legacy: `/Home/newArt/{id}`
  - New route: `/Home/NewArt/{id}`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/newArt/{id}`
  - Depends on:
    - `IArtistReadRepository.GetArtistProfileAsync`
    - artist assets under `wwwroot/legacy/content/artimg`
    - migrated artist profile Razor view

- Legacy: `/Home/PublicationList`
  - New route: `/Home/PublicationList`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/PublicationList`
  - Depends on:
    - `IPublicationReadRepository.GetLatestPublicationsAsync`
    - migrated publication list Razor view

- Legacy: `/Home/Publication/{id}`
  - New route: `/Home/Publication/{id}`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Home/Publication/{id}`
  - Depends on:
    - `IPublicationReadRepository.GetPublicationDetailSnapshotAsync`
    - migrated publication detail Razor view

## Tongou routes

- Legacy: `/Project_Tongou/Atrist/{id}`
  - New route: `/Project_Tongou/Atrist/{id}`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: `/Project_Tongou` and `/Project_Tongou/Index` currently resolve into the artist entry skeleton
  - Redirect quirks: artist id `96` now stays in the controller and redirects to `/Project_Tongou/beiniTD`
  - Single-artist only: legacy `Atrist` loads just the artist (no works query) and links to `WorkList`, so this route uses `GetArtistByIdAsync`, not the artist+works `GetArtistProfileAsync`
  - Depends on:
    - `ITongouReadRepository.GetArtistByIdAsync`
    - migrated Tongou artist Razor view

- Legacy: `/Project_Tongou/WorkList/{id}`
  - New route: `/Project_Tongou/WorkList/{id}`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Project_Tongou/WorkList/{id}`
  - Depends on:
    - `ITongouReadRepository.GetArtistProfileAsync`
    - migrated Tongou work-list Razor view

- Legacy: `/Project_Tongou/Work/{id}`
  - New route: `/Project_Tongou/Work/{id}`
  - Status: dedicated ASP.NET Core page skeleton exists
  - Compatibility: explicit ASP.NET Core alias exists for `/Project_Tongou/Work/{id}`
  - Redirect quirks: work ids `36`, `84`, `115`, `162`, and `163` now stay in the controller and redirect to their dedicated legacy action names
  - Depends on:
    - `ITongouReadRepository.GetWorkByIdAsync`
    - controller-preserved redirect quirks for special ids
    - migrated Tongou work-detail Razor view

## Activation rules

- Do not replace a stub route with real data until the corresponding repository method compiles and returns read-only models.
- Do not activate a route if it still requires write-side counters from the old MVC controller.
- Activate one route group at a time and compare against `/Users/arthur/Project/GitClone/web24.05`.
- Reuse `MigrationStubFactory` when reserving additional routes so stub messaging stays consistent across controllers.
- The ASP.NET Core page skeletons now expose route-specific query parameters and read-only guardrails directly in the UI, so EF Core wiring can be verified against the intended contract before activation.
- The same skeletons now also expose target read models and explicitly removed legacy write behaviors, so route activation can be checked against both projection shape and write-side cleanup.
- Public skeleton metadata is now centralized in `src/Szaipa.Web/Services/PublicReadRouteContractCatalog.cs`, so Home and Tongou controllers consume one shared contract source during the pre-EF-Core phase.
- The homepage landing preview and route preview groups are also sourced from that catalog now, which keeps dashboard guidance aligned with the actual route skeleton contracts.

## Ported real views (2026-06-21)

Each route below now has the legacy `web24.05` view ported verbatim and rebound to the read models, wired
behind the same flag gate (`ReadOnlyMigration:Enable{Szaipa,Tongou}ReadModels` + a live read-only connection).
With the DB off (default) every route returns its skeleton (HTTP 200); when activated it renders real data.
404 replaces the legacy NRE-on-missing-id, and no write-side counter (`ReadCount`/`VisityCount`/`HotCount`) ever runs.

- `/Home/Index` (+ `/`, `/Home/newIndex`) → real `NewIndex.cshtml` from `web24.05/Views/Home/newIndex.cshtml`
  (top-6 news + all publications by StartDate desc; news branch `Important != false`; exhibition active/ended by `Status`).
  DB-off still shows the migration dashboard (`Index.cshtml`).
- `/Home/newnews`, `/Home/newnewsread/{id}`, `/Home/newArt/{id}` → ported earlier this stage.
- `/Home/Publication/{id}` → real `Publication.cshtml` (added `MaxImg` to the detail read model for the swiper loop).
- `/Project_Tongou/{Atrist,Work,WorkList}` + specials `beini`, `beiniTD`, `mouliqiao`, `mouliqiao2`, `mengboshen`,
  `gaodaqing2` → all ported; view folder is `Views/ProjectTongou` (controller name), legacy id redirects preserved.

## Parity discrepancies vs web24.05 (decision needed)

These three Home routes were reserved as skeletons but are **not** faithful, live public links in `web24.05`:

- `/Home/vip` — **the real artist-list route** (nav `_newLayout` links `~/home/vip`). `vip.cshtml` *exists*
  (`db.Artist.OrderBy(Id)` asc) but uses the **old `_Layout.cshtml`** and links cards to the **old `/Home/Art/{id}`**
  detail — i.e. the legacy/old visual system, not the `_newLayout` + `newArt` chain ported so far. Porting it
  faithfully pulls in the old layout + old `Art` detail page (a separate workstream). Currently unported.
- `/Home/newvip` — `newvip.cshtml` is **missing** in `web24.05` (action does `OrderByDescending(Id)`); returns
  **500 on prod**. Dead/unfinished route. Kept as a skeleton; not a real public link.
- `/Home/PublicationList` — `PublicationList.cshtml` is **missing** in `web24.05`; returns **500 on prod** and the
  nav does not link to it (exhibition list is rendered inline on the landing `section07`; the nav's exhibition
  link is `~/publication/index`). Dead route. Kept as a skeleton.

Recommendation: leave `newvip`/`PublicationList` as skeletons (do not invent views — they are dead in the baseline).
The `vip` (+ old `_Layout` + old `Art`) chain is a genuine scope decision: port the old-layout artist chain now,
or defer until the planned old/new consolidation refactor.
