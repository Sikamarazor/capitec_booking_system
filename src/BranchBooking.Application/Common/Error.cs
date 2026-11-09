namespace BranchBooking.Application.Common;

public enum ErrorType
{
    /// <summary>The request is well-formed but its content is not acceptable. HTTP 400.</summary>
    Validation = 1,

    /// <summary>The thing asked for does not exist. HTTP 404.</summary>
    NotFound = 2,

    /// <summary>The request clashes with the current state. HTTP 409.</summary>
    Conflict = 3,

    /// <summary>A business rule forbids the action. HTTP 422.</summary>
    Policy = 4,
}

/// <summary>An expected business failure, returned as data rather than thrown.</summary>
/// <param name="Code">Stable machine-readable code, for example <c>slot_not_available</c>.</param>
/// <param name="Message">Safe, customer-readable explanation.</param>
/// <param name="Type">Decides the HTTP status.</param>
public sealed record Error(string Code, string Message, ErrorType Type);
