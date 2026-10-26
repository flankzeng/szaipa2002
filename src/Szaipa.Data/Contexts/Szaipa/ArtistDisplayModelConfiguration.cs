using Microsoft.EntityFrameworkCore;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// <see cref="ArtistDisplay"/> 的表映射，读上下文与后台写上下文共用，避免两处漂移。
/// 与 docs/sql/2026-10-26-vip-order.sql 保持一致：ArtistId 为非自增主键，IsHidden 默认 0。
/// </summary>
internal static class ArtistDisplayModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArtistDisplay>(e =>
        {
            e.ToTable("ArtistDisplay");
            e.HasKey(x => x.ArtistId);
            e.Property(x => x.ArtistId).ValueGeneratedNever();
            e.Property(x => x.Sort).HasDefaultValue(0);
            e.Property(x => x.IsHidden).HasDefaultValue(false);
        });
    }
}
