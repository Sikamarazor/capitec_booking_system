namespace BranchBooking.Application.Common;

public static class Errors
{
    public static readonly Error BranchNotFound =
        new("branch_not_found", "The branch was not found.", ErrorType.NotFound);

    public static readonly Error AppointmentTypeNotFound =
        new("appointment_type_not_found", "The appointment type was not found.", ErrorType.NotFound);

    public static Error DateOutsideBookingWindow(int maxAdvanceDays) =>
        new(
            "date_outside_booking_window",
            $"Appointments can be booked from today up to {maxAdvanceDays} days ahead.",
            ErrorType.Validation);
}
