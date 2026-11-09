using BranchBooking.Api.Configuration;
using BranchBooking.Api.Http;
using BranchBooking.Application.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BranchBooking.Api.Controllers;

/// <summary>What was queued or sent for a booking. Private to the holder of the reference, so never cached.</summary>
[ApiController]
[Route("api/v1/bookings/{reference}/messages")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BookingMessagesController(ListBookingMessages listBookingMessages) : ControllerBase
{
    /// <summary>
    /// The notifications for a booking, oldest first. With the simulated channel the subject and body of a sent
    /// message are included, so the web app can show what was "emailed".
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType<IReadOnlyList<NotificationMessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NotificationMessageDto>>> Get(string reference, CancellationToken cancellationToken)
    {
        var result = await listBookingMessages.ExecuteAsync(reference, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ProblemFor(result.Error);
        }

        return Ok(result.Value);
    }
}
