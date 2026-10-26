using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Create/edit form for an ArtistBook (艺术家出版著作) row. 出版年份为自由文本。</summary>
public sealed class ArtistBookFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int ArtistId { get; set; }

    [Required(ErrorMessage = "请输入书名")]
    [StringLength(200, ErrorMessage = "书名不能超过 200 字")]
    [Display(Name = "书名")]
    public string? Title { get; set; }

    [StringLength(200, ErrorMessage = "出版社不能超过 200 字")]
    [Display(Name = "出版社")]
    public string? Publisher { get; set; }

    [StringLength(50, ErrorMessage = "出版年份不能超过 50 字")]
    [Display(Name = "出版年份")]
    public string? PublishYear { get; set; }

    [Display(Name = "简介")]
    public string? Description { get; set; }

    [Display(Name = "排序（小的在前）")]
    public int Sort { get; set; }

    public string? CoverPath { get; set; }

    public string? CoverUrl =>
        string.IsNullOrEmpty(CoverPath) ? null : $"/Content/ArtImg/Artist/Book/{CoverPath}";
}
