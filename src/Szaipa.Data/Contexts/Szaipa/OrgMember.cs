namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// One person on the association's org chart (homepage 核心成员 / 顾问团队 blocks). It is an independent roster:
/// <see cref="ArtistId"/> optionally links the person to an <see cref="Artist"/>, in which case the public card
/// links to <c>/Home/Art/{ArtistId}</c>; an unlinked person still renders (without a link). Table is additive
/// (see docs/sql/2026-10-26-org-and-home-blocks.sql) — readers must tolerate it not existing yet.
/// </summary>
public sealed class OrgMember : ISortableRecord
{
    public int Id { get; set; }

    /// <summary>Display name, e.g. 张岚芊.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>职务 (会长 / 荣誉会长 / 副会长 / 监事 / 秘书长 / 理事 / 会员 / 顾问 / 策展人 …). Free text; <see cref="OrgRoles"/> holds the suggestions.</summary>
    public string? Role { get; set; }

    /// <summary>头衔 (艺术家 / 教授 / 评论家 …), shown after the role as "职务 | 头衔".</summary>
    public string? Title { get; set; }

    /// <summary>分组, see <see cref="OrgGroups"/>. The homepage renders 核心成员 and 顾问团队.</summary>
    public string GroupName { get; set; } = OrgGroups.Core;

    /// <summary>Optional link to <see cref="Artist.Id"/>. No EF navigation property (entities stay flat).</summary>
    public int? ArtistId { get; set; }

    /// <summary>Site-relative image URL under /Content (e.g. /Content/images/to black/vip-2zlq.png). When empty and an
    /// artist is linked, the artist's own avatar is used.</summary>
    public string? AvatarPath { get; set; }

    /// <summary>Intrinsic pixel size of <see cref="AvatarPath"/> (HTML width/height hint only). Auto-filled by the admin form.</summary>
    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public int Sort { get; set; }

    public bool IsVisible { get; set; } = true;
}

/// <summary>Known <see cref="OrgMember.GroupName"/> values.</summary>
public static class OrgGroups
{
    public const string Core = "核心成员";

    public const string Advisory = "顾问团队";

    public static readonly IReadOnlyList<string> All = new[] { Core, Advisory };
}

/// <summary>Suggested 职务 / 头衔 values for the admin form's datalist (the fields stay free text).</summary>
public static class OrgRoles
{
    public static readonly IReadOnlyList<string> Roles = new[]
    {
        "会长", "荣誉会长", "副会长", "监事", "秘书长", "理事", "会员", "顾问", "策展人"
    };

    public static readonly IReadOnlyList<string> Titles = new[]
    {
        "艺术家", "教授", "评论家", "考古学家", "艺术顾问"
    };
}
