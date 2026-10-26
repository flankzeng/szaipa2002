using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// <see cref="WeChatSyncRecord"/> 的表映射，读侧与写侧两个上下文共用，保证两边列定义一致
/// （和 <c>docs/sql/2026-10-26-wechat-sync.sql</c> 对齐）。
/// </summary>
internal static class WeChatSyncRecordModel
{
    public static void Configure(EntityTypeBuilder<WeChatSyncRecord> entity)
    {
        entity.ToTable("WeChatSyncRecord");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.SourceKey).IsRequired().HasMaxLength(300);
        entity.Property(x => x.SourceUrl).HasMaxLength(1000);
        entity.Property(x => x.ArticleId).HasMaxLength(100);
        entity.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        entity.Property(x => x.Source).IsRequired().HasMaxLength(20);
        entity.Property(x => x.IsHidden).HasDefaultValue(false);
        entity.HasIndex(x => x.SourceKey).IsUnique();
        entity.HasIndex(x => x.NewsId);
    }
}
