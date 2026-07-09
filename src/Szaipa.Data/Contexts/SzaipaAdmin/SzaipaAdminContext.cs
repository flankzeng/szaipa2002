using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Contexts.SzaipaAdmin;

/// <summary>
/// Write-capable EF Core context for the staff/admin backend over the legacy Szaipa database. Unlike
/// <see cref="SzaipaLegacyReadContext"/> (no-tracking, SaveChanges hard-blocked) this context tracks
/// changes and permits SaveChanges. It is only ever registered/constructed when
/// <see cref="Configuration.AdminWriteOptions.IsConfigured"/> is true, and its connection string must point
/// at a LOCAL writable copy of the data — never the production / Windows-connected database. Entity classes
/// are shared with the read context (one class per table) and live in <c>Szaipa.Data.Contexts.Szaipa</c>.
/// </summary>
public sealed class SzaipaAdminContext : DbContext
{
    public const string ContextName = "SzaipaAdminContext";

    public SzaipaAdminContext(DbContextOptions<SzaipaAdminContext> options)
        : base(options)
    {
    }

    // Shared content entities (read context exposes a subset; admin edits the full set).
    public DbSet<News> News => Set<News>();

    public DbSet<ArtNews> ArtNews => Set<ArtNews>();

    public DbSet<Artist> Artist => Set<Artist>();

    public DbSet<Works> Works => Set<Works>();

    public DbSet<Fav> Fav => Set<Fav>();

    public DbSet<Auction> Auction => Set<Auction>();

    public DbSet<Exhibition> Exhibition => Set<Exhibition>();

    public DbSet<Publication> Publication => Set<Publication>();

    public DbSet<ExhibitionWork> ExhibitionWork => Set<ExhibitionWork>();

    // Admin-only entities (not part of the public read surface).
    public DbSet<Staff> Staff => Set<Staff>();

    public DbSet<Diary> Diary => Set<Diary>();

    public DbSet<Company> Company => Set<Company>();

    public DbSet<AccessData> AccessData => Set<AccessData>();

    public DbSet<MonthData> MonthData => Set<MonthData>();

    public DbSet<Activity> Activity => Set<Activity>();

    public DbSet<Tag> Tag => Set<Tag>();

    public DbSet<WorksTag> WorksTag => Set<WorksTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<News>(e => { e.ToTable("News"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<ArtNews>(e => { e.ToTable("ArtNews"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Artist>(e => { e.ToTable("Artist"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Works>(e => { e.ToTable("Works"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Fav>(e => { e.ToTable("Fav"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Auction>(e => { e.ToTable("Auction"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Exhibition>(e => { e.ToTable("Exhibition"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Publication>(e => { e.ToTable("Publication"); e.HasKey(x => x.Id); e.Property(x => x.Type).HasDefaultValue(0); });
        modelBuilder.Entity<ExhibitionWork>(e => { e.ToTable("ExhibitionWork"); e.HasKey(x => x.Id); e.Property(x => x.SortOrder).HasDefaultValue(0); });

        modelBuilder.Entity<Staff>(e => { e.ToTable("Staff"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Diary>(e => { e.ToTable("Diary"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Company>(e => { e.ToTable("Company"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<AccessData>(e => { e.ToTable("AccessData"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<MonthData>(e => { e.ToTable("MonthData"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Activity>(e => { e.ToTable("Activity"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Tag>(e => { e.ToTable("Tag"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<WorksTag>(e => { e.ToTable("WorksTag"); e.HasKey(x => x.Id); });
    }
}
