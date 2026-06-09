namespace GymHub.Server.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string? ConnectionString { get; init; }

    public string? Host { get; init; }

    public int Port { get; init; } = 5432;

    public string? Name { get; init; }

    public string? User { get; init; }

    public string? Password { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}
