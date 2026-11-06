using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.IntegrationTests.Support;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public class GetBookingEndpointTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);
        return factory.CreateClient();
    }

    private async Task<string> InsertBookingAsync(Guid branchId, Guid typeId, int slotNumber = 1)
    {
        var booking = PersistenceTestData.NewBooking(branchId, typeId, TuesdayEightAm, slotNumber);
        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking.Reference.Value;
    }

    [Fact]
    public async Task Returns_the_booking_for_its_reference()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/bookings/{reference}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(reference, body.GetProperty("reference").GetString());
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal(branch.Id, body.GetProperty("branch").GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("canCancel").GetBoolean());
        Assert.Matches(@"^.\*+@", body.GetProperty("customerEmail").GetString()!);
        Assert.Matches(@"^\*+\d{3}$", body.GetProperty("customerPhone").GetString()!);
    }

    [Fact]
    public async Task Accepts_a_reference_typed_in_lower_case_without_hyphens()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        var typed = reference.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/bookings/{typed}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(reference, body.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task An_unknown_and_a_malformed_reference_look_identical()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var unknown = await client.GetAsync("/api/v1/bookings/BK-00000-00000", ct);
        using var malformed = await client.GetAsync("/api/v1/bookings/hello", ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        var a = await unknown.Content.ReadFromJsonAsync<JsonElement>(ct);
        var b = await malformed.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("booking_not_found", a.GetProperty("code").GetString());
        Assert.Equal("booking_not_found", b.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_booking_created_through_the_api_can_be_read_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/bookings", new
        {
            branchId = branch.Id,
            appointmentTypeId = type.Id,
            startsAtUtc = TuesdayEightAm,
            fullName = "Thandi Nkosi",
            email = "thandi@example.com",
            phone = "082 123 4567",
        }, ct);
        var reference = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("reference").GetString();

        using var response = await client.GetAsync(created.Headers.Location, ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(reference, body.GetProperty("reference").GetString());
    }
}
