using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Enums;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BranchBooking.Application.Bookings;

public sealed class RescheduleBooking(
    IBookingRepository bookings,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    BookingPolicy policy,
    SlotResolver slotResolver,
    BookingMapper mapper,
    TimeProvider clock,
    ILogger<RescheduleBooking> logger)
{
    private const int MaxConcurrencyRetries = 2;

    public async Task<Result<BookingDto>> ExecuteAsync(string? reference, DateTimeOffset newStartsAtUtc, CancellationToken cancellationToken)
    {
        if (!BookingReference.TryParse(reference, out var parsed))
        {
            return Result<BookingDto>.Failure(Errors.BookingNotFound);
        }

        var slotFailures = 0;
        var concurrencyFailures = 0;

        while (true)
        {
            // A failed save clears the change tracker, so each pass reloads the booking as the database holds it now.
            var booking = await bookings.GetByReferenceAsync(parsed, cancellationToken);
            if (booking is null)
            {
                return Result<BookingDto>.Failure(Errors.BookingNotFound);
            }

            if (booking.Status != BookingStatus.Confirmed)
            {
                return Result<BookingDto>.Failure(Errors.BookingNotActive);
            }

            if (!policy.CanChange(booking))
            {
                return Result<BookingDto>.Failure(Errors.ChangeWindowClosed(policy.ChangeCutoffHours));
            }

            if (!policy.HasRescheduleAllowance(booking))
            {
                return Result<BookingDto>.Failure(Errors.RescheduleLimitReached(policy.MaxReschedules));
            }

            var resolved = slotResolver.Resolve(booking.Branch, newStartsAtUtc);
            if (!resolved.IsSuccess)
            {
                return Result<BookingDto>.Failure(resolved.Error);
            }

            var slot = resolved.Value;
            if (slot.StartsAtUtc == booking.StartsAtUtc)
            {
                return Result<BookingDto>.Failure(Errors.SameSlot);
            }

            var taken = await bookings.GetConfirmedPositionsAsync(booking.BranchId, slot.StartsAtUtc, slot.EndsAtUtc, cancellationToken);
            var position = SlotPositions.FirstFree(taken, slot.StartsAtUtc, booking.Branch.SlotCapacity);
            if (position is null)
            {
                return Result<BookingDto>.Failure(Errors.SlotNotAvailable);
            }

            var now = clock.GetUtcNow();

            booking.Reschedule(slot, position.Value, now);
            notifications.Add(NotificationMessage.ForReschedule(booking, now));


            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (SlotAlreadyBookedException)
            {
                // Another request took that position first: read again and pick the next free one (decision D7).
                if (++slotFailures > booking.Branch.SlotCapacity)
                {
                    return Result<BookingDto>.Failure(Errors.SlotNotAvailable);
                }

                continue;
            }
            catch (ConcurrencyConflictException)
            {
                if (++concurrencyFailures > MaxConcurrencyRetries)
                {
                    return Result<BookingDto>.Failure(Errors.ConcurrentChange);
                }

                continue;
            }

            logger.LogInformation("Booking {BookingId} rescheduled (change {RescheduleCount})", booking.Id, booking.RescheduleCount);
            return Result<BookingDto>.Success(mapper.ToDto(booking));
        }
    }
}
