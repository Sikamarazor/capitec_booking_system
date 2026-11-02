using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.IntegrationTests.Support;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_health_returns_200_with_ok_status()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("BranchBooking.Api", body.GetProperty("service").GetString());
        Assert.True(body.TryGetProperty("timestampUtc", out _));
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/does-not-exist", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
