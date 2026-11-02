using BranchBooking.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        var version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        return Ok(new HealthResponse("ok", "BranchBooking.Api", version, clock.GetUtcNow()));
    }
}
