using System.ComponentModel.DataAnnotations;

namespace BranchBooking.Api.Contracts;

public sealed class RescheduleBookingRequest
{
    /// <summary>The new slot start exactly as returned by the availability endpoint.</summary>
    [Required]
    public DateTimeOffset? NewStartsAtUtc { get; init; }
}
