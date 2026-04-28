using ArmsFair.Shared;
using ArmsFair.Shared.Enums;

namespace ArmsFair.Server.Simulation;

public static class ProfitEngine
{
    public static int Calculate(
        WeaponCategory weapon,
        CountryStage   stage,
        SaleType       saleType,
        bool           isDualSupply,
        int            marketHeat,
        int            relationshipPoints)
    {
        if (saleType == SaleType.PeaceBroker) return 0;

        float stageMul = Balance.StageMultiplier[(int)stage];
        if (stageMul == 0) return 0;

        float heatMul = marketHeat >= Balance.MarketHeatBonusThresh
            ? Balance.MarketHeatProfitBonus
            : 1.0f;

        float relMul = relationshipPoints >= 15 ? Balance.RelTier4ProfitBonus
            : relationshipPoints >= 10          ? Balance.RelTier3ProfitBonus
            : relationshipPoints >=  5          ? Balance.RelTier2ProfitBonus
            :                                     1.0f;

        float profit = BaseProfit(weapon) * stageMul * heatMul * relMul;

        if (isDualSupply)                    profit *= Balance.DualSupplyProfitMul;
        if (saleType == SaleType.Covert)     profit *= Balance.CovertProfitPremium;
        if (saleType == SaleType.AidCover)   profit *= Balance.AidCoverProfitPenalty;

        return (int)MathF.Round(profit, MidpointRounding.AwayFromZero);
    }

    private static int BaseProfit(WeaponCategory w) => w switch
    {
        WeaponCategory.SmallArms  => Balance.ProfitSmallArms,
        WeaponCategory.Vehicles   => Balance.ProfitVehicles,
        WeaponCategory.AirDefense => Balance.ProfitAirDefense,
        WeaponCategory.Drones     => Balance.ProfitDrones,
        _ => throw new ArgumentOutOfRangeException(nameof(w))
    };
}
