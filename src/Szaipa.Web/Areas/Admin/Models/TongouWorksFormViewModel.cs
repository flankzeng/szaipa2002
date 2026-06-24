using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>Create/edit form for a 同构(Tongou) work row.</summary>
public sealed class TongouWorksFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int Atristid { get; set; }

    [Required(ErrorMessage = "请输入作品名称")]
    [Display(Name = "作品名称")]
    public string? Title { get; set; }

    [Display(Name = "尺寸")]
    public string? Size { get; set; }

    [Display(Name = "类型")]
    public string? Type { get; set; }

    [Display(Name = "创作年份")]
    public string? CreationDate { get; set; }

    /// <summary>Full <c>/Content/...</c> URL — same full-path convention as <c>HeardPath</c> above.</summary>
    public string? ImgPath { get; set; }
}
