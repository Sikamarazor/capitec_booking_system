using System.Net.Sockets;
using BranchBooking.Application.Notifications;
using BranchBooking.Infrastructure.Notifications;
using BranchBooking.UnitTests.Support;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class SmtpEmailSenderTests
{
    private static readonly OutgoingEmail Email = new(
        Guid.CreateVersion7(), "thandi@example.com", "Thandi Nkosi", "A subject", "A text body", "<p>A html body</p>");

    private static SmtpEmailSender NewSender(ISmtpTransport transport, int timeoutSeconds = 10) =>
        new(transport, Options.Create(new SmtpOptions
        {
            Host = "mailpit",
            Port = 1025,
            Security = SmtpSecurity.None,
            SenderEmail = "sender@example.com",
            SenderName = "Test Sender",
            TimeoutSeconds = timeoutSeconds,
        }));

    private static SmtpCommandException Refusal(int status) =>
        new(SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)status, "the server said thandi@example.com is unknown");

    [Fact]
    public void Identifies_itself_and_keeps_no_content()
    {
        var sender = NewSender(new FakeSmtpTransport());

        Assert.Equal("Smtp", sender.Channel);
        Assert.False(sender.RetainsContent);
    }

    [Fact]
    public async Task Sends_one_message_from_the_sender_to_the_customer_with_the_subject()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new FakeSmtpTransport();

        await NewSender(transport).SendAsync(Email, ct);

        var message = Assert.Single(transport.Sent);
        var from = Assert.Single(message.From.Mailboxes);
        Assert.Equal("sender@example.com", from.Address);
        Assert.Equal("Test Sender", from.Name);
        var to = Assert.Single(message.To.Mailboxes);
        Assert.Equal("thandi@example.com", to.Address);
        Assert.Equal("Thandi Nkosi", to.Name);
        Assert.Equal("A subject", message.Subject);
    }

    [Fact]
    public async Task Carries_both_the_text_and_the_html_version()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new FakeSmtpTransport();

        await NewSender(transport).SendAsync(Email, ct);

        var message = transport.Sent.Single();
        Assert.Equal("A text body", message.TextBody?.TrimEnd());
        Assert.Equal("<p>A html body</p>", message.HtmlBody?.TrimEnd());
    }

    [Fact]
    public async Task The_message_id_is_the_outbox_id_at_the_senders_domain_and_is_reported_as_the_provider_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new FakeSmtpTransport();
        var expected = $"{Email.MessageId:N}@example.com";

        var result = await NewSender(transport).SendAsync(Email, ct);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.ProviderMessageId);
        Assert.Equal(expected, transport.Sent.Single().MessageId);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task An_address_that_cannot_be_parsed_is_permanent_and_nothing_is_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new FakeSmtpTransport();

        var result = await NewSender(transport).SendAsync(Email with { ToEmail = "no-at-sign" }, ct);

        Assert.False(result.Succeeded);
        Assert.False(result.IsTransient);
        Assert.Equal("invalid_recipient", result.Error);
        Assert.Empty(transport.Sent);
    }

    [Theory]
    [InlineData(421)]
    [InlineData(450)]
    [InlineData(451)]
    [InlineData(452)]
    public async Task A_4xx_reply_is_transient_and_keeps_only_the_code(int status)
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await NewSender(FakeSmtpTransport.Failing(Refusal(status))).SendAsync(Email, ct);

        Assert.False(result.Succeeded);
        Assert.True(result.IsTransient);
        Assert.Equal($"smtp_{status}", result.Error);
        Assert.DoesNotContain("thandi", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(550)]
    [InlineData(553)]
    [InlineData(554)]
    public async Task A_5xx_reply_is_permanent_and_keeps_only_the_code(int status)
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await NewSender(FakeSmtpTransport.Failing(Refusal(status))).SendAsync(Email, ct);

        Assert.False(result.Succeeded);
        Assert.False(result.IsTransient);
        Assert.Equal($"smtp_{status}", result.Error);
        Assert.DoesNotContain("thandi", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reply_outside_the_failure_range_is_permanent_and_generic()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await NewSender(FakeSmtpTransport.Failing(Refusal(250))).SendAsync(Email, ct);

        Assert.False(result.IsTransient);
        Assert.Equal("smtp_error", result.Error);
    }

    [Fact]
    public async Task Refused_credentials_are_permanent_and_hide_the_exception_text()
    {
        var ct = TestContext.Current.CancellationToken;
        var failure = new MailKit.Security.AuthenticationException("the password hunter2 was refused");

        var result = await NewSender(FakeSmtpTransport.Failing(failure)).SendAsync(Email, ct);

        Assert.False(result.IsTransient);
        Assert.Equal("smtp_auth_failed", result.Error);
    }

    [Fact]
    public async Task A_protocol_error_is_transient()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await NewSender(FakeSmtpTransport.Failing(new SmtpProtocolException("unexpected reply"))).SendAsync(Email, ct);

        Assert.True(result.IsTransient);
        Assert.Equal("smtp_protocol_error", result.Error);
    }

    [Theory]
    [InlineData(typeof(SocketException))]
    [InlineData(typeof(IOException))]
    public async Task A_network_failure_is_transient_and_hides_the_exception_text(Type failureType)
    {
        var ct = TestContext.Current.CancellationToken;
        var failure = failureType == typeof(IOException)
            ? new IOException("lost the connection while sending to thandi@example.com")
            : (Exception)new SocketException();

        var result = await NewSender(FakeSmtpTransport.Failing(failure)).SendAsync(Email, ct);

        Assert.True(result.IsTransient);
        Assert.Equal("network_error", result.Error);
    }

    [Fact]
    public async Task A_timeout_reported_by_the_transport_is_transient()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await NewSender(FakeSmtpTransport.Failing(new TimeoutException())).SendAsync(Email, ct);

        Assert.True(result.IsTransient);
        Assert.Equal("timeout", result.Error);
    }

    [Fact]
    public async Task A_server_that_never_answers_is_cut_off_after_the_configured_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new FakeSmtpTransport { Behaviour = token => Task.Delay(Timeout.Infinite, token) };

        // Takes about one second: the smallest allowed TimeoutSeconds.
        var result = await NewSender(transport, timeoutSeconds: 1).SendAsync(Email, ct);

        Assert.True(result.IsTransient);
        Assert.Equal("timeout", result.Error);
    }

    [Fact]
    public async Task Cancellation_requested_by_the_caller_is_not_swallowed()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var transport = new FakeSmtpTransport { Behaviour = token => Task.Delay(Timeout.Infinite, token) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewSender(transport).SendAsync(Email, cancelled.Token));
    }

    [Fact]
    public async Task An_unexpected_exception_is_left_for_the_dispatcher_to_record()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSender(FakeSmtpTransport.Failing(new InvalidOperationException("boom"))).SendAsync(Email, ct));
    }
}
