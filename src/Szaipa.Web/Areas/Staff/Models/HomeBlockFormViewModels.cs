using System.ComponentModel.DataAnnotations;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>
/// Accepts an empty value or a site-relative image URL under <c>/Content/</c> (what the upload field stores),
/// rejecting anything else (external URLs, <c>javascript:</c>, path traversal). The value is rendered into
/// <c>src</c> attributes on public pages, so it must stay inside the content tree.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ContentImagePathAttribute : ValidationAttribute
{
    public ContentImagePathAttribute()
        : base("图片路径必须是 /Content/ 下的站内路径。")
    {
    }

    public override bool IsValid(object? value) =>
        value is null
        || (value is string text
            && (string.IsNullOrWhiteSpace(text)
                || (text.StartsWith("/Content/", StringComparison.Ordinal)
                    && !text.Contains("..", StringComparison.Ordinal)
                    && !text.Contains('\\')
                    && !text.Contains('?')
                    && !text.Contains('#')
                    && !text.Contains('"')
                    && !text.Contains('<')
                    && !text.Any(char.IsControl))));
}

/// <summary>Accepts an empty value or an absolute http(s) URL.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute()
        : base("链接必须以 http:// 或 https:// 开头。")
    {
    }

    public override bool IsValid(object? value) =>
        value is null
        || (value is string text
            && (string.IsNullOrWhiteSpace(text)
                || (Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))));
}

/// <summary>Edit/create form for an <see cref="OrgMember"/>.</summary>
public sealed class OrgMemberFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入姓名")]
    [StringLength(100, ErrorMessage = "姓名过长")]
    [Display(Name = "姓名")]
    public string? Name { get; set; }

    [StringLength(50, ErrorMessage = "职务过长")]
    [Display(Name = "职务")]
    public string? Role { get; set; }

    [StringLength(100, ErrorMessage = "头衔过长")]
    [Display(Name = "头衔")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "请选择分组")]
    [Display(Name = "分组")]
    public string GroupName { get; set; } = OrgGroups.Core;

    [Display(Name = "关联艺术家")]
    public int? ArtistId { get; set; }

    /// <summary>Full site-relative URL (e.g. /Content/ArtImg/Org/{guid}.png), stored by the upload field (data-store="url").</summary>
    [ContentImagePath]
    [StringLength(300)]
    public string? AvatarPath { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    [Display(Name = "在首页显示")]
    public bool IsVisible { get; set; } = true;

    /// <summary>For the form's dropdown; filled by the controller, never bound.</summary>
    public IReadOnlyList<ArtistAvatarOption> ArtistOptions { get; set; } = Array.Empty<ArtistAvatarOption>();
}

/// <summary>Edit/create form for a <see cref="SaleWork"/>.</summary>
public sealed class SaleWorkFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "艺术家")]
    public int? ArtistId { get; set; }

    [StringLength(100, ErrorMessage = "艺术家名过长")]
    [Display(Name = "艺术家名")]
    public string? ArtistName { get; set; }

    [Required(ErrorMessage = "请输入作品名")]
    [StringLength(200, ErrorMessage = "作品名过长")]
    [Display(Name = "作品名")]
    public string? Title { get; set; }

    [StringLength(100, ErrorMessage = "尺寸过长")]
    [Display(Name = "尺寸")]
    public string? Size { get; set; }

    [StringLength(100, ErrorMessage = "材质过长")]
    [Display(Name = "材质")]
    public string? Medium { get; set; }

    [Required(ErrorMessage = "请上传作品图片")]
    [ContentImagePath]
    [StringLength(300)]
    public string? ImagePath { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    [Display(Name = "在首页显示")]
    public bool IsVisible { get; set; } = true;

    public IReadOnlyList<ArtistAvatarOption> ArtistOptions { get; set; } = Array.Empty<ArtistAvatarOption>();
}

/// <summary>Edit/create form for a <see cref="Partner"/>.</summary>
public sealed class PartnerFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入名称")]
    [StringLength(200, ErrorMessage = "名称过长")]
    [Display(Name = "名称")]
    public string? Name { get; set; }

    [ContentImagePath]
    [StringLength(300)]
    public string? LogoPath { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    [HttpUrl]
    [StringLength(500, ErrorMessage = "链接过长")]
    [Display(Name = "链接")]
    public string? Link { get; set; }

    [Display(Name = "简介")]
    public string? Description { get; set; }

    [Display(Name = "在页面显示")]
    public bool IsVisible { get; set; } = true;
}

/// <summary>List page model for the org chart: members plus the artist names for linked rows.</summary>
public sealed class OrgMemberIndexViewModel
{
    public required IReadOnlyList<OrgMember> Members { get; init; }

    /// <summary>Artist id -> display name, for the "关联艺术家" column.</summary>
    public required IReadOnlyDictionary<int, string> ArtistNames { get; init; }
}

/// <summary>Model of the shared list-row action cell (move up/down, show/hide, edit, delete).</summary>
public sealed record SortableRowActionsModel(int Id, bool IsVisible, bool CanMoveUp, bool CanMoveDown, string DeletePrompt);
