using Microsoft.AspNetCore.Routing;

namespace BranchBooking.Api.Middleware;

/// <summary>
/// The route template of a request, for example <c>api/v1/bookings/{reference}</c>. Logs use this instead of the
/// URL because the URL contains the booking reference, which is a credential.
/// </summary>
public static class RequestRoute
{
    private const string ItemKey = "RouteTemplate";
    private const string Unmatched = "(no route)";

    /// <summary>Remembers the template; the exception handler later clears the endpoint, so capture it first.</summary>
    public static void Capture(HttpContext context) =>
        context.Items[ItemKey] = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? Unmatched;

    public static string Of(HttpContext context) =>
        context.Items[ItemKey] as string
        ?? (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
        ?? Unmatched;
}
