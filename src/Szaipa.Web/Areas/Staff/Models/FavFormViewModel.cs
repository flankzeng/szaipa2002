using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Create/edit form for a Fav (artist collection) row.</summary>
public sealed class FavFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int ArtistId { get; set; }

    [Required(ErrorMessage = "请输入名称")]
    [Display(Name = "名称")]
    public string? Title { get; set; }

    [Display(Name = "创作者")]
    public string? Creator { get; set; }

    [Display(Name = "年份")]
    public string? Year { get; set; }

    [Display(Name = "收藏地点")]
    public string? Location { get; set; }

    [Display(Name = "尺寸")]
    public string? Size { get; set; }

    [Display(Name = "材质")]
    public string? Material { get; set; }

    [Display(Name = "类型")]
    public string? Type { get; set; }

    [Display(Name = "省份")]
    public string? Province { get; set; }

    [Display(Name = "藏品编号")]
    public string? CollectNumber { get; set; }

    public string? CoverPath { get; set; }

    public string? CoverUrl =>
        string.IsNullOrEmpty(CoverPath) ? null : $"/Content/ArtImg/Artist/Fav/{CoverPath}";
}
