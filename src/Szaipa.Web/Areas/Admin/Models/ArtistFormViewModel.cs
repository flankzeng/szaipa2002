using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>Edit/create form for an Artist row. DeedsThings (年表) comes from the TipTap hidden field.</summary>
public sealed class ArtistFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入中文名")]
    [Display(Name = "中文名")]
    public string? ArtistNameCN { get; set; }

    [Display(Name = "英文名")]
    public string? ArtistNameEN { get; set; }

    [Display(Name = "性别")]
    public string? Sex { get; set; } = "男";

    [Display(Name = "国家")]
    public string? Nation { get; set; }

    [Display(Name = "城市")]
    public string? City { get; set; }

    [Display(Name = "级别")]
    public string? Title { get; set; }

    [Display(Name = "社会职务")]
    public string? Position { get; set; }

    [Display(Name = "主题色 1")]
    public string? Color1 { get; set; } = "#3e47e1";

    [Display(Name = "主题色 2")]
    public string? Color2 { get; set; } = "#151965";

    [Display(Name = "简介")]
    public string? Introduction { get; set; }

    [Display(Name = "荣誉")]
    public string? Honor { get; set; }

    [Display(Name = "艺术家年表")]
    public string? DeedsThings { get; set; }

    /// <summary>Avatar file name (e.g. {guid}.jpg), set by the upload field; rendered under /Content/ArtImg/Artist/.</summary>
    public string? Path { get; set; }

    /// <summary>Banner image 1 file name; rendered under /Content/ArtImg/Artist/Banner/.</summary>
    public string? Path1 { get; set; }

    /// <summary>Banner image 2 file name; rendered under /Content/ArtImg/Artist/Banner/.</summary>
    public string? Path2 { get; set; }

    public string? PathUrl => string.IsNullOrEmpty(Path) ? null : $"/Content/ArtImg/Artist/{Path}";

    public string? Path1Url => string.IsNullOrEmpty(Path1) ? null : $"/Content/ArtImg/Artist/Banner/{Path1}";

    public string? Path2Url => string.IsNullOrEmpty(Path2) ? null : $"/Content/ArtImg/Artist/Banner/{Path2}";
}
