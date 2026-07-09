using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Create/edit form for an exhibition (Publication). The gallery is managed by the JS component
/// which submits the final ordered image names in <see cref="GalleryOrder"/>.</summary>
public sealed class PublicationFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入中文标题")]
    [Display(Name = "中文标题")]
    public string? TitleCN { get; set; }

    [Display(Name = "英文标题")]
    public string? TitleEN { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "开幕时间")]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "结束时间")]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "请输入图片文件夹名（英文/拼音，作为图片存储目录）")]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "文件夹名只能用英文字母、数字、-、_")]
    [Display(Name = "图片文件夹")]
    public string? FolderName { get; set; }

    [Display(Name = "地点")]
    public string? Location { get; set; }

    [Display(Name = "状态")]
    public bool? Status { get; set; }

    [Display(Name = "主办")]
    public string? zhuban { get; set; }

    [Display(Name = "承办")]
    public string? chengban { get; set; }

    [Display(Name = "协办")]
    public string? xieban { get; set; }

    public string? CoverPath { get; set; }

    [Display(Name = "展览类型")]
    public int Type { get; set; }

    [Display(Name = "序（前言）")]
    public string? Preface { get; set; }

    [Display(Name = "落款")]
    public string? Signature { get; set; }

    /// <summary>Comma-separated ordered gallery image file names submitted by the gallery JS component.</summary>
    public string? GalleryOrder { get; set; }

    /// <summary>
    /// JSON-encoded ordered array of works-catalog rows ({category,title,artist,size,medium,imagePath})
    /// submitted by the works-manager JS component. Null when the component wasn't touched (leaves the
    /// existing catalog, if any, untouched); an explicit "[]" clears it.
    /// </summary>
    public string? WorksJson { get; set; }
}
