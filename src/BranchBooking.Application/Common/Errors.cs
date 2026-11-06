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

        public static Error InvalidCustomerDetails(string reason) =>
        new("invalid_customer_details", reason, ErrorType.Validation);

    public static readonly Error SlotNotOffered =
        new("slot_not_offered", "That is not an appointment time offered by this branch.", ErrorType.Validation);

    public static Error SlotTooSoon(int minimumNoticeMinutes) =>
        new(
            "slot_too_soon",
            $"Appointments must be booked at least {minimumNoticeMinutes} minutes ahead.",
            ErrorType.Policy);

    public static readonly Error SlotNotAvailable =
        new("slot_not_available", "That time has just been taken. Please choose another time.", ErrorType.Conflict);

}
