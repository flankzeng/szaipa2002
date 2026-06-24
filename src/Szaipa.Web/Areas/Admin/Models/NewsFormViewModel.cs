using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>Edit/create form for a News row. Content comes from the TipTap hidden field.</summary>
public sealed class NewsFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入主标题")]
    [Display(Name = "主标题")]
    public string? Title { get; set; }

    [Display(Name = "简介")]
    public string? Subtitle { get; set; }

    [Display(Name = "来源链接")]
    public string? Link { get; set; }

    [Display(Name = "重要新闻")]
    public bool Important { get; set; }

    /// <summary>Cover image file name (e.g. {guid}.jpg), set by the upload field; rendered under /Content/newsImg/.</summary>
    public string? CoverPath { get; set; }

    [Display(Name = "正文")]
    public string? Content { get; set; }

    public string? CoverUrl => string.IsNullOrEmpty(CoverPath) ? null : $"/Content/newsImg/{CoverPath}";

    public bool IsOriginal => string.IsNullOrWhiteSpace(Link);
}
