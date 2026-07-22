using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Szaipa.Web.Infrastructure;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class StaffAuthenticationCookiePolicyTests
{
    [Theory]
    [InlineData("Production", "Szaipa.Admin", CookieSecurePolicy.Always)]
    [InlineData("Staging", "Szaipa.Admin.Staging", CookieSecurePolicy.Always)]
    [InlineData("Development", "Szaipa.Admin.Development", CookieSecurePolicy.SameAsRequest)]
    public void EnvironmentGetsIsolatedCookieNameAndTransportPolicy(
        string environmentName,
        string expectedName,
        CookieSecurePolicy expectedSecurePolicy)
    {
        var result = StaffAuthenticationCookiePolicy.Resolve(environmentName);

        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expectedSecurePolicy, result.SecurePolicy);
    }
}
