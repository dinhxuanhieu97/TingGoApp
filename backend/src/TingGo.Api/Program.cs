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
        // SignalR websocket không gửi được header — nhận access_token qua query cho /hubs
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// --- CORS cho web dev (Next.js localhost:3000/3001) ---
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? ["http://localhost:3000", "http://localhost:3001"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // SignalR negotiate gửi credentials

// --- Rate limiting (PRD 5.3) — giới hạn theo IP cho public/auth, cấu hình qua appsettings ---
var publicLimit = builder.Configuration.GetValue("RateLimit:PublicPerMinute", 300);
var authLimit = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (path.StartsWithSegments("/api/v1/auth"))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = authLimit,
                    Window = TimeSpan.FromMinutes(1),
                });
        }
        if (path.StartsWithSegments("/api/v1/public"))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter($"public:{ip}",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = publicLimit,
                    Window = TimeSpan.FromMinutes(1),
                });
        }
        return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("none");
    });
});

// --- Cross-cutting ---
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        options => options.Configuration.ChannelPrefix =
            StackExchange.Redis.RedisChannel.Literal("tinggo-signalr")); // backplane (PRD Sprint 6)
builder.Services.AddHostedService<TingGo.Api.Workers.OutboxWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres")
    .AddCheck<RedisHealthCheck>("redis");

// --- Infrastructure entities (outbox, idempotency) ---
builder.Services.AddSingleton<TingGo.SharedKernel.Persistence.IModuleEntityConfigurator,
    TingGo.Infrastructure.Persistence.InfrastructureEntityConfigurator>();

// --- Modules (modular monolith) ---
foreach (var module in ModuleRegistry.Modules)
{
    module.AddModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseCors("web");
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
TingGo.Api.Endpoints.ReportEndpoints.Map(apiV1);
TingGo.Api.Endpoints.ImportEndpoints.Map(apiV1);

app.Run();

public partial class Program; // Cho WebApplicationFactory trong integration test
