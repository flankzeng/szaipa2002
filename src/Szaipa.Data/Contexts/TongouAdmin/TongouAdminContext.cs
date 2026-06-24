using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Tongou;

namespace Szaipa.Data.Contexts.TongouAdmin;

/// <summary>
/// Write-capable EF Core context for the 同构(Tongou) admin module, over a LOCAL writable copy of the
/// Tongou database. Unlike <see cref="TongouLegacyReadContext"/> (no-tracking, SaveChanges hard-blocked)
/// this context tracks changes and permits SaveChanges. It is only ever registered/constructed when
/// <see cref="Configuration.TongouAdminWriteOptions.IsConfigured"/> is true, and its connection string must
/// point at a local writable copy — never the production / Windows-connected database. Entity classes are
/// shared with the read context and live in <c>Szaipa.Data.Contexts.Tongou</c>.
/// </summary>
public sealed class TongouAdminContext : DbContext
{
    public const string ContextName = "TongouAdminContext";

    public TongouAdminContext(DbContextOptions<TongouAdminContext> options)
        : base(options)
    {
    }

    public DbSet<TongouAtrist> TongouAtrist => Set<TongouAtrist>();

    public DbSet<TongouWorks> TongouWorks => Set<TongouWorks>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TongouAtrist>(e => { e.ToTable("TongouAtrist"); e.HasKey(x => x.id); });
        modelBuilder.Entity<TongouWorks>(e => { e.ToTable("TongouWorks"); e.HasKey(x => x.id); });
    }
}
