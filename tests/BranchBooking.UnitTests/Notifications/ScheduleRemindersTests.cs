using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Notifications;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class ScheduleRemindersTests
{
    /// <summary>A confirmed booking <paramref name="hoursAhead"/> hours from now that was made <paramref name="ageHours"/> hours ago.</summary>
    private static Booking AddBooking(TestWorld world, double hoursAhead = 20, double ageHours = 5, string email = "thandi@example.com") =>
        world.AddConfirmedBookingCreatedAt(world.Now.AddHours(hoursAhead), world.Now.AddHours(-ageHours), email: email);

    [Fact]
    public async Task Queues_a_reminder_for_a_booking_that_has_entered_the_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = AddBooking(world);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(new ReminderSummary(1, 1, 0), summary);
        var message = Assert.Single(world.Store.Messages);
        Assert.Equal(NotificationKind.Reminder, message.Kind);
        Assert.Equal(booking.Id, message.BookingId);
        Assert.Equal(booking.StartsAtUtc, message.StartsAtUtc);
    }

    [Fact]
    public async Task The_reminder_is_pending_due_now_and_keyed_by_booking_and_start_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = AddBooking(world);

        await world.ScheduleReminders().ExecuteAsync(ct);

        var message = Assert.Single(world.Store.Messages);
        Assert.Equal(NotificationStatus.Pending, message.Status);
        Assert.Equal(world.Now, message.NextAttemptAtUtc);
        Assert.Equal($"reminder:{booking.Id:N}:{booking.StartsAtUtc.ToUnixTimeSeconds()}", message.DedupeKey);
    }

    [Theory]
    [InlineData(1440, true)]    // exactly 24 hours ahead: inside
    [InlineData(1441, false)]   // a minute beyond: not yet
    [InlineData(61, true)]      // just over the minimum lead: inside
    [InlineData(60, false)]     // exactly the minimum lead: too close
    [InlineData(5, false)]
    public async Task Only_appointments_inside_the_window_are_reminded(int minutesAhead, bool expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        AddBooking(world, hoursAhead: minutesAhead / 60.0);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(expected ? 1 : 0, summary.Queued);
    }

    [Theory]
    [InlineData(0, false)]      // changed just now
    [InlineData(239, false)]    // a minute short of the gap
    [InlineData(240, true)]     // exactly the gap
    [InlineData(600, true)]
    public async Task A_booking_must_have_been_left_alone_for_the_gap(int ageMinutes, bool expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        AddBooking(world, ageHours: ageMinutes / 60.0);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(expected ? 1 : 0, summary.Queued);
    }

    [Fact]
    public async Task A_cancelled_booking_is_not_reminded()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        AddBooking(world).Cancel(world.Now);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(new ReminderSummary(0, 0, 0), summary);
        Assert.Empty(world.Store.Messages);
    }

    [Fact]
    public async Task A_booking_that_already_has_a_reminder_for_its_time_is_not_reminded_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = AddBooking(world);
        world.Store.Messages.Add(NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, world.Now.AddMinutes(-1)));

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(0, summary.Found);
        Assert.Single(world.Store.Messages);
    }

    [Fact]
    public async Task Running_the_scan_twice_queues_one_reminder()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        AddBooking(world);

        var first = await world.ScheduleReminders().ExecuteAsync(ct);
        var second = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(1, first.Queued);
        Assert.Equal(0, second.Found);
        Assert.Single(world.Store.Messages);
    }

    [Fact]
    public async Task A_rescheduled_booking_is_reminded_again_for_its_new_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = AddBooking(world, hoursAhead: 30, ageHours: 10);
        var oldStart = booking.StartsAtUtc;
        world.Store.Messages.Add(NotificationMessage.ForReminder(booking.Id, oldStart, world.Now.AddHours(-1)));

        booking.Reschedule(DomainTestData.NewSlot(world.Now.AddHours(22)), 1, world.Now.AddHours(-6));
        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(1, summary.Queued);
        Assert.Equal(2, world.Store.Messages.Count);
        Assert.Contains(world.Store.Messages, m => m.StartsAtUtc == booking.StartsAtUtc && m.StartsAtUtc != oldStart);
    }

    [Fact]
    public async Task A_reschedule_inside_the_gap_waits_for_the_gap_to_pass()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = AddBooking(world, hoursAhead: 30, ageHours: 10);
        booking.Reschedule(DomainTestData.NewSlot(world.Now.AddHours(22)), 1, world.Now.AddHours(-1));

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(0, summary.Found);
    }

    [Fact]
    public async Task Takes_the_earliest_appointments_first_up_to_the_batch_size()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(reminderOptions: new ReminderOptions { BatchSize = 2 });
        var latest = AddBooking(world, hoursAhead: 22);
        var earliest = AddBooking(world, hoursAhead: 10);
        var middle = AddBooking(world, hoursAhead: 15);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(2, summary.Queued);
        Assert.Contains(world.Store.Messages, m => m.BookingId == earliest.Id);
        Assert.Contains(world.Store.Messages, m => m.BookingId == middle.Id);
        Assert.DoesNotContain(world.Store.Messages, m => m.BookingId == latest.Id);
    }

    [Fact]
    public async Task A_longer_lead_reminds_earlier()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(reminderOptions: new ReminderOptions { LeadHours = 48 });
        AddBooking(world, hoursAhead: 40);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(1, summary.Queued);
    }

    [Fact]
    public async Task A_duplicate_found_by_the_database_is_counted_and_the_rest_continue()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var first = AddBooking(world, hoursAhead: 10);
        var second = AddBooking(world, hoursAhead: 12);
        world.Store.SaveFailures.Enqueue(new NotificationAlreadyQueuedException());

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(new ReminderSummary(2, 1, 1), summary);
        var saved = Assert.Single(world.Store.Messages);
        Assert.Equal(second.Id, saved.BookingId);
        Assert.NotEqual(first.Id, saved.BookingId);
    }

    [Fact]
    public async Task Nothing_is_queued_when_reminders_are_disabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(reminderOptions: new ReminderOptions { Enabled = false });
        AddBooking(world);

        var summary = await world.ScheduleReminders().ExecuteAsync(ct);

        Assert.Equal(new ReminderSummary(0, 0, 0), summary);
        Assert.Empty(world.Store.Messages);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_scan()
    {
        var world = new TestWorld();
        AddBooking(world);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => world.ScheduleReminders().ExecuteAsync(cancelled.Token));

        Assert.Empty(world.Store.Messages);
    }

    [Fact]
    public async Task Logs_counts_but_nothing_that_identifies_a_customer()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var booking = AddBooking(world);
        var logger = new ListLogger<ScheduleReminders>();

        await world.ScheduleReminders(logger).ExecuteAsync(ct);

        var text = logger.AllText;
        Assert.Contains("1 due, 1 queued", text, StringComparison.Ordinal);
        Assert.DoesNotContain("thandi@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain(booking.Reference.Value, text, StringComparison.Ordinal);
    }
}
