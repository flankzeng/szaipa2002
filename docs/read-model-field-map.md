# Read Model Field Map

## Goal

This map keeps the first EF Core read pass mechanical.
Each section shows the legacy entity fields, the old MVC projection behavior, and the target model in `src/Szaipa.Data`.

## News

### Legacy entity

- Source: `szaipa2022/Models/News.cs`
- Fields used by public read flows:
  - `Id`
  - `Title`
  - `Subtitle`
  - `Autor`
  - `original`
  - `link`
  - `Date`
  - `Content`
  - `CoverPath`
  - `Important`

### Legacy projection

- Old summary DTO: `newsreadlist`
- Mapping from `HomeController.newslist(List<News>)`:
  - `Id -> Id`
  - `Title -> Title`
  - `Subtitle -> Subtitle`
  - `CoverPath -> ImgTitle`
  - `link -> link`
  - `original -> or`
  - `Autor -> Autor`
  - `Date -> formatted string`
  - `Important ?? false -> Important`

### New target models

- `NewsSummaryModel`
  - `Id <- News.Id`
  - `Title <- News.Title`
  - `Subtitle <- News.Subtitle`
  - `Date <- News.Date`
  - `CoverPath <- News.CoverPath`
  - `Important <- News.Important ?? false`
- `NewsDetailModel`
  - `Id <- News.Id`
  - `Title <- News.Title`
  - `Subtitle <- News.Subtitle`
  - `Author <- News.Autor`
  - `Date <- News.Date`
  - `Content <- News.Content`
  - `CoverPath <- News.CoverPath`

### Ignore during read-only phase

- `ReadCount`
- `ImgTitle`
- `Activity`
- `Year`
- Any write-back tied to `ReadCount`

## Publication

### Legacy entity

- Source: `szaipa2022/Models/Publication.cs`
- Fields used by public read flows:
  - `Id`
  - `TitleCN`
  - `TitleEN`
  - `StartDate`
  - `EndDate`
  - `FolderName`
  - `MaxImg`
  - `LogoPath`
  - `CoverPath`
  - `EditRecord`
  - `Status`
  - `Location`
  - `zhuban`
  - `chengban`
  - `xieban`

### Legacy projection

- Old summary DTO: `publicationActiveList`
- Mapping from `HomeController.publicationlist(List<Publication>)`:
  - `TitleCN -> TitleCN`
  - `TitleEN -> TitleEN`
  - `Id -> Id`
  - `StartDate -> formatted string`
  - `EndDate -> formatted string`
  - `FolderName -> FolderName`
  - `MaxImg -> MaxImg`
  - `LogoPath -> LogoPath`
  - `CoverPath -> CoverPath`
  - `EditRecord -> EditRecord`
  - `Location -> Location`
  - `Status ?? false -> Status`
  - `zhuban -> zhuban`
  - `chengban -> chengban`
  - `xieban -> xieban`

### New target models

- `PublicationSummaryModel`
  - `Id <- Publication.Id`
  - `TitleCn <- Publication.TitleCN`
  - `TitleEn <- Publication.TitleEN`
  - `StartDate <- Publication.StartDate`
  - `EndDate <- Publication.EndDate`
  - `CoverPath <- Publication.CoverPath`
  - `Location <- Publication.Location`
  - `FolderName <- Publication.FolderName` (landing exhibition cards build `/Content/images/{FolderName}/{n}.jpg`)
  - `Organizer <- Publication.zhuban`
  - `Host <- Publication.chengban`
  - `CoHost <- Publication.xieban`
  - `Status <- Publication.Status` (nullable; landing view partitions: `true` = active, `false` = ended, `null` = dropped from both)
- `PublicationDetailModel`
  - `Id <- Publication.Id`
  - `TitleCn <- Publication.TitleCN`
  - `TitleEn <- Publication.TitleEN`
  - `StartDate <- Publication.StartDate`
  - `EndDate <- Publication.EndDate`
  - `FolderName <- Publication.FolderName`
  - `CoverPath <- Publication.CoverPath`
  - `LogoPath <- Publication.LogoPath`
  - `MaxImagePath <- derived from MaxImg or later asset convention verification`
  - `Location <- Publication.Location`
  - `Organizer <- Publication.zhuban`
  - `Host <- Publication.chengban`
  - `CoHost <- Publication.xieban`
  - `EditRecord <- Publication.EditRecord`

### Ignore during read-only phase

- `ReadCount`
- Any detail-page write-back tied to `ReadCount`

## Artist

### Legacy entity

