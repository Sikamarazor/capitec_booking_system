namespace BranchBooking.Api.Contracts;

/// <summary>Optional body for a cancellation. The length limit is enforced by the use case.</summary>
public sealed class CancelBookingRequest
{
    public string? Reason { get; init; }
}
