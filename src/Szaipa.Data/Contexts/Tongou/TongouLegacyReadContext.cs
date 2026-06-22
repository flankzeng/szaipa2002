using Microsoft.EntityFrameworkCore;

namespace Szaipa.Data.Contexts.Tongou;

/// <summary>
/// Read-only EF Core context over the legacy Tongou database, hand-authored from the EF6 Database-First
/// model (no live DB contact). SaveChanges is hard-blocked and queries default to no-tracking, so this
/// context cannot mutate the live database. The legacy tables use a lowercase <c>id</c> primary key.
/// </summary>
public sealed class TongouLegacyReadContext : DbContext
{
    public const string ContextName = "TongouLegacyReadContext";

    public const string LegacySourceName = "TongouEntities";

    public TongouLegacyReadContext(DbContextOptions<TongouLegacyReadContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<TongouAtrist> TongouAtrist => Set<TongouAtrist>();

    public DbSet<TongouWorks> TongouWorks => Set<TongouWorks>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TongouAtrist>(entity =>
        {
            entity.ToTable("TongouAtrist");
            entity.HasKey(e => e.id);
        });

        modelBuilder.Entity<TongouWorks>(entity =>
        {
            entity.ToTable("TongouWorks");
            entity.HasKey(e => e.id);
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
        new("TongouLegacyReadContext is read-only. Writes to the legacy Tongou database are not permitted "
            + "during the public read-only migration.");
}
