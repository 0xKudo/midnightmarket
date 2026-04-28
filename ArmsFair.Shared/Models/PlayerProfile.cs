namespace ArmsFair.Shared.Models;

public record PlayerProfile
{
    public required string Id           { get; init; }
    public required string Username     { get; init; }
    public required string HomeNation   { get; init; }
    public string?         CompanyName  { get; init; }
    public string          Status       { get; init; } = "active"; // "active" | "observer" | "collapsed"
    public int             Capital      { get; init; } = Balance.StartingCapital;
    public int             Reputation   { get; init; } = Balance.StartingReputation;
    public int             SharePrice   { get; init; } = Balance.StartingSharePrice;
    public int             PeaceCredits { get; init; }
    public int             LatentRisk   { get; init; }
}
