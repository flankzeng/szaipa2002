# EF Core Scaffold Checklist

## What is already ready

- `.NET 10` SDK is installed locally at `/Users/arthur/.dotnet`
- `src/Szaipa.Web` and `src/Szaipa.Data` both compile
- Legacy data sources are modeled separately as `Szaipa` and `Tongou`
- Scaffold command suggestions are generated in code without embedding secrets
- `Microsoft.EntityFrameworkCore.SqlServer` is referenced by `src/Szaipa.Data`
- Both contexts are **hand-authored from the EF6 Database-First model** (no live DB contact), with property
  names mirroring the legacy columns so the commands below diff cleanly when run for real:
  - `SzaipaLegacyReadContext` + `News`, `Publication`, `Artist`, `Works`, `ArtNews`, `Fav`, `Auction`,
    `Exhibition` under `src/Szaipa.Data/Contexts/Szaipa`
  - `TongouLegacyReadContext` + `TongouAtrist`, `TongouWorks` under `src/Szaipa.Data/Contexts/Tongou`
    (lowercase `id` primary key, `Atristid` foreign key)
- All four read repositories are implemented against these contexts and unit-tested (28 tests, SQLite in-memory)

## Validating the manual translation against the live schema

When a verified read-only connection is available, run the scaffold below into a throwaway folder and diff
its generated `News`/`Publication`/context against the hand-authored versions to catch any column/nullability
drift. The manual translation is the source the repositories compile against today; the real scaffold is the
validator, not a from-scratch step.

## What still needs approval before real scaffolding

- Install EF Core packages for SQL Server and design-time tooling
- Restore NuGet packages from the network
- Optionally install or activate `dotnet-ef` if the SDK workload does not already provide it

## Local secret inputs

Use environment variables instead of checking connection strings into the repo. Every value below is an
explicit placeholder; never replace it in this tracked document.

```bash
export SZAIPA_READONLY_CONNECTION='Server=__SQL_HOST__;Database=...;User Id=__READONLY_USER__;Password=__SET_ME__;TrustServerCertificate=True'
export TONGOU_READONLY_CONNECTION='Server=__SQL_HOST__;Database=...;User Id=__READONLY_USER__;Password=__SET_ME__;TrustServerCertificate=True'
```

## First intended scaffold commands

```bash
/Users/arthur/.dotnet/dotnet ef dbcontext scaffold "$SZAIPA_READONLY_CONNECTION" Microsoft.EntityFrameworkCore.SqlServer --project src/Szaipa.Data/Szaipa.Data.csproj --startup-project src/Szaipa.Web/Szaipa.Web.csproj --context SzaipaLegacyReadContext --output-dir Contexts/Szaipa --force --no-onconfiguring

/Users/arthur/.dotnet/dotnet ef dbcontext scaffold "$TONGOU_READONLY_CONNECTION" Microsoft.EntityFrameworkCore.SqlServer --project src/Szaipa.Data/Szaipa.Data.csproj --startup-project src/Szaipa.Web/Szaipa.Web.csproj --context TongouLegacyReadContext --output-dir Contexts/Tongou --force --no-onconfiguring
```

## First pass rules

- Only scaffold after the target source is set to `ReadOnly`
- Start with `Szaipa`, not `Tongou`
- Do not implement any write path in the first EF Core pass
- Keep visit counts, analytics, and diary updates outside the read repositories
