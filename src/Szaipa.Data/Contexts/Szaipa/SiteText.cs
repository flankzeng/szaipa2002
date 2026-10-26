using Microsoft.EntityFrameworkCore;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// 现场编辑（T6）用的站点文案表：模板里原本写死的标题/段落按 Key 存一份可改的值。
/// 没有记录（或表还没建）时，页面一律显示模板里的默认原文，所以新增这张表不会改变现有页面。
/// </summary>
public sealed class SiteText
{
    /// <summary>文案键，如 <c>home.core.title</c>；小写字母/数字/点/横线/下划线。</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary><see cref="SiteText"/> 的表映射，读写上下文共用；列长度与 docs/sql/2026-10-26-site-text.sql 一致。</summary>
internal static class SiteTextModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteText>(e =>
        {
            e.ToTable("SiteText");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Value).IsRequired();
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });
    }
}
