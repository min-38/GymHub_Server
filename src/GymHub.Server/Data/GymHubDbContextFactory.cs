using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GymHub.Server.Data;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations add/database update).
/// It loads the same configuration sources as the app (appsettings + user-secrets +
/// environment) so <c>dotnet ef database update</c> targets the real database. When
/// no database configuration is present (e.g. CI generating a migration), it falls
/// back to a placeholder connection, since the model alone needs no live database.
/// </summary>
public sealed class GymHubDbContextFactory : IDesignTimeDbContextFactory<GymHubDbContext>
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Database=gymhub;Username=gymhub;Password=design-time";

    public GymHubDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(GymHubDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<GymHubDbContext>()
            .UseNpgsql(ResolveConnectionString(configuration))
            .Options;

        return new GymHubDbContext(options);
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        try
        {
            return DatabaseConnectionStringFactory.Create(configuration);
        }
        catch (InvalidOperationException)
        {
            return PlaceholderConnectionString;
        }
    }
}
