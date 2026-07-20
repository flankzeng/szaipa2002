using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Szaipa.Web.Services;

/// <summary>
/// Reads the application-owned derived-image manifest once and resolves only exact, safe allowlist entries.
/// Manifest failures never prevent a valid legacy image from being rendered.
/// </summary>
public sealed class DerivedImageResolver : IDerivedImageResolver
{
    private const int SupportedSchemaVersion = 1;
    private const string ContentPrefix = "/Content/";
    private const string DerivedUrlPrefix = "/media/derived/";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyDictionary<string, string> _avifByOriginalPath;

    public DerivedImageResolver(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _avifByOriginalPath = LoadManifest(environment.WebRootPath);
    }

    public DerivedImageSource? Resolve(string? originalUrl)
    {
        if (!TryNormalizeOriginalUrl(originalUrl, allowSuffix: true, out var lookupPath))
        {
            return null;
        }

        _avifByOriginalPath.TryGetValue(lookupPath, out var avifUrl);
        return new DerivedImageSource(originalUrl!, avifUrl);
    }

    private static IReadOnlyDictionary<string, string> LoadManifest(string? webRootPath)
    {
        if (!TryResolveWebRoot(webRootPath, out var webRoot, out var derivedRoot))
        {
            return EmptyManifest();
        }

        ManifestDocument? document;
        try
        {
            var manifestPath = Path.Combine(derivedRoot, "manifest.json");
            using var stream = File.OpenRead(manifestPath);
            document = JsonSerializer.Deserialize<ManifestDocument>(stream, ManifestJsonOptions);
        }
        catch (Exception exception) when (IsManifestException(exception))
        {
            return EmptyManifest();
        }

        if (document is null || document.SchemaVersion != SupportedSchemaVersion)
        {
            return EmptyManifest();
        }

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in document.Images ?? [])
        {
            if (entry is null
                || !TryNormalizeOriginalUrl(entry.Original, allowSuffix: false, out var originalPath)
                || !TryResolveDerivedUrl(entry.Avif, webRoot, derivedRoot, out var avifUrl)
                || ambiguous.Contains(originalPath))
            {
                continue;
            }

            // Duplicate semantic source paths make the allowlist ambiguous, even when their casing differs.
            if (resolved.ContainsKey(originalPath))
            {
                resolved.Remove(originalPath);
                ambiguous.Add(originalPath);
                continue;
            }

            resolved.Add(originalPath, avifUrl);
        }

