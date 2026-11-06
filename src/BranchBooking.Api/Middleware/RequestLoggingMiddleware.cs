using System.Diagnostics;

namespace BranchBooking.Api.Middleware;

/// <summary>One log line per request: method, route template, status and duration. Never the URL, query or body.</summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        RequestRoute.Capture(context);
        var started = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "{Method} {Route} responded {StatusCode} in {ElapsedMs:0.0} ms",
                context.Request.Method,
                RequestRoute.Of(context),
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
