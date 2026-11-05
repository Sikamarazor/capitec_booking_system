namespace BranchBooking.Domain.Entities;

/// <summary>Local opening window for one weekday. A weekday with no row means the branch is closed.</summary>
public sealed class BranchOpeningHours
{
    private BranchOpeningHours()
    {
        // Required by EF Core.
    }

    public Guid BranchId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    internal static BranchOpeningHours Create(Guid branchId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt) =>
        new() { BranchId = branchId, DayOfWeek = dayOfWeek, OpensAt = opensAt, ClosesAt = closesAt };
}
