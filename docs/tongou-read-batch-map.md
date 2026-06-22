# Tongou Read Batch Map

## Goal

This document translates the public read flows in `Project_TongouController` into a read-only EF Core checklist.
Admin add/edit actions are intentionally out of scope for the first migration pass.

## Public read flows

### `Work`

- Source tables:
  - `TongouWorks`
- Queries:
  - Load one `TongouWorks` row by `id`.
- Redirect compatibility:
  - Preserve the special-case redirects for `36`, `84`, `115`, `162`, and `163`.
  - Dedicated route targets are `mengboshen`, `beini`, `mouliqiao`, `mouliqiao2`, and `gaodaqing2`.
- Remove:
  - `TongouWorks.VisityCount++`
  - `tongou.SaveChanges()`

### `Atrist`

- Source tables:
  - `TongouAtrist`
- Queries:
  - Load one `TongouAtrist` row by `id`.
- Redirect compatibility:
  - Preserve the special-case redirect for `96`.
  - Dedicated route target is `beiniTD`.
- Remove:
  - Any future hot-count or visit-count write behavior

### `WorkList`

- Source tables:
  - `TongouAtrist`
  - `TongouWorks`
- Queries:
  - Load one `TongouAtrist` row by `id`.
  - Load all `TongouWorks` rows where `Atristid == id`.
- Target read model:
  - `TongouArtistProfileSnapshotModel`

## Not part of this phase

- `AtristAdd`
- `AtristEdit`
- `WorkAdd`
- `WorkEdit`
- Staff-only JSON list endpoints
- QR code generation and file output

## Pre-EF Core readiness now captured in ASP.NET Core

- The new `ProjectTongouController` already keeps the special work and artist redirect rules in the controller layer.
- Dedicated ASP.NET Core skeleton endpoints now exist for:
  - `/Project_Tongou/beini`
  - `/Project_Tongou/beiniTD`
  - `/Project_Tongou/mouliqiao`
  - `/Project_Tongou/mouliqiao2`
  - `/Project_Tongou/mengboshen`
  - `/Project_Tongou/gaodaqing2`
- That means the future `ITongouReadRepository` can stay focused on pure read-only entity projection instead of reproducing legacy route branching.
