# Tongou Field Map

## Goal

This map keeps the Tongou EF Core migration aligned with the public MVC behavior while staying read-only.

## `TongouAtrist`

### Legacy entity

- Source: `szaipa2022/Models/TongouAtrist.cs`
- Fields used by public flows:
  - `id`
  - `Name`
  - `WorksCount`
  - `AboutText`
  - `Title`
  - `HeardPath`

### New target models

- `TongouArtistModel`
  - `Id <- TongouAtrist.id`
  - `Name <- TongouAtrist.Name`
  - `Title <- TongouAtrist.Title`
  - `HeardPath <- TongouAtrist.HeardPath`
  - `AboutText <- TongouAtrist.AboutText`
  - `WorksCount <- TongouAtrist.WorksCount`
- `TongouArtistProfileSnapshotModel`
  - `Artist <- TongouArtistModel`
  - `Works <- IReadOnlyList<TongouWorkModel>`

### Ignore during read-only phase

- `Aboutid`
- `HotCount`

## `TongouWorks`

### Legacy entity

- Source: `szaipa2022/Models/TongouWorks.cs`
- Fields used by public flows:
  - `id`
  - `Atristid`
  - `AtristidName`
  - `Title`
  - `ImgPath`
  - `Size`
  - `Type`
  - `CreationDate`

### New target model

- `TongouWorkModel`
  - `Id <- TongouWorks.id`
  - `ArtistId <- TongouWorks.Atristid`
  - `ArtistName <- TongouWorks.AtristidName`
  - `Title <- TongouWorks.Title`
  - `ImgPath <- TongouWorks.ImgPath`
  - `Size <- TongouWorks.Size`
  - `Type <- TongouWorks.Type`
  - `CreationDate <- TongouWorks.CreationDate`

### Ignore during read-only phase

- `VisityCount`
- `HotCount`
- Any write-back tied to view counts

## `Project`

### Legacy entity

- Source: `szaipa2022/Models/Project.cs`
- Current status:
  - Present in the old EF model, but not clearly used by the public read flows we have prioritized yet.

### Action

- Defer `Project` mapping until we confirm a real public Razor route depends on it in the new migration sequence.
