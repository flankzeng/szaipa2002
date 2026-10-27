using Xunit;

namespace Szaipa.Web.Tests;

/// <summary>
/// IIS 进程内托管契约：发布的 web.config 用 hostingModel="inprocess"，宿主必须接入 IIS 服务器。
/// 2026-10-27 服务器实测：只有 UseKestrel 时 IIS 请求进不来，启动超时 500.30。
/// </summary>
public sealed class IisHostingContractTests
{
    [Fact]
    public void Program_wires_iis_server_after_kestrel()
    {
        var program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web", "Program.cs"));

        var kestrel = program.IndexOf(".UseKestrel()", StringComparison.Ordinal);
        var iis = program.IndexOf(".UseIIS()", StringComparison.Ordinal);
        Assert.True(kestrel >= 0, "Program.cs 应调用 UseKestrel()（本地 dotnet run）。");
        Assert.True(iis > kestrel, "Program.cs 必须在 UseKestrel() 之后调用 UseIIS()，否则 IIS 进程内托管收不到请求。");
        Assert.Contains(".UseIISIntegration()", program, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "src", "Szaipa.Web")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
