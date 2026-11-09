using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BranchBooking.Domain.Enums;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public class CancelBookingEndpointTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);
        return factory.CreateClient();
    }

    private async Task<string> InsertBookingAsync(Guid branchId, Guid typeId)
    {
        var booking = PersistenceTestData.NewBooking(branchId, typeId, TuesdayEightAm, 1);
        await using var context = db.CreateContext();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking.Reference.Value;
    }

    [Fact]
    public async Task Cancels_with_no_body_and_returns_the_cancelled_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();

        using var response = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", content: null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("canCancel").GetBoolean());

        await using var read = db.CreateContext();
        Assert.Equal(BookingStatus.Cancelled, (await read.Bookings.SingleAsync(ct)).Status);
    }

    [Fact]
    public async Task Stores_the_reason_when_one_is_given()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/cancel", new { reason = "Plans changed" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var read = db.CreateContext();
        Assert.Equal("Plans changed", (await read.Bookings.SingleAsync(ct)).CancellationReason);
    }

    [Fact]
    public async Task Cancelling_twice_returns_200_both_times()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();

        using var first = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);
        using var second = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task The_place_becomes_bookable_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 1);
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();
        var request = new
        {
            branchId = branch.Id,
            appointmentTypeId = type.Id,
            startsAtUtc = TuesdayEightAm,
            fullName = "Sipho Dlamini",
            email = "sipho@example.com",
            phone = "0831234567",
        };

        using var whileFull = await client.PostAsJsonAsync("/api/v1/bookings", request, ct);
        using var cancel = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);
        using var afterwards = await client.PostAsJsonAsync("/api/v1/bookings", request, ct);

        Assert.Equal(HttpStatusCode.Conflict, whileFull.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterwards.StatusCode);
    }

    [Fact]
    public async Task Inside_the_cut_off_is_422_with_a_code_and_the_booking_stays_confirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();
        factory.Clock.Set(TuesdayEightAm.AddMinutes(-90));

        using var response = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("change_window_closed", body.GetProperty("code").GetString());
        await using var read = db.CreateContext();
        Assert.Equal(BookingStatus.Confirmed, (await read.Bookings.SingleAsync(ct)).Status);
    }

    [Fact]
    public async Task A_reason_over_the_limit_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/cancel", new { reason = new string('x', 501) }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("invalid_cancellation_reason", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unknown_reference_is_404()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsync("/api/v1/bookings/BK-00000-00000/cancel", null, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await InsertBookingAsync(branch.Id, type.Id);
        using var client = CreateClient();
        using var content = new StringContent("{not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
