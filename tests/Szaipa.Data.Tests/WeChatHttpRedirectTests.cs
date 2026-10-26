using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Szaipa.Data.DependencyInjection;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class WeChatHttpRedirectTests
{
    [Theory]
    [InlineData(WeChatSyncService.PageClientName)]
    [InlineData(WeChatImageLocalizer.ClientName)]
    public void Content_fetch_clients_disable_automatic_redirects(string clientName)
    {
        var services = new ServiceCollection();
        services.AddSzaipaData(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        using var createdHandler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
        HttpMessageHandler handler = createdHandler;
        while (handler is DelegatingHandler delegating)
        {
            handler = GetInnerHandler(delegating);
        }

        Assert.False(Assert.IsType<HttpClientHandler>(handler).AllowAutoRedirect);
    }

    private static HttpMessageHandler GetInnerHandler(DelegatingHandler handler) =>
        handler.InnerHandler ?? throw new InvalidOperationException("命名 HTTP 客户端没有内部 handler。");
}
