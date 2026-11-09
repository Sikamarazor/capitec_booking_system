using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.Exceptions;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Domain;

public class NotificationMessageTests
{
    private static readonly DateTimeOffset Now = DomainTestData.Now;

    private static NotificationMessage NewPending() =>
        NotificationMessage.ForConfirmation(DomainTestData.NewBooking(), Now);

    [Fact]
    public void A_confirmation_starts_pending_and_due_now_with_the_booking_start_as_snapshot()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1));

        var message = NotificationMessage.ForConfirmation(booking, Now);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(booking.Id, message.BookingId);
        Assert.Equal(NotificationKind.Confirmation, message.Kind);
        Assert.Equal($"confirmation:{booking.Id:N}", message.DedupeKey);
        Assert.Equal(booking.StartsAtUtc, message.StartsAtUtc);
        Assert.Equal(NotificationStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Equal(Now, message.NextAttemptAtUtc);
        Assert.Equal(Now, message.CreatedAtUtc);
        Assert.Null(message.SentAtUtc);
        Assert.Null(message.Channel);
    }

    [Fact]
    public void A_cancellation_has_its_own_key()
    {
        var booking = DomainTestData.NewBooking();

        var message = NotificationMessage.ForCancellation(booking, Now);

        Assert.Equal(NotificationKind.Cancellation, message.Kind);
        Assert.Equal($"cancellation:{booking.Id:N}", message.DedupeKey);
    }

    [Fact]
    public void Each_reschedule_gets_a_different_key()
    {
        var booking = DomainTestData.NewBooking(Now.AddDays(1));
        booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(2)), 1, Now);
        var first = NotificationMessage.ForReschedule(booking, Now);
        booking.Reschedule(DomainTestData.NewSlot(Now.AddDays(3)), 1, Now);
        var second = NotificationMessage.ForReschedule(booking, Now);

        Assert.Equal($"reschedule:{booking.Id:N}:1", first.DedupeKey);
        Assert.Equal($"reschedule:{booking.Id:N}:2", second.DedupeKey);
        Assert.Equal(booking.StartsAtUtc, second.StartsAtUtc);
    }

    [Fact]
    public void A_reminder_is_unique_per_booking_and_start_time()
    {
        var bookingId = Guid.CreateVersion7();
        var start = Now.AddDays(1);

        var a = NotificationMessage.ForReminder(bookingId, start, Now);
        var b = NotificationMessage.ForReminder(bookingId, start, Now.AddMinutes(5));
        var moved = NotificationMessage.ForReminder(bookingId, start.AddHours(1), Now);

        Assert.Equal(a.DedupeKey, b.DedupeKey);
        Assert.NotEqual(a.DedupeKey, moved.DedupeKey);
        Assert.Equal(NotificationKind.Reminder, a.Kind);
        Assert.True(a.DedupeKey.Length <= NotificationMessage.DedupeKeyMaxLength);
    }

    [Fact]
    public void A_message_needs_a_booking()
    {
        Assert.Throws<DomainException>(() => NotificationMessage.ForReminder(Guid.Empty, Now.AddDays(1), Now));
    }

    [Fact]
    public void Marking_sent_records_the_channel_time_and_provider_id_and_counts_the_attempt()
    {
        var message = NewPending();
        var later = Now.AddMinutes(1);

        message.MarkSent("Simulated", "sim-1", "Subject", "Body", later);

        Assert.Equal(NotificationStatus.Sent, message.Status);
        Assert.Equal("Simulated", message.Channel);
        Assert.Equal("sim-1", message.ProviderMessageId);
        Assert.Equal("Subject", message.RenderedSubject);
        Assert.Equal("Body", message.RenderedBody);
        Assert.Equal(later, message.SentAtUtc);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.Detail);
        Assert.Null(message.LockedUntilUtc);
    }

    [Fact]
    public void Content_that_is_too_long_is_truncated_rather_than_rejected()
    {
        var message = NewPending();

        message.MarkSent("Simulated", new string('p', 500), new string('s', 500), new string('b', 5000), Now);

        Assert.Equal(NotificationMessage.ProviderMessageIdMaxLength, message.ProviderMessageId!.Length);
        Assert.Equal(NotificationMessage.SubjectMaxLength, message.RenderedSubject!.Length);
        Assert.Equal(NotificationMessage.BodyMaxLength, message.RenderedBody!.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a-channel-name-that-is-far-too-long")]
    public void A_sent_message_needs_a_sensible_channel(string channel)
    {
        var message = NewPending();

        Assert.Throws<DomainException>(() => message.MarkSent(channel, null, null, null, Now));
    }

    [Fact]
    public void A_message_cannot_be_sent_twice_or_after_being_skipped()
    {
        var sent = NewPending();
        sent.MarkSent("Simulated", null, null, null, Now);
        var skipped = NewPending();
        skipped.MarkSkipped("superseded");

        Assert.Throws<DomainException>(() => sent.MarkSent("Simulated", null, null, null, Now));
        Assert.Throws<DomainException>(() => skipped.MarkSent("Simulated", null, null, null, Now));
    }

    [Fact]
    public void Skipping_records_the_reason_and_sends_nothing()
    {
        var message = NewPending();

        message.MarkSkipped("booking_not_confirmed");

        Assert.Equal(NotificationStatus.Skipped, message.Status);
        Assert.Equal("booking_not_confirmed", message.Detail);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.SentAtUtc);
    }

    [Fact]
    public void Skipping_needs_a_reason()
    {
        Assert.Throws<DomainException>(() => NewPending().MarkSkipped("  "));
    }

    [Fact]
    public void A_transient_failure_keeps_the_message_pending_and_schedules_the_next_try()
    {
        var message = NewPending();

        message.RecordFailure("brevo_429", isTransient: true, Now, maxAttempts: 5, retryDelay: TimeSpan.FromSeconds(30));

        Assert.Equal(NotificationStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(Now.AddSeconds(30), message.NextAttemptAtUtc);
        Assert.Equal("brevo_429", message.Detail);
    }

    [Fact]
    public void A_permanent_failure_ends_the_message()
    {
        var message = NewPending();

        message.RecordFailure("brevo_400:invalid_parameter", isTransient: false, Now, maxAttempts: 5, retryDelay: TimeSpan.FromSeconds(30));

        Assert.Equal(NotificationStatus.Failed, message.Status);
        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public void The_last_allowed_attempt_ends_the_message_even_for_a_transient_failure()
    {
        var message = NewPending();

        message.RecordFailure("brevo_500", true, Now, maxAttempts: 2, retryDelay: TimeSpan.FromSeconds(1));
        message.RecordFailure("brevo_500", true, Now, maxAttempts: 2, retryDelay: TimeSpan.FromSeconds(1));

        Assert.Equal(NotificationStatus.Failed, message.Status);
        Assert.Equal(2, message.Attempts);
    }

    [Theory]
    [InlineData("", 5, 30)]
    [InlineData("brevo_500", 0, 30)]
    [InlineData("brevo_500", 5, -1)]
    public void A_failure_needs_a_code_a_positive_attempt_limit_and_a_non_negative_delay(string error, int maxAttempts, int delaySeconds)
    {
        var message = NewPending();

        Assert.Throws<DomainException>(() =>
            message.RecordFailure(error, true, Now, maxAttempts, TimeSpan.FromSeconds(delaySeconds)));
    }

    [Fact]
    public void A_sent_message_cannot_fail_afterwards()
    {
        var message = NewPending();
        message.MarkSent("Simulated", null, null, null, Now);

        Assert.Throws<DomainException>(() => message.RecordFailure("x", true, Now, 5, TimeSpan.Zero));
    }
}
