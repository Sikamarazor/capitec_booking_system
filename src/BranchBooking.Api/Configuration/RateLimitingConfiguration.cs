using System.Globalization;
using System.Threading.RateLimiting;
using BranchBooking.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BranchBooking.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Create, cancel and reschedule requests per client per minute.</summary>
    public int WritePermitsPerMinute { get; set; } = 10;

    /// <summary>Lookups by reference per client per minute. Keeps guessing references impractical.</summary>
    public int LookupPermitsPerMinute { get; set; } = 30;

    /// <summary>All requests per client per minute (health checks excluded).</summary>
    public int GlobalPermitsPerMinute { get; set; } = 300;

    public bool IsValid() =>
        WritePermitsPerMinute is >= 1 and <= 10_000
        && LookupPermitsPerMinute is >= 1 and <= 10_000
        && GlobalPermitsPerMinute is >= 1 and <= 100_000;
}

public static class RateLimitPolicies
{
    public const string Write = "booking-write";
    public const string Lookup = "booking-lookup";
}

public static class RateLimitingConfiguration
{
    /// <summary>
    /// Fixed-window limits per client address. Behind a reverse proxy the client address is only correct when the
    /// proxy's <c>X-Forwarded-For</c> is trusted (see <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> in docs/api/README.md).
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .Validate(options => options.IsValid(), "RateLimiting settings are out of range; see docs/api/README.md.")
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        // Configured lazily so the values (and test overrides) are read from the final configuration.
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingOptions>>((limiter, settings) =>
        {
            var config = settings.Value;
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                !config.Enabled || IsHealth(context.Request.Path)
                    ? RateLimitPartition.GetNoLimiter("unlimited")
                    : RateLimitPartition.GetFixedWindowLimiter("global:" + Client(context), _ => PerMinute(config.GlobalPermitsPerMinute)));

            limiter.AddPolicy(RateLimitPolicies.Write, context =>
                config.Enabled
                    ? RateLimitPartition.GetFixedWindowLimiter("write:" + Client(context), _ => PerMinute(config.WritePermitsPerMinute))
                    : RateLimitPartition.GetNoLimiter("unlimited"));

            limiter.AddPolicy(RateLimitPolicies.Lookup, context =>
                config.Enabled
                    ? RateLimitPartition.GetFixedWindowLimiter("lookup:" + Client(context), _ => PerMinute(config.LookupPermitsPerMinute))
                    : RateLimitPartition.GetNoLimiter("unlimited"));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Detail = "Too many requests. Please wait a moment and try again.",
                };
                problem.Extensions["code"] = "rate_limited";
                problem.AddCorrelation(http);

                await http.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
            };
        });

        return services;
    }

    private static bool IsHealth(PathString path) =>
        path.StartsWithSegments("/api/v1/health") || path.StartsWithSegments("/health");

    private static string Client(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
