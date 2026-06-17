using GymHub.Server.Data;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymHub.Server.Auth;

public sealed record AuthResult(string Token, User User);

/// <summary>
/// Verifies a Google ID token, resolves it to a GymHub user, and issues an access token.
/// The first Google sign-in "claims" the legacy owner row seeded by the data migration so
/// the pre-existing single-user data is linked to the owner's real account.
/// </summary>
public sealed class GoogleAuthService(
    GymHubDbContext db,
    TokenService tokenService,
    IOptions<AuthOptions> options)
{
    // Must match the placeholder seeded in AddUsersAndUserScoping.
    private const string LegacyOwnerEmail = "owner@gymhub.local";

    private readonly AuthOptions _options = options.Value;

    public async Task<AuthResult?> SignInWithGoogleAsync(string idToken, CancellationToken ct)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [_options.OAuth.Google.ClientId] });
        }
        catch (InvalidJwtException)
        {
            return null;
        }

        var user = await ResolveUserAsync(payload.Subject, payload.Email, payload.Name, payload.EmailVerified, ct);
        return new AuthResult(tokenService.CreateToken(user), user);
    }

    internal async Task<User> ResolveUserAsync(
        string subject, string email, string? name, bool emailVerified, CancellationToken ct)
    {
        var displayName = name ?? email;

        var bySub = await db.Users.FirstOrDefaultAsync(u => u.GoogleSub == subject, ct);
        if (bySub is not null)
        {
            return bySub;
        }

        // Email-based linking trusts the address to identify a pre-existing account, so it must
        // only run when Google asserts the email is verified — otherwise an unverified-email token
        // could hijack another user's account or claim the legacy owner row.
        if (emailVerified)
        {
            var byEmail = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (byEmail is not null)
            {
                byEmail.GoogleSub = subject;
                await db.SaveChangesAsync(ct);
                return byEmail;
            }

            var legacyOwner = await db.Users.FirstOrDefaultAsync(
                u => u.GoogleSub == null && u.Email == LegacyOwnerEmail, ct);
            if (legacyOwner is not null)
            {
                legacyOwner.GoogleSub = subject;
                legacyOwner.Email = email;
                legacyOwner.DisplayName = displayName;
                await db.SaveChangesAsync(ct);
                return legacyOwner;
            }
        }

        var created = new User
        {
            Email = email,
            GoogleSub = subject,
            DisplayName = displayName,
        };
        db.Users.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}
