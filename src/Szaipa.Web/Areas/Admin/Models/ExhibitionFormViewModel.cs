using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>Create/edit form for an Exhibition (per-artist exhibition) row. Dates are legacy free-text fields.</summary>
public sealed class ExhibitionFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int ArtistId { get; set; }

    [Required(ErrorMessage = "请输入展览名称")]
    [Display(Name = "展览名称")]
    public string? Title { get; set; }

    [Display(Name = "地点")]
    public string? Location { get; set; }

    [Display(Name = "开始日期")]
    public string? StartDate { get; set; }

    [Display(Name = "结束日期")]
    public string? EndDate { get; set; }

    [Display(Name = "链接")]
    public string? Link { get; set; }

    public string? CoverPath { get; set; }

    public string? CoverUrl =>
        string.IsNullOrEmpty(CoverPath) ? null : $"/Content/ArtImg/Artist/Exhibition/{CoverPath}";
}
