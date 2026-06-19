using GymHub.Server.Auth;
using GymHub.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymHub.Server.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    GoogleAuthService googleAuth,
    GymHubDbContext db,
    TokenService tokenService,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    // 데이터 마이그레이션이 시드한 레거시 소유자 행(기존 단일 사용자 데이터의 주인).
    private const string LegacyOwnerEmail = "owner@gymhub.local";

    private readonly AuthOptions _options = authOptions.Value;
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

    /// <summary>
    /// 개발/테스트 전용. <c>Auth:AllowDevLogin=true</c> 일 때만 동작하며, Google 없이
    /// 레거시 소유자 계정으로 토큰을 발급한다(기존 데이터로 바로 진입). 운영에선 비활성.
    /// </summary>
    [HttpPost("dev-login")]
    public async Task<IActionResult> DevLogin(CancellationToken ct)
    {
        if (!_options.AllowDevLogin)
        {
            return NotFound();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == LegacyOwnerEmail, ct);
        if (user is null)
        {
            user = new User { Email = LegacyOwnerEmail, DisplayName = "테스트 사용자" };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        return Ok(new AuthResponse(tokenService.CreateToken(user), user.Id, user.Email, user.DisplayName));
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
