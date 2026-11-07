using BranchBooking.Infrastructure.Notifications;
using MimeKit;

namespace BranchBooking.UnitTests.Support;

/// <summary>Records every message it is given and optionally behaves like a server that fails.</summary>
internal sealed class FakeSmtpTransport : ISmtpTransport
{
    public List<MimeMessage> Sent { get; } = [];

    /// <summary>Runs after the message is recorded; throw from it to simulate a failure, or await to simulate a slow server.</summary>
    public Func<CancellationToken, Task>? Behaviour { get; set; }

    public static FakeSmtpTransport Failing(Exception failure) => new() { Behaviour = _ => Task.FromException(failure) };

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);

        if (Behaviour is not null)
        {
            await Behaviour(cancellationToken);
        }
    }
}
