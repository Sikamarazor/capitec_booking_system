using BranchBooking.Api.Http;
using BranchBooking.Application.Availability;
using BranchBooking.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BranchBooking.Api.Controllers;

[ApiController]
[Route("api/v1/branches")]
public sealed class BranchesController(ListBranches listBranches, GetAvailability getAvailability) : ControllerBase
{
    /// <summary>Active branches with their opening hours.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BranchDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await listBranches.ExecuteAsync(cancellationToken));

    /// <summary>The slots offered on a local date (<c>yyyy-MM-dd</c>) with the places still free.</summary>
    [HttpGet("{id:guid}/availability")]
    [ProducesResponseType<AvailabilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvailabilityDto>> GetAvailability(
        Guid id, [FromQuery, BindRequired] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await getAvailability.ExecuteAsync(id, date, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ProblemFor(result.Error);
        }

        return Ok(result.Value);
    }
}
