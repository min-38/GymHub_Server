using GymHub.Server.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GymHub.Server.Tests;

public sealed class DatabaseConnectionStringFactoryTests
{
    [Fact]
    public void CreateBuildsConnectionStringFromStructuredDatabaseOptions()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Host"] = "example.neon.tech",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "gymhub",
            ["Database:User"] = "gymhub_owner",
            ["Database:Password"] = "secret",
            ["Database:TimeoutSeconds"] = "45",
        });

        var connectionString = DatabaseConnectionStringFactory.Create(configuration);

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("example.neon.tech", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("gymhub", builder.Database);
        Assert.Equal("gymhub_owner", builder.Username);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal("Require", builder["Channel Binding"]?.ToString());
        Assert.Equal(45, builder.Timeout);
    }

    [Fact]
    public void CreatePreservesRawConnectionStringAndForcesNeonSslRequirements()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=example.neon.tech;Database=gymhub;Username=gymhub_owner;Password=secret;Ssl Mode=Disable",
        });

        var connectionString = DatabaseConnectionStringFactory.Create(configuration);

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("example.neon.tech", builder.Host);
        Assert.Equal("gymhub", builder.Database);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal("Require", builder["Channel Binding"]?.ToString());
        Assert.Equal(30, builder.Timeout);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
