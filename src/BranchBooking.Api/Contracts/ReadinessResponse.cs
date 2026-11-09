namespace BranchBooking.Api.Contracts;

public sealed record ReadinessResponse(string Status, string Database, DateTimeOffset TimestampUtc);
