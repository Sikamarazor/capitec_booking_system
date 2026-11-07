using BranchBooking.Application.Notifications;
using BranchBooking.Domain.Enums;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class NotificationContentBuilderTests
{
    private static RenderedMessage Render(TestWorld world, NotificationKind kind, out string reference)
    {
        var booking = world.AddConfirmedBooking(TestWorld.TuesdayEightAmUtc);
        reference = booking.Reference.Value;
        var message = world.AddPendingMessage(booking, kind);
        return world.Content.Build(message, booking);
    }

    [Fact]
    public void A_confirmation_has_everything_the_customer_needs_and_nothing_private()
    {
        var world = new TestWorld();

        var rendered = Render(world, NotificationKind.Confirmation, out var reference);

        Assert.Equal("Your branch appointment is confirmed", rendered.Subject);
        Assert.Contains("Hello Thandi Nkosi,", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains($"Reference: {reference}", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains("Appointment: Test type", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains("Branch: Test Branch, 1 Test Street", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains("When: Tuesday, 3 November 2026 at 08:00 (branch time)", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains("What to bring: Bring ID", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8080/manage", rendered.TextBody, StringComparison.Ordinal);
        Assert.EndsWith("BranchBooking", rendered.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("thandi@example.com", rendered.TextBody + rendered.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("0821234567", rendered.TextBody + rendered.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(reference, rendered.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void The_html_version_carries_the_same_facts()
    {
        var world = new TestWorld();

        var rendered = Render(world, NotificationKind.Confirmation, out var reference);

        Assert.Contains(reference, rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Tuesday, 3 November 2026 at 08:00", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("<a href=\"http://localhost:8080/manage\">", rendered.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cancellation_offers_a_new_booking_and_leaves_out_what_to_bring()
    {
        var world = new TestWorld();

        var rendered = Render(world, NotificationKind.Cancellation, out _);

        Assert.Equal("Your branch appointment was cancelled", rendered.Subject);
        Assert.Contains("Your appointment has been cancelled.", rendered.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("What to bring", rendered.TextBody, StringComparison.Ordinal);
        Assert.Contains("book a new appointment at http://localhost:8080.", rendered.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reschedule_says_the_appointment_has_moved()
    {
        var world = new TestWorld();

        var rendered = Render(world, NotificationKind.Reschedule, out _);

        Assert.Equal("Your branch appointment has been rescheduled", rendered.Subject);
        Assert.Contains("moved to the time below", rendered.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reminder_is_labelled_as_one()
    {
        var world = new TestWorld();

        var rendered = Render(world, NotificationKind.Reminder, out _);

        Assert.Equal("Reminder: your branch appointment", rendered.Subject);
        Assert.Contains("reminder of your upcoming appointment", rendered.TextBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotificationKind.Confirmation)]
    [InlineData(NotificationKind.Cancellation)]
    [InlineData(NotificationKind.Reschedule)]
    [InlineData(NotificationKind.Reminder)]
    public void Every_kind_has_a_short_subject_and_both_bodies(NotificationKind kind)
    {
        var world = new TestWorld();

        var rendered = Render(world, kind, out _);

        Assert.InRange(rendered.Subject.Length, 1, 200);
        Assert.False(string.IsNullOrWhiteSpace(rendered.TextBody));
        Assert.False(string.IsNullOrWhiteSpace(rendered.HtmlBody));
    }

    [Fact]
    public void Values_are_encoded_in_the_html_and_left_alone_in_the_text()
    {
        var world = new TestWorld(notificationOptions: new NotificationOptions { BrandName = "<b>Bank</b> & Co" });

        var rendered = Render(world, NotificationKind.Confirmation, out _);

        Assert.Contains("&lt;b&gt;Bank&lt;/b&gt; &amp; Co", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Bank</b>", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.EndsWith("<b>Bank</b> & Co", rendered.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_trailing_slash_on_the_base_url_does_not_double_up()
    {
        var world = new TestWorld(notificationOptions: new NotificationOptions { PublicBaseUrl = "https://book.example.com/" });

        var rendered = Render(world, NotificationKind.Confirmation, out _);

        Assert.Contains("https://book.example.com/manage.", rendered.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("//manage", rendered.TextBody, StringComparison.Ordinal);
    }
}
