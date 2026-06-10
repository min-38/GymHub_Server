using GymHub.Server.Auth;
using GymHub.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(GoogleAuthService googleAuth, GymHubDbContext db) : ControllerBase
{
    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleSignInRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { error = "idToken is required." });
        }

        var result = await googleAuth.SignInWithGoogleAsync(request.IdToken, ct);
        if (result is null)
        {
            return Unauthorized(new { error = "Invalid Google token." });
        }

        return Ok(new AuthResponse(result.Token, result.User.Id, result.User.Email, result.User.DisplayName));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null
            ? NotFound()
            : Ok(new AuthResponse(string.Empty, user.Id, user.Email, user.DisplayName));
    }
}

public sealed record GoogleSignInRequest(string IdToken);

public sealed record AuthResponse(string Token, int UserId, string Email, string DisplayName);
