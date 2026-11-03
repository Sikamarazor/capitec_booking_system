namespace BranchBooking.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    /// <summary>Name under the <c>ConnectionStrings</c> configuration section.</summary>
    public const string ConnectionStringName = "BranchBooking";

    public string ConnectionString { get; set; } = string.Empty;
}
