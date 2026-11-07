using BranchBooking.Application.Availability;
using BranchBooking.Application.Bookings;
using BranchBooking.Application.Catalog;
using BranchBooking.Domain.Entities;
using BranchBooking.Domain.ValueObjects;
using BranchBooking.Application.Notifications;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using BranchBooking.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BranchBooking.UnitTests.Support;

/// <summary>
/// One branch (Monday to Friday 08:00 to 17:00, Saturday 08:00 to 13:00, 30-minute slots), one appointment type,
/// in-memory repositories and a fixed clock. Use-case factory methods are added by each work item.
/// </summary>
internal sealed class TestWorld
{
    /// <summary>Tuesday 3 November 2026 08:00 South Africa time: a normal bookable slot.</summary>
    public static readonly DateTimeOffset TuesdayEightAmUtc = new(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);

    public NotificationOptions NotificationSettings { get; }

    public InMemoryNotificationQueue NotificationQueue { get; }

    public ScriptedSender Sender { get; }

    public NotificationContentBuilder Content { get; }

    public NotificationPolicy NotificationPolicy { get; }

    public TestWorld(int slotCapacity = 2, BookingPolicyOptions? policyOptions = null, DateTimeOffset? now = null,
        NotificationOptions? notificationOptions = null)
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
        Notifications = new InMemoryNotificationRepository(Store);
        UnitOfWork = new FakeUnitOfWork(Store);
        NotificationSettings = notificationOptions ?? new NotificationOptions();
        NotificationQueue = new InMemoryNotificationQueue(Store);
        Sender = new ScriptedSender();
        Content = new NotificationContentBuilder(Options.Create(NotificationSettings));
        NotificationPolicy = new NotificationPolicy(Options.Create(NotificationSettings));
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

    public InMemoryNotificationRepository Notifications { get; }

    /// <summary>Adds an existing confirmed booking at this world's branch and type, as if saved earlier.</summary>
    public Booking AddConfirmedBooking(
        DateTimeOffset startsAtUtc,
        int slotNumber = 1,
        string email = "thandi@example.com",
        BookingReference? reference = null)
    {
        var booking = Booking.Create(
            reference ?? BookingReference.Generate(),
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

    public SlotResolver SlotResolver => new(Policy);

    public BookingMapper Mapper => new(Policy);

    public CreateBooking CreateBooking() =>
        new(Branches, Types, Bookings, Notifications, UnitOfWork, SlotResolver, Mapper, Clock, NullLogger<CreateBooking>.Instance);

    public CreateBookingCommand ValidCommand(DateTimeOffset? startsAtUtc = null) =>
        new(Branch.Id, Type.Id, startsAtUtc ?? TuesdayEightAmUtc, "Thandi Nkosi", "thandi@example.com", "082 123 4567");

    public GetBooking GetBooking() => new(Bookings, Mapper);
    
    public CancelBooking CancelBooking() =>
        new(Bookings, Notifications, UnitOfWork, Policy, Mapper, Clock, NullLogger<CancelBooking>.Instance);
    /// <summary>
    /// Simulates what a failed save leaves behind in a real database: the in-memory changes are gone and the
    /// stored row is back to a fresh confirmed booking with the same reference.
    /// </summary>
    public Booking ResetToFresh(BookingReference reference, DateTimeOffset startsAtUtc, int slotNumber = 1)
    {
        Store.Bookings.RemoveAll(b => b.Reference == reference);
        return AddConfirmedBooking(startsAtUtc, slotNumber, reference: reference);
    }

    public RescheduleBooking RescheduleBooking() =>
        new(Bookings, Notifications, UnitOfWork, Policy, SlotResolver, Mapper, Clock, NullLogger<RescheduleBooking>.Instance);
    
    public ListBookingMessages ListBookingMessages() => new(Bookings, Notifications);

    public DispatchNotifications DispatchNotifications(ILogger<DispatchNotifications>? logger = null) =>
        new(NotificationQueue, UnitOfWork, Sender, Content, NotificationPolicy, Clock, logger ?? NullLogger<DispatchNotifications>.Instance);

    /// <summary>Queues a pending message of the given kind for a booking, as the use cases would have, and returns it.</summary>
    public NotificationMessage AddPendingMessage(Booking booking, NotificationKind kind = NotificationKind.Confirmation)
    {
        var message = kind switch
        {
            NotificationKind.Confirmation => NotificationMessage.ForConfirmation(booking, Now),
            NotificationKind.Cancellation => NotificationMessage.ForCancellation(booking, Now),
            NotificationKind.Reschedule => NotificationMessage.ForReschedule(booking, Now),
            _ => NotificationMessage.ForReminder(booking.Id, booking.StartsAtUtc, Now),
        };

        Store.Messages.Add(message);
        return message;
    }
}


