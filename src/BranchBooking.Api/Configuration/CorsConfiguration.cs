using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace BranchBooking.Api.Configuration;

public static class CorsConfiguration
{
    /// <summary>
    /// No origin is allowed unless <c>Cors:AllowedOrigins</c> lists it. The web app is served from the same origin as
    /// the API (nginx in Docker, the Angular proxy in development), so the default needs no CORS at all.
    /// A wildcard is refused: a booking API must not be callable from any website.
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors();

        services.AddOptions<CorsOptions>().Configure(cors =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length == 0)
            {
                return;
            }

            if (origins.Any(o => o.Contains('*', StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Cors:AllowedOrigins must list exact origins; wildcards are not allowed.");
            }

            cors.AddDefaultPolicy(policy => policy
                .WithOrigins(origins)
                .WithMethods("GET", "POST")
                .WithHeaders("Content-Type", "X-Correlation-Id")
                .WithExposedHeaders("X-Correlation-Id", "Retry-After")
                .SetPreflightMaxAge(TimeSpan.FromHours(1)));
        });

        return services;
    }
}
