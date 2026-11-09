using System.Net.Http.Json;
using System.Text.Json;
using BranchBooking.Application.Notifications;
using BranchBooking.Domain.Enums;
using BranchBooking.Infrastructure.Notifications;
using BranchBooking.IntegrationTests.Support;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Xunit;

namespace BranchBooking.IntegrationTests.Notifications;

[Collection(DatabaseCollection.Name)]
public class SmtpDispatchTests(PostgresFixture db, DatabaseApiFactory factory)
{
    private static readonly DateTimeOffset TuesdayEightAm = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    /// <summary>Remembers every message and fails with a chosen exception, or accepts.</summary>
    private sealed class RecordingTransport(Exception? failure = null) : ISmtpTransport
    {
        public List<MimeMessage> Sent { get; } = [];

        public Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private static SmtpCommandException Refusal(int status) =>
        new(SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)status, "the server said thandi@example.com is unknown");

    private async Task<string> BookAsync(Guid branchId, Guid typeId)
    {
        factory.Clock.Set(PersistenceTestData.Now);
        using var client = factory.CreateClient();
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

    private async Task<DispatchSummary> DispatchAsync(ISmtpTransport transport)
    {
        await using var provider = WorkerTestHost.Build(
            factory.Clock,
            services => services.AddSingleton(transport),
            ("Notifications:Channel", "Smtp"),
            ("Notifications:Smtp:Host", "localhost"),
            ("Notifications:Smtp:Port", "1025"),
            ("Notifications:Smtp:Security", "None"),
            ("Notifications:Smtp:SenderEmail", "sender@example.com"));

        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchNotifications>().ExecuteAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_accepted_message_keeps_the_message_id_but_not_the_text()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        var reference = await BookAsync(branch.Id, type.Id);
        var transport = new RecordingTransport();

        var summary = await DispatchAsync(transport);

        Assert.Equal(new DispatchSummary(1, 1, 0, 0, 0), summary);
        var sent = Assert.Single(transport.Sent);
        Assert.Contains(reference, sent.TextBody, StringComparison.Ordinal);
        Assert.Equal("thandi@example.com", sent.To.Mailboxes.Single().Address);

        await using var read = db.CreateContext();
        var message = await read.NotificationMessages.SingleAsync(ct);
        Assert.Equal(NotificationStatus.Sent, message.Status);
        Assert.Equal("Smtp", message.Channel);
        Assert.Equal($"{message.Id:N}@example.com", message.ProviderMessageId);
        Assert.Null(message.RenderedSubject);
        Assert.Null(message.RenderedBody);
    }

    [Fact]
    public async Task A_refused_message_fails_for_good_with_only_a_code_recorded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        await BookAsync(branch.Id, type.Id);

        var summary = await DispatchAsync(new RecordingTransport(Refusal(550)));

        Assert.Equal(new DispatchSummary(1, 0, 0, 0, 1), summary);
        await using var read = db.CreateContext();
        var message = await read.NotificationMessages.SingleAsync(ct);
        Assert.Equal(NotificationStatus.Failed, message.Status);
        Assert.Equal("smtp_550", message.Detail);
        Assert.Null(message.RenderedBody);
    }

    [Fact]
    public async Task A_temporarily_refused_message_waits_and_is_sent_on_a_later_pass()
    {
        var ct = TestContext.Current.CancellationToken;
        var (branch, type) = await db.PrepareAsync();
        await BookAsync(branch.Id, type.Id);

        var busy = await DispatchAsync(new RecordingTransport(Refusal(451)));
        Assert.Equal(1, busy.Retried);

        factory.Clock.Set(PersistenceTestData.Now.AddMinutes(10));
        var later = await DispatchAsync(new RecordingTransport());

        Assert.Equal(1, later.Sent);
        await using var read = db.CreateContext();
        var message = await read.NotificationMessages.SingleAsync(ct);
        Assert.Equal(NotificationStatus.Sent, message.Status);
        Assert.Equal(2, message.Attempts);
    }
}
