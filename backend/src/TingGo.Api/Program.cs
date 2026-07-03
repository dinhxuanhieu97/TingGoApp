using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// --- AuthN/AuthZ (JWT — ADR-003, cấu hình Jwt trong appsettings) ---
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret chưa cấu hình.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "tinggo",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "tinggo",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

// Serve ảnh menu local (dev). Production: S3 + CloudFront (ADR-002).
var imagePath = Path.GetFullPath(builder.Configuration["ImageStorage:LocalPath"] ?? "uploads/images");
Directory.CreateDirectory(imagePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imagePath),
    RequestPath = "/files/images",
});

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
