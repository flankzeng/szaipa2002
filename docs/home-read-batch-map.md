# Home Read Batch Map

## Goal

This document turns the legacy `HomeController` read paths into a checklist for the first EF Core implementation pass.
Everything here should stay read-only and must not recreate the old `SaveChanges()` side effects.

## Batch 1: landing page and news

### `newIndex`

- Source tables:
  - `News`
  - `Publication`
- Queries:
  - Latest 6 `News` rows ordered by `Date` descending.
  - Full `Publication` list ordered by `StartDate` descending.
- Projection details:
  - `News.CoverPath` -> landing-page image path
  - `News.Title` -> title
  - `News.Subtitle` -> subtitle, trimmed to 90 chars in the old controller
  - `News.Date` -> display date
  - `News.Important` -> featured-row layout switch
- Remove:
  - `HaveViti()`
  - Any diary creation or visit logging

### `newnews`

- Source tables:
  - `News`
- Queries:
  - Full `News` list ordered by `Date` descending.
- Projection details:
  - Preserve `Important`, `Title`, `Subtitle`, `CoverPath`, `Date`, and `Id`.
- Remove:
  - Any session-based visit logging
  - Any write behavior

### `newnewsread`

- Source tables:
  - `News`
- Queries:
  - One `News` row by `Id`.
  - Latest 5 `News` rows ordered by `Date` descending for the sidebar.
- Remove:
  - `News.ReadCount++`
  - `Diary.NewsVisit++`
  - `SaveChanges()`

## Batch 2: publications

### `PublicationList`

- Source tables:
  - `Publication`
- Queries:
  - Full `Publication` list ordered by `Id` descending.
- Projection details:
  - Preserve title, dates, cover/logo paths, folder info, location, and organizer fields.
  - Rebuild the old `publicationActiveList` shape as a modern read model.

### `Publication`

- Source tables:
  - `Publication`
- Queries:
  - One `Publication` row by `Id`.
  - Related publication list for the detail page.
- Target read model:
  - `PublicationDetailSnapshotModel`
- Remove:
  - `Publication.ReadCount++`
  - `SaveChanges()`

## Batch 3: artist browsing

### `newArt`

- Source tables:
  - `Artist`
  - `Works`
  - `ArtNews`
  - `Fav`
  - `Auction`
- Queries:
  - One `Artist` row by `Id`.
  - All `Works` rows by `ArtistId`.
  - Latest side-panel rows by `ArtistId`.
- Target read model:
  - `ArtistProfileSnapshotModel`
- Mapping note:
  - Use one neutral side-panel item model for `ArtNews`, `Fav`, and `Auction` summaries instead of forcing them all through a news-specific DTO.
- Notes:
  - The legacy controller uses `ArtNews` for both latest news and a "publication" placeholder; we should verify whether that is intentional or a bug before we mirror it exactly.
- Remove:
  - `Artist.VisitCount++`
  - `Diary.ArtVisit++`
  - `SaveChanges()`

## Implementation rule

When EF Core scaffolding begins, implement one action group at a time and verify HTML output against `/Users/arthur/Project/GitClone/web24.05` before enabling the next group.
