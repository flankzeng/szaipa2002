# EF Core First Implementation Checklist

## Goal

This checklist starts after we are allowed to connect to verified read-only SQL Server credentials.
It is intentionally limited to the first public read flows and excludes admin writes.

## Phase 0: safety before scaffolding

- Confirm the connection is read-only at the SQL Server side, not only in app settings.
- Keep `RuntimeSafety:UseLegacyDataSources=false` until scaffolded code is generated and reviewed.
- Keep `RuntimeSafety:AllowLiveDatabase=false` throughout the first implementation pass.
- Store the connection string in a local environment variable, never in tracked config.

## Phase 1: scaffold contexts

- Add the required EF Core SQL Server tooling packages to `src/Szaipa.Data`.
- Generate `SzaipaLegacyReadContext` from `SZAIPA_READONLY_CONNECTION`.
- Generate `TongouLegacyReadContext` from `TONGOU_READONLY_CONNECTION`.
- Review generated entity names and nullability before wiring any DI usage.
- Do not enable the repositories in a running page until generated code is checked in and understood.

## Phase 2: wire read-only infrastructure

- Register scaffolded contexts with read-only connection policies only.
- Ensure the new contexts are never used for `SaveChanges()` in public flows.
- Prefer `AsNoTracking()` for first-pass query implementations.
- Keep old redirect quirks in controllers, not repositories.

## Phase 3: implement repositories in this order

All four are now implemented as read-only EF Core repositories (`AsNoTracking`, no `SaveChanges`), each covered by SQLite-in-memory unit tests under `tests/Szaipa.Data.Tests`:

1. `INewsReadRepository` — **done** (`NewsReadRepositoryTests`)
2. `IPublicationReadRepository` — **done** (`PublicationReadRepositoryTests`)
3. `IArtistReadRepository` — **done** (`ArtistReadRepositoryTests`; unordered `Take(1)` side panels, ArtNews-as-publication placeholder, per-artist Exhibition feed)
4. `ITongouReadRepository` — **done** (`TongouReadRepositoryTests`; over `TongouLegacyReadContext`)

Projections are centralized in `src/Szaipa.Data/Services/Home/SzaipaHomeProjections.cs` and
`src/Szaipa.Data/Services/Tongou/TongouReadProjections.cs` (single source, prevents contract drift).

> Remaining for each route: wire the controller to inject the repository, port the legacy Razor view, then
> **activate and compare against `web24.05`** — which needs a verified read-only connection (the repositories
> compile and pass unit tests today, but page activation/parity verification is the connection-gated step).

Use the parameterized first-pass query blueprint in `src/Szaipa.Data/Scaffolding/LegacyRepositoryPlans.cs` while implementing these methods.
Cross-check each route against its ASP.NET Core skeleton page, which now exposes:

- target repository method
- repository blueprint purpose, entities, and query steps sourced from `LegacyRepositoryPlans`
- query parameters
- target read models
- write behaviors that must be removed
- route-level read-only guardrails

`LegacyRepositoryPlans` now carries a one-to-one method entry for every interface method, so each pre-EF-Core page contract maps directly to exactly one future implementation method:

- `INewsReadRepository`: `GetHomePageSnapshotAsync`, `GetLatestNewsAsync`, `GetNewsByIdAsync`, `GetRelatedNewsAsync`, `SearchNewsAsync`
- `IArtistReadRepository`: `GetArtistsAsync`, `GetArtistByIdAsync`, `GetArtistWorksAsync`, `GetArtistProfileAsync`
- `IPublicationReadRepository`: `GetLatestPublicationsAsync`, `GetPublicationByIdAsync`, `GetPublicationDetailSnapshotAsync`
- `ITongouReadRepository`: `GetArtistByIdAsync`, `GetArtistProfileAsync`, `GetWorkByIdAsync`, `GetWorksByArtistIdAsync`

Composite/snapshot methods (`GetHomePageSnapshotAsync`, `GetArtistProfileAsync`, `GetPublicationDetailSnapshotAsync`, and the Tongou `GetArtistProfileAsync`) are assembled in `src/Szaipa.Data/Composition/LegacyReadModelComposer.cs` from the single-purpose methods plus the shared display rules in `src/Szaipa.Data/Composition/LegacyDisplayRules.cs`. Implement the single-purpose methods first, then compose.

That blueprint already captures critical legacy values such as:

- landing-page news count = `6`
- news-detail sidebar count = `5`
- artist side-panel feed count = `1`
- publication and artist default sort expectations

## Phase 4: page-by-page activation order

1. `Home/newIndex`
2. `Home/newnews`
3. `Home/newnewsread`
4. `Home/PublicationList`
5. `Home/Publication`
6. `Home/newvip`
7. `Home/newArt`
8. `Project_Tongou/Atrist`
9. `Project_Tongou/WorkList`
10. `Project_Tongou/Work`
11. Tongou special routes (`beiniTD`, `beini`, `mouliqiao`, `mouliqiao2`, `mengboshen`, `gaodaqing2`)

Before activating any route, confirm its row in the dashboard **Public read-only link readiness** table reads `✓ Ready` (contract complete, blueprint aligned, asset mapping listed, write-removal defined). That table is generated from the same contract catalog and repository plans used at activation time, so it is the gate, not a separate status to maintain.

## Verification rule for every activated page

- Compare output against `/Users/arthur/Project/GitClone/web24.05`.
- Confirm no counter fields changed in the backing database (`ReadCount`, `VisitCount`, `VisityCount`, `HotCount`).
- Confirm no `SaveChanges()` path exists in the request pipeline.
- Confirm null legacy values do not crash the new projections (legacy rows allow null `Date`, `Subtitle`, `CoverPath`, etc.).
- Confirm the legacy date display still matches: `newslist` emitted `yyyy年MM月dd日`, and the landing view's `{0:yyyy.MM.dd}` format string left that pre-formatted string unchanged, so the visible landing date format is `yyyy年MM月dd日`. Reproduce the visible result, not the format string.
- Capture any legacy quirks that must stay in controllers for compatibility (Tongou id-based redirects).

## Explicitly deferred

- Staff/admin pages
- QR code generation
- File export flows
- Diary / analytics writebacks
- Any mutation of `ReadCount`, `VisitCount`, `VisityCount`, or `HotCount`
