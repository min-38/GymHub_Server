namespace GymHub.Server.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public OAuthOptions OAuth { get; init; } = new();

    /// <summary>Symmetric signing key for the JWTs this server issues.</summary>
    public string JwtKey { get; init; } = "";

    public string JwtIssuer { get; init; } = "gymhub-server";

    public string JwtAudience { get; init; } = "gymhub-app";

    public int JwtLifetimeHours { get; init; } = 720;
}

public sealed class OAuthOptions
{
    public GoogleOAuthOptions Google { get; init; } = new();
}

public sealed class GoogleOAuthOptions
{
    /// <summary>Google OAuth client id accepted as the ID token audience.</summary>
    public string ClientId { get; init; } = "";

    public string ClientSecret { get; init; } = "";
}
