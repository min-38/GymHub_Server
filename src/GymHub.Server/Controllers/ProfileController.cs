using GymHub.Server.Auth;
using GymHub.Server.Body;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[Authorize]
[ApiController]
[Route("profile")]
public sealed class ProfileController(ProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var profile = await profileService.GetAsync(User.GetUserId(), ct);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] ProfileDto request, CancellationToken ct)
    {
        var saved = await profileService.SaveAsync(User.GetUserId(), request, ct);
        return Ok(saved);
    }
}
