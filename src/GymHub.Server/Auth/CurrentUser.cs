using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymHub.Server.Auth;

public static class CurrentUser
{
    /// <summary>Reads the authenticated GymHub user id from the JWT subject claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid user id.");
    }
}
