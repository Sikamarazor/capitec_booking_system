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
    
    public static readonly Error BookingNotFound =
        new("booking_not_found", "No booking was found for that reference.", ErrorType.NotFound);

    public static Error ChangeWindowClosed(int cutoffHours) =>
        new(
            "change_window_closed",
            $"A booking can only be changed up to {cutoffHours} hours before it starts. Please contact the branch.",
            ErrorType.Policy);

    public static readonly Error ConcurrentChange =
        new("concurrent_change", "The booking was changed at the same time. Please try again.", ErrorType.Conflict);

    public static Error InvalidCancellationReason(int maxLength) =>
        new("invalid_cancellation_reason", $"The reason can be at most {maxLength} characters.", ErrorType.Validation);

    public static readonly Error BookingNotActive =
        new("booking_not_active", "Only a confirmed booking can be rescheduled.", ErrorType.Conflict);

    public static Error RescheduleLimitReached(int maxReschedules) =>
        new(
            "reschedule_limit_reached",
            $"A booking can be rescheduled at most {maxReschedules} times. Please cancel it and book again.",
            ErrorType.Policy);

    public static readonly Error SameSlot =
        new("same_slot", "The booking is already at that time. Please choose a different time.", ErrorType.Validation);

}
