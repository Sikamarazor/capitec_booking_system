using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.Exceptions;
using BranchBooking.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BranchBooking.Application.Bookings;

public sealed class CreateBooking(
    IBranchRepository branches,
    IAppointmentTypeRepository appointmentTypes,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    SlotResolver slotResolver,
    BookingMapper mapper,
    TimeProvider clock,
    ILogger<CreateBooking> logger)
{
    public async Task<Result<BookingDto>> ExecuteAsync(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        CustomerContact customer;
        try
        {
            customer = CustomerContact.Create(command.FullName, command.Email, command.Phone);
        }
        catch (DomainException ex)
        {
            return Result<BookingDto>.Failure(Errors.InvalidCustomerDetails(ex.Message));
        }

        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null || !branch.IsActive)
        {
            return Result<BookingDto>.Failure(Errors.BranchNotFound);
        }

        var type = await appointmentTypes.GetByIdAsync(command.AppointmentTypeId, cancellationToken);
        if (type is null || !type.IsActive)
        {
            return Result<BookingDto>.Failure(Errors.AppointmentTypeNotFound);
        }

        var resolved = slotResolver.Resolve(branch, command.StartsAtUtc);
        if (!resolved.IsSuccess)
        {
            return Result<BookingDto>.Failure(resolved.Error);
        }

        var slot = resolved.Value;

        // Every failed attempt means another request gained a position, so capacity + 1 attempts always
        // end in either success or "full" (decision D7).
        var maxAttempts = branch.SlotCapacity + 1;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var taken = await bookings.GetConfirmedPositionsAsync(branch.Id, slot.StartsAtUtc, slot.EndsAtUtc, cancellationToken);
            var position = SlotPositions.FirstFree(taken, slot.StartsAtUtc, branch.SlotCapacity);
            if (position is null)
            {
                return Result<BookingDto>.Failure(Errors.SlotNotAvailable);
            }

            var booking = Booking.Create(
                BookingReference.Generate(), branch.Id, type.Id, slot, position.Value, customer, clock.GetUtcNow());
            bookings.Add(booking);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (SlotAlreadyBookedException)
            {
                continue;   // Another request took that position first: read again and pick the next free one.
            }
            catch (DuplicateBookingReferenceException)
            {
                continue;   // Vanishingly unlikely; the next attempt generates a new reference.
            }

            logger.LogInformation(
                "Booking {BookingId} created at branch {BranchCode} for {AppointmentTypeCode}",
                booking.Id, branch.Code, type.Code);

            return Result<BookingDto>.Success(mapper.ToDto(booking, branch, type));
        }

        return Result<BookingDto>.Failure(Errors.SlotNotAvailable);
    }
}
