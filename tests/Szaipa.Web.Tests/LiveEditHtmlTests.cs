using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Html;
using Szaipa.Data.Services.LiveEdit;
using Szaipa.Web.LiveEdit;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class LiveEditHtmlTests
{
    [Fact]
    public void Multiline_field_keeps_exact_original_for_editor_without_exposing_markup()
    {
        var editor = CreateEditor(active: true);
        const string value = "第一行\n第二行 \"引号\" <内容>";

        var html = Render(editor.Field("Artist", 42, "Introduction", value));
        var encodedValue = Regex.Match(html, "data-le-value=\"([^\"]*)\"").Groups[1].Value;

        Assert.NotEmpty(encodedValue);
        Assert.Equal(value, WebUtility.HtmlDecode(encodedValue));
        Assert.DoesNotContain("<内容>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Visitor_and_missing_row_do_not_receive_edit_metadata()
    {
        const string value = "第一行\n第二行";
        var visitor = CreateEditor(active: false);
        Assert.Equal(HtmlEncoder.Default.Encode(value), Render(visitor.Field("Artist", 42, "Introduction", value)));

        var editor = CreateEditor(active: true);
        Assert.Empty(Render(editor.Attr("Publication", null, "Preface", value)));
    }

    [Fact]
    public void Excerpt_truncates_for_visitors_but_keeps_full_text_while_editing()
    {
        var subtitle = new string('字', 60);
        var visitor = Render(CreateEditor(active: false).FieldExcerpt("News", 7, "Subtitle", subtitle, 50));
        Assert.Equal(new string('字', 50) + "…", WebUtility.HtmlDecode(visitor));
        Assert.Equal("短简介", WebUtility.HtmlDecode(Render(CreateEditor(active: false).FieldExcerpt("News", 7, "Subtitle", "短简介", 50))));

        var editing = Render(CreateEditor(active: true).FieldExcerpt("News", 7, "Subtitle", subtitle, 50));
        Assert.Contains(subtitle, WebUtility.HtmlDecode(editing), StringComparison.Ordinal); // 编辑态给全文
    }

    private static LiveEditHtml CreateEditor(bool active)
    {
        var context = new DefaultHttpContext();
        if (active) context.Items["__szaipa_liveedit"] = true;
        return new LiveEditHtml(new HttpContextAccessor { HttpContext = context }, new EmptySiteText(), HtmlEncoder.Default);
    }

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private sealed class EmptySiteText : ISiteTextProvider
    {
        public string? Get(string key) => null;
        public void Invalidate() { }
    }
}
