using BranchBooking.Application.Abstractions;
using BranchBooking.Application.Common;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.Application.Bookings;

/// <summary>
/// Looks a booking up by its reference. Malformed and unknown references return the same error, so the API
/// does not reveal which references are well-formed. Nothing is logged: the reference is a credential.
/// </summary>
public sealed class GetBooking(IBookingRepository bookings, BookingMapper mapper)
{
    public async Task<Result<BookingDto>> ExecuteAsync(string? reference, CancellationToken cancellationToken)
    {
        if (!BookingReference.TryParse(reference, out var parsed))
        {
            return Result<BookingDto>.Failure(Errors.BookingNotFound);
        }

        var booking = await bookings.GetByReferenceAsync(parsed, cancellationToken);
        if (booking is null)
        {
            return Result<BookingDto>.Failure(Errors.BookingNotFound);
        }

        return Result<BookingDto>.Success(mapper.ToDto(booking));
    }
}
