using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.UnitTests.Support;

internal static class DomainTestData
{
    /// <summary>Monday 2 November 2026, 09:00 in South Africa (07:00 UTC).</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 7, 0, 0, TimeSpan.Zero);

    public static Branch NewBranch(int slotLengthMinutes = 30, int slotCapacity = 2)
    {
        var branch = Branch.Create(
            Guid.CreateVersion7(),
            "TEST-001",
            "Test Branch",
            "1 Test Street",
            "Johannesburg",
            "Gauteng",
            "Africa/Johannesburg",
            slotLengthMinutes,
            slotCapacity);

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            branch.AddOpeningHours(day, new TimeOnly(8, 0), new TimeOnly(17, 0));
        }

        branch.AddOpeningHours(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(13, 0));
        return branch;
    }

    public static CustomerContact NewCustomer() =>
        CustomerContact.Create("Thandi Nkosi", "thandi@example.com", "082 123 4567");

    public static TimeSlot NewSlot(DateTimeOffset startsAtUtc, int minutes = 30) =>
        new(startsAtUtc, startsAtUtc.AddMinutes(minutes));

    /// <summary>A confirmed booking starting one day after <see cref="Now"/> unless told otherwise.</summary>
    public static Booking NewBooking(DateTimeOffset? startsAtUtc = null, int slotNumber = 1) =>
        Booking.Create(
            BookingReference.Generate(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            NewSlot(startsAtUtc ?? Now.AddDays(1)),
            slotNumber,
            NewCustomer(),
            Now);
}
