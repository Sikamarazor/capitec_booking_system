using BranchBooking.Api.Contracts;
using BranchBooking.Api.Http;
using BranchBooking.Application.Bookings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using BranchBooking.Api.Configuration;
using Microsoft.AspNetCore.RateLimiting;


namespace BranchBooking.Api.Controllers;

/// <summary>Booking responses are private to the customer who holds the reference, so they are never cached.</summary>
[ApiController]
[Route("api/v1/bookings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BookingsController(
    CreateBooking createBooking,
    GetBooking getBooking,
    CancelBooking cancelBooking,
    RescheduleBooking rescheduleBooking) : ControllerBase
{
    /// <summary>Books a branch appointment and returns the confirmation with its reference.</summary>
    [EnableRateLimiting(RateLimitPolicies.Write)]
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

    /// <summary>Shows a booking. Any casing, and with or without hyphens, is accepted for the reference.</summary>
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [HttpGet("{reference}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Get(string reference, CancellationToken cancellationToken)
    {
        var result = await getBooking.ExecuteAsync(reference, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ProblemFor(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>Cancels a booking and frees its place. Cancelling twice is harmless. The body is optional.</summary>
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HttpPost("{reference}/cancel")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Cancel(
        string reference,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelBookingRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await cancelBooking.ExecuteAsync(reference, request?.Reason, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ProblemFor(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>Moves a booking to another time at the same branch, for the same appointment type.</summary>
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HttpPost("{reference}/reschedule")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingDto>> Reschedule(
        string reference, RescheduleBookingRequest request, CancellationToken cancellationToken)
    {
        var result = await rescheduleBooking.ExecuteAsync(reference, request.NewStartsAtUtc!.Value, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ProblemFor(result.Error);
        }

        return Ok(result.Value);
    }
}
