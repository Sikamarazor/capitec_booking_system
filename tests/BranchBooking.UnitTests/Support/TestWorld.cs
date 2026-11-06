using BranchBooking.Application.Availability;
using BranchBooking.Application.Bookings;
using BranchBooking.Application.Catalog;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace BranchBooking.UnitTests.Support;

/// <summary>
/// One branch (Monday to Friday 08:00 to 17:00, Saturday 08:00 to 13:00, 30-minute slots), one appointment type,
/// in-memory repositories and a fixed clock. Use-case factory methods are added by each work item.
/// </summary>
internal sealed class TestWorld
{
    /// <summary>Tuesday 3 November 2026 08:00 South Africa time: a normal bookable slot.</summary>
    public static readonly DateTimeOffset TuesdayEightAmUtc = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    public TestWorld(int slotCapacity = 2, BookingPolicyOptions? policyOptions = null, DateTimeOffset? now = null)
    {
        Now = now ?? DomainTestData.Now;
        Branch = DomainTestData.NewBranch(slotCapacity: slotCapacity);
        Type = AppointmentType.Create(Guid.CreateVersion7(), "TEST_TYPE", "Test type", "Description", "Bring ID", 1);
        Clock = new FixedTimeProvider(Now);
        Policy = new BookingPolicy(Options.Create(policyOptions ?? new BookingPolicyOptions()), Clock);
        Store = new InMemoryStore();
        Branches = new InMemoryBranchRepository(Branch);
        Types = new InMemoryAppointmentTypeRepository(Type);
        Bookings = new InMemoryBookingRepository(Store);
        UnitOfWork = new FakeUnitOfWork(Store);
    }

    public DateTimeOffset Now { get; }

    public Branch Branch { get; }

    public AppointmentType Type { get; }

    public FixedTimeProvider Clock { get; }

    public BookingPolicy Policy { get; }

    public InMemoryStore Store { get; }

    public InMemoryBranchRepository Branches { get; }

    public InMemoryAppointmentTypeRepository Types { get; }

    public InMemoryBookingRepository Bookings { get; }

    public FakeUnitOfWork UnitOfWork { get; }

    public ListAppointmentTypes ListAppointmentTypes() => new(Types);

    public ListBranches ListBranches() => new(Branches);

    public GetAvailability GetAvailability() => new(Branches, Bookings, Policy);

    /// <summary>Adds an existing confirmed booking at this world's branch and type, as if saved earlier.</summary>
    public Booking AddConfirmedBooking(DateTimeOffset startsAtUtc, int slotNumber = 1, string email = "thandi@example.com")
    {
        var booking = Booking.Create(
            BookingReference.Generate(),
            Branch.Id,
            Type.Id,
            DomainTestData.NewSlot(startsAtUtc),
            slotNumber,
            CustomerContact.Create("Thandi Nkosi", email, "0821234567"),
            Now);

        booking.AttachNavigation(Branch, Type);
        Store.Bookings.Add(booking);
        return booking;
    }
}
