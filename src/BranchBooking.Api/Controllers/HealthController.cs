using BranchBooking.Api.Contracts;
using BranchBooking.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(TimeProvider clock, IDatabaseProbe databaseProbe) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        var version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        return Ok(new HealthResponse("ok", "BranchBooking.Api", version, clock.GetUtcNow()));
    }

    [HttpGet("ready")]
    [ProducesResponseType<ReadinessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReadinessResponse>> GetReady(CancellationToken cancellationToken)
    {
        var databaseUp = await databaseProbe.CanConnectAsync(cancellationToken);
        var body = new ReadinessResponse(databaseUp ? "ready" : "not_ready", databaseUp ? "up" : "down", clock.GetUtcNow());

        return databaseUp ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
