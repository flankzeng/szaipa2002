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

    public DbSet<WeChatSyncRecord> WeChatSyncRecord => Set<WeChatSyncRecord>();

    public DbSet<ArtistPublication> ArtistPublication => Set<ArtistPublication>();

    public DbSet<PublicationBlock> PublicationBlock => Set<PublicationBlock>();

    public DbSet<FeaturedExhibition> FeaturedExhibition => Set<FeaturedExhibition>();

    public DbSet<OrgMember> OrgMember => Set<OrgMember>();

    public DbSet<SaleWork> SaleWork => Set<SaleWork>();

    public DbSet<Partner> Partner => Set<Partner>();

    public DbSet<ArtistBook> ArtistBook => Set<ArtistBook>();

    public DbSet<SiteText> SiteText => Set<SiteText>();
    public DbSet<ArtistDisplay> ArtistDisplay => Set<ArtistDisplay>();

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
        modelBuilder.Entity<Artist>(e => { e.ToTable("Artist"); e.HasKey(x => x.Id); e.Property(x => x.HideDefaultExhibitions).HasDefaultValue(false); });
        modelBuilder.Entity<Works>(e => { e.ToTable("Works"); e.HasKey(x => x.Id); e.Property(x => x.Sort).HasDefaultValue(0); });
        modelBuilder.Entity<Fav>(e => { e.ToTable("Fav"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Auction>(e => { e.ToTable("Auction"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Exhibition>(e => { e.ToTable("Exhibition"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Publication>(e => { e.ToTable("Publication"); e.HasKey(x => x.Id); e.Property(x => x.Type).HasDefaultValue(0); });
        modelBuilder.Entity<ExhibitionWork>(e => { e.ToTable("ExhibitionWork"); e.HasKey(x => x.Id); e.Property(x => x.SortOrder).HasDefaultValue(0); });
        modelBuilder.Entity<WeChatSyncRecord>(e => WeChatSyncRecordModel.Configure(e));

        modelBuilder.Entity<ArtistPublication>(e => { e.ToTable("ArtistPublication"); e.HasKey(x => x.Id); e.Property(x => x.Sort).HasDefaultValue(0); });

        modelBuilder.Entity<PublicationBlock>(e => { e.ToTable("PublicationBlock"); e.HasKey(x => x.Id); e.Property(x => x.SortOrder).HasDefaultValue(0); e.Property(x => x.BlockType).HasMaxLength(30); });
        modelBuilder.Entity<FeaturedExhibition>(e => { e.ToTable("FeaturedExhibition"); e.HasKey(x => x.Id); e.Property(x => x.SortOrder).HasDefaultValue(0); e.Property(x => x.IsVisible).HasDefaultValue(true); e.Property(x => x.ShowInCarousel).HasDefaultValue(false); e.Property(x => x.Status).HasDefaultValue(true); });

        modelBuilder.Entity<Staff>(e => { e.ToTable("Staff"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Diary>(e => { e.ToTable("Diary"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Company>(e => { e.ToTable("Company"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<AccessData>(e => { e.ToTable("AccessData"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<MonthData>(e => { e.ToTable("MonthData"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Activity>(e => { e.ToTable("Activity"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Tag>(e => { e.ToTable("Tag"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<WorksTag>(e => { e.ToTable("WorksTag"); e.HasKey(x => x.Id); });

        // Additive homepage-block tables (OrgMember / SaleWork / Partner), shared mapping with the read context.
        HomeBlocksModelConfiguration.Configure(modelBuilder);

        // 艺术家出版著作，与读上下文共用同一份映射。
        ArtistBookModelConfiguration.Configure(modelBuilder);

        // 现场编辑：站点文案（模板写死文字的可改副本），与读上下文共用映射。
        SiteTextModelConfiguration.Configure(modelBuilder);
        // 会员风采（Vip）排序与显隐旁表，与读上下文共用同一份映射。
        ArtistDisplayModelConfiguration.Configure(modelBuilder);
    }
}
