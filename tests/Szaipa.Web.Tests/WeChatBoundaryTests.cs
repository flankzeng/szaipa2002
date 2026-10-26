using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;
using Szaipa.Web.Configuration;
using Szaipa.Web.Controllers;
using Szaipa.Web.Services.Admin;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class WeChatBoundaryTests
{
    [Fact]
    public async Task Image_store_writes_only_under_content_root_and_rejects_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "wechat-image-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new WeChatImageStore(Options.Create(new LegacyAssetsOptions { ContentRoot = root }));
            Assert.True(store.IsAvailable);
            Assert.Equal("/Content/WeChat/2025/10/a1.png", await store.SaveAsync("WeChat/2025/10", "a1.png", [1, 2, 3], CancellationToken.None));
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(root, "WeChat", "2025", "10", "a1.png")));
            Assert.Equal("/Content/WeChat/2025/10/a1.png", store.TryGetExistingUrl("WeChat/2025/10", "a1.png"));
            Assert.Null(await store.SaveAsync("../outside", "bad.png", [4], CancellationToken.None));
            Assert.Null(await store.SaveAsync("WeChat/2025/10", "../bad.png", [4], CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(root, "outside", "bad.png")));

            var blockedPath = Path.Combine(root, "WeChat", "2025", "10", "blocked.png");
            Directory.CreateDirectory(blockedPath); // 目标路径是目录：模拟 rename/I/O 失败，不能报告虚假成功。
            Assert.Null(await store.SaveAsync("WeChat/2025/10", "blocked.png", [5], CancellationToken.None));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(blockedPath)!, "*.tmp"));

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync("WeChat/2025/10", "canceled.png", [6], canceled.Token));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(blockedPath)!, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Callback_is_closed_by_default_and_validates_signature_before_queueing()
    {
        var queue = new WeChatImportQueue();
        var status = new WeChatSourceStatusStore();
        var disabled = Controller(new WeChatOptions(), queue, status);
        Assert.IsType<NotFoundResult>(disabled.Verify("x", "y", "z", "echo"));

        var options = new WeChatOptions { Callback = { Enabled = true, Token = "local-token" } };
        var controller = Controller(options, queue, status);
        Assert.IsType<StatusCodeResult>(controller.Verify("bad", "1", "2", "echo"));
        var signature = Signature(options.Callback.Token, "1", "2");
        Assert.Equal("echo", Assert.IsType<ContentResult>(controller.Verify(signature, "1", "2", "echo")).Content);

        var body = "<xml><MsgType>event</MsgType><Event>MASSSENDJOBFINISH</Event><ArticleUrlResult><ArticleUrl>https://mp.weixin.qq.com/s/ShortCode</ArticleUrl></ArticleUrlResult></xml>";
        controller = Controller(options, queue, status);
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await controller.Receive("bad", "1", "2", CancellationToken.None)).StatusCode);

        controller = Controller(options, queue, status);
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
        var response = Assert.IsType<ContentResult>(await controller.Receive(signature, "1", "2", CancellationToken.None));
        Assert.Equal("success", response.Content);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var queued = new List<string>();
        try
        {
            await foreach (var url in queue.ReadAllAsync(cancellation.Token))
            {
                queued.Add(url);
            }
        }
        catch (OperationCanceledException) { }
        Assert.Equal(new[] { "https://mp.weixin.qq.com/s/ShortCode" }, queued);
    }

    private static WeChatCallbackController Controller(WeChatOptions options, WeChatImportQueue queue, IWeChatSourceStatusStore status)
    {
        var controller = new WeChatCallbackController(Options.Create(options), queue, status)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.ControllerContext.RouteData = new RouteData();
        return controller;
    }

    private static string Signature(string token, string timestamp, string nonce)
    {
        var values = new[] { token, timestamp, nonce };
        Array.Sort(values, StringComparer.Ordinal);
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(string.Concat(values)))).ToLowerInvariant();
    }
}
