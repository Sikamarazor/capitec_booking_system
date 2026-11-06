using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.Application.Abstractions;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

public class HardeningEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ValidFormatReference = "BK-ABCDE-FGHJK";

    private sealed class ThrowingBookingRepository : IBookingRepository
    {
        public void Add(Booking booking) => throw new InvalidOperationException("secret-connection-detail");

        public Task<Booking?> GetByReferenceAsync(BookingReference reference, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret-connection-detail");

        public Task<IReadOnlyList<BookedPosition>> GetConfirmedPositionsAsync(
            Guid branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret-connection-detail");
    }

    private HttpClient ClientWith(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        }).CreateClient();

    [Fact]
    public async Task The_correlation_id_is_echoed_on_the_response()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.Add("X-Correlation-Id", "test-id-1");

        using var response = await client.SendAsync(request, ct);

        Assert.Equal("test-id-1", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task A_correlation_id_is_generated_when_none_is_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health", ct);

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health", ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task An_unknown_route_is_a_404_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/does-not-exist", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(body.TryGetProperty("traceId", out _));
        Assert.True(body.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task An_invalid_body_is_a_400_with_a_code_and_field_errors()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
        Assert.True(body.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task An_unexpected_exception_is_a_500_problem_that_hides_the_cause_and_keeps_the_correlation_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBookingRepository>();
                services.AddScoped<IBookingRepository, ThrowingBookingRepository>();
            })).CreateClient();

        using var response = await client.GetAsync($"/api/v1/bookings/{ValidFormatReference}", ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var text = await response.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("secret-connection-detail", text, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("internal_error", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task No_log_line_contains_the_reference_that_was_looked_up()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new ListLoggerProvider();
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBookingRepository>();
                services.AddScoped<IBookingRepository, ThrowingBookingRepository>();
            });
        }).CreateClient();

        using var response = await client.GetAsync($"/api/v1/bookings/{ValidFormatReference}", ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, m => m.Contains(ValidFormatReference, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs.Messages, m => m.Contains("api/v1/bookings/{reference}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lookups_beyond_the_limit_get_429_with_retry_after()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("RateLimiting:LookupPermitsPerMinute", "3"));

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last?.Dispose();
            last = await client.GetAsync("/api/v1/bookings/hello", ct);
            statuses.Add(last.StatusCode);
        }

        using (last)
        {
            Assert.Equal(
                [HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests],
                statuses);
            Assert.True(last!.Headers.Contains("Retry-After"));
            var body = await last.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("rate_limited", body.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Health_checks_are_never_rate_limited()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("RateLimiting:GlobalPermitsPerMinute", "2"));

        for (var i = 0; i < 10; i++)
        {
            using var response = await client.GetAsync("/api/v1/health", ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Rate_limiting_can_be_switched_off()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("RateLimiting:Enabled", "false"), ("RateLimiting:LookupPermitsPerMinute", "1"));

        for (var i = 0; i < 5; i++)
        {
            using var response = await client.GetAsync("/api/v1/bookings/hello", ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Out_of_range_rate_limit_settings_stop_the_host_from_starting()
    {
        var ct = TestContext.Current.CancellationToken;

        var failure = await Record.ExceptionAsync(async () =>
        {
            using var client = ClientWith(("RateLimiting:WritePermitsPerMinute", "0"));
            using var response = await client.GetAsync("/api/v1/health", ct);
        });

        Assert.NotNull(failure);
    }

    [Fact]
    public async Task The_openapi_document_lists_every_endpoint_when_enabled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("OpenApi:Enabled", "true"));

        using var response = await client.GetAsync("/openapi/v1.json", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var paths = body.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/v1/appointment-types", paths);
        Assert.Contains("/api/v1/branches", paths);
        Assert.Contains("/api/v1/branches/{id}/availability", paths);
        Assert.Contains("/api/v1/bookings", paths);
        Assert.Contains("/api/v1/bookings/{reference}", paths);
        Assert.Contains("/api/v1/bookings/{reference}/cancel", paths);
        Assert.Contains("/api/v1/bookings/{reference}/reschedule", paths);
    }

    [Fact]
    public async Task The_openapi_document_is_not_served_when_disabled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("OpenApi:Enabled", "false"));

        using var response = await client.GetAsync("/openapi/v1.json", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_listed_origin_is_allowed_and_another_is_not()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("Cors:AllowedOrigins:0", "https://book.example.com"));

        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        allowed.Headers.Add("Origin", "https://book.example.com");
        using var blocked = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        blocked.Headers.Add("Origin", "https://evil.example.net");

        using var allowedResponse = await client.SendAsync(allowed, ct);
        using var blockedResponse = await client.SendAsync(blocked, ct);

        Assert.Equal("https://book.example.com", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task A_preflight_for_a_listed_origin_is_answered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ClientWith(("Cors:AllowedOrigins:0", "https://book.example.com"));
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/bookings");
        request.Headers.Add("Origin", "https://book.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request, ct);

        Assert.Equal("https://book.example.com", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_wildcard_origin_stops_the_host_from_starting()
    {
        var ct = TestContext.Current.CancellationToken;

        var failure = await Record.ExceptionAsync(async () =>
        {
            using var client = ClientWith(("Cors:AllowedOrigins:0", "*"));
            using var response = await client.GetAsync("/api/v1/health", ct);
        });

        Assert.NotNull(failure);
    }
}
