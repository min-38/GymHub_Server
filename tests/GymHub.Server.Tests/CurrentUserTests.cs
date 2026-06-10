using System.Security.Claims;
using GymHub.Server.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymHub.Server.Tests;

public sealed class CurrentUserTests
{
    [Fact]
    public void GetUserIdReadsSubjectClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, "7")]));

        Assert.Equal(7, principal.GetUserId());
    }

    [Fact]
    public void GetUserIdThrowsWhenSubjectMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
    }
}
