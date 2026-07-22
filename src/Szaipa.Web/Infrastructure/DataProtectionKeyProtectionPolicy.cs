namespace Szaipa.Web.Infrastructure;

/// <summary>
/// Keeps the explicit file-system key ring encrypted at rest in production. The supported production
/// target is Windows IIS, where machine-scoped DPAPI survives app-pool/release switches on one server.
/// </summary>
public static class DataProtectionKeyProtectionPolicy
{
    public static bool ShouldUseMachineDpapi(bool isDevelopment, bool isWindows)
    {
        if (isDevelopment)
        {
            return false;
        }

        if (!isWindows)
        {
            throw new PlatformNotSupportedException(
                "Non-Development deployments require Windows DPAPI protection for the persistent Data Protection key ring.");
        }

        return true;
    }
}
