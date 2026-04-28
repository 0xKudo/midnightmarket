using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared.Models;

public record CountryState
{
    public required string Iso              { get; init; }
    public required string Name             { get; init; }
    public CountryStage    Stage            { get; init; }
    public int             Tension          { get; init; }
    public string          DemandType       { get; init; } = "none"; // "none" | "covert" | "open"
    public bool            InitialStage2Plus { get; init; }
}
