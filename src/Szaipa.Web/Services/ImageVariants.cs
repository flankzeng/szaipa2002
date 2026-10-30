using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Services;

/// <summary>图片变体（T8 图片流量优化）配置，配置段 <c>ImageVariants</c>。</summary>
public sealed class ImageVariantOptions
{
    public const string SectionName = "ImageVariants";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// WebP 缓存目录（绝对路径，须在发布目录之外、站点进程可写）。Development 未配置时默认
    /// <c>{ContentRoot}/App_Data/image-cache</c>（已 gitignore）；Production 未配置则整体关闭、回退原图。
    /// </summary>
    public string CacheRoot { get; set; } = string.Empty;

    /// <summary>大图（1600/2400，看细节用）的 WebP 质量（1–100）。80 在照片类图片上肉眼与原图难以区分。</summary>
    public int Quality { get; set; } = 80;

    /// <summary>
    /// 小图（480/960，卡片/缩略图/预览用）的 WebP 质量。72 仍明显优于旧的 q30 JPEG 预览，
    /// 对笔触细密的作品图能把体积压到与旧预览相当。
    /// </summary>
    public int PreviewQuality { get; set; } = 72;
}

/// <summary>
/// 把 /Content 下的 JPG/PNG 按需转成指定宽度的 WebP：首次请求从原图生成并写入缓存目录，之后直接返回缓存文件。
/// 原图永不修改/删除；任何失败都回退原图 URL，绝不影响页面。
/// URL 形如 <c>/_img/960/Content/images/man/10006.jpg?v=…</c>，v 随原图修改时间/大小变化，可放心长缓存。
/// </summary>
public interface IImageVariants
{
    bool Enabled { get; }

    /// <summary>返回指定宽度变体的 URL；不符合条件（非 /Content、非 JPG/PNG、文件不存在、功能关闭）时原样返回。</summary>
    string Url(string originalUrl, int width);

    /// <summary>
    /// 生成 480/960/1600/2400 四档 WebP 的 srcset；不符合条件或原图小于 <paramref name="minBytes"/>（小图标不值得）时返回 null。
    /// </summary>
    string? SrcSet(string originalUrl, long minBytes = 40_000);

    /// <summary>
    /// 给一段富文本 HTML 里指向 /Content 的 &lt;img&gt; 补 srcset/sizes（新闻正文等数据库内容用）；
    /// 已转好 MP4 的大 GIF 换成静音循环播放的 &lt;video&gt;（见 <c>ImageVariants.GifVideoTag</c>）。
    /// </summary>
    string AddSrcSetToHtml(string? html, string sizes);

    /// <summary>取得（必要时生成）变体缓存文件的物理路径；失败返回 null。</summary>
    Task<string?> GetOrCreateAsync(int width, string relativeContentPath, CancellationToken cancellationToken);
}

public sealed class ImageVariants : IImageVariants
{
    public static readonly int[] Widths = { 480, 960, 1600, 2400 };

    /// <summary>编码规则版本：改动尺寸上限/质量等规则时递增，旧缓存与浏览器缓存随之自动失效。</summary>
    public const string EncoderVersion = "e3";
    private const string ContentPrefix = "/Content/";
    private static readonly TimeSpan VersionTtl = TimeSpan.FromMinutes(5);

    private readonly string? _contentRoot;
    private readonly string? _cacheRoot;
    private readonly int _quality;
    private readonly int _previewQuality;
    private readonly ILogger<ImageVariants> _logger;
    private readonly ConcurrentDictionary<string, (string? Version, DateTime CheckedAt)> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _throttle = new(Math.Max(1, Environment.ProcessorCount / 2));

    public ImageVariants(
        IOptions<LegacyAssetsOptions> legacyAssets,
        IOptions<ImageVariantOptions> options,
        IHostEnvironment environment,
        ILogger<ImageVariants> logger)
    {
        _logger = logger;
        _quality = Math.Clamp(options.Value.Quality, 1, 100);
        _previewQuality = Math.Clamp(options.Value.PreviewQuality, 1, 100);
        var contentRoot = legacyAssets.Value.ContentRoot;
        var cacheRoot = options.Value.CacheRoot;
        if (string.IsNullOrWhiteSpace(cacheRoot) && environment.IsDevelopment())
        {
            cacheRoot = Path.Combine(environment.ContentRootPath, "App_Data", "image-cache");
        }

        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(contentRoot)
            || string.IsNullOrWhiteSpace(cacheRoot) || !Path.IsPathRooted(cacheRoot))
        {
            return;
        }

