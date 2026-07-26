# Route Activation Map

Updated: 2026-07-26

The modern public surface is fully activated against read-only repositories. Public visitors never see migration dashboards or route skeletons.

## Canonical Home routes

| Route | Backing content |
|---|---|
| `/`, `/Home/Index` | `INewsReadRepository.GetHomePageSnapshotAsync` |
| `/Home/News` | `INewsReadRepository.GetLatestNewsAsync` |
| `/Home/NewsRead/{id}` | `GetNewsByIdAsync` + `GetRelatedNewsAsync` |
| `/Home/Vip` | `IArtistReadRepository.GetArtistsAsync` |
| `/Home/About` | Static modern Razor content |
| `/Home/Art/{id}` | `IArtistReadRepository.GetArtistProfileAsync` |
| `/Home/Art/{id}/Archive` | `IArtistReadRepository.GetArtistArchiveAsync` |
| `/Home/Art/News/{id}` | `IArtistReadRepository.GetArtistArticleAsync` |
| `/Home/PublicationList` | `IPublicationReadRepository.GetLatestPublicationsAsync` |
| `/Home/Publication/{id}` | Database detail, then read-only fallback catalog for retired galleries |

Missing list data renders the formal empty state. A detail route that cannot safely load its backing data returns 503; a missing record returns 404. No public read route increments legacy counters.

## Compatibility only

`/Home/newIndex`, `/Home/newnews`, `/Home/newnewsread/{id}`, `/Home/newvip`, `/Home/newabout`, `/Home/newArt/{id}`, `/Home/ArtNews/{id}` and `/Home/ArtNewsRead/{id}` are permanent redirects. They must not appear in modern internal links.

## Retired route groups

- ProjectTongou public browsing returns 410 without database access.
- zengfeng returns 410.
- Fourteen simple exhibition slugs redirect to fixed `Publication` IDs; three bespoke exhibition pages remain.

## Activation guardrails

- Public data comes only from the read-only EF Core contexts and projected read models.
- `ReadCount`, `VisitCount`, `HotCount` and other legacy write-side behavior never runs.
- Old controller content is migrated into a modern repository and page before its compatibility route is retired.
- External `/Content` remains a runtime volume; it is not copied into or deleted from the modern repository.
