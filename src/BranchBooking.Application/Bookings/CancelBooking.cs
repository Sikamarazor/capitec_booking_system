using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BranchBooking.Application.Bookings;

public sealed class CancelBooking(
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    BookingPolicy policy,
    BookingMapper mapper,
    TimeProvider clock,
    ILogger<CancelBooking> logger)
{
    private const int MaxAttempts = 3;

    public async Task<Result<BookingDto>> ExecuteAsync(string? reference, string? reason, CancellationToken cancellationToken)
    {
        if (!BookingReference.TryParse(reference, out var parsed))
        {
            return Result<BookingDto>.Failure(Errors.BookingNotFound);
        }

        if (reason?.Trim().Length > Booking.CancellationReasonMaxLength)
        {
            return Result<BookingDto>.Failure(Errors.InvalidCancellationReason(Booking.CancellationReasonMaxLength));
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // A conflict clears the change tracker, so every attempt starts from what the database holds now.
            var booking = await bookings.GetByReferenceAsync(parsed, cancellationToken);
            if (booking is null)
            {
                return Result<BookingDto>.Failure(Errors.BookingNotFound);
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return Result<BookingDto>.Success(mapper.ToDto(booking));   // Idempotent: already done.
            }

            if (!policy.CanChange(booking))
            {
                return Result<BookingDto>.Failure(Errors.ChangeWindowClosed(policy.ChangeCutoffHours));
            }

            booking.Cancel(clock.GetUtcNow(), reason);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                continue;
            }

            logger.LogInformation("Booking {BookingId} cancelled", booking.Id);
            return Result<BookingDto>.Success(mapper.ToDto(booking));
        }

        return Result<BookingDto>.Failure(Errors.ConcurrentChange);
    }
}
