using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using ArmsFair.Server.Data;
using ArmsFair.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ArmsFair.Server.Services;

public class AuthService(ArmsFairDb db, IConfiguration config, ILogger<AuthService> logger)
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_\-]{3,20}$", RegexOptions.Compiled);

    public async Task<(PlayerEntity Player, string Token)> RegisterAsync(
        string username, string email, string password)
    {
        if (!UsernameRegex.IsMatch(username))
            throw new ArgumentException("Username must be 3–20 characters: letters, digits, underscore, hyphen.");

        var emailNorm = email.Trim().ToLowerInvariant();

        var exists = await db.Players.AnyAsync(p =>
            p.Username == username || p.Email == emailNorm);
        if (exists)
            throw new InvalidOperationException("Username or email already registered.");

        var player = new PlayerEntity
        {
            Username     = username,
            Email        = emailNorm,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            CreatedAt    = DateTime.UtcNow
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        logger.LogInformation("Registered player {Username} ({Id})", player.Username, player.Id);
        return (player, MintToken(player));
    }

    public async Task<(PlayerEntity Player, string Token)?> LoginAsync(string usernameOrEmail, string password)
    {
        var norm = usernameOrEmail.Trim().ToLowerInvariant();

        var player = await db.Players.FirstOrDefaultAsync(p =>
            p.Username == usernameOrEmail || p.Email == norm);

        if (player is null || player.PasswordHash is null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, player.PasswordHash))
            return null;

        player.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (player, MintToken(player));
    }

    public async Task<PlayerEntity?> ValidateTokenAsync(string token)
    {
        var principal = TryValidate(token);
        if (principal is null) return null;

        var idStr = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(idStr, out var id)) return null;

        return await db.Players.FindAsync(id);
    }

    private string MintToken(PlayerEntity player)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [
                new Claim(JwtRegisteredClaimNames.Sub,  player.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, player.Username),
            ],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? TryValidate(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = false,
                ValidateAudience         = false,
                ClockSkew                = TimeSpan.Zero
            }, out _);
        }
        catch { return null; }
    }

    private string JwtKey =>
        config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
}
