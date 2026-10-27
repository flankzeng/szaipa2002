using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// 后台保存兜底：数据库因「内容超出列宽 / 必填列为空」拒绝写入时（生产库列宽与非空约束比代码校验更严，
/// 2026-10-27 staging 实测），不再返回 500，而是回到上一页并给出可读提示。其他异常照常抛出。
/// </summary>
public sealed class StaffSaveErrorFilter : IExceptionFilter
{
    // 2628/8152：字符串截断；515：不能插入 NULL；547：外键/检查约束冲突。
    private static readonly int[] Handled = { 2628, 8152, 515, 547 };

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled
            || !string.Equals(context.RouteData.Values["area"] as string, "Staff", StringComparison.OrdinalIgnoreCase)
            || context.Exception is not DbUpdateException { InnerException: SqlException sql }
            || !Handled.Contains(sql.Number))
        {
            return;
        }

        var message = sql.Number switch
        {
            515 => "保存失败：有必填项为空，请补全后再保存。",
            547 => "保存失败：关联的数据不存在或已被删除，请刷新后重试。",
            _ => "保存失败：某一项内容超出了数据库允许的长度（如名称、标题、地点过长），请缩短后再保存。",
        };
        var tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
        tempData["Error"] = message;
        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        context.Result = Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                         && string.Equals(uri.Host, context.HttpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            ? new RedirectResult(uri.PathAndQuery)
            : new RedirectResult("/Staff/Dashboard/Index");
        context.ExceptionHandled = true;
    }
}
