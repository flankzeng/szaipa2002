using Microsoft.EntityFrameworkCore;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Read-only EF Core context over the legacy Szaipa (public site) database. It is hand-authored from the
/// EF6 Database-First model as a pre-connection translation; a real <c>dotnet ef dbcontext scaffold</c>
/// can later validate it against a verified read-only connection. SaveChanges is hard-blocked and queries
/// default to no-tracking, so this context cannot mutate the live Windows database.
/// </summary>
public sealed class SzaipaLegacyReadContext : DbContext
{
    public const string ContextName = "SzaipaLegacyReadContext";

    public const string LegacySourceName = "SzaipaEntities";

    public SzaipaLegacyReadContext(DbContextOptions<SzaipaLegacyReadContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<News> News => Set<News>();

    public DbSet<Publication> Publication => Set<Publication>();

    public DbSet<Artist> Artist => Set<Artist>();

    public DbSet<Works> Works => Set<Works>();

    public DbSet<ArtNews> ArtNews => Set<ArtNews>();

    public DbSet<Fav> Fav => Set<Fav>();

    public DbSet<Auction> Auction => Set<Auction>();

    public DbSet<Exhibition> Exhibition => Set<Exhibition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<News>(entity =>
        {
            entity.ToTable("News");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Publication>(entity =>
        {
            entity.ToTable("Publication");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artist");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Works>(entity =>
        {
            entity.ToTable("Works");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<ArtNews>(entity =>
        {
            entity.ToTable("ArtNews");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Fav>(entity =>
        {
            entity.ToTable("Fav");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Auction>(entity =>
        {
            entity.ToTable("Auction");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Exhibition>(entity =>
        {
            entity.ToTable("Exhibition");
            entity.HasKey(e => e.Id);
        });
    }

    public override int SaveChanges() => throw ReadOnlyViolation();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnlyViolation();

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) => throw ReadOnlyViolation();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw ReadOnlyViolation();

    private static InvalidOperationException ReadOnlyViolation() =>
        new("SzaipaLegacyReadContext is read-only. Writes to the legacy Szaipa database are not permitted "
            + "during the public read-only migration.");
}
