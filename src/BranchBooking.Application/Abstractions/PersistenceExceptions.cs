namespace BranchBooking.Application.Abstractions;

/// <summary>The chosen position was taken by another confirmed booking first.</summary>
public sealed class SlotAlreadyBookedException(Exception? innerException = null)
    : Exception("The selected time is no longer available.", innerException);

/// <summary>A newly generated booking reference collided with an existing one. Generate another and retry.</summary>
public sealed class DuplicateBookingReferenceException(Exception? innerException = null)
    : Exception("The booking reference already exists.", innerException);

/// <summary>The booking was changed by someone else after it was loaded.</summary>
public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("The booking was changed by someone else. Reload it and try again.", innerException);

/// <summary>A notification with the same dedupe key is already queued (for example by another Worker).</summary>
public sealed class NotificationAlreadyQueuedException(Exception? innerException = null)
    : Exception("The notification is already queued.", innerException);
