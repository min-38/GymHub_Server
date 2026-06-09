using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<GymHubDbContext>(options =>
    options.UseNpgsql(DatabaseConnectionStringFactory.Create(builder.Configuration)));

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var context = migrationScope.ServiceProvider.GetRequiredService<GymHubDbContext>();
    var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

    await context.Database.MigrateAsync();

    Console.WriteLine(pending.Count == 0
        ? "Database schema is already up to date."
        : $"Applied migrations: {string.Join(", ", pending)}");
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