- Source: `szaipa2022/Models/Artist.cs`
- Fields used by `newvip`, `newArt`, and artist detail flows:
  - `Id`
  - `ArtistNameCN`
  - `ArtistNameEN`
  - `Path`
  - `Nation`
  - `City`
  - `Title`
  - `Honor`
  - `Introduction`
  - `Path1`
  - `Path2`

### New target models

- `ArtistSummaryModel`
  - `Id <- Artist.Id`
  - `ArtistNameCn <- Artist.ArtistNameCN`
  - `ArtistNameEn <- Artist.ArtistNameEN`
  - `Title <- Artist.Title`
  - `Path <- Artist.Path`
  - `Introduction <- Artist.Introduction`
- `ArtistDetailModel`
  - `Id <- Artist.Id`
  - `ArtistNameCn <- Artist.ArtistNameCN`
  - `ArtistNameEn <- Artist.ArtistNameEN`
  - `Title <- Artist.Title`
  - `Nation <- Artist.Nation`
  - `City <- Artist.City`
  - `Honor <- Artist.Honor`
  - `Introduction <- Artist.Introduction`
  - `Path <- Artist.Path`
  - `Path1 <- Artist.Path1`
  - `Path2 <- Artist.Path2`

### Ignore during read-only phase

- `VisitCount`
- `Activity`
- `EditRecord`
- Any artist visit logging

## Works

### Legacy entity

- Source: `szaipa2022/Models/Works.cs`
- Fields used by artist browsing:
  - `Id`
  - `ArtistId`
  - `Title`
  - `Path`
  - `Width`
  - `Height`
  - `Tags`

### New target model

- `WorkSummaryModel`
  - `Id <- Works.Id`
  - `ArtistId <- Works.ArtistId`
  - `Title <- Works.Title`
  - `Path <- Works.Path`
  - `Width <- Works.Width`
  - `Height <- Works.Height`
  - `Tags <- Works.Tags`

## Artist side-panel sources

> Ordering note: legacy `newArt` takes each side-panel feed via an **unordered** `.Where(d => d.ArtistId == id).ToList().Take(1)` — no `OrderByDescending`. It therefore surfaces the **first** row in default/PK order (lowest Id), not the newest, despite the `最新` (latest) comment. Reproduce the unordered single-row behavior for parity; do **not** add `OrderByDescending(Date)`.

### `ArtNews`

- Source: `szaipa2022/Models/ArtNews.cs`
- Target:
  - `ArtistSidePanelItemModel`
  - `Id <- ArtNews.Id`
  - `ArtistId <- ArtNews.ArtistId`
  - `Title <- ArtNews.Title`
  - `Subtitle <- ArtNews.SubTitle`
  - `Date <- ArtNews.Date`
  - `CoverPath <- ArtNews.CoverPath`

### `Fav`

- Source: `szaipa2022/Models/Fav.cs`
- Target:
  - `ArtistSidePanelItemModel`
  - `Id <- Fav.Id`
  - `ArtistId <- Fav.ArtistId`
  - `Title <- Fav.Title`
  - `Subtitle <- Fav.Location`
  - `Date <- null`
  - `CoverPath <- Fav.CoverPath`

### `Auction`

- Source: `szaipa2022/Models/Auction.cs`
- Target:
  - `ArtistSidePanelItemModel`
  - `Id <- Auction.Id`
  - `ArtistId <- Auction.ArtistId`
  - `Title <- Auction.Title`
  - `Subtitle <- Auction.Price`
  - `Date <- null`
  - `CoverPath <- Auction.CoverPath`

## Artist related-exhibition feed

### `Exhibition` (per-artist)

- Source: `szaipa2022/Models/Exhibition.cs` (legacy `newArt` does `db.Exhibition.Where(e => e.ArtistId == id)`).
- Distinct from the landing-page exhibition list, which is sourced from `Publication`.
- Target:
  - `ArtistExhibitionItemModel`
  - `Id <- Exhibition.Id`
  - `ArtistId <- Exhibition.ArtistId`
  - `Title <- Exhibition.Title`
  - `CoverPath <- Exhibition.CoverPath`
  - `Location <- Exhibition.Location`
  - `Link <- Exhibition.Link`
  - `StartDate <- Exhibition.StartDate` (legacy `string`, not `DateTime`)
  - `EndDate <- Exhibition.EndDate` (legacy `string`, not `DateTime`)

## Known ambiguity to verify later

- The old `newArt` action uses `ArtNews` again for the "latest publication" slot.
- `PublicationDetailModel.MaxImagePath` may need to be derived from an asset naming convention rather than a direct database field.
- Some old views format dates in Razor while some format dates in the controller; prefer keeping raw `DateTime?` values in the new repositories and formatting in Razor.
- The landing page trims news subtitles to 90 characters; the new composition helper now centralizes that rule in `Szaipa.Data.Composition`.
