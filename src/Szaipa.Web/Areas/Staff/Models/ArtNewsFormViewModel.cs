using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Create/edit form for an ArtNews (per-artist news) row. Content comes from the TipTap field.</summary>
public sealed class ArtNewsFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int ArtistId { get; set; }

    [Required(ErrorMessage = "请输入标题")]
    [Display(Name = "标题")]
    public string? Title { get; set; }

    [Display(Name = "简介")]
    public string? SubTitle { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "日期")]
    public DateTime? Date { get; set; }

    public string? CoverPath { get; set; }

    [Display(Name = "正文")]
    public string? Content { get; set; }

    public string? CoverUrl =>
        string.IsNullOrEmpty(CoverPath) ? null : $"/Content/ArtImg/Artist/ArtNews/{CoverPath}";
}
