using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TingGo.Api.Errors;
using TingGo.Api.Hubs;
using TingGo.Api.Modules;
using TingGo.Infrastructure.Health;
using TingGo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Infrastructure ---
builder.Services.AddDbContext<TingGoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// --- Cross-cutting ---
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres")
    .AddCheck<RedisHealthCheck>("redis");

// --- Modules (modular monolith) ---
foreach (var module in ModuleRegistry.Modules)
{
    module.AddModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

app.MapHealthChecks("/health");
app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok", serverTimeUtc = DateTimeOffset.UtcNow }));

app.MapHub<OrderHub>("/hubs/orders");

var apiV1 = app.MapGroup("/api/v1");
foreach (var module in ModuleRegistry.Modules)
{
    module.MapEndpoints(apiV1);
}

app.Run();

public partial class Program; // Cho WebApplicationFactory trong integration test
