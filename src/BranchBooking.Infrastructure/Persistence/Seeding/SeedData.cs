using BranchBooking.Domain.Entities;

namespace BranchBooking.Infrastructure.Persistence.Seeding;

/// <summary>Demo reference data. Each call returns new instances with new ids.</summary>
public static class SeedData
{
    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public static IReadOnlyList<AppointmentType> AppointmentTypes() =>
    [
        AppointmentType.Create(
            Guid.CreateVersion7(),
            "OPEN_ACCOUNT",
            "Open an account",
            "Speak to a consultant about opening a new account.",
            "A valid identity document.",
            sortOrder: 1),
        AppointmentType.Create(
            Guid.CreateVersion7(),
            "CARD_COLLECTION",
            "Collect a card",
            "Collect a card that has been prepared for you.",
            "A valid identity document and your collection notification, if you received one.",
            sortOrder: 2),
        AppointmentType.Create(
            Guid.CreateVersion7(),
            "LOAN_APPLICATION",
            "Apply for a loan",
            "Speak to a consultant about applying for a loan.",
            "A valid identity document and proof of income.",
            sortOrder: 3),
    ];

    public static IReadOnlyList<Branch> Branches() =>
    [
        DemoBranch("DEMO-JHB-01", "Demo Branch Johannesburg", "Johannesburg", "Gauteng", slotCapacity: 3),
        DemoBranch("DEMO-CPT-01", "Demo Branch Cape Town", "Cape Town", "Western Cape", slotCapacity: 2),
        DemoBranch("DEMO-DBN-01", "Demo Branch Durban", "Durban", "KwaZulu-Natal", slotCapacity: 2),
        DemoBranch("DEMO-PTA-01", "Demo Branch Pretoria", "Pretoria", "Gauteng", slotCapacity: 2),
        DemoBranch("DEMO-PLZ-01", "Demo Branch Gqeberha", "Gqeberha", "Eastern Cape", slotCapacity: 1),
    ];

    private static Branch DemoBranch(string code, string name, string city, string province, int slotCapacity)
    {
        var branch = Branch.Create(
            Guid.CreateVersion7(),
            code,
            name,
            "1 Demo Street (sample address)",
            city,
            province,
            "Africa/Johannesburg",
            slotLengthMinutes: 30,
            slotCapacity);

        foreach (var day in Weekdays)
        {
            branch.AddOpeningHours(day, new TimeOnly(8, 0), new TimeOnly(17, 0));
        }

        branch.AddOpeningHours(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(13, 0));
        return branch;
    }
}
