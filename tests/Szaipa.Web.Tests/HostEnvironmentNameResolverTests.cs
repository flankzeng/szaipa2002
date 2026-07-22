using Microsoft.Extensions.Hosting;
using Szaipa.Web.Infrastructure;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class HostEnvironmentNameResolverTests
{
    [Fact]
    public void MissingEnvironmentDefaultsToProduction()
    {
        var result = HostEnvironmentNameResolver.Resolve([], null, null);

        Assert.Equal(Environments.Production, result);
    }

    [Theory]
    [InlineData("Development", null, "Development")]
    [InlineData(null, "Staging", "Staging")]
    [InlineData(" production ", "Production", "production")]
    public void EnvironmentVariablesResolveOneCanonicalName(
        string? aspNetCoreEnvironment,
        string? dotNetEnvironment,
        string expected)
    {
        var result = HostEnvironmentNameResolver.Resolve(
            [],
            aspNetCoreEnvironment,
            dotNetEnvironment);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConflictingEnvironmentVariablesFailClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HostEnvironmentNameResolver.Resolve(
                [],
                aspNetCoreEnvironment: "Development",
                dotNetEnvironment: "Production"));

        Assert.Contains("conflict", exception.Message);
    }

    [Theory]
    [InlineData("--environment=Development")]
    [InlineData("--environment", "Development")]
    public void CommandLineEnvironmentIsAuthoritative(params string[] args)
    {
        var result = HostEnvironmentNameResolver.Resolve(
            args,
            aspNetCoreEnvironment: "Production",
            dotNetEnvironment: "Staging");

        Assert.Equal("Development", result);
    }

    [Theory]
    [InlineData("../Development")]
    [InlineData("Development/../Production")]
    [InlineData("Production:Backup")]
    [InlineData("测试")]
    public void UnsafeEnvironmentNameIsRejected(string environmentName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HostEnvironmentNameResolver.Resolve(
                [$"--environment={environmentName}"],
                null,
                null));

        Assert.Contains("may contain only", exception.Message);
    }
}
