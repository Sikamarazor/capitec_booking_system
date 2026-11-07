using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.Domain.Entities;
using BranchBooking.IntegrationTests.Support;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public class BookingMessagesEndpointTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);
        return factory.CreateClient();
    }

    [Fact]
    public async Task A_booking_made_through_the_api_has_one_pending_confirmation()
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

        using var response = await client.GetAsync($"/api/v1/bookings/{reference}/messages", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal("Confirmation", body[0].GetProperty("kind").GetString());
        Assert.Equal("Pending", body[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body[0].GetProperty("body").ValueKind);
    }

    [Fact]
    public async Task A_cancellation_through_the_api_adds_a_cancellation_message()
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
        using var cancel = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);

        using var response = await client.GetAsync($"/api/v1/bookings/{reference}/messages", ct);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(["Confirmation", "Cancellation"], body.EnumerateArray().Select(m => m.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task A_sent_simulated_message_shows_its_subject_and_body()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var booking = PersistenceTestData.NewBooking(branch.Id, type.Id, TuesdayEightAm);
        var message = NotificationMessage.ForConfirmation(booking, PersistenceTestData.Now);
        message.MarkSent("Simulated", "sim-1", "Hello subject", "Hello body", PersistenceTestData.Now.AddMinutes(1));
        await using (var write = db.CreateContext())
        {
            write.Bookings.Add(booking);
            write.NotificationMessages.Add(message);
            await write.SaveChangesAsync(ct);
        }

        using var client = CreateClient();

        using var response = await client.GetAsync($"/api/v1/bookings/{booking.Reference.Value}/messages", ct);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Sent", body[0].GetProperty("status").GetString());
        Assert.Equal("Hello subject", body[0].GetProperty("subject").GetString());
        Assert.Equal("Hello body", body[0].GetProperty("body").GetString());
    }

    [Fact]
    public async Task An_unknown_and_a_malformed_reference_are_both_404_booking_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var unknown = await client.GetAsync("/api/v1/bookings/BK-00000-00000/messages", ct);
        using var malformed = await client.GetAsync("/api/v1/bookings/hello/messages", ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        var body = await malformed.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("booking_not_found", body.GetProperty("code").GetString());
    }
}
