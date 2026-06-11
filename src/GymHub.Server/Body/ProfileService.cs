using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Body;

/// <summary>Get/save the authenticated user's profile.</summary>
public sealed class ProfileService(GymHubDbContext db)
{
    public async Task<ProfileDto> GetAsync(int userId, CancellationToken ct)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        return profile is null
            ? new ProfileDto(null, null, null, null, null)
            : new ProfileDto(profile.Name, profile.Gender, profile.BirthDate, profile.Height, profile.Weight);
    }

    public async Task<ProfileDto> SaveAsync(int userId, ProfileDto request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new Profile { UserId = userId };
            db.Profiles.Add(profile);
        }

        profile.Name = request.Name;
        profile.Gender = request.Gender;
        profile.BirthDate = request.BirthDate;
        profile.Height = request.Height;
        profile.Weight = request.Weight;

        await db.SaveChangesAsync(ct);
        return request;
    }
}

public sealed record ProfileDto(string? Name, string? Gender, DateOnly? BirthDate, double? Height, double? Weight);
