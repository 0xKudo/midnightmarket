using ArmsFair.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArmsFair.Server.Data;

public class ArmsFairDb(DbContextOptions<ArmsFairDb> options) : DbContext(options)
{
    public DbSet<GameSessionEntity> GameSessions { get; set; } = null!;
    public DbSet<PlayerStatEntity>  PlayerStats  { get; set; } = null!;
    public DbSet<AuditLogEntity>    AuditLogs    { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<GameSessionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.Phase).HasMaxLength(32);
            e.Property(x => x.EndingType).HasMaxLength(64);
            e.HasMany(x => x.PlayerStats)
             .WithOne(x => x.GameSession)
             .HasForeignKey(x => x.GameId);
            e.HasMany(x => x.AuditLogs)
             .WithOne(x => x.GameSession)
             .HasForeignKey(x => x.GameId);
        });

        b.Entity<PlayerStatEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PlayerId).HasMaxLength(128);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.EndingType).HasMaxLength(64);
            e.HasIndex(x => x.PlayerId);
            e.HasIndex(x => x.GameId);
        });

        b.Entity<AuditLogEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PlayerId).HasMaxLength(128);
            e.Property(x => x.ActionType).HasMaxLength(64);
            e.HasIndex(x => new { x.GameId, x.Round });
        });
    }
}
