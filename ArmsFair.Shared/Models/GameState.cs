using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared.Models;

public record GameState
{
    public required string             GameId     { get; init; }
    public int                         Round      { get; init; }
    public GamePhase                   Phase      { get; init; }
    public required WorldTracks        Tracks     { get; init; }
    public required List<CountryState> Countries  { get; init; }
    public required List<PlayerProfile> Players   { get; init; }
    public int                         CompletedReconstructionContracts { get; init; }
    public string                      EndingType { get; init; } = "none";
}
