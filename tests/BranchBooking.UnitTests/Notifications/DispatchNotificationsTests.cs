using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Notifications;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class DispatchNotificationsTests
{
    private static readonly DateTimeOffset WednesdayEightAm = new(2026, 11, 4, 6, 0, 0, TimeSpan.Zero);

    private static (TestWorld World, Booking Booking, NotificationMessage Message) Arrange(
        NotificationKind kind = NotificationKind.Confirmation, NotificationOptions? options = null)
    {
        var world = new TestWorld(notificationOptions: options);
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var message = world.AddPendingMessage(booking, kind);
        return (world, booking, message);
    }

    [Fact]
    public async Task Sends_a_confirmation_to_the_customer_and_marks_it_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, booking, message) = Arrange();

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(new DispatchSummary(1, 1, 0, 0, 0), summary);
        var email = Assert.Single(world.Sender.Sent);
        Assert.Equal(message.Id, email.MessageId);
        Assert.Equal("thandi@example.com", email.ToEmail);
        Assert.Equal("Thandi Nkosi", email.ToName);
        Assert.Equal("Your branch appointment is confirmed", email.Subject);
        Assert.Contains(booking.Reference.Value, email.TextBody, StringComparison.Ordinal);
        Assert.Equal(NotificationStatus.Sent, message.Status);
        Assert.Equal("Simulated", message.Channel);
        Assert.Equal("provider-1", message.ProviderMessageId);
        Assert.Equal(world.Now, message.SentAtUtc);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(1, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Keeps_the_rendered_content_when_the_sender_retains_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, booking, message) = Arrange();

        await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal("Your branch appointment is confirmed", message.RenderedSubject);
        Assert.Contains(booking.Reference.Value, message.RenderedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_no_content_when_the_sender_does_not_retain_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange();
        world.Sender.RetainsContent = false;
        world.Sender.Channel = "Brevo";

        await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal("Brevo", message.Channel);
        Assert.Null(message.RenderedSubject);
        Assert.Null(message.RenderedBody);
    }

    [Fact]
    public async Task Does_nothing_when_nothing_is_queued()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(new DispatchSummary(0, 0, 0, 0, 0), summary);
        Assert.Empty(world.Sender.Sent);
        Assert.Equal(0, world.Store.SaveCalls);
    }

    [Fact]
    public async Task Leaves_a_message_alone_until_its_retry_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange();
        message.RecordFailure("brevo_500", true, world.Now, 5, TimeSpan.FromHours(1));

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(0, summary.Claimed);
        Assert.Empty(world.Sender.Sent);
    }

    [Fact]
    public async Task Skips_a_confirmation_when_the_booking_was_cancelled_meanwhile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, booking, message) = Arrange();
        booking.Cancel(world.Now);

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(1, summary.Skipped);
        Assert.Empty(world.Sender.Sent);
        Assert.Equal(NotificationStatus.Skipped, message.Status);
        Assert.Equal("booking_not_confirmed", message.Detail);
    }

    [Fact]
    public async Task Skips_a_message_that_a_reschedule_made_obsolete()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, booking, message) = Arrange();
        booking.Reschedule(DomainTestData.NewSlot(WednesdayEightAm), 1, world.Now);

        await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(NotificationStatus.Skipped, message.Status);
        Assert.Equal("superseded", message.Detail);
        Assert.Empty(world.Sender.Sent);
    }

    [Fact]
    public async Task Skips_a_message_for_an_appointment_that_has_already_started()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var started = Booking.Create(
            BookingReference.Generate(),
            world.Branch.Id,
            world.Type.Id,
            DomainTestData.NewSlot(world.Now.AddMinutes(-30)),
            1,
            CustomerContact.Create("Thandi Nkosi", "thandi@example.com", "0821234567"),
            world.Now.AddHours(-2));
        started.AttachNavigation(world.Branch, world.Type);
        world.Store.Bookings.Add(started);
        var message = world.AddPendingMessage(started);

        await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(NotificationStatus.Skipped, message.Status);
        Assert.Equal("appointment_passed", message.Detail);
    }

    [Fact]
    public async Task Sends_a_cancellation_for_a_cancelled_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, booking, _) = Arrange(NotificationKind.Cancellation);
        booking.Cancel(world.Now);

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(1, summary.Sent);
        Assert.Equal("Your branch appointment was cancelled", Assert.Single(world.Sender.Sent).Subject);
    }

    [Fact]
    public async Task Skips_a_cancellation_for_a_booking_that_is_not_cancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange(NotificationKind.Cancellation);

        await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal("booking_not_cancelled", message.Detail);
        Assert.Empty(world.Sender.Sent);
    }

    [Fact]
    public async Task A_transient_failure_keeps_the_message_pending_with_the_base_delay()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange();
        world.Sender.Enqueue(SendResult.Transient("brevo_429"));

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(1, summary.Retried);
        Assert.Equal(NotificationStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(world.Now.AddSeconds(30), message.NextAttemptAtUtc);
        Assert.Equal("brevo_429", message.Detail);
    }

    [Fact]
    public async Task The_second_failure_waits_twice_as_long()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange();
        message.RecordFailure("brevo_500", true, world.Now, 5, TimeSpan.Zero);
        world.Sender.Enqueue(SendResult.Transient("brevo_500"));

        await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(2, message.Attempts);
        Assert.Equal(world.Now.AddSeconds(60), message.NextAttemptAtUtc);
    }

    [Fact]
    public async Task A_permanent_failure_ends_the_message_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange();
        world.Sender.Enqueue(SendResult.Permanent("brevo_400:invalid_parameter"));

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(NotificationStatus.Failed, message.Status);
        Assert.Equal("brevo_400:invalid_parameter", message.Detail);
    }

    [Fact]
    public async Task The_last_allowed_attempt_ends_a_transient_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange(options: new NotificationOptions { MaxAttempts = 2 });
        message.RecordFailure("brevo_500", true, world.Now, 2, TimeSpan.Zero);
        world.Sender.Enqueue(SendResult.Transient("brevo_500"));

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(NotificationStatus.Failed, message.Status);
        Assert.Equal(2, message.Attempts);
    }

    [Fact]
    public async Task A_sender_that_throws_is_a_transient_failure_that_records_only_the_exception_type()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _, message) = Arrange();
        world.Sender.EnqueueException(new InvalidOperationException("mailbox thandi@example.com rejected"));

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(1, summary.Retried);
        Assert.Equal("InvalidOperationException", message.Detail);
        Assert.DoesNotContain("thandi", message.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_persistence_conflict_stops_the_batch_but_not_the_pass()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld();
        var first = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc, email: "a@example.com");
        var second = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc.AddMinutes(30), email: "b@example.com");
        world.AddPendingMessage(first);
        var untouched = world.AddPendingMessage(second);
        world.Store.SaveFailures.Enqueue(new ConcurrencyConflictException());

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(2, summary.Claimed);
        Assert.Equal(0, summary.Sent);
        Assert.Single(world.Sender.Sent);
        Assert.Equal(NotificationStatus.Pending, untouched.Status);
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_pass()
    {
        var world = new TestWorld();
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        world.AddPendingMessage(booking);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => world.DispatchNotifications().ExecuteAsync(cancelled.Token));

        Assert.Empty(world.Sender.Sent);
    }

    [Fact]
    public async Task Takes_the_oldest_messages_first_up_to_the_batch_size()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new TestWorld(notificationOptions: new NotificationOptions { BatchSize = 2 });
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        var oldest = NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, world.Now.AddMinutes(-3));
        var middle = NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, world.Now.AddMinutes(-2));
        var newest = NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, world.Now.AddMinutes(-1));
        world.Store.Messages.AddRange([newest, oldest, middle]);

        var summary = await world.DispatchNotifications().ExecuteAsync(ct);

        Assert.Equal(2, summary.Claimed);
        Assert.Equal(NotificationStatus.Sent, oldest.Status);
        Assert.Equal(NotificationStatus.Sent, middle.Status);
        Assert.Equal(NotificationStatus.Pending, newest.Status);
    }

    [Fact]
    public async Task Logs_ids_but_never_the_email_the_reference_or_the_text()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, booking, _) = Arrange();
        var logger = new ListLogger<DispatchNotifications>();
        world.Sender.Enqueue(SendResult.Transient("brevo_429"));

        await world.DispatchNotifications(logger).ExecuteAsync(ct);

        var text = logger.AllText;
        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain("thandi@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain(booking.Reference.Value, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hello Thandi", text, StringComparison.Ordinal);
    }
}
