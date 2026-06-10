using System.Text;
using GymHub.Server.Auth;
using GymHub.Server.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GymHub.Server.Tests;

public sealed class TokenServiceTests
{
    private static readonly AuthOptions Options = new()
    {
        JwtKey = "this-is-a-test-signing-key-long-enough-32+",
        JwtIssuer = "gymhub-server",
        JwtAudience = "gymhub-app",
        JwtLifetimeHours = 1,
    };

    [Fact]
    public async Task CreateTokenProducesJwtValidatableWithConfiguredParameters()
    {
        var service = new TokenService(MicrosoftOptions(Options));
        var user = new User { Id = 42, Email = "owner@example.com" };

        var token = service.CreateToken(user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Options.JwtIssuer,
            ValidAudience = Options.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.JwtKey)),
        });

        Assert.True(result.IsValid);
        Assert.Equal("42", result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("owner@example.com", result.Claims[JwtRegisteredClaimNames.Email]);
    }

    [Fact]
    public async Task TokenSignedWithDifferentKeyFailsValidation()
    {
        var service = new TokenService(MicrosoftOptions(Options));
        var token = service.CreateToken(new User { Id = 1, Email = "a@b.com" });

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Options.JwtIssuer,
            ValidAudience = Options.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-totally-different-signing-key-32-chars")),
        });

        Assert.False(result.IsValid);
    }

    private static IOptions<AuthOptions> MicrosoftOptions(AuthOptions options) =>
        Microsoft.Extensions.Options.Options.Create(options);
}
