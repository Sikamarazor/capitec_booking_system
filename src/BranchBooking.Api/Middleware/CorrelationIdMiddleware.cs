using System.Text.RegularExpressions;

namespace BranchBooking.Api.Middleware;

/// <summary>
/// Gives every request an id, echoed in <c>X-Correlation-Id</c> and attached to every log line of the request.
/// A caller-supplied id is kept only when it is short and made of safe characters, so it cannot inject log content.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "CorrelationId";

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeId();

    public static string? From(HttpContext context) => context.Items[ItemKey] as string;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = supplied is not null && SafeId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        context.Response.Headers[HeaderName] = id;

        // The exception handler resets the response, so set the header again just before it is sent.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}
