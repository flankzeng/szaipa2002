using Szaipa.Web.Configuration;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// Resolves the security-monitoring file roots without touching the file system. Production-like
/// environments fail closed when the server security root would be missing, relative, or tied to an
/// immutable release — the same contract as <see cref="DataProtectionKeyPathResolver"/>.
/// </summary>
public static class SecurityMonitoringPathResolver
{
    private static readonly string DevelopmentSampleFallback = Path.Combine("SecuritySampleData");

    private static readonly string DevelopmentAuditFallback = Path.Combine(
        "App_Data",
        "security",
        "audit");

    public static SecurityMonitoringPaths Resolve(
        string contentRoot,
        SecurityMonitoringOptions options,
        bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            throw new ArgumentException("The application content root is required.", nameof(contentRoot));
        }

        var normalizedContentRoot = Path.GetFullPath(contentRoot);

        var serverSecurityRoot = ResolveServerSecurityRoot(normalizedContentRoot, options, isDevelopment);
        var auditDirectory = ResolveAuditDirectory(normalizedContentRoot, options, isDevelopment, serverSecurityRoot);
        var unblockQueue = ResolveQueueDirectory(
            normalizedContentRoot,
            options.UnblockQueueDirectory,
            serverSecurityRoot,
            "unblock_queue",
            isDevelopment);
        var whitelistQueue = ResolveQueueDirectory(
            normalizedContentRoot,
            options.WhitelistEditQueueDirectory,
            serverSecurityRoot,
            "whitelist_edit_queue",
            isDevelopment);

        return new SecurityMonitoringPaths(
            serverSecurityRoot,
            auditDirectory,
            unblockQueue,
            whitelistQueue,
            options.RequestAuditEnabled,
            options.AuditKeepDays,
            options.AuditMaxLineLength,
            options.AuditSkipPaths ?? Array.Empty<string>());
    }

    private static string ResolveServerSecurityRoot(
        string normalizedContentRoot,
        SecurityMonitoringOptions options,
        bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(options.ServerSecurityRoot))
        {
            if (isDevelopment)
            {
                var sample = Path.GetFullPath(Path.Combine(normalizedContentRoot, DevelopmentSampleFallback));
                // Prefer the committed sample data when present so the dashboard renders out of the box;
                // otherwise fall back to a writable App_Data location.
                return Directory.Exists(sample)
                    ? sample
                    : Path.GetFullPath(Path.Combine(normalizedContentRoot, "App_Data", "security"));
            }

            throw new InvalidOperationException(
                "SecurityMonitoring:ServerSecurityRoot is required outside Development and must point to "
                + "the persistent C:\\Security directory (outside the application release root).");
        }

        return ResolveAbsolutePath(
            normalizedContentRoot,
            options.ServerSecurityRoot,
            isDevelopment,
            "SecurityMonitoring:ServerSecurityRoot");
    }

    private static string ResolveAuditDirectory(
        string normalizedContentRoot,
        SecurityMonitoringOptions options,
        bool isDevelopment,
        string serverSecurityRoot)
    {
        if (!string.IsNullOrWhiteSpace(options.AuditDirectory))
        {
            return ResolveAbsolutePath(
                normalizedContentRoot,
                options.AuditDirectory,
                isDevelopment,
                "SecurityMonitoring:AuditDirectory");
        }

        // Development keeps the rolling logs out of the (possibly committed) sample tree and the release
        // root: default to App_Data/security/audit. Production derives the audit dir from the server root.
        if (isDevelopment)
        {
            return Path.GetFullPath(Path.Combine(normalizedContentRoot, DevelopmentAuditFallback));
        }

        if (!string.IsNullOrWhiteSpace(serverSecurityRoot))
        {
            return Path.Combine(serverSecurityRoot, "audit");
        }

        throw new InvalidOperationException(
            "SecurityMonitoring:AuditDirectory is required outside Development when ServerSecurityRoot is unset.");
    }

    private static string ResolveQueueDirectory(
        string normalizedContentRoot,
        string configured,
        string serverSecurityRoot,
        string fallbackName,
        bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ResolveAbsolutePath(
                normalizedContentRoot,
                configured,
                isDevelopment,
                $"SecurityMonitoring:{(fallbackName == "unblock_queue" ? "UnblockQueueDirectory" : "WhitelistEditQueueDirectory")}");
        }

        return string.IsNullOrWhiteSpace(serverSecurityRoot)
            ? throw new InvalidOperationException(
                $"SecurityMonitoring:ServerSecurityRoot is required to derive the {fallbackName} queue directory.")
            : Path.Combine(serverSecurityRoot, fallbackName);
    }

    private static string ResolveAbsolutePath(
        string normalizedContentRoot,
        string configured,
        bool isDevelopment,
        string settingName)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configured.Trim());
        if (!Path.IsPathRooted(expanded))
        {
            if (isDevelopment)
            {
                return Path.GetFullPath(Path.Combine(normalizedContentRoot, expanded));
            }

            throw new InvalidOperationException(
                $"{settingName} must be an absolute path outside Development.");
        }

        var normalized = Path.GetFullPath(expanded);
        if (!isDevelopment && IsWithin(normalized, normalizedContentRoot))
        {
            throw new InvalidOperationException(
                $"{settingName} must be outside the application release root so it survives atomic release switches.");
        }

        return normalized;
    }

    private static bool IsWithin(string candidatePath, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        if (relativePath == ".")
        {
            return true;
        }

        if (Path.IsPathRooted(relativePath) || relativePath == "..")
        {
            return false;
        }

        return !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
