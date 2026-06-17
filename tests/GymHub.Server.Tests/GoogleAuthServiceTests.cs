using GymHub.Server.Auth;
using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymHub.Server.Tests;

public sealed class GoogleAuthServiceTests
{
    private const string LegacyOwnerEmail = "owner@gymhub.local";

    [Fact]
    public async Task FirstSignInClaimsLegacyOwnerWithoutCreatingNewUser()
    {
        await using var db = NewDb();
        db.Users.Add(new User { Id = 1, Email = LegacyOwnerEmail, GoogleSub = null, DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var service = NewService(db);

        var user = await service.ResolveUserAsync("google-sub-1", "real@gmail.com", "Real Name", true, default);

        Assert.Equal(1, user.Id);
        Assert.Equal("google-sub-1", user.GoogleSub);
        Assert.Equal("real@gmail.com", user.Email);
        Assert.Equal("Real Name", user.DisplayName);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task ExistingGoogleSubReturnsSameUser()
    {
        await using var db = NewDb();
        db.Users.Add(new User { Id = 5, Email = "x@y.com", GoogleSub = "sub-5" });
        await db.SaveChangesAsync();
        var service = NewService(db);

        var user = await service.ResolveUserAsync("sub-5", "x@y.com", "X", true, default);

        Assert.Equal(5, user.Id);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task NewAccountWithoutLegacyOwnerCreatesUser()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var user = await service.ResolveUserAsync("sub-new", "new@gmail.com", "New", true, default);

        Assert.Equal("sub-new", user.GoogleSub);
        Assert.Equal("new@gmail.com", user.Email);
        Assert.True(user.Id > 0);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task SecondAccountAfterOwnerClaimedCreatesSeparateUser()
    {
        await using var db = NewDb();
        // Owner already claimed (real email + sub), so a different account must not hijack it.
        db.Users.Add(new User { Id = 1, Email = "owner@gmail.com", GoogleSub = "owner-sub" });
        await db.SaveChangesAsync();
        var service = NewService(db);

        var user = await service.ResolveUserAsync("other-sub", "other@gmail.com", "Other", true, default);

        Assert.NotEqual(1, user.Id);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Fact]
    public async Task UnverifiedEmailDoesNotClaimLegacyOwnerOrLinkByEmail()
    {
        await using var db = NewDb();
        db.Users.Add(new User { Id = 1, Email = LegacyOwnerEmail, GoogleSub = null, DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var service = NewService(db);

        // emailVerified: false — the legacy owner row must NOT be claimed; a separate user is created.
        var user = await service.ResolveUserAsync("attacker-sub", "owner@gmail.com", "Attacker", false, default);

        Assert.NotEqual(1, user.Id);
        Assert.Equal("attacker-sub", user.GoogleSub);
        var legacyOwner = await db.Users.FirstAsync(u => u.Id == 1);
        Assert.Null(legacyOwner.GoogleSub);
        Assert.Equal(LegacyOwnerEmail, legacyOwner.Email);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    private static GymHubDbContext NewDb() =>
        new(new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GoogleAuthService NewService(GymHubDbContext db)
    {
        var options = Options.Create(new AuthOptions { JwtKey = "test-key-test-key-test-key-test-key-32" });
        return new GoogleAuthService(db, new TokenService(options), options);
    }
}
