using BranchBooking.Api.Contracts;
using BranchBooking.Api.Http;
using BranchBooking.Application.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Controllers;

/// <summary>Booking responses are private to the customer who holds the reference, so they are never cached.</summary>
[ApiController]
[Route("api/v1/bookings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BookingsController(CreateBooking createBooking) : ControllerBase
{
    /// <summary>Books a branch appointment and returns the confirmation with its reference.</summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateBookingCommand(
            request.BranchId!.Value,
            request.AppointmentTypeId!.Value,
            request.StartsAtUtc!.Value,
            request.FullName!,
            request.Email!,
            request.Phone!);

        var result = await createBooking.ExecuteAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ProblemFor(result.Error);
        }

        return Created($"/api/v1/bookings/{result.Value.Reference}", result.Value);
    }
}
