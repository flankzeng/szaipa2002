# First Pass Asset Batches

## Goal

This file narrows the asset migration plan down to the first real page activations, so we only move what each page group actually needs.

## Batch 1: Home landing and news

### Routes

- `/Home/Index`
- `/Home/NewNews`
- `/Home/NewNewsRead/{id}`

### First folders to migrate

- `/Users/arthur/Project/GitClone/web24.05/Content/newsImg`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/newsImg`
- `/Users/arthur/Project/GitClone/web24.05/Content/icon`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/icon`
- `/Users/arthur/Project/GitClone/web24.05/Content/fonts`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/fonts`
- `/Users/arthur/Project/GitClone/web24.05/Content/Model`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/model`
- `/Users/arthur/Project/GitClone/web24.05/Content/123`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/123`

### Why this batch is first

- These routes are the first consumers of `INewsReadRepository` and `IPublicationReadRepository`.
- They also have the highest visual dependency on legacy icons, fonts, and news cover assets.

## Batch 2: Publication detail and list

### Routes

- `/Home/PublicationList`
- `/Home/Publication/{id}`

### Additional folders to review

- `Content/123`
- `Content/icon`
- publication-specific images referenced from the publish snapshot

### Notes

- The publication detail page may need a subset of assets derived from folder naming rather than direct database paths.
- Validate how `MaxImg` relates to the actual publish output before copying more than the initial summary assets.

## Batch 3: Artist browsing

### Routes

- `/Home/NewVip`
- `/Home/NewArt/{id}`

### First folders to migrate

- `/Users/arthur/Project/GitClone/web24.05/Content/ArtImg`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/artimg`
- `/Users/arthur/Project/GitClone/web24.05/Content/icon`
  - target: `src/Szaipa.Web/wwwroot/legacy/content/icon`
- any artist-page CSS or imagery under `Content/Model`

## Batch 4: Tongou browsing

### Routes

- `/Project_Tongou/Atrist/{id}`
- `/Project_Tongou/WorkList/{id}`
- `/Project_Tongou/Work/{id}`

### First folders to review

- Tongou page-specific images under the publish snapshot
- shared icon/fonts only if the migrated Tongou views still depend on them

## Rule

Do not move an asset folder just because it exists in `web24.05`.
Move it only when one of the currently activated route groups has a concrete dependency on it.
