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

    public DbSet<ExhibitionWork> ExhibitionWork => Set<ExhibitionWork>();

    public DbSet<WeChatSyncRecord> WeChatSyncRecord => Set<WeChatSyncRecord>();

    public DbSet<PublicationBlock> PublicationBlock => Set<PublicationBlock>();

    public DbSet<FeaturedExhibition> FeaturedExhibition => Set<FeaturedExhibition>();

    public DbSet<Artist> Artist => Set<Artist>();

    public DbSet<Works> Works => Set<Works>();

    public DbSet<ArtNews> ArtNews => Set<ArtNews>();

    public DbSet<Fav> Fav => Set<Fav>();

    public DbSet<Auction> Auction => Set<Auction>();

    public DbSet<Exhibition> Exhibition => Set<Exhibition>();

    public DbSet<ArtistPublication> ArtistPublication => Set<ArtistPublication>();
    public DbSet<OrgMember> OrgMember => Set<OrgMember>();

    public DbSet<SaleWork> SaleWork => Set<SaleWork>();

    public DbSet<Partner> Partner => Set<Partner>();

    public DbSet<ArtistBook> ArtistBook => Set<ArtistBook>();

    public DbSet<SiteText> SiteText => Set<SiteText>();

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
            entity.Property(e => e.Type).HasDefaultValue(0);
        });

        modelBuilder.Entity<ExhibitionWork>(entity =>
        {
            entity.ToTable("ExhibitionWork");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
        });

        modelBuilder.Entity<WeChatSyncRecord>(entity => WeChatSyncRecordModel.Configure(entity));

        modelBuilder.Entity<PublicationBlock>(entity =>
        {
            entity.ToTable("PublicationBlock");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.BlockType).HasMaxLength(30);
        });

        modelBuilder.Entity<FeaturedExhibition>(entity =>
        {
            entity.ToTable("FeaturedExhibition");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.IsVisible).HasDefaultValue(true);
            entity.Property(e => e.ShowInCarousel).HasDefaultValue(false);
            entity.Property(e => e.Status).HasDefaultValue(true);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artist");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HideDefaultExhibitions).HasDefaultValue(false);
        });

        modelBuilder.Entity<Works>(entity =>
        {
            entity.ToTable("Works");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sort).HasDefaultValue(0);
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

        // T4：艺术家 ↔ 展览关联（老库无此表，读侧缺表时降级）。
        modelBuilder.Entity<ArtistPublication>(entity =>
        {
            entity.ToTable("ArtistPublication");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sort).HasDefaultValue(0);
        });
        // Additive homepage-block tables (OrgMember / SaleWork / Partner); may not exist in the live DB yet.
        HomeBlocksModelConfiguration.Configure(modelBuilder);

        // 艺术家出版著作（老库无此表，读侧缺表时降级为空）。
        ArtistBookModelConfiguration.Configure(modelBuilder);

        // 现场编辑：站点文案（老库无此表，读侧缺表时一律用模板原文）。
        SiteTextModelConfiguration.Configure(modelBuilder);
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
