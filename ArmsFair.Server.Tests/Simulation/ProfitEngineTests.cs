using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Server.Simulation;
using FluentAssertions;

namespace ArmsFair.Server.Tests.Simulation;

public class ProfitEngineTests
{
    [Fact]
    public void DroneOpenSaleStage3_BaseProfit()
    {
        // 34 * 1.8 = 61.2 → 61
        var profit = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.HotWar,
            SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        profit.Should().Be(61);
    }

    [Fact]
    public void CovertPremiumApplied()
    {
        var open   = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open,   isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        var covert = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Covert, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        covert.Should().BeGreaterThan(open);
        covert.Should().Be((int)MathF.Round(open * Balance.CovertProfitPremium, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void AidCoverPenaltyApplied()
    {
        var open = ProfitEngine.Calculate(WeaponCategory.Vehicles, CountryStage.Active,
            SaleType.Open,     isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        var aid  = ProfitEngine.Calculate(WeaponCategory.Vehicles, CountryStage.Active,
            SaleType.AidCover, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        aid.Should().BeLessThan(open);
    }

    [Fact]
    public void MarketHeatOver80_AddsProfitBonus()
    {
        var normal = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open, isDualSupply: false, marketHeat: 79, relationshipPoints: 0);
        var hot    = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open, isDualSupply: false, marketHeat: 80, relationshipPoints: 0);
        hot.Should().BeGreaterThan(normal);
    }

    [Fact]
    public void RelationshipTier4_AppliesProfitBonus()
    {
        var tier1 = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        var tier4 = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 15);
        tier4.Should().BeGreaterThan(tier1);
    }

    [Fact]
    public void FailedState_ZeroProfit()
    {
        var profit = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.FailedState,
            SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        profit.Should().Be(0);
    }

    [Fact]
    public void PeaceBroker_ZeroProfit()
    {
        var profit = ProfitEngine.Calculate(WeaponCategory.SmallArms, CountryStage.Active,
            SaleType.PeaceBroker, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        profit.Should().Be(0);
    }

    [Fact]
    public void DualSupply_IncreasesProfit()
    {
        var normal = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        var dual   = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
            SaleType.Open, isDualSupply: true,  marketHeat: 50, relationshipPoints: 0);
        dual.Should().BeGreaterThan(normal);
    }

    [Fact]
    public void SmallArmsStage2_BaseProfit()
    {
        // 4 * 1.0 = 4
        var profit = ProfitEngine.Calculate(WeaponCategory.SmallArms, CountryStage.Active,
            SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
        profit.Should().Be(4);
    }
}
