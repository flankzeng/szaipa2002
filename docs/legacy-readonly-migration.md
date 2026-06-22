# Legacy Read-Only Migration Notes

## Scope

This phase prepares the new .NET 10 solution to consume the legacy SQL Server databases in a controlled, read-only way.

## Current rule set

- `src/Szaipa.Web` must not connect to legacy databases by default.
- `src/Szaipa.Data` owns the legacy data-source policy.
- `Szaipa` and `Tongou` remain separate contexts.
- Connection strings stay out of Git and only appear in `appsettings.Local.json`.
- The first EF Core pass should be read-only only.

## Planned EF Core context names

- `SzaipaEntities` -> `SzaipaLegacyReadContext`
- `TongouEntities` -> `TongouLegacyReadContext`

## Expected folder layout

- `src/Szaipa.Data/Contexts/Szaipa`
- `src/Szaipa.Data/Contexts/Tongou`
- `src/Szaipa.Data/Entities/Szaipa`
- `src/Szaipa.Data/Entities/Tongou`

## First migration targets

- Read-only pages and queries used by `HomeController`
- Artist and publication browsing flows
- Search queries that do not write audit or analytics data
- Tongou project listing and lookup flows

## First query contracts in the new data layer

- `INewsReadRepository`
- `IArtistReadRepository`
- `IPublicationReadRepository`
- `ITongouReadRepository`

These are placeholder contracts only. They make the read-side surface explicit before EF Core entities are generated.
The repositories are registered in DI, but their current implementations deliberately throw until EF Core scaffolding is in place.

## First read models in code

- `NewsSummaryModel`
- `NewsDetailModel`
- `HomePageSnapshotModel`
- `ArtistSummaryModel`
- `ArtistDetailModel`
- `ArtistProfileSnapshotModel`
- `ArtNewsSummaryModel`
- `WorkSummaryModel`
- `PublicationSummaryModel`
- `TongouArtistModel`
- `TongouWorkModel`

## Scaffold command service

- `ILegacyScaffoldCommandService`
- `LegacyScaffoldCommandService`

The service builds suggested `dotnet ef dbcontext scaffold` commands with environment-variable placeholders, so secrets do not need to appear in code or docs.

## Migration dashboard coverage

- `src/Szaipa.Web` shows the current legacy source access modes.
- The dashboard lists scaffold readiness per source.
- The dashboard renders the suggested scaffold commands, required environment variable names, and read-only guardrails.
- This page is meant to be the operator checklist before any real database connection is enabled locally.

## What stays out of read repositories

- `ReadCount`, `VisitCount`, and diary statistics updates
- Access logging
- Any `SaveChanges()` behavior from the old MVC controllers

## Batch order

1. `Home content`
2. `Artist browsing`
3. `Tongou browsing`

## Not part of this phase

- Write operations
- Admin flows
- Background jobs
- Any controller action that mutates shared production data
