using Microsoft.EntityFrameworkCore;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// <see cref="ArtistBook"/> 的表映射，读上下文与后台写上下文共用，避免两处漂移。
/// 列长度与 docs/sql/2026-10-26-artist-books.sql 保持一致。
/// </summary>
internal static class ArtistBookModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArtistBook>(e =>
        {
            e.ToTable("ArtistBook");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Publisher).HasMaxLength(200);
            e.Property(x => x.PublishYear).HasMaxLength(50);
            e.Property(x => x.CoverPath).HasMaxLength(300);
            e.Property(x => x.Sort).HasDefaultValue(0);
        });
    }
}
