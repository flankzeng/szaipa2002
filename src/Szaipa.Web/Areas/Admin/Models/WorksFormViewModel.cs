using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>Create/edit form for a Works (artist artwork) row.</summary>
public sealed class WorksFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int ArtistId { get; set; }

    [Required(ErrorMessage = "请输入作品名称")]
    [Display(Name = "作品名称")]
    public string? Title { get; set; }

    [Display(Name = "材质 / 尺寸")]
    public string? Content { get; set; }

    [Display(Name = "标签")]
    public string? Tags { get; set; }

    public string? Path { get; set; }

    public string? PathUrl =>
        string.IsNullOrEmpty(Path) ? null : $"/Content/ArtImg/Artist/works-narrow/{Path}";
}
