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
public class ReminderEndToEndTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset Start = PersistenceTestData.Now;                       // Monday 07:00 UTC
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WednesdayEightAm = new(2026, 11, 4, 6, 0, 0, TimeSpan.Zero);

    private async Task<string> BookAsync(HttpClient client, Guid branchId, Guid typeId)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/bookings", new
        {
            branchId,
            appointmentTypeId = typeId,
            startsAtUtc = TuesdayEightAm,
            fullName = "Thandi Nkosi",
            email = "thandi@example.com",
            phone = "082 123 4567",
        }, TestContext.Current.CancellationToken);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("reference").GetString()!;
    }

    private async Task<ReminderSummary> ScheduleAsync(params (string Key, string Value)[] settings)
    {
        await using var provider = WorkerTestHost.Build(factory.Clock, null, settings);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ScheduleReminders>().ExecuteAsync(TestContext.Current.CancellationToken);
    }

    private async Task<DispatchSummary> DispatchAsync()
    {
        await using var provider = WorkerTestHost.Build(factory.Clock);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchNotifications>().ExecuteAsync(TestContext.Current.CancellationToken);
    }

    private HttpClient StartClient()
    {
        factory.Clock.Set(Start);
        return factory.CreateClient();
    }

    [Fact]
    public async Task A_fresh_booking_is_reminded_only_after_the_gap_and_the_reminder_is_sent_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = StartClient();
        var reference = await BookAsync(client, branch.Id, type.Id);

        var tooSoon = await ScheduleAsync();
        factory.Clock.Set(Start.AddHours(5));
        var due = await ScheduleAsync();
        var again = await ScheduleAsync();
        var dispatched = await DispatchAsync();

        Assert.Equal(0, tooSoon.Found);
        Assert.Equal(new ReminderSummary(1, 1, 0), due);
        Assert.Equal(0, again.Found);
        Assert.Equal(2, dispatched.Sent);   // the confirmation and the reminder

        using var response = await client.GetAsync($"/api/v1/bookings/{reference}/messages", ct);
        var messages = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var subjects = messages.EnumerateArray().Select(m => m.GetProperty("subject").GetString()).ToList();
        Assert.Contains("Your branch appointment is confirmed", subjects);
        Assert.Contains("Reminder: your branch appointment", subjects);
    }

    [Fact]
    public async Task A_rescheduled_booking_is_reminded_for_its_new_time_and_the_old_reminder_is_superseded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = StartClient();
        var reference = await BookAsync(client, branch.Id, type.Id);

        factory.Clock.Set(Start.AddHours(5));
        Assert.Equal(1, (await ScheduleAsync()).Queued);                       // reminder for Tuesday, still unsent
        using var move = await client.PostAsJsonAsync($"/api/v1/bookings/{reference}/reschedule", new { newStartsAtUtc = WednesdayEightAm }, ct);
        Assert.True(move.IsSuccessStatusCode);

        factory.Clock.Set(Start.AddHours(25));                                  // Wednesday's appointment is now 22 hours away
        var second = await ScheduleAsync();
        await DispatchAsync();

        Assert.Equal(1, second.Queued);
        await using var read = db.CreateContext();
        var reminders = await read.NotificationMessages
            .Where(m => m.Kind == NotificationKind.Reminder)
            .OrderBy(m => m.StartsAtUtc)
            .ToListAsync(ct);
        Assert.Equal(2, reminders.Count);
        Assert.Equal(NotificationStatus.Skipped, reminders[0].Status);
        Assert.Equal("superseded", reminders[0].Detail);
        Assert.Equal(NotificationStatus.Sent, reminders[1].Status);
        Assert.Equal(WednesdayEightAm, reminders[1].StartsAtUtc);
    }

    [Fact]
    public async Task A_cancelled_booking_is_never_reminded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = StartClient();
        var reference = await BookAsync(client, branch.Id, type.Id);
        using var cancel = await client.PostAsync($"/api/v1/bookings/{reference}/cancel", null, ct);

        factory.Clock.Set(Start.AddHours(5));
        var summary = await ScheduleAsync();

        Assert.Equal(0, summary.Found);
        await using var read = db.CreateContext();
        Assert.False(await read.NotificationMessages.AnyAsync(m => m.Kind == NotificationKind.Reminder, ct));
    }

    [Fact]
    public async Task Nothing_is_queued_when_reminders_are_switched_off()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = StartClient();
        await BookAsync(client, branch.Id, type.Id);

        factory.Clock.Set(Start.AddHours(5));
        var summary = await ScheduleAsync(("Notifications:Reminders:Enabled", "false"));

        Assert.Equal(0, summary.Found);
        await using var read = db.CreateContext();
        Assert.False(await read.NotificationMessages.AnyAsync(m => m.Kind == NotificationKind.Reminder, ct));
    }

    [Fact]
    public async Task Two_scans_at_the_same_time_queue_exactly_one_reminder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        using var client = StartClient();
        await BookAsync(client, branch.Id, type.Id);
        factory.Clock.Set(Start.AddHours(5));

        var summaries = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ScheduleAsync()));

        Assert.Equal(1, summaries.Sum(s => s.Queued));
        await using var read = db.CreateContext();
        Assert.Equal(1, await read.NotificationMessages.CountAsync(m => m.Kind == NotificationKind.Reminder, ct));
    }
}
