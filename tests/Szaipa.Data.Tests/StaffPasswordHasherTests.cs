using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Locks in login parity with the legacy <c>Get_MD5(password, "utf-8")</c> (a lowercase 32-char hex MD5 of the
/// UTF-8 bytes, still accepted and flagged for upgrade) and the salted PBKDF2 <c>p1$</c> format that replaces it.
/// </summary>
public sealed class StaffPasswordHasherTests
{
    [Theory]
    [InlineData("admin", "21232f297a57a5a743894a0e4a801fc3")]
    [InlineData("123456", "e10adc3949ba59abbe56e057f20f883e")]
    [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
    public void Md5Hex_matches_legacy_lowercase_hex(string password, string expected)
    {
        Assert.Equal(expected, StaffPasswordHasher.Md5Hex(password));
    }

    [Fact]
    public void Legacy_md5_hash_is_accepted_and_flagged_for_upgrade()
    {
        var stored = StaffPasswordHasher.Md5Hex("s3cret");

        Assert.Equal(StaffPasswordCheck.ValidNeedsUpgrade, StaffPasswordHasher.Check("s3cret", stored));
        Assert.Equal(StaffPasswordCheck.ValidNeedsUpgrade, StaffPasswordHasher.Check("s3cret", stored.ToUpperInvariant()));
        Assert.Equal(StaffPasswordCheck.Invalid, StaffPasswordHasher.Check("wrong", stored));
        Assert.False(StaffPasswordHasher.Verify("s3cret", null));
        Assert.False(StaffPasswordHasher.Verify("s3cret", ""));
    }

    [Fact]
    public void New_hash_fits_the_legacy_column_and_verifies()
    {
        var stored = StaffPasswordHasher.Hash("密码 s3cret");

        Assert.StartsWith(StaffPasswordHasher.Prefix, stored);
        Assert.True(stored.Length <= 50, $"Staff.Password is nvarchar(50); got {stored.Length}");
        Assert.Equal(StaffPasswordCheck.Valid, StaffPasswordHasher.Check("密码 s3cret", stored));
        Assert.Equal(StaffPasswordCheck.Invalid, StaffPasswordHasher.Check("密码 s3cre", stored));
    }

    [Fact]
    public void New_hash_uses_a_fresh_salt_each_time()
    {
        Assert.NotEqual(StaffPasswordHasher.Hash("same"), StaffPasswordHasher.Hash("same"));
    }

    [Theory]
    [InlineData("p1$")]
    [InlineData("p1$abc")]
    [InlineData("p1$!!!$???")]
    public void Malformed_new_hash_is_rejected(string stored)
    {
        Assert.Equal(StaffPasswordCheck.Invalid, StaffPasswordHasher.Check("anything", stored));
    }
}
