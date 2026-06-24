using System.ComponentModel.DataAnnotations;

namespace Szaipa.Web.Areas.Admin.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "请输入账户名")]
    [Display(Name = "账户名")]
    public string? StaffName { get; set; }

    [Required(ErrorMessage = "请输入密码")]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string? Password { get; set; }
}
