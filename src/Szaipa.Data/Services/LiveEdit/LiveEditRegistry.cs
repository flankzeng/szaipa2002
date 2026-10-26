using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.LiveEdit;

/// <summary>字段在现场编辑里的编辑方式。</summary>
public enum LiveEditKind
{
    /// <summary>单行纯文本（回车即保存）。</summary>
    Text,

    /// <summary>多行纯文本（保留换行）。</summary>
    Lines,

    /// <summary>富文本 HTML（弹出编辑器，保存前白名单清洗）。</summary>
    Html
}

public sealed record LiveEditField(string Name, LiveEditKind Kind, int MaxLength, bool Required = false);

public sealed record LiveEditEntity(string Name, string Noun, Type ClrType, IReadOnlyDictionary<string, LiveEditField> Fields);

/// <summary>
/// 现场编辑可写入的实体与字段白名单。前台页面只能通过这里列出的 (实体, 字段) 修改数据库，
/// 其他列（路径、排序、关联 Id、审计字段等）一律拒绝——即便请求被伪造也改不到。
/// 实体名用 CLR 类名，与前台 <c>data-le-entity</c> 一致。
/// </summary>
public static class LiveEditRegistry
{
    private static readonly Dictionary<string, LiveEditEntity> Entities = new(StringComparer.Ordinal)
    {
        [nameof(News)] = Entity<News>("新闻",
            Field(nameof(News.Title), LiveEditKind.Text, 200, required: true),
            Field(nameof(News.Subtitle), LiveEditKind.Lines, 1000),
            Field(nameof(News.Content), LiveEditKind.Html, 200_000)),
        [nameof(ArtNews)] = Entity<ArtNews>("艺术家资讯",
            Field(nameof(ArtNews.Title), LiveEditKind.Text, 200, required: true),
            Field(nameof(ArtNews.SubTitle), LiveEditKind.Lines, 1000),
            Field(nameof(ArtNews.Content), LiveEditKind.Html, 200_000)),
        [nameof(Artist)] = Entity<Artist>("艺术家",
            Field(nameof(Artist.ArtistNameCN), LiveEditKind.Text, 100, required: true),
            Field(nameof(Artist.ArtistNameEN), LiveEditKind.Text, 100),
            Field(nameof(Artist.Title), LiveEditKind.Text, 200),
            Field(nameof(Artist.Position), LiveEditKind.Lines, 1000),
            Field(nameof(Artist.Introduction), LiveEditKind.Lines, 4000),
            Field(nameof(Artist.Honor), LiveEditKind.Lines, 4000),
            Field(nameof(Artist.DeedsThings), LiveEditKind.Html, 100_000)),
        [nameof(Works)] = Entity<Works>("作品",
            Field(nameof(Works.Title), LiveEditKind.Text, 200),
            Field(nameof(Works.Content), LiveEditKind.Lines, 2000)),
        [nameof(Publication)] = Entity<Publication>("展览",
            Field(nameof(Publication.TitleCN), LiveEditKind.Text, 200, required: true),
            Field(nameof(Publication.TitleEN), LiveEditKind.Text, 200),
            Field(nameof(Publication.Location), LiveEditKind.Text, 200),
            Field(nameof(Publication.zhuban), LiveEditKind.Text, 200),
            Field(nameof(Publication.chengban), LiveEditKind.Text, 200),
            Field(nameof(Publication.xieban), LiveEditKind.Text, 200),
            Field(nameof(Publication.Preface), LiveEditKind.Lines, 20_000),
            Field(nameof(Publication.Signature), LiveEditKind.Text, 200)),
        [nameof(FeaturedExhibition)] = Entity<FeaturedExhibition>("首页推荐",
            Field(nameof(FeaturedExhibition.Title), LiveEditKind.Text, 200, required: true),
            Field(nameof(FeaturedExhibition.YearText), LiveEditKind.Text, 20),
            Field(nameof(FeaturedExhibition.InfoLines), LiveEditKind.Lines, 2000)),
        [nameof(OrgMember)] = Entity<OrgMember>("组织成员",
            Field(nameof(OrgMember.Name), LiveEditKind.Text, 50, required: true),
            Field(nameof(OrgMember.Role), LiveEditKind.Text, 50),
            Field(nameof(OrgMember.Title), LiveEditKind.Text, 50)),
        [nameof(SaleWork)] = Entity<SaleWork>("在售作品",
            Field(nameof(SaleWork.ArtistName), LiveEditKind.Text, 50),
            Field(nameof(SaleWork.Title), LiveEditKind.Text, 200, required: true),
            Field(nameof(SaleWork.Size), LiveEditKind.Text, 100),
            Field(nameof(SaleWork.Medium), LiveEditKind.Text, 100)),
        [nameof(Partner)] = Entity<Partner>("合作伙伴",
            Field(nameof(Partner.Name), LiveEditKind.Lines, 200, required: true),
            Field(nameof(Partner.Description), LiveEditKind.Lines, 4000)),
        [nameof(ArtistBook)] = Entity<ArtistBook>("出版著作",
            Field(nameof(ArtistBook.Title), LiveEditKind.Text, 200, required: true),
            Field(nameof(ArtistBook.Publisher), LiveEditKind.Text, 200),
            Field(nameof(ArtistBook.PublishYear), LiveEditKind.Text, 50),
            Field(nameof(ArtistBook.Description), LiveEditKind.Lines, 4000)),
    };

    public static bool TryGet(string? entity, string? field, out LiveEditEntity? entry, out LiveEditField? fieldEntry)
    {
        entry = null;
        fieldEntry = null;
        return entity is not null && field is not null
            && Entities.TryGetValue(entity, out entry)
            && entry.Fields.TryGetValue(field, out fieldEntry);
    }

    /// <summary>站点文案键：小写字母开头，只含小写字母/数字/点/横线/下划线。</summary>
    public static bool IsValidSiteTextKey(string? key) =>
        key is { Length: >= 2 and <= 200 }
        && char.IsAsciiLetterLower(key[0])
        && key.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '-' or '_');

    public const int SiteTextMaxLength = 20_000;

    private static LiveEditEntity Entity<T>(string noun, params LiveEditField[] fields) =>
        new(typeof(T).Name, noun, typeof(T), fields.ToDictionary(f => f.Name, StringComparer.Ordinal));

    private static LiveEditField Field(string name, LiveEditKind kind, int maxLength, bool required = false) =>
        new(name, kind, maxLength, required);
}
