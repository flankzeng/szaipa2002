using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Security;

namespace Szaipa.Web.Middleware;

/// <summary>
/// Writes one line per non-static, non-skipped HTTP request to the rolling audit log. Placed after the
/// static-file middleware so asset requests are already short-circuited; the skip policy additionally
/// covers extension-based misses and the /Content legacy tree. Register conditionally in Program.cs when
/// <c>SecurityMonitoring:RequestAuditEnabled</c> is true.
/// </summary>
public sealed class RequestAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityMonitoringPaths _paths;
    private readonly ISecurityAuditWriter _writer;
    private readonly IOptions<SecurityMonitoringOptions> _options;

    public RequestAuditMiddleware(
        RequestDelegate next,
        SecurityMonitoringPaths paths,
        ISecurityAuditWriter writer,
        IOptions<SecurityMonitoringOptions> options)
    {
        _next = next;
        _paths = paths;
        _writer = writer;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            WriteEntry(context, stopwatch.ElapsedMilliseconds);
        }
    }

    private void WriteEntry(HttpContext context, long elapsedMs)
    {
        if (!_options.Value.RequestAuditEnabled)
        {
            return;
        }

        // The exception-handler middleware re-executes the pipeline after an error; the inner invocation is
        // the interesting one and the outer one would double-log the same request.
        if (context.Features.Get<IExceptionHandlerFeature>() != null)
        {
            return;
        }

        var path = context.Request.Path.Value;
        if (RequestAuditSkipPolicy.ShouldSkip(path, _paths.AuditSkipPaths))
        {
            return;
        }

        var line = RequestAuditEntry.Format(
            DateTimeOffset.Now,
            ClientIpResolver.Resolve(context),
            context.Request.Method,
            path ?? string.Empty,
            context.Response.StatusCode,
            elapsedMs,
            context.Request.Headers["User-Agent"].ToString());

        _writer.WriteRequest(line);
    }
}
