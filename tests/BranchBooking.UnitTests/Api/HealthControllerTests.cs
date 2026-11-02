using BranchBooking.Api.Contracts;
using BranchBooking.Api.Controllers;
using BranchBooking.UnitTests.Support;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BranchBooking.UnitTests.Api;

public class HealthControllerTests
{
    [Fact]
    public void Get_returns_ok_with_status_ok_and_clock_time()
    {
        var now = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);
        var sut = new HealthController(new FixedTimeProvider(now));

        var result = sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ok", body.Status);
        Assert.Equal("BranchBooking.Api", body.Service);
        Assert.Equal(now, body.TimestampUtc);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
    }
}
