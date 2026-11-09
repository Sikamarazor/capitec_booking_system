namespace BranchBooking.Domain.Exceptions;

/// <summary>Raised when a business rule or invariant would be violated.</summary>
public class DomainException(string message) : Exception(message);
