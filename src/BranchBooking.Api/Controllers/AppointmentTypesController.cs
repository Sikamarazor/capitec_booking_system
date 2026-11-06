using BranchBooking.Application.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace BranchBooking.Api.Controllers;

[ApiController]
[Route("api/v1/appointment-types")]
public sealed class AppointmentTypesController(ListAppointmentTypes listAppointmentTypes) : ControllerBase
{
    /// <summary>The reasons a customer can book a visit for.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AppointmentTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentTypeDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await listAppointmentTypes.ExecuteAsync(cancellationToken));
}
