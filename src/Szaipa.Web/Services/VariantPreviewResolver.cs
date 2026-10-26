namespace Szaipa.Web.Services;

/// <summary>
/// T8：把原有「q30w1200 预览」解析统一升级为 960 宽 WebP 变体（画质更好、体积相近或更小）。
/// 变体不可用（功能关闭、非 JPG/PNG、原图不存在）时回退到原来的 q30 预览解析器，行为与以前一致。
/// 所有调用 <see cref="ILegacyImagePreviewResolver.Resolve"/> 的视图（卡片、头像、缩略图、封面）因此无需逐个修改。
/// </summary>
public sealed class VariantPreviewResolver : ILegacyImagePreviewResolver
{
    public const int PreviewWidth = 960;

    private readonly IImageVariants _variants;
    private readonly LegacyImagePreviewResolver _legacy;

    public VariantPreviewResolver(IImageVariants variants, LegacyImagePreviewResolver legacy)
    {
        _variants = variants;
        _legacy = legacy;
    }

    public string Resolve(string originalUrl)
    {
        var variant = _variants.Url(originalUrl, PreviewWidth);
        return ReferenceEquals(variant, originalUrl) || variant == originalUrl ? _legacy.Resolve(originalUrl) : variant;
    }
}

/// <summary><c>GET /_img/{width}/Content/...</c>：返回（必要时生成）WebP 变体；不可用时 302 回原图。</summary>
public static class ImageVariantEndpoint
{
    public static async Task HandleAsync(HttpContext context, IImageVariants variants, int width, string path)
    {
        if (!path.StartsWith("Content/", StringComparison.Ordinal) || !ImageVariants.Widths.Contains(width))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var relative = path["Content/".Length..];
        var file = await variants.GetOrCreateAsync(width, relative, context.RequestAborted);
        if (file is null)
        {
            // 生成失败/功能关闭：退回原图，页面照常显示。
            context.Response.Redirect("/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString)));
            return;
        }

        context.Response.ContentType = "image/webp";
        // URL 带 ?v=（随原图修改时间/大小变化），可长期缓存。
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        await context.Response.SendFileAsync(file, context.RequestAborted);
    }
}
