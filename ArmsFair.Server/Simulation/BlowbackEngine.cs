using ArmsFair.Shared;
using ArmsFair.Shared.Enums;

namespace ArmsFair.Server.Simulation;

public static class BlowbackEngine
{
    public static float ComputeTraceChance(
        WeaponCategory weapon,
        SaleType       saleType,
        bool           isAidExposed,
        bool           isGrayChannel,
        bool           isVostok,
        bool           highLatentRisk,
        CountryStage   stage)
    {
        float chance = weapon switch
        {
            WeaponCategory.SmallArms          => Balance.TraceSmallArms,
            WeaponCategory.Vehicles           => Balance.TraceVehicles,
            WeaponCategory.CombatHelicopters  => Balance.TraceCombatHelicopters,
            WeaponCategory.FighterJets        => Balance.TraceFighterJets,
            WeaponCategory.Drones             => Balance.TraceDrones,
            WeaponCategory.AirDefense         => Balance.TraceAirDefense,
            WeaponCategory.CruiseMissiles     => Balance.TraceCruiseMissiles,
            WeaponCategory.IcbmComponents     => Balance.TraceIcbmComponents,
            WeaponCategory.NuclearWarhead     => Balance.TraceNuclearWarhead,
            WeaponCategory.FissileMaterials   => Balance.TraceFissileMaterials,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon))
        };

        if (saleType == SaleType.Covert)  chance += Balance.TraceModCovert;
        if (isAidExposed)                 chance += Balance.TraceModAidExposed;
        if (isGrayChannel)                chance += Balance.TraceModGrayChannel;
        if (isVostok)                     chance += Balance.TraceModVostok;
        if (highLatentRisk)               chance += Balance.TraceModHighLatentRisk;

        if (stage == CountryStage.HotWar)              chance += Balance.TraceModHotWar;
        if (stage == CountryStage.HumanitarianCrisis)  chance += Balance.TraceModCrisis;

        return MathF.Max(0f, chance);
    }

    public static int ComputeRepLoss(WeaponCategory weapon, SaleType saleType, bool isDualSupply)
    {
        int loss = weapon switch
        {
            WeaponCategory.SmallArms          => Balance.RepLossBlowbackSmallArms,
            WeaponCategory.Vehicles           => Balance.RepLossBlowbackVehicles,
            WeaponCategory.CombatHelicopters  => Balance.RepLossBlowbackCombatHelicopters,
            WeaponCategory.FighterJets        => Balance.RepLossBlowbackFighterJets,
            WeaponCategory.Drones             => Balance.RepLossBlowbackDrones,
            WeaponCategory.AirDefense         => Balance.RepLossBlowbackAirDefense,
            WeaponCategory.CruiseMissiles     => Balance.RepLossBlowbackCruiseMissiles,
            WeaponCategory.IcbmComponents     => Balance.RepLossBlowbackIcbmComponents,
            WeaponCategory.NuclearWarhead     => Balance.RepLossBlowbackNuclearWarhead,
            WeaponCategory.FissileMaterials   => Balance.RepLossBlowbackFissileMaterials,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon))
        };

        if (saleType == SaleType.Covert) loss += Balance.RepLossBlowbackCovertBonus;
        if (isDualSupply)                loss += Balance.RepLossBlowbackDualSupply;

        return loss;
    }

    public static bool IsTraced(float traceChance, Random rng) =>
        rng.NextDouble() < traceChance;
}
