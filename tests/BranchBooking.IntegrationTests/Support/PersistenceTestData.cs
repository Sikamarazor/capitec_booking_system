using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;

namespace BranchBooking.IntegrationTests.Support;

internal static class PersistenceTestData
{
    /// <summary>Monday 2 November 2026, 09:00 in South Africa (07:00 UTC).</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 7, 0, 0, TimeSpan.Zero);

    /// <summary>Monday to Friday 08:00 to 17:00 and Saturday 08:00 to 13:00, 30-minute slots.</summary>
    public static Branch NewBranch(string code = "TEST-001", string name = "Test Branch", int slotCapacity = 2)
    {
        var branch = Branch.Create(
            Guid.CreateVersion7(), code, name, "1 Test Street", "Johannesburg", "Gauteng", "Africa/Johannesburg", 30, slotCapacity);

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            branch.AddOpeningHours(day, new TimeOnly(8, 0), new TimeOnly(17, 0));
        }

        branch.AddOpeningHours(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(13, 0));
        return branch;
    }

    public static AppointmentType NewType(string code = "TEST_TYPE", string name = "Test type", int sortOrder = 1) =>
        AppointmentType.Create(Guid.CreateVersion7(), code, name, "Description", "Bring ID", sortOrder);

    public static Booking NewBooking(
        Guid branchId, Guid typeId, DateTimeOffset startsAtUtc, int slotNumber = 1, BookingReference? reference = null) =>
        Booking.Create(
            reference ?? BookingReference.Generate(),
            branchId,
            typeId,
            new TimeSlot(startsAtUtc, startsAtUtc.AddMinutes(30)),
            slotNumber,
            CustomerContact.Create("Thandi Nkosi", "thandi@example.com", "082 123 4567"),
            Now);
}
