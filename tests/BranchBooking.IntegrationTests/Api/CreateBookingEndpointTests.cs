using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.Domain.Enums;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BranchBooking.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public class CreateBookingEndpointTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);
        return factory.CreateClient();
    }

    private static object Body(Guid branchId, Guid typeId, DateTimeOffset start, string email = "thandi@example.com") => new
    {
        branchId,
        appointmentTypeId = typeId,
        startsAtUtc = start,
        fullName = "Thandi Nkosi",
        email,
        phone = "082 123 4567",
    };

    [Fact]
    public async Task Creates_a_booking_and_returns_201_with_the_reference()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var reference = body.GetProperty("reference").GetString()!;
        Assert.StartsWith("BK-", reference, StringComparison.Ordinal);
        Assert.Equal($"/api/v1/bookings/{reference}", response.Headers.Location?.OriginalString);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal("t*****@example.com", body.GetProperty("customerEmail").GetString());
        Assert.Equal("*******567", body.GetProperty("customerPhone").GetString());
        Assert.Equal("08:00", body.GetProperty("startsAtLocal").GetDateTimeOffset().ToString("HH:mm"));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        await using var read = db.CreateContext();
        var saved = await read.Bookings.SingleAsync(ct);
        Assert.Equal(reference, saved.Reference.Value);
        Assert.Equal(BookingStatus.Confirmed, saved.Status);
        Assert.Equal(1, saved.SlotNumber);
        Assert.Equal("0821234567", saved.Customer.Phone);
    }

    [Fact]
    public async Task A_badly_formed_email_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm, "not-an-email"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_fields_are_400()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings", new { fullName = "Thandi Nkosi" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_email_the_domain_rejects_is_400_with_a_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();

        // Passes the [EmailAddress] attribute but has no dot in the domain, which the domain rules refuse.
        using var response = await client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm, "a@localhost"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("invalid_customer_details", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unknown_branch_is_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, type) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings", Body(Guid.CreateVersion7(), type.Id, TuesdayEightAm), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("branch_not_found", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_time_that_is_not_a_slot_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm.AddMinutes(10)), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("slot_not_offered", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_full_slot_is_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 1);
        using var client = CreateClient();

        using var first = await client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm), ct);
        using var second = await client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm, "other@example.com"), ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("slot_not_available", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Simultaneous_requests_never_book_more_places_than_the_branch_has()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync(slotCapacity: 2);
        using var client = CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            client.PostAsJsonAsync("/api/v1/bookings", Body(branch.Id, type.Id, TuesdayEightAm, $"person{i}@example.com"), ct)));

        try
        {
            Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

            await using var read = db.CreateContext();
            var positions = await read.Bookings.Where(b => b.Status == BookingStatus.Confirmed).Select(b => b.SlotNumber).ToListAsync(ct);
            Assert.Equal([1, 2], positions.Order());
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
