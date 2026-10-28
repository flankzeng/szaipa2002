using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Szaipa.Data.Services.LiveEdit;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.LiveEdit;

/// <summary>
/// 现场编辑（T6）的开关：只有「已登录后台 + 带现场编辑 cookie」的请求才进入编辑态。
/// 普通访客、未开启编辑的管理员看到的 HTML 与改动前完全一致。
/// </summary>
public static class LiveEditState
{
    public const string CookieName = "szaipa_liveedit";
    private const string ItemKey = "__szaipa_liveedit";

    public static bool IsActive(HttpContext context) => context.Items[ItemKey] is true;

    /// <summary>在 UseAuthentication 之后调用。编辑态响应强制 no-store，避免带编辑标记的页面进任何缓存。</summary>
    public static IApplicationBuilder UseLiveEdit(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Cookies[CookieName] == "1"
            && context.User.Identity?.IsAuthenticated == true
            && !context.Request.Path.StartsWithSegments("/Staff"))
        {
            var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
            if ((await authorization.AuthorizeAsync(context.User, AdminAuthorization.StaffPolicy)).Succeeded)
            {
                context.Items[ItemKey] = true;
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });
            }
        }

        await next();
    });
}

/// <summary>
/// 视图里用 <c>@LiveEdit</c> 调用的标记助手（_ViewImports 注入）：
/// <list type="bullet">
/// <item><c>@LiveEdit.Text("home.core.title", "核心成员")</c> —— 模板写死的文字改为可编辑文案（存 SiteText，默认原文）。</item>
/// <item><c>&lt;h1 @LiveEdit.Attr("News", id, "Title")&gt;</c> —— 给显示数据库字段的元素加编辑标记（仅编辑态输出）。</item>
/// </list>
/// 非编辑态：Text 只输出（可能被改过的）文字本身，Attr 输出空，不额外包任何元素。
/// </summary>
public sealed class LiveEditHtml
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ISiteTextProvider _siteText;
    private readonly HtmlEncoder _encoder;

    // 用站点注册的 HtmlEncoder（UnicodeRanges.All），中文按原样输出，与 Razor 的 @value 一致。
    public LiveEditHtml(IHttpContextAccessor accessor, ISiteTextProvider siteText, HtmlEncoder encoder)
    {
        _accessor = accessor;
        _siteText = siteText;
        _encoder = encoder;
    }

    public bool Active => _accessor.HttpContext is { } context && LiveEditState.IsActive(context);

    /// <summary>单行文案。</summary>
    public IHtmlContent Text(string key, string fallback) => SiteText(key, fallback, multiline: false);

    /// <summary>多行文案：换行显示为 &lt;br /&gt;。</summary>
    public IHtmlContent Lines(string key, string fallback) => SiteText(key, fallback, multiline: true);

    /// <summary>
    /// 输出数据库字段的文字：非编辑态与 <c>@value</c> 完全相同（HTML 编码、不加任何元素）；
    /// 编辑态外包一个带编辑标记的 span。适合元素里还混有其他内容、不便直接加属性的场合。
    /// </summary>
    public IHtmlContent Field(string entity, int? id, string field, string? value)
    {
        var body = _encoder.Encode(value ?? string.Empty);
        var attributes = Attr(entity, id, field, value);
        return ReferenceEquals(attributes, HtmlString.Empty)
            ? new HtmlString(body)
            : new HtmlString($"<span{((HtmlString)attributes).Value}>{body}</span>");
    }

    /// <summary>
    /// 可拖拽排序 / 可新增的列表容器标记（现场编辑态才输出）：<c>data-le-sortable</c> = 实体，
    /// <c>data-le-group</c> = 排序范围（如 OrgMember 的分组）。live-edit.js 据此加拖拽手柄和「+ 新增」。
    /// </summary>
    public IHtmlContent SortableGroup(string entity, string group) =>
        Active
            ? new HtmlString($" data-le-sortable=\"{_encoder.Encode(entity)}\" data-le-group=\"{_encoder.Encode(group)}\"")
            : HtmlString.Empty;

    /// <summary>列表项的排序标记；回退内容（id ≤ 0，库里没有这一行）不可拖。</summary>
    public IHtmlContent SortableItem(int id) =>
        Active && id > 0 ? new HtmlString($" data-le-sort-id=\"{id}\"") : HtmlString.Empty;

    /// <summary>
    /// 列表里的摘要：非编辑态超过 <paramref name="maxLength"/> 字截断并加省略号（新闻简介列表只显示 50 字）；
    /// 编辑态输出全文，避免在截断后的文字上编辑把原文改短。
    /// </summary>
    public IHtmlContent FieldExcerpt(string entity, int? id, string field, string? value, int maxLength)
    {
        if (Active || value is null)
        {
            return Field(entity, id, field, value);
        }

        var text = value.Trim();
        var info = new System.Globalization.StringInfo(text);
        return new HtmlString(_encoder.Encode(info.LengthInTextElements <= maxLength
            ? text
            : info.SubstringByTextElements(0, maxLength).TrimEnd() + "…"));
    }

    /// <summary>数据库字段的编辑标记；id 为空/非正（回退内容）、字段不在白名单或非编辑态时输出空。</summary>
    public IHtmlContent Attr(string entity, int? id, string field, string? editValue = null)
    {
        if (!Active || id is not > 0 || !LiveEditRegistry.TryGet(entity, field, out _, out var fieldEntry))
        {
            return HtmlString.Empty;
        }

        var encoder = _encoder;
        // 多行字段的原值不能依赖 innerText：HTML 可能折叠换行或按段落拆开。
        var valueAttribute = fieldEntry!.Kind == LiveEditKind.Lines && editValue is not null
            ? $" data-le-value=\"{encoder.Encode(editValue)}\""
            : string.Empty;
        return new HtmlString(
            $" data-le-entity=\"{encoder.Encode(entity)}\" data-le-id=\"{id.Value}\" data-le-field=\"{encoder.Encode(field)}\" data-le-kind=\"{KindName(fieldEntry.Kind)}\"{valueAttribute}");
    }

    private IHtmlContent SiteText(string key, string fallback, bool multiline)
    {
        var value = _siteText.Get(key) ?? fallback;
        var encoder = _encoder;
        var body = multiline
            ? string.Join("<br />", value.Replace("\r\n", "\n").Split('\n').Select(encoder.Encode))
            : encoder.Encode(value);
        if (!Active)
        {
            return new HtmlString(body);
        }

        var valueAttribute = multiline ? $" data-le-value=\"{encoder.Encode(value)}\"" : string.Empty;
        return new HtmlString(
            $"<span data-le-site=\"{encoder.Encode(key)}\" data-le-kind=\"{(multiline ? "lines" : "text")}\"{valueAttribute}>{body}</span>");
    }

    private static string KindName(LiveEditKind kind) => kind switch
    {
        LiveEditKind.Html => "html",
        LiveEditKind.Lines => "lines",
        _ => "text",
    };
}
