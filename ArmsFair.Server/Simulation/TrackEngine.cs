using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;

namespace ArmsFair.Server.Simulation;

public static class TrackEngine
{
    public static WorldTracks ApplyOpenSale(WorldTracks tracks, WeaponCategory weapon, CountryStage stage, bool isDualSupply)
        => Apply(tracks, GetOpenDeltas(weapon), stage, isDualSupply);

    public static WorldTracks ApplyCovertSale(WorldTracks tracks, WeaponCategory weapon, CountryStage stage, bool isDualSupply)
        => Apply(tracks, GetCovertDeltas(weapon), stage, isDualSupply);

    public static WorldTracks ApplyAidCoverSale(WorldTracks tracks, WeaponCategory weapon, CountryStage stage)
        => Apply(tracks, GetAidDeltas(weapon), stage, isDualSupply: false);

    public static WorldTracks ApplyPeaceBroker(WorldTracks tracks) =>
        (tracks with
        {
            MarketHeat   = tracks.MarketHeat   + Balance.PeaceMarketHeat,
            CivilianCost = tracks.CivilianCost + Balance.PeaceCivilianCost,
            Instability    = tracks.Instability    + Balance.PeaceInstability,
        }).Clamp();

    public static WorldTracks ApplyCovertTrace(WorldTracks tracks) =>
        (tracks with { SanctionsRisk = tracks.SanctionsRisk + Balance.CovertTraceSanctionsHit }).Clamp();

    private static WorldTracks Apply(WorldTracks tracks, int[] deltas, CountryStage stage, bool isDualSupply)
    {
        float mul = Balance.StageMultiplier[(int)stage];
        if (isDualSupply) mul *= Balance.DualSupplyTrackMul;

        return (tracks with
        {
            MarketHeat    = tracks.MarketHeat    + Round(deltas[0] * mul),
            CivilianCost  = tracks.CivilianCost  + Round(deltas[1] * mul),
            Instability     = tracks.Instability     + Round(deltas[2] * mul),
            SanctionsRisk = tracks.SanctionsRisk + Round(deltas[3] * mul),
            GeoTension    = tracks.GeoTension    + Round(deltas[4] * mul),
        }).Clamp();
    }

    private static int Round(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    private static int[] GetOpenDeltas(WeaponCategory w) => w switch
    {
        WeaponCategory.SmallArms          => Balance.OpenSmallArms,
        WeaponCategory.Vehicles           => Balance.OpenVehicles,
        WeaponCategory.CombatHelicopters  => Balance.OpenCombatHelicopters,
        WeaponCategory.FighterJets        => Balance.OpenFighterJets,
        WeaponCategory.Drones             => Balance.OpenDrones,
        WeaponCategory.AirDefense         => Balance.OpenAirDefense,
        WeaponCategory.CruiseMissiles     => Balance.OpenCruiseMissiles,
        WeaponCategory.IcbmComponents     => Balance.OpenIcbmComponents,
        WeaponCategory.NuclearWarhead     => Balance.OpenNuclearWarhead,
        WeaponCategory.FissileMaterials   => Balance.OpenFissileMaterials,
        _ => throw new ArgumentOutOfRangeException(nameof(w))
    };

    private static int[] GetCovertDeltas(WeaponCategory w) => w switch
    {
        WeaponCategory.SmallArms          => Balance.CovertSmallArms,
        WeaponCategory.Vehicles           => Balance.CovertVehicles,
        WeaponCategory.CombatHelicopters  => Balance.CovertCombatHelicopters,
        WeaponCategory.FighterJets        => Balance.CovertFighterJets,
        WeaponCategory.Drones             => Balance.CovertDrones,
        WeaponCategory.AirDefense         => Balance.CovertAirDefense,
        WeaponCategory.CruiseMissiles     => Balance.CovertCruiseMissiles,
        WeaponCategory.IcbmComponents     => Balance.CovertIcbmComponents,
        WeaponCategory.NuclearWarhead     => Balance.CovertNuclearWarhead,
        WeaponCategory.FissileMaterials   => Balance.CovertFissileMaterials,
        _ => throw new ArgumentOutOfRangeException(nameof(w))
    };

    private static int[] GetAidDeltas(WeaponCategory w) => w switch
    {
        WeaponCategory.SmallArms          => Balance.AidSmallArms,
        WeaponCategory.Vehicles           => Balance.AidVehicles,
        WeaponCategory.CombatHelicopters  => Balance.AidCombatHelicopters,
        WeaponCategory.FighterJets        => Balance.AidFighterJets,
        WeaponCategory.Drones             => Balance.AidDrones,
        WeaponCategory.AirDefense         => Balance.AidAirDefense,
        WeaponCategory.CruiseMissiles     => Balance.AidCruiseMissiles,
        WeaponCategory.IcbmComponents     => Balance.AidIcbmComponents,
        WeaponCategory.NuclearWarhead     => Balance.AidNuclearWarhead,
        WeaponCategory.FissileMaterials   => Balance.AidFissileMaterials,
        _ => throw new ArgumentOutOfRangeException(nameof(w))
    };
}
