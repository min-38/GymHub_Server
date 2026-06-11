using GymHub.Server.Auth;
using GymHub.Server.Body;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[Authorize]
[ApiController]
[Route("body-measurements")]
public sealed class BodyMeasurementsController(BodyService bodyService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool chronological, CancellationToken ct)
    {
        var measurements = await bodyService.GetMeasurementsAsync(User.GetUserId(), chronological, ct);
        return Ok(measurements);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertBodyMeasurementRequest request, CancellationToken ct)
    {
        var result = await bodyService.UpsertMeasurementAsync(User.GetUserId(), request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await bodyService.DeleteMeasurementAsync(User.GetUserId(), id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
