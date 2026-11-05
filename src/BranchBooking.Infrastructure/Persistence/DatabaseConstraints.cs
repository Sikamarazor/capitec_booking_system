namespace BranchBooking.Infrastructure.Persistence;

/// <summary>Names of database constraints that application code needs to recognise.</summary>
public static class DatabaseConstraints
{
    /// <summary>Unique partial index: one confirmed booking per branch, start time and slot number.</summary>
    public const string ConfirmedSlotUnique = "ux_bookings_confirmed_slot";

    public const string BookingReferenceUnique = "ux_bookings_reference";

    public const string BranchCodeUnique = "ux_branches_code";

    public const string AppointmentTypeCodeUnique = "ux_appointment_types_code";

    public const string BookingBranchForeignKey = "fk_bookings_branches_branch_id";
}
