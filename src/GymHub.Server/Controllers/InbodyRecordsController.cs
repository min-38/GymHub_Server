using GymHub.Server.Auth;
using GymHub.Server.Body;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[Authorize]
[ApiController]
[Route("inbody-records")]
public sealed class InbodyRecordsController(BodyService bodyService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool chronological, CancellationToken ct)
    {
        var records = await bodyService.GetInbodyRecordsAsync(User.GetUserId(), chronological, ct);
        return Ok(records);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertInbodyRecordRequest request, CancellationToken ct)
    {
        var result = await bodyService.UpsertInbodyRecordAsync(User.GetUserId(), request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await bodyService.DeleteInbodyRecordAsync(User.GetUserId(), id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
