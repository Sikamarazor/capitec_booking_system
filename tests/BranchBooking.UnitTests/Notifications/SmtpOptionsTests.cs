using BranchBooking.Infrastructure.Notifications;
using Xunit;

namespace BranchBooking.UnitTests.Notifications;

public class SmtpOptionsTests
{
    private static SmtpOptions Mailpit() => new()
    {
        Host = "mailpit",
        Port = 1025,
        Security = SmtpSecurity.None,
        SenderEmail = "noreply@branchbooking.example",
    };

    [Fact]
    public void A_mailpit_style_set_without_credentials_is_valid()
    {
        Assert.True(Mailpit().IsValid());
    }

    [Fact]
    public void A_real_server_set_with_credentials_over_starttls_is_valid()
    {
        var options = Mailpit();
        options.Host = "smtp.example.com";
        options.Port = 587;
        options.Security = SmtpSecurity.StartTls;
        options.Username = "user";
        options.Password = "password-not-real";

        Assert.True(options.IsValid());
    }

    [Fact]
    public void The_defaults_alone_are_not_valid_because_the_host_and_sender_are_missing()
    {
        Assert.False(new SmtpOptions().IsValid());
    }

    [Theory]
    [InlineData("", 1025, "noreply@branchbooking.example", "BranchBooking", 10)]
    [InlineData("mailpit", 0, "noreply@branchbooking.example", "BranchBooking", 10)]
    [InlineData("mailpit", 65536, "noreply@branchbooking.example", "BranchBooking", 10)]
    [InlineData("mailpit", 1025, "", "BranchBooking", 10)]
    [InlineData("mailpit", 1025, "not-an-address", "BranchBooking", 10)]
    [InlineData("mailpit", 1025, "noreply@branchbooking.example", "", 10)]
    [InlineData("mailpit", 1025, "noreply@branchbooking.example", "BranchBooking", 0)]
    [InlineData("mailpit", 1025, "noreply@branchbooking.example", "BranchBooking", 61)]
    public void An_incomplete_or_out_of_range_set_is_invalid(string host, int port, string sender, string name, int timeout)
    {
        var options = new SmtpOptions
        {
            Host = host,
            Port = port,
            Security = SmtpSecurity.None,
            SenderEmail = sender,
            SenderName = name,
            TimeoutSeconds = timeout,
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void An_unknown_security_value_is_invalid()
    {
        var options = Mailpit();
        options.Security = (SmtpSecurity)99;

        Assert.False(options.IsValid());
    }

    [Theory]
    [InlineData("user", "")]
    [InlineData("", "password-not-real")]
    public void A_username_without_a_password_or_the_reverse_is_invalid(string username, string password)
    {
        var options = Mailpit();
        options.Security = SmtpSecurity.StartTls;
        options.Username = username;
        options.Password = password;

        Assert.False(options.IsValid());
    }

    [Fact]
    public void Credentials_are_refused_without_encryption_so_a_password_is_never_sent_in_clear_text()
    {
        var options = Mailpit();
        options.Security = SmtpSecurity.None;
        options.Username = "user";
        options.Password = "password-not-real";

        Assert.False(options.IsValid());
    }
}
