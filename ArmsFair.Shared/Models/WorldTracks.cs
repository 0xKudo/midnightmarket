using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared.Models;

public record WorldTracks
{
    public int MarketHeat    { get; init; }
    public int CivilianCost  { get; init; }
    public int Stability     { get; init; }
    public int SanctionsRisk { get; init; }
    public int GeoTension    { get; init; }

    public static WorldTracks Initial(GameMode mode = GameMode.Realistic) => mode switch
    {
        GameMode.EqualWorld => new()
        {
            MarketHeat    = Balance.EqualWorldMarketHeat,
            CivilianCost  = Balance.EqualWorldCivilianCost,
            Stability     = Balance.EqualWorldStability,
            SanctionsRisk = Balance.EqualWorldSanctionsRisk,
            GeoTension    = Balance.EqualWorldGeoTension,
        },
        GameMode.BlankSlate => new()
        {
            MarketHeat    = Balance.BlankSlateMarketHeat,
            CivilianCost  = Balance.BlankSlateCivilianCost,
            Stability     = Balance.BlankSlateStability,
            SanctionsRisk = Balance.BlankSlateSanctionsRisk,
            GeoTension    = Balance.BlankSlateGeoTension,
        },
        GameMode.HotWorld => new()
        {
            MarketHeat    = Balance.HotWorldMarketHeat,
            CivilianCost  = Balance.HotWorldCivilianCost,
            Stability     = Balance.HotWorldStability,
            SanctionsRisk = Balance.HotWorldSanctionsRisk,
            GeoTension    = Balance.HotWorldGeoTension,
        },
        _ => new()  // Realistic and Custom both start from Realistic defaults;
        {           // Custom overrides individual values after construction.
            MarketHeat    = Balance.StartMarketHeat,
            CivilianCost  = Balance.StartCivilianCost,
            Stability     = Balance.StartStability,
            SanctionsRisk = Balance.StartSanctionsRisk,
            GeoTension    = Balance.StartGeoTension,
        }
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
