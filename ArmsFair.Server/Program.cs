using ArmsFair.Server.Data;
using ArmsFair.Server.Hubs;
using ArmsFair.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ArmsFairDb>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Redis ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// ── HTTP clients ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("acled");
builder.Services.AddHttpClient("gpi");

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PhaseOrchestrator>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddSingleton<TickerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TickerService>());

// ── SignalR ─────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── JWT Auth ────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = false,
            ValidateAudience         = false
        };
        // Allow JWT via query string for SignalR WebSocket handshake
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/gamehub"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── API / Swagger ────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

var app = builder.Build();

// ── Middleware ───────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Hubs ─────────────────────────────────────────────────────────────────────
app.MapHub<GameHub>("/gamehub");

// ── Auth endpoints ────────────────────────────────────────────────────────────
app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth) =>
{
    try
    {
        var (player, token) = await auth.RegisterAsync(req.Username, req.Email, req.Password);
        return Results.Ok(new
        {
            token,
            profile = new { id = player.Id, username = player.Username, homeNationIso = player.HomeNationIso }
        });
    }
    catch (ArgumentException ex)       { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth) =>
{
    var result = await auth.LoginAsync(req.UsernameOrEmail, req.Password);
    if (result is null) return Results.Unauthorized();

    var (player, token) = result.Value;
    return Results.Ok(new
    {
        token,
        profile = new { id = player.Id, username = player.Username, homeNationIso = player.HomeNationIso }
    });
});

app.MapGet("/api/auth/me", async (HttpContext ctx, AuthService auth) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ") == true ? authHeader[7..] : null;
    if (token is null) return Results.Unauthorized();

    var player = await auth.ValidateTokenAsync(token);
    if (player is null) return Results.Unauthorized();

    return Results.Ok(new
    {
        id            = player.Id,
        username      = player.Username,
        homeNationIso = player.HomeNationIso,
        createdAt     = player.CreatedAt
    });
}).RequireAuthorization();

// ── Health check ─────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// ── Request types ─────────────────────────────────────────────────────────────
record RegisterRequest(string Username, string Email, string Password);
record LoginRequest(string UsernameOrEmail, string Password);
