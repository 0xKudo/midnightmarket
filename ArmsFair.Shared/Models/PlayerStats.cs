namespace ArmsFair.Shared.Models;

public record PlayerStats
{
    public int   GamesPlayed              { get; init; }
    public int   GamesWon                 { get; init; }
    public int   WarsStarted              { get; init; }
    public int   FailedStatesCaused       { get; init; }
    public int   SmallArmsSold            { get; init; }
    public int   VehiclesSold             { get; init; }
    public int   AirDefenseSold           { get; init; }
    public int   DronesSold               { get; init; }
    public long  TotalProfitEarned        { get; init; }
    public long  TotalCivilianCost        { get; init; }
    public int   CeasefiresBrokered       { get; init; }
    public int   CoupsFunded              { get; init; }
    public int   CoupsSucceeded           { get; init; }
    public int   TimesWhistleblown        { get; init; }
    public int   TimesWhistleblower       { get; init; }
    public int   TotalWarParticipations   { get; init; }
    public int   WorldPeaceAchieved       { get; init; }
    public int   CompanyCollapses         { get; init; }
    public int   ReconstructionWins       { get; init; }
    public float AvgFinalReputation       { get; init; }
}
