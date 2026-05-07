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
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddSingleton<PhaseOrchestrator>();
builder.Services.AddSingleton<LobbyService>();
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
            profile = new { id = player.Id, username = player.Username, homeNationIso = player.HomeNationIso, companyName = player.CompanyName }
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
        profile = new { id = player.Id, username = player.Username, homeNationIso = player.HomeNationIso, companyName = player.CompanyName }
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
        companyName   = player.CompanyName,
        createdAt     = player.CreatedAt
    });
}).RequireAuthorization();

app.MapPost("/api/auth/profile", async (UpdateProfileRequest req, HttpContext ctx, AuthService auth, ArmsFairDb db) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ") == true ? authHeader[7..] : null;
    if (token is null) return Results.Unauthorized();

    var player = await auth.ValidateTokenAsync(token);
    if (player is null) return Results.Unauthorized();

    if (req.HomeNationIso is not null) player.HomeNationIso = req.HomeNationIso;
    if (req.CompanyName   is not null) player.CompanyName   = req.CompanyName;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        id            = player.Id,
        username      = player.Username,
        homeNationIso = player.HomeNationIso,
        companyName   = player.CompanyName,
        createdAt     = player.CreatedAt
    });
}).RequireAuthorization();

// ── Lobby endpoints ───────────────────────────────────────────────────────────

// GET /api/rooms — list open public rooms
app.MapGet("/api/rooms", (LobbyService lobby) =>
    Results.Ok(lobby.ListOpen()))
    .RequireAuthorization();

// GET /api/rooms/{id} — get a specific room (also accepts invite code)
app.MapGet("/api/rooms/{id}", (string id, LobbyService lobby) =>
{
    var room = lobby.GetByRoomId(id) ?? lobby.GetByInviteCode(id);
    return room is null ? Results.NotFound() : Results.Ok(room);
}).RequireAuthorization();

// POST /api/rooms — create a room
app.MapPost("/api/rooms", (CreateRoomRequest req, HttpContext ctx, LobbyService lobby, AuthService auth) =>
{
    var playerId = ctx.User.FindFirst("sub")?.Value;
    if (playerId is null) return Results.Unauthorized();

    var username = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                   ?? ctx.User.FindFirst("name")?.Value
                   ?? "Unknown";

    var room = lobby.CreateRoom(
        hostPlayerId : playerId,
        hostUsername : username,
        roomName     : req.RoomName ?? "",
        playerSlots  : Math.Clamp(req.PlayerSlots, 2, 6),
        timerPreset  : req.TimerPreset ?? "standard",
        voiceEnabled : req.VoiceEnabled,
        aiFillIn     : req.AiFillIn,
        isPrivate    : req.IsPrivate,
        gameMode     : req.GameMode);

    return Results.Ok(room);
}).RequireAuthorization();

// POST /api/rooms/{id}/join — join a room by id or invite code
app.MapPost("/api/rooms/{id}/join", (string id, HttpContext ctx, LobbyService lobby) =>
{
    var playerId = ctx.User.FindFirst("sub")?.Value;
    if (playerId is null) return Results.Unauthorized();

    var room = lobby.GetByRoomId(id) ?? lobby.GetByInviteCode(id);
    if (room is null) return Results.NotFound(new { error = "Room not found." });
    if (room.IsStarted) return Results.BadRequest(new { error = "Game already started." });
    if (room.PlayerIds.Count >= room.PlayerSlots)
        return Results.BadRequest(new { error = "Room is full." });

    var joined = lobby.TryJoin(room.RoomId, playerId);
    return joined
        ? Results.Ok(lobby.GetByRoomId(room.RoomId))
        : Results.Conflict(new { error = "Could not join room — try again." });
}).RequireAuthorization();

// ── Health check ─────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// ── Request types ─────────────────────────────────────────────────────────────
record RegisterRequest(string Username, string Email, string Password);
record LoginRequest(string UsernameOrEmail, string Password);
record UpdateProfileRequest(string? HomeNationIso, string? CompanyName);
record CreateRoomRequest(
    string?  RoomName,
    int      PlayerSlots,
    string?  TimerPreset,
    bool     VoiceEnabled,
    bool     AiFillIn,
    bool     IsPrivate,
    ArmsFair.Shared.Enums.GameMode GameMode = ArmsFair.Shared.Enums.GameMode.Realistic);
