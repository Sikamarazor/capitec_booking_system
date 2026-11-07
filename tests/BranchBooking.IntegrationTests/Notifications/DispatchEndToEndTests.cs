using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.Application.Notifications;
using BranchBooking.Domain.Enums;
using BranchBooking.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BranchBooking.IntegrationTests.Notifications;

[Collection(DatabaseCollection.Name)]
public class DispatchEndToEndTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WednesdayEightAm = new(2026, 11, 4, 6, 0, 0, TimeSpan.Zero);

    private sealed class FailingSender : INotificationSender
    {
        public string Channel => "Brevo";

        public bool RetainsContent => false;

        public Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken) =>
            Task.FromResult(SendResult.Transient("brevo_503"));
    }

    private HttpClient CreateClient()
    {
        factory.Clock.Set(PersistenceTestData.Now);
        return factory.CreateClient();
    }

    private static async Task<string> BookAsync(HttpClient client, Guid branchId, Guid typeId, CancellationToken ct)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/bookings", new
        {
            branchId,
            appointmentTypeId = typeId,
            startsAtUtc = TuesdayEightAm,
            fullName = "Thandi Nkosi",
            email = "thandi@example.com",
            phone = "082 123 4567",
        }, ct);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("reference").GetString()!;
    }

    private async Task<DispatchSummary> DispatchAsync(Action<IServiceCollection>? configure = null)
    {
        await using var provider = WorkerTestHost.Build(factory.Clock, configure);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchNotifications>().ExecuteAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> MessagesAsync(HttpClient client, string reference, CancellationToken ct)
    {
        using var response = await client.GetAsync($"/api/v1/bookings/{reference}/messages", ct);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    [Fact]
    public async Task A_confirmation_is_sent_through_the_simulated_channel_and_shown_by_the_api()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();
        var reference = await BookAsync(client, branch.Id, type.Id, ct);
        Assert.Equal("Pending", (await MessagesAsync(client, reference, ct))[0].GetProperty("status").GetString());

        var summary = await DispatchAsync();

        Assert.Equal(new DispatchSummary(1, 1, 0, 0, 0), summary);
        var message = (await MessagesAsync(client, reference, ct))[0];
        Assert.Equal("Sent", message.GetProperty("status").GetString());
        Assert.Equal("Simulated", message.GetProperty("channel").GetString());
        Assert.Equal("Your branch appointment is confirmed", message.GetProperty("subject").GetString());
        Assert.Contains(reference, message.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Contains("Tuesday, 3 November 2026 at 08:00", message.GetProperty("body").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_pass_finds_nothing_left_to_send()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();
        await BookAsync(client, branch.Id, type.Id, ct);

        await DispatchAsync();
        var second = await DispatchAsync();

        Assert.Equal(0, second.Claimed);
    }

    [Fact]
    public async Task A_booking_cancelled_before_dispatch_gets_only_a_cancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();
        var reference = await BookAsync(client, branch.Id, type.Id, ct);
        using var cancel = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);

        var summary = await DispatchAsync();

        Assert.Equal(new DispatchSummary(2, 1, 1, 0, 0), summary);
        var messages = await MessagesAsync(client, reference, ct);
        Assert.Equal("Skipped", messages[0].GetProperty("status").GetString());
        Assert.Equal("Sent", messages[1].GetProperty("status").GetString());
        Assert.Equal("Your branch appointment was cancelled", messages[1].GetProperty("subject").GetString());
    }

    [Fact]
    public async Task A_booking_rescheduled_before_dispatch_has_its_confirmation_superseded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();
        var reference = await BookAsync(client, branch.Id, type.Id, ct);
        using var move = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);

        await DispatchAsync();

        var messages = await MessagesAsync(client, reference, ct);
        Assert.Equal("Skipped", messages[0].GetProperty("status").GetString());
        Assert.Equal("Sent", messages[1].GetProperty("status").GetString());
        Assert.Contains("Wednesday, 4 November 2026 at 08:00", messages[1].GetProperty("body").GetString(), StringComparison.Ordinal);

        await using var read = db.CreateContext();
        var skipped = await read.NotificationMessages.SingleAsync(m => m.Kind == NotificationKind.Confirmation, ct);
        Assert.Equal("superseded", skipped.Detail);
    }

    [Fact]
    public async Task A_failing_channel_leaves_the_message_pending_for_a_later_retry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = CreateClient();
        var reference = await BookAsync(client, branch.Id, type.Id, ct);

        var summary = await DispatchAsync(services => services.AddScoped<INotificationSender, FailingSender>());

        Assert.Equal(new DispatchSummary(1, 0, 0, 1, 0), summary);
        await using var read = db.CreateContext();
        var message = await read.NotificationMessages.SingleAsync(ct);
        Assert.Equal(NotificationStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("brevo_503", message.Detail);
        Assert.Equal(PersistenceTestData.Now.AddSeconds(30), message.NextAttemptAtUtc);
        Assert.Equal("Pending", (await MessagesAsync(client, reference, ct))[0].GetProperty("status").GetString());

        // Not due yet, so an immediate second pass leaves it alone; after the delay it is claimed again.
        Assert.Equal(0, (await DispatchAsync()).Claimed);
        factory.Clock.Set(PersistenceTestData.Now.AddMinutes(10));
        Assert.Equal(1, (await DispatchAsync()).Sent);
    }
}
