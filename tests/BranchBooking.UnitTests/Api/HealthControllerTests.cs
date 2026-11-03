using BranchBooking.Api.Contracts;
using BranchBooking.Api.Controllers;
using BranchBooking.UnitTests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BranchBooking.UnitTests.Api;

public class HealthControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);

    private static HealthController CreateSut(bool databaseUp) =>
        new(new FixedTimeProvider(Now), new StubDatabaseProbe(databaseUp));

    [Fact]
    public void Get_returns_ok_with_status_ok_and_clock_time()
    {
        var result = CreateSut(databaseUp: true).Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ok", body.Status);
        Assert.Equal("BranchBooking.Api", body.Service);
        Assert.Equal(Now, body.TimestampUtc);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
    }

    [Fact]
    public void Get_does_not_depend_on_the_database()
    {
        var result = CreateSut(databaseUp: false).Get();

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetReady_returns_200_when_database_is_up()
    {
        var result = await CreateSut(databaseUp: true).GetReady(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReadinessResponse>(ok.Value);
        Assert.Equal("ready", body.Status);
        Assert.Equal("up", body.Database);
    }

    [Fact]
    public async Task GetReady_returns_503_when_database_is_down()
    {
        var result = await CreateSut(databaseUp: false).GetReady(TestContext.Current.CancellationToken);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        var body = Assert.IsType<ReadinessResponse>(response.Value);
        Assert.Equal("not_ready", body.Status);
        Assert.Equal("down", body.Database);
    }
}
