namespace ArmsFair.Server.Data.Entities;

public class AuditLogEntity
{
    public long     Id         { get; set; }
    public string   GameId     { get; set; } = null!;
    public int      Round      { get; set; }
    public string   PlayerId   { get; set; } = null!;
    public string   ActionType { get; set; } = null!;  // "open_sale" | "covert_sale" | "coup" | etc.
    public string   DetailJson { get; set; } = null!;  // serialized action detail
    public DateTime OccurredAt { get; set; }

    public GameSessionEntity GameSession { get; set; } = null!;
}
