using Microsoft.AspNetCore.Razor.TagHelpers;
using Szaipa.Web.Services;

namespace Szaipa.Web.TagHelpers;

/// <summary>
/// T8 图片流量优化：所有指向 /Content 下 JPG/PNG（≥40KB）的 &lt;img&gt; 自动补 WebP 多尺寸 srcset，
/// 浏览器按屏幕宽度和像素密度挑最合适的一档；原 src 保留作回退，DOM 结构与样式不变。
/// 已有 srcset、预览图（/Content/_preview）、已是变体（/_img）或加了 data-no-variants 的图片不处理。
/// </summary>
[HtmlTargetElement("img", Attributes = "src", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ImageVariantTagHelper : TagHelper
{
    private readonly IImageVariants _variants;

    public ImageVariantTagHelper(IImageVariants variants)
    {
        _variants = variants;
    }

    // 在其他 img 相关 TagHelper（如 asp-append-version）之后运行。
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("srcset") || output.Attributes.ContainsName("data-no-variants")
            || output.Attributes["src"]?.Value?.ToString() is not { Length: > 0 } src)
        {
            return;
        }

        if (_variants.SrcSet(src) is not { } srcset)
        {
            return;
        }

        output.Attributes.SetAttribute("srcset", srcset);
        if (!output.Attributes.ContainsName("sizes"))
        {
            output.Attributes.SetAttribute("sizes", "100vw");
        }
    }
}
