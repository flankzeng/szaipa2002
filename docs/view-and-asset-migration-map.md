# View And Asset Migration Map

## Goal

This map defines where legacy publish assets and Razor views should land in the new ASP.NET Core app.
It keeps the migration incremental instead of copying the old `Content` tree blindly.

## Current source of truth

- Runtime parity reference:
  - `/Users/arthur/Project/GitClone/web24.05`
- New app root:
  - `src/Szaipa.Web`

## View migration targets

- `Views/Home/newIndex.cshtml`
  - Target: `src/Szaipa.Web/Views/Home/Index.cshtml`
  - Notes: This is the main public landing page target for the first migration pass.

- `Views/Home/newnews.cshtml`
  - Target: `src/Szaipa.Web/Views/Home/NewNews.cshtml`
  - Notes: Should follow the first `INewsReadRepository` implementation.
  - Status: dedicated ASP.NET Core page skeleton exists in `src/Szaipa.Web/Views/Home/NewNews.cshtml`

- `Views/Home/newnewsread.cshtml`
  - Target: `src/Szaipa.Web/Views/Home/NewNewsRead.cshtml`
  - Notes: Needs the news detail snapshot plus sidebar summaries.
  - Status: dedicated ASP.NET Core page skeleton exists in `src/Szaipa.Web/Views/Home/NewNewsRead.cshtml`

- `Views/Home/newvip.cshtml`
  - Target: `src/Szaipa.Web/Views/Home/NewVip.cshtml`
  - Notes: Introduce only after artist summary queries are wired.
  - Status: dedicated ASP.NET Core page skeleton exists in `src/Szaipa.Web/Views/Home/NewVip.cshtml`

- `Views/Home/newArt.cshtml`
  - Target: `src/Szaipa.Web/Views/Home/NewArt.cshtml`
  - Notes: Depends on `ArtistProfileSnapshotModel`.
  - Status: dedicated ASP.NET Core page skeleton exists in `src/Szaipa.Web/Views/Home/NewArt.cshtml`

- `Views/Home/Publication.cshtml` and publication list pages
  - Target:
    - `src/Szaipa.Web/Views/Home/PublicationList.cshtml`
    - `src/Szaipa.Web/Views/Home/Publication.cshtml`
  - Notes: Depends on `IPublicationReadRepository`.
  - Status:
    - dedicated ASP.NET Core page skeleton exists in `src/Szaipa.Web/Views/Home/PublicationList.cshtml`
    - dedicated ASP.NET Core page skeleton exists in `src/Szaipa.Web/Views/Home/Publication.cshtml`

- `Views/Project_Tongou/*`
  - Target: `src/Szaipa.Web/Views/ProjectTongou/*`
  - Notes: Keep the controller namespace and folder naming aligned when those routes are introduced.
  - Status: dedicated ASP.NET Core page skeletons exist in `src/Szaipa.Web/Views/ProjectTongou/*`

## Asset migration targets

- `Content/newsImg`
  - Target: `src/Szaipa.Web/wwwroot/legacy/content/newsImg`
  - Used by: news landing, news list, news detail

- `Content/icon`
  - Target: `src/Szaipa.Web/wwwroot/legacy/content/icon`
  - Used by: landing page arrows, navigation accents

- `Content/fonts`
  - Target: `src/Szaipa.Web/wwwroot/legacy/content/fonts`
  - Used by: typography parity for the modernized Razor pages

- `Content/Model`
  - Target: `src/Szaipa.Web/wwwroot/legacy/content/model`
  - Used by: old model-specific CSS and image dependencies

- `Content/ArtImg`
  - Target: `src/Szaipa.Web/wwwroot/legacy/content/artimg`
  - Used by: artist and works pages

- `Content/123`
  - Target: `src/Szaipa.Web/wwwroot/legacy/content/123`
  - Used by: landing-page exhibition and themed section assets

## Migration rules

- Do not copy the whole `Content` directory at once.
- Move only the folders needed by the next activated page batch.
- Preserve case carefully when moving assets from the publish snapshot.
- Prefer `wwwroot/legacy/...` during the first pass so we can refactor paths later without losing parity.
- Keep temporary or generated folders such as `Content/TempFile` out of the first migration pass.
- The ASP.NET Core page skeletons now expose their first asset dependencies directly in the UI, so route-by-route asset moves can be validated from the migration workspace.
- The page skeletons also now render inside a shared migration shell, which means visual migration can proceed with one reusable structure while route-specific content is still being ported.
- The page skeletons also carry route-specific query parameters and read-only guardrails, so repository wiring decisions can be cross-checked from the UI without enabling live data sources.
