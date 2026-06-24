using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Create/edit form for a Company (member enterprise) row.</summary>
public sealed class CompanyFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入企业中文名")]
    [Display(Name = "企业中文名")]
    public string? NameCN { get; set; }

    [Display(Name = "企业英文名")]
    public string? NameEN { get; set; }

    [Display(Name = "法人 / CEO")]
    public string? CEO { get; set; }

    [Display(Name = "经营范围")]
    public string? Business { get; set; }

    [Display(Name = "地址")]
    public string? Address { get; set; }

    /// <summary>Logo file name (e.g. {guid}.jpg), set by the upload field; rendered under /Content/ArtImg/Company/.</summary>
    public string? ImgPath { get; set; }

    public string? ImgUrl => string.IsNullOrEmpty(ImgPath) ? null : $"/Content/ArtImg/Company/{ImgPath}";
}
