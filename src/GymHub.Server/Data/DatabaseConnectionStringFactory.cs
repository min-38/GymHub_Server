using Npgsql;

namespace GymHub.Server.Data;

public static class DatabaseConnectionStringFactory
{
    public static string Create(IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
        var builder = CreateBuilder(configuration, options);

        builder.SslMode = SslMode.Require;
        builder["Channel Binding"] = "Require";
        builder.Timeout = Math.Max(15, options?.TimeoutSeconds ?? 30);

        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder CreateBuilder(
        IConfiguration configuration,
        DatabaseOptions? options)
    {
        var raw = configuration.GetConnectionString("Default")
            ?? configuration.GetValue<string>("Database:ConnectionString");

        return !string.IsNullOrWhiteSpace(raw)
            ? new NpgsqlConnectionStringBuilder(raw)
            : FromStructuredOptions(options);
    }

    private static NpgsqlConnectionStringBuilder FromStructuredOptions(DatabaseOptions? options)
    {
        if (options is null)
        {
            throw new InvalidOperationException("Database configuration is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.Host)
            || string.IsNullOrWhiteSpace(options.Name)
            || string.IsNullOrWhiteSpace(options.User)
            || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "Database configuration requires Host, Name, User, and Password values.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Name,
            Username = options.User,
            Password = options.Password,
        };
    }
}
