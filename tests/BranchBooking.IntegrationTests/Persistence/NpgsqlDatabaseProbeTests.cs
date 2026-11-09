using BranchBooking.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace BranchBooking.IntegrationTests.Persistence;

public class NpgsqlDatabaseProbeTests
{
    private const string ConnectionVariable = "BRANCHBOOKING_TEST_CONNECTION";

    [Fact]
    public async Task CanConnectAsync_returns_false_when_nothing_is_listening()
    {
        // Port 1 is reserved and not served; the short timeout keeps the test fast.
        await using var dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2;Pooling=false");
        var sut = new NpgsqlDatabaseProbe(dataSource, NullLogger<NpgsqlDatabaseProbe>.Instance);

        var result = await sut.CanConnectAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task CanConnectAsync_returns_true_against_a_real_database()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"Set {ConnectionVariable} to run this test.");

        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var sut = new NpgsqlDatabaseProbe(dataSource, NullLogger<NpgsqlDatabaseProbe>.Instance);

        var result = await sut.CanConnectAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
    }
}
