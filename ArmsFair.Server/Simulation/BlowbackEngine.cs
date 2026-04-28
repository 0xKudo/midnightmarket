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
            WeaponCategory.SmallArms  => Balance.TraceSmallArms,
            WeaponCategory.Vehicles   => Balance.TraceVehicles,
            WeaponCategory.AirDefense => Balance.TraceAirDefense,
            WeaponCategory.Drones     => Balance.TraceDrones,
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
            WeaponCategory.SmallArms  => Balance.RepLossBlowbackSmallArms,
            WeaponCategory.Vehicles   => Balance.RepLossBlowbackVehicles,
            WeaponCategory.AirDefense => Balance.RepLossBlowbackAirDefense,
            WeaponCategory.Drones     => Balance.RepLossBlowbackDrones,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon))
        };

        if (saleType == SaleType.Covert) loss += Balance.RepLossBlowbackCovertBonus;
        if (isDualSupply)                loss += Balance.RepLossBlowbackDualSupply;

        return loss;
    }

    public static bool IsTraced(float traceChance, Random rng) =>
        rng.NextDouble() < traceChance;
}
