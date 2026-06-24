using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Locks in login parity with the legacy <c>Get_MD5(password, "utf-8")</c>: a lowercase 32-char hex MD5 of
/// the UTF-8 bytes. If this drifts, existing staff accounts can no longer sign in.
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
    public void Verify_is_true_for_matching_password_and_false_otherwise()
    {
        var stored = StaffPasswordHasher.Md5Hex("s3cret");

        Assert.True(StaffPasswordHasher.Verify("s3cret", stored));
        Assert.False(StaffPasswordHasher.Verify("wrong", stored));
        Assert.False(StaffPasswordHasher.Verify("s3cret", null));
    }
}