        return resolved;
    }

    private static bool TryResolveWebRoot(
        string? configuredWebRoot,
        out string webRoot,
        out string derivedRoot)
    {
        webRoot = string.Empty;
        derivedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredWebRoot))
        {
            return false;
        }

        try
        {
            webRoot = Path.GetFullPath(configuredWebRoot);
            derivedRoot = Path.GetFullPath(Path.Combine(webRoot, "media", "derived"));
            return IsPathWithin(derivedRoot, webRoot);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            webRoot = string.Empty;
            derivedRoot = string.Empty;
            return false;
        }
    }

    private static bool TryResolveDerivedUrl(
        string? candidateUrl,
        string webRoot,
        string derivedRoot,
        out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(candidateUrl) || candidateUrl != candidateUrl.Trim())
        {
            return false;
        }

        var fragmentIndex = candidateUrl.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            return false;
        }

        var queryIndex = candidateUrl.IndexOf('?');
        var urlPath = queryIndex < 0 ? candidateUrl : candidateUrl[..queryIndex];
        var versionSuffix = queryIndex < 0 ? string.Empty : candidateUrl[queryIndex..];
        if (!IsSafeVersionSuffix(versionSuffix)
            || !urlPath.StartsWith(DerivedUrlPrefix, StringComparison.OrdinalIgnoreCase)
            || !TryNormalizeSegments(urlPath[DerivedUrlPrefix.Length..], out var relativePath, out var encodedPath)
            || !Path.GetExtension(relativePath).Equals(".avif", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string physicalPath;
        try
        {
            physicalPath = Path.GetFullPath(Path.Combine(
                derivedRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return false;
        }

        if (!IsPathWithin(physicalPath, derivedRoot)
            || !IsPathWithin(physicalPath, webRoot)
            || !IsRegularNonReparseFile(physicalPath)
            || HasReparsePointBetween(physicalPath, derivedRoot))
        {
            return false;
        }

        normalizedUrl = DerivedUrlPrefix + encodedPath + versionSuffix;
        return true;
    }

    private static bool TryNormalizeOriginalUrl(
        string? candidateUrl,
        bool allowSuffix,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidateUrl) || candidateUrl != candidateUrl.Trim())
        {
            return false;
        }

        var suffixIndex = FindSuffixIndex(candidateUrl);
        if (!allowSuffix && suffixIndex >= 0)
        {
            return false;
        }

        var urlPath = suffixIndex < 0 ? candidateUrl : candidateUrl[..suffixIndex];
        var suffix = suffixIndex < 0 ? string.Empty : candidateUrl[suffixIndex..];
        if (!IsSafeOriginalSuffix(suffix)
            || !urlPath.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase)
            || !TryNormalizeSegments(urlPath[ContentPrefix.Length..], out var relativePath, out _))
        {
            return false;
        }

        var extension = Path.GetExtension(relativePath);
        if (!IsSupportedOriginalExtension(extension))
        {
            return false;
        }

        normalizedPath = ContentPrefix + relativePath;
        return true;
    }

    private static bool TryNormalizeSegments(
        string encodedRelativePath,
        out string decodedRelativePath,
        out string encodedNormalizedPath)
    {
        decodedRelativePath = string.Empty;
        encodedNormalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(encodedRelativePath))
        {
            return false;
        }

        var sourceSegments = encodedRelativePath.Split('/');
        var decodedSegments = new string[sourceSegments.Length];
        var encodedSegments = new string[sourceSegments.Length];

        for (var index = 0; index < sourceSegments.Length; index++)
        {
            var segment = sourceSegments[index];
            try
            {
                // Decode a bounded number of layers so double-encoded separators/traversal cannot pass validation.
                for (var pass = 0; pass < 3; pass++)
                {
                    var next = Uri.UnescapeDataString(segment);
                    if (next == segment)
                    {
                        break;
                    }

                    segment = next;
                }
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(segment)
                || segment is "." or ".."
                || segment.Contains('/')
                || segment.Contains('\\')
                || segment.Contains('%')
                || segment.Contains('?')
                || segment.Contains('#')
                || segment.IndexOf('\0') >= 0
                || segment.Any(char.IsControl))
            {
                return false;
            }

            decodedSegments[index] = segment;
            encodedSegments[index] = Uri.EscapeDataString(segment);
        }

        decodedRelativePath = string.Join('/', decodedSegments);
        encodedNormalizedPath = string.Join('/', encodedSegments);
        return true;
    }

    private static bool IsSafeOriginalSuffix(string suffix)
    {
        if (suffix.Length == 0)
        {
            return true;
        }

        if (suffix[0] is not ('?' or '#'))
        {
            return false;
        }

        return !suffix.Any(character =>
            char.IsControl(character)
            || char.IsWhiteSpace(character)
            || character is '\\' or '"' or '\'');
    }

    private static bool IsSafeVersionSuffix(string suffix)
    {
        const string prefix = "?v=";
        if (suffix.Length == 0)
        {
            return true;
        }

        if (!suffix.StartsWith(prefix, StringComparison.Ordinal) || suffix.Length == prefix.Length)
        {
            return false;
        }

        return suffix[prefix.Length..].All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.' or '~');
    }

    private static bool IsRegularNonReparseFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return !attributes.HasFlag(FileAttributes.Directory)
                && !attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool HasReparsePointBetween(string filePath, string rootPath)
    {
        try
        {
            var root = new DirectoryInfo(rootPath);
            if (!root.Exists || root.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }

            for (var directory = new FileInfo(filePath).Directory;
                 directory is not null && IsPathWithin(directory.FullName, rootPath);
                 directory = directory.Parent)
            {
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }

                if (PathEquals(directory.FullName, rootPath))
                {
                    break;
                }
            }

            return false;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return true;
        }
    }

    private static bool IsPathWithin(string candidatePath, string rootPath)
    {
        try
        {
            var relative = Path.GetRelativePath(rootPath, candidatePath);
            return relative != ".."
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !Path.IsPathRooted(relative);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static int FindSuffixIndex(string url)
    {
        var queryIndex = url.IndexOf('?');
        var fragmentIndex = url.IndexOf('#');
        if (queryIndex < 0)
        {
            return fragmentIndex;
        }

        return fragmentIndex < 0 ? queryIndex : Math.Min(queryIndex, fragmentIndex);
    }

    private static bool IsSupportedOriginalExtension(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    private static bool IsManifestException(Exception exception) =>
        exception is JsonException || IsFileSystemException(exception);

    private static bool IsFileSystemException(Exception exception) =>
        exception is ArgumentException or IOException or NotSupportedException
            or UnauthorizedAccessException or System.Security.SecurityException;

    private static IReadOnlyDictionary<string, string> EmptyManifest() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed class ManifestDocument
    {
        public int SchemaVersion { get; set; }

        public List<ManifestEntry?>? Images { get; set; }
    }

    private sealed class ManifestEntry
    {
        public string? Original { get; set; }

        public string? Avif { get; set; }
    }
}
