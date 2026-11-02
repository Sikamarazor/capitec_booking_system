namespace BranchBooking.Api.Contracts;

public sealed record HealthResponse(string Status, string Service, string Version, DateTimeOffset TimestampUtc);
