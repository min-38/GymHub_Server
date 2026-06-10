using System.Security.Claims;
using System.Text;
using GymHub.Server.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GymHub.Server.Auth;

/// <summary>Mints the GymHub access tokens (JWT) that the app sends on every API call.</summary>
public sealed class TokenService(IOptions<AuthOptions> options)
{
    private readonly AuthOptions _options = options.Value;

    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.JwtIssuer,
            Audience = _options.JwtAudience,
            Expires = DateTime.UtcNow.AddHours(_options.JwtLifetimeHours),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
            ]),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
