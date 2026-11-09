using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.Application.Notifications;

/// <summary>What was queued or sent for a booking. The reference is the credential; nothing is logged.</summary>
public sealed class ListBookingMessages(IBookingRepository bookings, INotificationRepository notifications)
{
    public async Task<Result<IReadOnlyList<NotificationMessageDto>>> ExecuteAsync(string? reference, CancellationToken cancellationToken)
    {
        if (!BookingReference.TryParse(reference, out var parsed))
        {
            return Result<IReadOnlyList<NotificationMessageDto>>.Failure(Errors.BookingNotFound);
        }

        var booking = await bookings.GetByReferenceAsync(parsed, cancellationToken);
        if (booking is null)
        {
            return Result<IReadOnlyList<NotificationMessageDto>>.Failure(Errors.BookingNotFound);
        }

        var messages = await notifications.ListForBookingAsync(booking.Id, cancellationToken);

        IReadOnlyList<NotificationMessageDto> dtos = messages
            .Select(m => new NotificationMessageDto(
                m.Kind.ToString(),
                m.Status.ToString(),
                m.Channel,
                m.CreatedAtUtc,
                m.SentAtUtc,
                m.Status == NotificationStatus.Sent ? m.RenderedSubject : null,
                m.Status == NotificationStatus.Sent ? m.RenderedBody : null))
            .ToList();

        return Result<IReadOnlyList<NotificationMessageDto>>.Success(dtos);
    }
}
