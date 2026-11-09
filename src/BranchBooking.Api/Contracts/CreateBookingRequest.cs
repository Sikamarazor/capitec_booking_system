using System.ComponentModel.DataAnnotations;

namespace BranchBooking.Api.Contracts;

/// <summary>Shape checks only; the business rules are enforced by the use case.</summary>
public sealed class CreateBookingRequest
{
    [Required]
    public Guid? BranchId { get; init; }

    [Required]
    public Guid? AppointmentTypeId { get; init; }

    /// <summary>The slot start exactly as returned by the availability endpoint, for example <c>2026-11-03T06:00:00Z</c>.</summary>
    [Required]
    public DateTimeOffset? StartsAtUtc { get; init; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string? FullName { get; init; }

    [Required, StringLength(254), EmailAddress]
    public string? Email { get; init; }

    [Required, StringLength(20, MinimumLength = 9)]
    public string? Phone { get; init; }
}
