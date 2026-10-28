using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Services.Traffic;

namespace Szaipa.Web.Controllers;

[ApiController]
[Route("api/traffic/events")]
public sealed class TrafficEventsController : ControllerBase
{
    private const int MaxBodyBytes = 2048;
    private readonly ITrafficEventStore _store;
    private readonly IOptions<SecurityMonitoringOptions> _options;

    public TrafficEventsController(ITrafficEventStore store, IOptions<SecurityMonitoringOptions> options)
    {
        _store = store;
        _options = options;
    }

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    [Consumes("application/json", "text/plain")]
    public async Task<IActionResult> Collect(CancellationToken cancellationToken)
    {
        if (!_options.Value.RequestAuditEnabled) return NotFound();
        if (!IsSameOriginRequest()) return StatusCode(StatusCodes.Status403Forbidden);
        if (Request.ContentLength is > MaxBodyBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        using var bodyBuffer = new MemoryStream(MaxBodyBytes);
        var readBuffer = new byte[512];
        while (true)
        {
            var read = await Request.Body.ReadAsync(readBuffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (bodyBuffer.Length + read > MaxBodyBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
            await bodyBuffer.WriteAsync(readBuffer.AsMemory(0, read), cancellationToken);
        }

        TrafficEventRequest? item;
        try
        {
            item = JsonSerializer.Deserialize<TrafficEventRequest>(bodyBuffer.ToArray(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 4 });
        }
        catch (JsonException) { return BadRequest(); }
        catch (IOException) { return BadRequest(); }

        if (item is null || item.EventId == Guid.Empty || !TrafficEventPolicy.IsValidKind(item.Kind)
            || !TrafficEventPolicy.TryNormalizePath(item.PagePath, out var pagePath))
            return BadRequest();

        string? targetPath = null;
        if (item.Kind == "click")
        {
            if (!TrafficEventPolicy.TryNormalizePath(item.TargetPath, out var normalizedTarget)) return BadRequest();
            targetPath = normalizedTarget;
        }
        else if (!string.IsNullOrEmpty(item.TargetPath)) return BadRequest();

        // RemoteIpAddress is trusted as the resolver's post-ForwardedHeaders value. Never use the
        // general client-IP helper here because it accepts arbitrary X-Forwarded-For input.
        var key = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var accepted = await _store.RecordAsync(item with { PagePath = pagePath, TargetPath = targetPath }, key, cancellationToken);
        return accepted ? Accepted() : StatusCode(StatusCodes.Status429TooManyRequests);
    }

    private bool IsSameOriginRequest()
        => TrafficOriginPolicy.IsSameOrigin(Request.Headers["Origin"].ToString(),
            Request.Headers["Sec-Fetch-Site"].ToString(), Request.Scheme, Request.Host.Host, Request.Host.Port);
}
