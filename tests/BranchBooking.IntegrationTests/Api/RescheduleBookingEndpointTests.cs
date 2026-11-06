using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public class RescheduleBookingEndpointTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WednesdayEightAm = new(2026, 11, 4, 6, 0, 0, TimeSpan.Zero);

    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);
        return factory.CreateClient();
    }

    private async Task<string> InsertBookingAsync(Guid branchId, Guid typeId, DateTimeOffset start, int slotNumber = 1)
    {
        var booking = PersistenceTestData.NewBooking(branchId, typeId, start, slotNumber);
        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking.Reference.Value;
    }

    [Fact]
    public async Task Moves_the_booking_and_returns_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(WednesdayEightAm, body.GetProperty("startsAtUtc").GetDateTimeOffset());
        Assert.Equal(1, body.GetProperty("rescheduleCount").GetInt32());

        await using var read = db.CreateContext();
        var saved = await read.Bookings.SingleAsync(ct);
        Assert.Equal(WednesdayEightAm, saved.StartsAtUtc);
        Assert.Equal(1, saved.RescheduleCount);
    }

    [Fact]
    public async Task The_old_place_is_free_again_afterwards()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 1);
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();

        using var move = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);
        using var availability = await client.GetAsync($"/api/v1/branches/{branch.Id}/availability?date=2026-11-03", ct);

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var body = await availability.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, body.GetProperty("slots")[0].GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task A_full_target_slot_is_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 1);
        await InsertBookingAsync(branch.Id, type.Id, WednesdayEightAm);
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("slot_not_available", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_same_time_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = TuesdayEightAm }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("same_slot", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Inside_the_cut_off_is_422()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();
        factory.Clock.Set(TuesdayEightAm.AddMinutes(-90));

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("change_window_closed", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_cancelled_booking_is_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();
        using var cancel = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("booking_not_active", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_missing_body_field_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_reference_is_404()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings/BK-00000-00000/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Two_bookings_moving_into_the_last_place_never_both_succeed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 1);
        var first = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm);
        var second = await InsertBookingAsync(branch.Id, type.Id, TuesdayEightAm.AddMinutes(30));
        using var client = CreateClient();

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/bookings/{first}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct),
            client.PostAsJsonAsync($"/api/v1/bookings/{second}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct));

        try
        {
            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

            await using var read = db.CreateContext();
            Assert.Equal(1, await read.Bookings.CountAsync(b => b.StartsAtUtc == WednesdayEightAm, ct));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
