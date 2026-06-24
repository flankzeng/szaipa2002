using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Create/edit form for a 同构(Tongou) artist row.</summary>
public sealed class TongouAtristFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "请输入艺术家姓名")]
    [Display(Name = "姓名")]
    public string? Name { get; set; }

    [Display(Name = "头衔")]
    public string? Title { get; set; }

    [Display(Name = "简介")]
    public string? AboutText { get; set; }

    /// <summary>
    /// Full <c>/Content/...</c> URL, set by the upload field — unlike the Szaipa-side modules, the Tongou
    /// schema stores the complete path in this column (see <c>data-store="url"</c> on the upload field).
    /// </summary>
    public string? HeardPath { get; set; }
}
