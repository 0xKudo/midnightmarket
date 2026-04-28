namespace ArmsFair.Server.Data.Entities;

public class GameSessionEntity
{
    public string   Id          { get; set; } = null!;
    public int      Round       { get; set; }
    public string   Phase       { get; set; } = null!;
    public string   StateJson   { get; set; } = null!;  // serialized GameState
    public string   EndingType  { get; set; } = "none";
    public bool     IsComplete  { get; set; }
    public DateTime CreatedAt   { get; set; }
    public DateTime UpdatedAt   { get; set; }

    public ICollection<PlayerStatEntity> PlayerStats { get; set; } = new List<PlayerStatEntity>();
    public ICollection<AuditLogEntity>   AuditLogs   { get; set; } = new List<AuditLogEntity>();
}
