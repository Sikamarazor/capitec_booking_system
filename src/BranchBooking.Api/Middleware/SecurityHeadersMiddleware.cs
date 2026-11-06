namespace BranchBooking.Api.Middleware;

/// <summary>
/// Headers for a JSON API: nothing may be sniffed, framed, or loaded from it. TLS and HSTS are handled by the
/// reverse proxy that terminates TLS, so they are not set here.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        Apply(context.Response);
        context.Response.OnStarting(() =>
        {
            Apply(context.Response);
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static void Apply(HttpResponse response)
    {
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    }
}
