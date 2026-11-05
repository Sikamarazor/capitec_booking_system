using BranchBooking.Domain.Enums;
using BranchBooking.Domain.Exceptions;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.Domain.Entities;

public sealed class Booking
{
    public const int CancellationReasonMaxLength = 500;

    private Booking()
    {
        // Required by EF Core.
    }

    /// <summary>Internal key. Never shown to customers; they use <see cref="Reference"/>.</summary>
    public Guid Id { get; private set; }

    public BookingReference Reference { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid AppointmentTypeId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    /// <summary>Position within the slot, from 1 to the branch's slot capacity.</summary>
    public int SlotNumber { get; private set; }

    public CustomerContact Customer { get; private set; } = null!;

    public BookingStatus Status { get; private set; }

    public int RescheduleCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>). Maintained by the database.</summary>
    public uint Version { get; private set; }

    public Branch Branch { get; private set; } = null!;

    public AppointmentType AppointmentType { get; private set; } = null!;

    public TimeSlot Slot => new(StartsAtUtc, EndsAtUtc);

    public static Booking Create(
        BookingReference reference,
        Guid branchId,
        Guid appointmentTypeId,
        TimeSlot slot,
        int slotNumber,
        CustomerContact customer,
        DateTimeOffset nowUtc)
    {
        if (reference == default)
        {
            throw new DomainException("A booking reference is required.");
        }

        if (branchId == Guid.Empty || appointmentTypeId == Guid.Empty)
        {
            throw new DomainException("A branch and an appointment type are required.");
        }

        ArgumentNullException.ThrowIfNull(customer);
        EnsureSlotNumber(slotNumber);
        EnsureInFuture(slot, nowUtc);

        return new Booking
        {
            Id = Guid.CreateVersion7(),
            Reference = reference,
            BranchId = branchId,
            AppointmentTypeId = appointmentTypeId,
            StartsAtUtc = slot.StartsAtUtc,
            EndsAtUtc = slot.EndsAtUtc,
            SlotNumber = slotNumber,
            Customer = customer,
            Status = BookingStatus.Confirmed,
            RescheduleCount = 0,
            CreatedAtUtc = nowUtc.ToUniversalTime(),
            UpdatedAtUtc = nowUtc.ToUniversalTime(),
        };
    }

    public void Cancel(DateTimeOffset nowUtc, string? reason = null)
    {
        EnsureConfirmed("cancelled");
        EnsureNotStarted(nowUtc, "cancelled");

        var trimmed = reason?.Trim();
        if (trimmed is { Length: > CancellationReasonMaxLength })
        {
            throw new DomainException($"The cancellation reason is at most {CancellationReasonMaxLength} characters.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = nowUtc.ToUniversalTime();
        CancellationReason = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Reschedule(TimeSlot newSlot, int newSlotNumber, DateTimeOffset nowUtc)
    {
        EnsureConfirmed("rescheduled");
        EnsureNotStarted(nowUtc, "rescheduled");
        EnsureSlotNumber(newSlotNumber);
        EnsureInFuture(newSlot, nowUtc);

        if (newSlot.StartsAtUtc == StartsAtUtc && newSlot.EndsAtUtc == EndsAtUtc && newSlotNumber == SlotNumber)
        {
            throw new DomainException("The new time is the same as the current time.");
        }

        StartsAtUtc = newSlot.StartsAtUtc;
        EndsAtUtc = newSlot.EndsAtUtc;
        SlotNumber = newSlotNumber;
        RescheduleCount++;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    private void EnsureConfirmed(string action)
    {
        if (Status != BookingStatus.Confirmed)
        {
            throw new DomainException($"Only a confirmed booking can be {action}.");
        }
    }

    private void EnsureNotStarted(DateTimeOffset nowUtc, string action)
    {
        if (StartsAtUtc <= nowUtc)
        {
            throw new DomainException($"A booking that has already started cannot be {action}.");
        }
    }

    private static void EnsureSlotNumber(int slotNumber)
    {
        if (slotNumber < 1)
        {
            throw new DomainException("Slot number must be 1 or more.");
        }
    }

    private static void EnsureInFuture(TimeSlot slot, DateTimeOffset nowUtc)
    {
        if (slot.StartsAtUtc <= nowUtc)
        {
            throw new DomainException("The appointment must start in the future.");
        }
    }
}
