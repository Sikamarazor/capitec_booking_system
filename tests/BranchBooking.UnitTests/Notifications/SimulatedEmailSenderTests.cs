using BranchBooking.Application.Notifications;
using BranchBooking.Infrastructure.Notifications;
using BranchBooking.UnitTests.Support;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class SimulatedEmailSenderTests
{
    private static OutgoingEmail NewEmail(Guid? id = null) =>
        new(id ?? Guid.CreateVersion7(), "thandi@example.com", "Thandi Nkosi", "A subject", "A text body", "<p>A html body</p>");

    [Fact]
    public async Task Succeeds_with_a_provider_id_made_from_the_message_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var sender = new SimulatedEmailSender(new ListLogger<SimulatedEmailSender>());

        var result = await sender.SendAsync(NewEmail(id), ct);

        Assert.True(result.Succeeded);
        Assert.Equal($"sim-{id:N}", result.ProviderMessageId);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Identifies_itself_and_asks_for_content_to_be_kept()
    {
        var sender = new SimulatedEmailSender(new ListLogger<SimulatedEmailSender>());

        Assert.Equal("Simulated", sender.Channel);
        Assert.True(sender.RetainsContent);
    }

    [Fact]
    public async Task Logs_the_message_id_but_not_the_address_subject_or_body()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new ListLogger<SimulatedEmailSender>();
        var sender = new SimulatedEmailSender(logger);
        var email = NewEmail();

        await sender.SendAsync(email, ct);

        var text = logger.AllText;
        Assert.Contains(email.MessageId.ToString(), text, StringComparison.Ordinal);
        Assert.DoesNotContain("thandi@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("A subject", text, StringComparison.Ordinal);
        Assert.DoesNotContain("A text body", text, StringComparison.Ordinal);
    }
}
