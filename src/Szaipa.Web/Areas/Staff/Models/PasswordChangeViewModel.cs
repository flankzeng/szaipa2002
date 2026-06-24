using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Change-password form for the signed-in staff member (legacy StaffController.PasswordChange).</summary>
public sealed class PasswordChangeViewModel
{
    [Required(ErrorMessage = "请输入原密码")]
    [DataType(DataType.Password)]
    [Display(Name = "原密码")]
    public string? CurrentPassword { get; set; }

    [Required(ErrorMessage = "请输入新密码")]
    [MinLength(6, ErrorMessage = "新密码至少 6 位")]
    [DataType(DataType.Password)]
    [Display(Name = "新密码")]
    public string? NewPassword { get; set; }

    [Required(ErrorMessage = "请再次输入新密码")]
    [DataType(DataType.Password)]
    [Display(Name = "确认新密码")]
    [Compare(nameof(NewPassword), ErrorMessage = "两次新密码输入不一致")]
    public string? ConfirmPassword { get; set; }
}
