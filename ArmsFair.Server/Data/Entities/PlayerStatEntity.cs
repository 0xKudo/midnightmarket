namespace ArmsFair.Server.Data.Entities;

public class PlayerStatEntity
{
    public int    Id           { get; set; }
    public string PlayerId     { get; set; } = null!;
    public string Username     { get; set; } = null!;
    public string GameId       { get; set; } = null!;

    // Final-round snapshot
    public int    FinalProfit      { get; set; }
    public int    FinalReputation  { get; set; }
    public int    FinalSharePrice  { get; set; }
    public int    FinalCapital     { get; set; }
    public int    FinalPeaceCredits { get; set; }
    public int    FinalLatentRisk  { get; set; }

    // Lifetime counters
    public int    TotalSales       { get; set; }
    public int    CovertSales      { get; set; }
    public int    AidCoverSales    { get; set; }
    public int    PeaceBrokerActs  { get; set; }
    public int    BlowbackEvents   { get; set; }
    public int    CoupsAttempted   { get; set; }
    public int    CoupsSucceeded   { get; set; }
    public int    WhistleblowsUsed { get; set; }

    public string EndingType   { get; set; } = "none";
    public bool   IsWinner     { get; set; }
    public DateTime RecordedAt { get; set; }

    public GameSessionEntity GameSession { get; set; } = null!;
}
