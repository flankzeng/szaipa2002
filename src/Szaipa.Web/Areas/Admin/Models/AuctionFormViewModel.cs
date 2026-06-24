using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

/// <summary>Create/edit form for an Auction (artist auction record) row. Date is a legacy free-text field.</summary>
public sealed class AuctionFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "请选择艺术家")]
    [Display(Name = "艺术家")]
    public int ArtistId { get; set; }

    [Required(ErrorMessage = "请输入名称")]
    [Display(Name = "作品名称")]
    public string? Title { get; set; }

    [Display(Name = "成交价")]
    public string? Price { get; set; }

    [Display(Name = "人民币")]
    public string? RMB { get; set; }

    [Display(Name = "港币")]
    public string? HKD { get; set; }

    [Display(Name = "美元")]
    public string? USD { get; set; }

    [Display(Name = "拍卖日期")]
    public string? Date { get; set; }

    public string? CoverPath { get; set; }

    public string? CoverUrl =>
        string.IsNullOrEmpty(CoverPath) ? null : $"/Content/ArtImg/Artist/Auction/{CoverPath}";
}
