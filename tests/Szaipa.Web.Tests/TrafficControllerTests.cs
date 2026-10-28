using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Controllers;
using Szaipa.Web.Services.Traffic;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class TrafficControllerTests
{
    [Theory]
    [InlineData("http", "http://example.test", null, true, "same-origin")]
    [InlineData("https", "https://example.test", null, true, "same-origin")]
    [InlineData("https", "https://example.test:8443", 8443, true, "same-origin")]
    [InlineData("http", "https://example.test", null, false, "same-origin")]
    [InlineData("https", "https://attacker.test", null, false, "same-origin")]
    [InlineData("https", "https://example.test:8443", null, false, "same-origin")]
    [InlineData("https", "https://example.test", null, false, "cross-site")]
    public void SameOriginPolicy_UsesEffectivePortAndRejectsUntrustedOrigins(
        string scheme, string origin, int? requestPort, bool expected, string fetchSite)
    {
        Assert.Equal(expected, TrafficOriginPolicy.IsSameOrigin(origin, fetchSite, scheme, "example.test", requestPort));
    }

    [Fact]
    public async Task Collect_RejectsOversizedChunkedBodyByActualBytes()
    {
        var store = new FakeTrafficEventStore();
        var controller = new TrafficEventsController(store, Options.Create(new SecurityMonitoringOptions { RequestAuditEnabled = true }));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        context.Request.Headers["Origin"] = "https://example.test";
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers["Transfer-Encoding"] = "chunked";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 2049)));
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Collect(default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task Collect_AcceptsBoundedBodyWithoutContentLengthOrTransferEncoding()
    {
        var store = new FakeTrafficEventStore();
        var controller = new TrafficEventsController(store, Options.Create(new SecurityMonitoringOptions { RequestAuditEnabled = true }));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("example.test");
        context.Request.Headers["Origin"] = "http://example.test";
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"eventId\":\"0c65ed78-56bd-431e-89f8-751c2d782865\",\"kind\":\"view\",\"pagePath\":\"/Home/News\"}"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Collect(default);

        Assert.IsType<AcceptedResult>(result);
        Assert.True(store.WasCalled);
    }

    private sealed class FakeTrafficEventStore : ITrafficEventStore
    {
        public bool WasCalled { get; private set; }
        public Task<bool> RecordAsync(TrafficEventRequest request, string clientKey, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(true);
        }
        public Task<TrafficSummary> ReadSummaryAsync(int days, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new TrafficSummary(0, 0, Array.Empty<TrafficTopPage>(), Array.Empty<TrafficEvent>(), false));
    }
}
