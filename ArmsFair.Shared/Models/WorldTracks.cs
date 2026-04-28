namespace ArmsFair.Shared.Models;

public record WorldTracks
{
    public int MarketHeat    { get; init; }
    public int CivilianCost  { get; init; }
    public int Stability     { get; init; }
    public int SanctionsRisk { get; init; }
    public int GeoTension    { get; init; }

    public static WorldTracks Initial() => new()
    {
        MarketHeat    = Balance.StartMarketHeat,
        CivilianCost  = Balance.StartCivilianCost,
        Stability     = Balance.StartStability,
        SanctionsRisk = Balance.StartSanctionsRisk,
        GeoTension    = Balance.StartGeoTension,
    };

    public WorldTracks Clamp() => this with
    {
        MarketHeat    = Math.Clamp(MarketHeat,    0, 100),
        CivilianCost  = Math.Clamp(CivilianCost,  0, 100),
        Stability     = Math.Clamp(Stability,     0, 100),
        SanctionsRisk = Math.Clamp(SanctionsRisk, 0, 100),
        GeoTension    = Math.Clamp(GeoTension,    0, 100),
    };
}
