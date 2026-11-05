using Xunit;

namespace BranchBooking.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL database";
}
