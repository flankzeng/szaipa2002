using Microsoft.EntityFrameworkCore;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Table mapping for the additive homepage-block entities (<see cref="OrgMember"/>, <see cref="SaleWork"/>,
/// <see cref="Partner"/>), shared by <c>SzaipaLegacyReadContext</c> and <c>SzaipaAdminContext</c> so the two
/// contexts cannot drift. Column sizes mirror docs/sql/2026-10-26-org-and-home-blocks.sql.
/// </summary>
internal static class HomeBlocksModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgMember>(e =>
        {
            e.ToTable("OrgMember");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50);
            e.Property(x => x.Title).HasMaxLength(100);
            e.Property(x => x.GroupName).HasMaxLength(50).IsRequired();
            e.Property(x => x.AvatarPath).HasMaxLength(300);
        });

        modelBuilder.Entity<SaleWork>(e =>
        {
            e.ToTable("SaleWork");
            e.HasKey(x => x.Id);
            e.Property(x => x.ArtistName).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Size).HasMaxLength(100);
            e.Property(x => x.Medium).HasMaxLength(100);
            e.Property(x => x.ImagePath).HasMaxLength(300);
        });

        modelBuilder.Entity<Partner>(e =>
        {
            e.ToTable("Partner");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.LogoPath).HasMaxLength(300);
            e.Property(x => x.Link).HasMaxLength(500);
        });
    }
}
