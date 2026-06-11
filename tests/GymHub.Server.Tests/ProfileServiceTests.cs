using GymHub.Server.Body;
using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class ProfileServiceTests
{
    private const int UserId = 1;
    private const int OtherUserId = 2;

    [Fact]
    public async Task GetAsync_NoProfile_ReturnsEmptyDto()
    {
        using var db = CreateContext();
        var service = new ProfileService(db);

        var result = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(new ProfileDto(null, null, null, null, null), result);
    }

    [Fact]
    public async Task SaveAsync_WithoutExistingProfile_CreatesProfile()
    {
        using var db = CreateContext();
        var service = new ProfileService(db);
        var request = new ProfileDto("Minseo", "male", new DateOnly(1995, 3, 1), 175, 70);

        var result = await service.SaveAsync(UserId, request, CancellationToken.None);

        Assert.Equal(request, result);
        var stored = await db.Profiles.SingleAsync();
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal("Minseo", stored.Name);
    }

    [Fact]
    public async Task SaveAsync_WithExistingProfile_UpdatesInPlace()
    {
        using var db = CreateContext();
        db.Profiles.Add(new Profile { UserId = UserId, Name = "Old", Weight = 65 });
        await db.SaveChangesAsync();
        var service = new ProfileService(db);
        var request = new ProfileDto("New", "female", new DateOnly(1996, 4, 2), 165, 60);

        await service.SaveAsync(UserId, request, CancellationToken.None);

        var stored = await db.Profiles.SingleAsync();
        Assert.Equal("New", stored.Name);
        Assert.Equal("female", stored.Gender);
        Assert.Equal(60, stored.Weight);
    }

    [Fact]
    public async Task GetAsync_ReturnsOnlyOwnProfile()
    {
        using var db = CreateContext();
        db.Profiles.Add(new Profile { UserId = OtherUserId, Name = "Other" });
        await db.SaveChangesAsync();
        var service = new ProfileService(db);

        var result = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Null(result.Name);
    }

    private static GymHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GymHubDbContext(options);
    }
}
