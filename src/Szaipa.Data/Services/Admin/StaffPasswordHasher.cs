using System.Security.Cryptography;
using System.Text;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Reproduces the legacy <c>StaffController.Get_MD5(password, "utf-8")</c> hashing so existing staff
/// accounts can authenticate against the migrated backend unchanged: a lowercase 32-character hex MD5
/// digest of the UTF-8 password bytes. MD5 is weak, but parity with stored hashes is required first;
/// upgrading the scheme is a deliberate later step.
/// </summary>
public static class StaffPasswordHasher
{
    public static string Md5Hex(string password)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool Verify(string password, string? storedHash) =>
        !string.IsNullOrEmpty(storedHash)
        && string.Equals(Md5Hex(password), storedHash, StringComparison.OrdinalIgnoreCase);
}
