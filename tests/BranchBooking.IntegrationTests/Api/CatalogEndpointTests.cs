using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.IntegrationTests.Support;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public class CatalogEndpointTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);   // Monday 2 November 2026, 09:00 SAST
        return factory.CreateClient();
    }

    [Fact]
    public async Task Lists_appointment_types()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/v1/appointment-types", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal("TEST_TYPE", body[0].GetProperty("code").GetString());
        Assert.Equal("Bring ID", body[0].GetProperty("whatToBring").GetString());
    }

    [Fact]
    public async Task Lists_branches_with_opening_hours()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, _) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/v1/branches", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(branch.Id, body[0].GetProperty("id").GetGuid());
        Assert.Equal("Africa/Johannesburg", body[0].GetProperty("timeZoneId").GetString());
        var hours = body[0].GetProperty("openingHours");
        Assert.Equal(6, hours.GetArrayLength());
        Assert.Equal("Monday", hours[0].GetProperty("day").GetString());
        Assert.Equal("08:00", hours[0].GetProperty("opensAt").GetString());
    }

    [Fact]
    public async Task Availability_shows_remaining_places_after_a_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var start = new DateTimeOffset(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);   // Tuesday 08:00 SAST

        await using (var setup = db.CreateContext())
        {
            setup.Bookings.Add(PersistenceTestData.NewBooking(branch.Id, type.Id, start, slotNumber: 1));
            await setup.SaveChangesAsync(ct);
        }

        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/branches/{branch.Id}/availability?date=2026-11-03", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("2026-11-03", body.GetProperty("date").GetString());
        var slots = body.GetProperty("slots");
        Assert.Equal(18, slots.GetArrayLength());
        Assert.Equal("08:00", slots[0].GetProperty("localStart").GetString());
        Assert.Equal(1, slots[0].GetProperty("remaining").GetInt32());
        Assert.True(slots[0].GetProperty("isAvailable").GetBoolean());
        Assert.Equal(2, slots[1].GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task Availability_for_a_closed_day_is_an_empty_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, _) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/branches/{branch.Id}/availability?date=2026-11-08", ct);   // Sunday

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(0, body.GetProperty("slots").GetArrayLength());
    }

    [Fact]
    public async Task Availability_for_an_unknown_branch_is_404_with_a_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/branches/{Guid.CreateVersion7()}/availability?date=2026-11-03", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("branch_not_found", body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?date=not-a-date")]
    [InlineData("?date=2026-13-45")]
    public async Task Availability_requires_a_valid_date(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, _) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/branches/{branch.Id}/availability{query}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Availability_outside_the_booking_window_is_400_with_a_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, _) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/branches/{branch.Id}/availability?date=2026-12-30", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("date_outside_booking_window", body.GetProperty("code").GetString());
    }
}
