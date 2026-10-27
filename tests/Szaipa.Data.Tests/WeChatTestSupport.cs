using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Szaipa.Data.Services.WeChat;

namespace Szaipa.Data.Tests;

/// <summary>公众号同步测试共用的假 HTTP / 假图片存储 / fixture 读取。测试不得访问外网。</summary>
internal static class WeChatTestSupport
{
    /// <summary>最小的合法 PNG（1x1），足够通过魔数判型。</summary>
    public static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), Encoding.UTF8);

    /// <summary>同时满足 <see cref="IOptions{TOptions}"/> 与 <see cref="IOptionsMonitor{TOptions}"/> 的固定值配置。</summary>
    public static TestOptions<T> Opts<T>(T value) where T : class => new(value);
}

/// <summary>测试用配置：<see cref="Set"/> 模拟后台设置页保存后的配置变化（触发 OnChange）。</summary>
internal sealed class TestOptions<T> : IOptions<T>, IOptionsMonitor<T> where T : class
{
    private readonly List<Action<T, string?>> _listeners = new();

    public TestOptions(T value) => CurrentValue = value;

    public T Value => CurrentValue;

    public T CurrentValue { get; private set; }

    public T Get(string? name) => CurrentValue;

    public void Set(T value)
    {
        CurrentValue = value;
        Action<T, string?>[] listeners;
        lock (_listeners)
        {
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            listener(value, Options.DefaultName);
        }
    }

    public IDisposable OnChange(Action<T, string?> listener)
    {
        lock (_listeners)
        {
            _listeners.Add(listener);
        }

        return new Subscription(() =>
        {
            lock (_listeners)
            {
                _listeners.Remove(listener);
            }
        });
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<string> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request.Method + " " + request.RequestUri);
        }

        return Task.FromResult(_respond(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    public static HttpResponseMessage Bytes(byte[] data) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

internal sealed class FakeImageStore : IWeChatImageStore
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public bool IsAvailable { get; set; } = true;

    // 正文图并发下载（2026-10-28）：读写都加锁，和真实存储一样可被多线程调用。
    public string? TryGetExistingUrl(string subfolder, string fileName)
    {
        lock (Files)
        {
            return Files.ContainsKey($"{subfolder}/{fileName}") ? $"/Content/{subfolder}/{fileName}" : null;
        }
    }

    public Task<string?> SaveAsync(string subfolder, string fileName, byte[] data, CancellationToken cancellationToken)
    {
        lock (Files)
        {
            Files[$"{subfolder}/{fileName}"] = data;
        }

        return Task.FromResult<string?>($"/Content/{subfolder}/{fileName}");
    }
}
