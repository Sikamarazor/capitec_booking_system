using System.Globalization;
using System.Net;
using System.Text;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using Microsoft.Extensions.Options;

namespace BranchBooking.Application.Notifications;

public sealed record RenderedMessage(string Subject, string TextBody, string HtmlBody);

/// <summary>
/// Writes the email for a message from the booking as it is now. Times are shown on the branch's wall clock.
/// Plain text and a small, self-contained HTML version (every value encoded, nothing loaded from elsewhere).
/// The email address and phone number are never included.
/// </summary>
public sealed class NotificationContentBuilder(IOptions<NotificationOptions> options)
{
    public RenderedMessage Build(NotificationMessage message, Booking booking)
    {
        var settings = options.Value;
        var branch = booking.Branch;
        var type = booking.AppointmentType;

        var (subject, intro) = message.Kind switch
        {
            NotificationKind.Confirmation => ("Your branch appointment is confirmed", "Your appointment is confirmed."),
            NotificationKind.Cancellation => ("Your branch appointment was cancelled", "Your appointment has been cancelled."),
            NotificationKind.Reschedule => ("Your branch appointment has been rescheduled", "Your appointment has moved to the time below."),
            NotificationKind.Reminder => ("Reminder: your branch appointment", "This is a reminder of your upcoming appointment."),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Kind, "Unknown notification kind."),
        };

        var when = branch.ToLocal(booking.StartsAtUtc)
            .ToString("dddd, d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture) + " (branch time)";

        var details = new List<(string Label, string Value)>
        {
            ("Reference", booking.Reference.Value),
            ("Appointment", type.Name),
            ("Branch", $"{branch.Name}, {branch.AddressLine}, {branch.City}"),
            ("When", when),
        };

        if (message.Kind != NotificationKind.Cancellation)
        {
            details.Add(("What to bring", type.WhatToBring));
        }

        var baseUrl = settings.PublicBaseUrl.TrimEnd('/');
        var link = message.Kind == NotificationKind.Cancellation ? baseUrl : baseUrl + settings.ManagePath;
        var closingBefore = message.Kind == NotificationKind.Cancellation
            ? "You can book a new appointment at "
            : "Keep your reference safe: you need it to view, change or cancel your booking at ";

        var greeting = $"Hello {booking.Customer.FullName},";

        var text = new StringBuilder()
            .AppendLine(greeting)
            .AppendLine()
            .AppendLine(intro)
            .AppendLine();
        foreach (var (label, value) in details)
        {
            text.AppendLine($"{label}: {value}");
        }

        text.AppendLine()
            .AppendLine($"{closingBefore}{link}.")
            .AppendLine()
            .Append(settings.BrandName);

        var html = new StringBuilder()
            .Append($"<p>{Encode(greeting)}</p>")
            .Append($"<p>{Encode(intro)}</p>")
            .Append("<table role=\"presentation\" cellpadding=\"4\" cellspacing=\"0\">");
        foreach (var (label, value) in details)
        {
            html.Append($"<tr><th align=\"left\" valign=\"top\">{Encode(label)}</th><td>{Encode(value)}</td></tr>");
        }

        html.Append("</table>")
            .Append($"<p>{Encode(closingBefore)}<a href=\"{Encode(link)}\">{Encode(link)}</a>.</p>")
            .Append($"<p>{Encode(settings.BrandName)}</p>");

        return new RenderedMessage(subject, text.ToString(), html.ToString());
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
