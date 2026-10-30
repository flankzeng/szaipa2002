using System.Security.Cryptography;
using System.Text;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// 后台账号密码的存储与校验（2026-10-30 升级）。
/// <list type="bullet">
/// <item>新格式 <c>p1$盐$摘要</c>：PBKDF2-SHA256，<see cref="Iterations"/> 次迭代，每个账号独立随机盐。
///   长度固定 44 字符，放得进旧表 <c>Staff.Password nvarchar(50)</c>，不用改表结构；</item>
/// <item>旧格式：旧站 <c>Get_MD5(password, "utf-8")</c> 的 32 位小写 MD5。仍可登录，
///   登录成功后由调用方用 <see cref="Hash"/> 换成新格式（<see cref="StaffPasswordCheck.ValidNeedsUpgrade"/>），
///   所以大家正常登录一次就自动升级，不需要重设密码。</item>
/// </list>
/// 注意：升级过的账号无法再被 2026-10-30 之前的版本（只认 MD5）校验，回滚到旧版本前要考虑这一点。
/// </summary>
public static class StaffPasswordHasher
{
    public const string Prefix = "p1$";

    public const int Iterations = 600_000;

    private const int SaltBytes = 12;

    private const int HashBytes = 18;

    // 账号不存在时也跑一遍同样耗时的校验，避免靠响应时间猜出哪些用户名存在。
    private static readonly string DummyHash = Hash(Guid.NewGuid().ToString("N"));

    public static string Md5Hex(string password)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>生成新格式哈希（每次随机盐，同一密码两次结果不同）。</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        return Prefix + Base64Url(salt) + "$" + Base64Url(Derive(password, salt));
    }

    public static StaffPasswordCheck Check(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            Check(password, DummyHash);
            return StaffPasswordCheck.Invalid;
        }

        if (storedHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var parts = storedHash[Prefix.Length..].Split('$');
            if (parts.Length != 2 || !TryFromBase64Url(parts[0], out var salt) || !TryFromBase64Url(parts[1], out var expected))
            {
                return StaffPasswordCheck.Invalid;
            }

            return CryptographicOperations.FixedTimeEquals(Derive(password, salt), expected)
                ? StaffPasswordCheck.Valid
                : StaffPasswordCheck.Invalid;
        }

        var legacy = Encoding.ASCII.GetBytes(Md5Hex(password));
        var stored = Encoding.ASCII.GetBytes(storedHash.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(legacy, stored)
            ? StaffPasswordCheck.ValidNeedsUpgrade
            : StaffPasswordCheck.Invalid;
    }

    public static bool Verify(string password, string? storedHash) =>
        Check(password, storedHash) != StaffPasswordCheck.Invalid;

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryFromBase64Url(string text, out byte[] bytes)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        bytes = new byte[base64.Length];
        if (Convert.TryFromBase64String(base64, bytes, out var written))
        {
            bytes = bytes[..written];
            return true;
        }

        bytes = [];
        return false;
    }
}

public enum StaffPasswordCheck
{
    Invalid,
    Valid,

    /// <summary>密码正确，但还是旧站 MD5 格式，应当换成新格式保存。</summary>
    ValidNeedsUpgrade
}