        _contentRoot = Path.GetFullPath(contentRoot);
        _cacheRoot = Path.GetFullPath(cacheRoot);
    }

    public bool Enabled => _contentRoot is not null && _cacheRoot is not null;

    public string Url(string originalUrl, int width)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(originalUrl) || !Widths.Contains(width))
        {
            return originalUrl;
        }

        var cut = originalUrl.IndexOfAny(new[] { '?', '#' });
        var urlPath = cut < 0 ? originalUrl : originalUrl[..cut];
        if (!urlPath.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        var encodedRelative = urlPath[ContentPrefix.Length..];
        var relative = Uri.UnescapeDataString(encodedRelative);
        if (TryResolveSource(relative) is not { } source)
        {
            return originalUrl;
        }

        var version = GetVersion(source);
        return version is null ? originalUrl : $"/_img/{width}/Content/{encodedRelative}?v={version}";
    }

    public string? SrcSet(string originalUrl, long minBytes = 40_000)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(originalUrl))
        {
            return null;
        }

        var cut = originalUrl.IndexOfAny(new[] { '?', '#' });
        var urlPath = cut < 0 ? originalUrl : originalUrl[..cut];
        if (!urlPath.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase)
            || urlPath.StartsWith("/Content/_preview/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (TryResolveSource(Uri.UnescapeDataString(urlPath[ContentPrefix.Length..])) is not { } source)
        {
            return null;
        }

        try
        {
            if (new FileInfo(source).Length < minBytes)
            {
                return null;
            }
        }
        catch (IOException)
        {
            return null;
        }

        return string.Join(", ", Widths.Select(width => $"{Url(urlPath, width)} {width}w"));
    }

    private static readonly System.Text.RegularExpressions.Regex ImgTag = new(
        "<img\\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex StyleAttribute = new(
        "\\sstyle\\s*=\\s*(\"[^\"]*\"|'[^']*')", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex AltAttribute = new(
        "\\salt\\s*=\\s*(\"([^\"]*)\"|'([^']*)')", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex SrcAttribute = new(
        "\\ssrc\\s*=\\s*([\"'])(/Content/[^\"']+)\\1", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    public string AddSrcSetToHtml(string? html, string sizes)
    {
        if (!Enabled || string.IsNullOrEmpty(html) || html.IndexOf("<img", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return html ?? string.Empty;
        }

        return ImgTag.Replace(html, match =>
        {
            var tag = match.Value;
            if (tag.Contains("srcset", StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }

            var src = SrcAttribute.Match(tag);
            if (!src.Success)
            {
                return tag;
            }

            var url = System.Net.WebUtility.HtmlDecode(src.Groups[2].Value);
            if (GifVideoTag(tag, url) is { } video)
            {
                return video;
            }

            if (SrcSet(url) is not { } srcset)
            {
                return tag;
            }

            var insert = $" srcset=\"{System.Net.WebUtility.HtmlEncode(srcset)}\" sizes=\"{sizes}\"";
            return tag.Insert(src.Index + src.Length, insert);
        });
    }

    /// <summary>
    /// GIF 动图换视频（2026-10-30）：公众号文章里的大 GIF 单张可达 6MB，全站 23 张大 GIF 合计 46.6MB。
    /// 在本机用 ffmpeg 转成同名旁挂文件 <c>xxx.gif.mp4</c>（H.264，静音）和首帧 <c>xxx.gif.jpg</c>，放在原 GIF 旁边，
    /// 体积约为原来的 1/6，画面、帧数、时长一致。有旁挂文件的 GIF 输出为自动播放、静音、循环、行内播放的 video，
    /// 看起来和 GIF 一样；没有旁挂文件（小装饰图、以后新同步的 GIF）原样输出 img。原 GIF 不改不删。
    /// </summary>
    private string? GifVideoTag(string imgTag, string url)
    {
        var cut = url.IndexOfAny(new[] { '?', '#' });
        var urlPath = cut < 0 ? url : url[..cut];
        if (!urlPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || !urlPath.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase)
            || TryResolveContentFile(Uri.UnescapeDataString(urlPath[ContentPrefix.Length..])) is not { } gif
            || GetVersion(gif + ".mp4") is not { } videoVersion)
        {
            return null;
        }

        var encodedVideo = System.Net.WebUtility.HtmlEncode($"{urlPath}.mp4?v={videoVersion}");
        var poster = GetVersion(gif + ".jpg") is { } posterVersion
            ? $" poster=\"{System.Net.WebUtility.HtmlEncode($"{urlPath}.jpg?v={posterVersion}")}\""
            : string.Empty;
        var style = StyleAttribute.Match(imgTag) is { Success: true } styleMatch ? " " + styleMatch.Value.Trim() : string.Empty;
        var alt = AltAttribute.Match(imgTag) is { Success: true } altMatch
            ? System.Net.WebUtility.HtmlDecode(altMatch.Groups[2].Value + altMatch.Groups[3].Value)
            : string.Empty;
        var label = alt.Length > 0
            ? $" aria-label=\"{System.Net.WebUtility.HtmlEncode(alt)}\""
            : string.Empty;
        return $"<video class=\"gif-video\" src=\"{encodedVideo}\"{poster} autoplay muted loop playsinline preload=\"auto\"{label}{style}></video>";
    }

    public async Task<string?> GetOrCreateAsync(int width, string relativeContentPath, CancellationToken cancellationToken)
    {
        if (!Enabled || !Widths.Contains(width) || TryResolveSource(relativeContentPath) is not { } source)
        {
            return null;
        }

        var target = Path.GetFullPath(Path.Combine(_cacheRoot!, EncoderVersion, width.ToString(), relativeContentPath + ".webp"));
        if (!target.StartsWith(_cacheRoot! + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        if (IsFresh(target, source))
        {
            return target;
        }

        var gate = _locks.GetOrAdd(target, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh(target, source))
            {
                return target;
            }

            await _throttle.WaitAsync(cancellationToken);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                if (!Encode(source, width, width <= 960 ? _previewQuality : _quality, temp))
                {
                    TryDelete(temp);
                    return null;
                }

                File.Move(temp, target, overwrite: true);
                return target;
            }
            finally
            {
                _throttle.Release();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("图片变体写入失败，回退原图：{Type}", exception.GetType().Name);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>解码原图（按需降采样、修正 EXIF 方向）→ 缩到「宽 ≤ width、高 ≤ 1.5·width」→ 编码 WebP。不放大小图。</summary>
    public static bool Encode(string sourcePath, int width, int quality, string outputPath)
    {
        using var codec = SKCodec.Create(sourcePath);
        if (codec is null)
        {
            return false;
        }

        var origin = codec.EncodedOrigin;
        var rotated = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var raw = codec.Info;
        var displayWidth = rotated ? raw.Height : raw.Width;
        var displayHeight = rotated ? raw.Width : raw.Height;
        // 限制在「宽 width × 高 1.5·width」的框内：竖长构图的作品图只按宽度缩会高达数千像素、体积居高不下。
        var scale = Math.Min(1f, Math.Min(width / (float)displayWidth, width * 1.5f / displayHeight));
        var targetW = Math.Max(1, (int)Math.Round(raw.Width * scale));
        var targetH = Math.Max(1, (int)Math.Round(raw.Height * scale));

        // JPEG 支持按 1/2、1/4、1/8 直接降采样解码，大图省内存省时间；解出的尺寸不小于目标。
        var decodeSize = codec.GetScaledDimensions(scale);
        if (decodeSize.Width < targetW || decodeSize.Height < targetH)
        {
            decodeSize = new SKSizeI(raw.Width, raw.Height);
        }

        using var decoded = new SKBitmap(new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            return false;
        }

        using var resized = decoded.Width == targetW && decoded.Height == targetH
            ? decoded.Copy()
            : decoded.Resize(new SKImageInfo(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized is null)
        {
            return false;
        }

        using var oriented = ApplyOrigin(resized, origin);
        using var image = SKImage.FromBitmap(oriented);
        using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
        if (data is null)
        {
            return false;
        }

        using var stream = File.Create(outputPath);
        data.SaveTo(stream);
        return true;
    }

    private static SKBitmap ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        // 只处理常见的旋转方向；镜像类（极少见）按原样输出。
        var (degrees, swap) = origin switch
        {
            SKEncodedOrigin.RightTop => (90, true),
            SKEncodedOrigin.BottomRight => (180, false),
            SKEncodedOrigin.LeftBottom => (270, true),
            _ => (0, false),
        };
        if (degrees == 0)
        {
            return bitmap.Copy();
        }

        var output = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
        using var canvas = new SKCanvas(output);
        canvas.Translate(output.Width / 2f, output.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
        canvas.DrawBitmap(bitmap, 0, 0);
        return output;
    }

    private string? TryResolveSource(string relative)
    {
        var extension = Path.GetExtension(relative);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? TryResolveContentFile(relative)
            : null;
    }

    private string? TryResolveContentFile(string relative)
    {
        if (_contentRoot is null || string.IsNullOrWhiteSpace(relative) || relative.Contains('\\')
            || relative.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(_contentRoot, relative));
        return full.StartsWith(_contentRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(full)
            ? full
            : null;
    }

    private string? GetVersion(string source)
    {
        var now = DateTime.UtcNow;
        if (_versions.TryGetValue(source, out var cached) && now - cached.CheckedAt < VersionTtl)
        {
            return cached.Version;
        }

        string? version = null;
        try
        {
            var info = new FileInfo(source);
            if (info.Exists)
            {
                version = EncoderVersion + ((info.LastWriteTimeUtc.Ticks ^ info.Length) & 0xFFFFFFFFFFFF).ToString("x");
            }
        }
        catch (IOException)
        {
        }

        _versions[source] = (version, now);
        return version;
    }

    private static bool IsFresh(string target, string source) =>
        File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
