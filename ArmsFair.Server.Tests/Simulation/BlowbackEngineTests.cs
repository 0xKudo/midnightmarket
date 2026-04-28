using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Server.Simulation;
using FluentAssertions;

namespace ArmsFair.Server.Tests.Simulation;

public class BlowbackEngineTests
{
    [Fact]
    public void SmallArms_BaseTraceChance()
    {
        var chance = BlowbackEngine.ComputeTraceChance(
            WeaponCategory.SmallArms, SaleType.Open,
            isAidExposed: false, isGrayChannel: false, isVostok: false,
            highLatentRisk: false, stage: CountryStage.Active);
        chance.Should().BeApproximately(Balance.TraceSmallArms, 0.0001f);
    }

    [Fact]
    public void Drones_BaseTraceChance()
    {
        var chance = BlowbackEngine.ComputeTraceChance(
            WeaponCategory.Drones, SaleType.Open,
            isAidExposed: false, isGrayChannel: false, isVostok: false,
            highLatentRisk: false, stage: CountryStage.Active);
        chance.Should().BeApproximately(Balance.TraceDrones, 0.0001f);
    }

    [Fact]
    public void CovertSale_AddsTraceMod()
    {
        var open   = BlowbackEngine.ComputeTraceChance(WeaponCategory.Vehicles, SaleType.Open,
            false, false, false, false, CountryStage.Active);
        var covert = BlowbackEngine.ComputeTraceChance(WeaponCategory.Vehicles, SaleType.Covert,
            false, false, false, false, CountryStage.Active);
        covert.Should().BeApproximately(open + Balance.TraceModCovert, 0.0001f);
    }

    [Fact]
    public void GrayChannel_ReducesTraceChance()
    {
        var normal = BlowbackEngine.ComputeTraceChance(WeaponCategory.Vehicles, SaleType.Open,
            false, false, false, false, CountryStage.Active);
        var gray   = BlowbackEngine.ComputeTraceChance(WeaponCategory.Vehicles, SaleType.Open,
            false, isGrayChannel: true, false, false, CountryStage.Active);
        gray.Should().BeLessThan(normal);
    }

    [Fact]
    public void HotWar_IncreasesTraceChance()
    {
        var active = BlowbackEngine.ComputeTraceChance(WeaponCategory.SmallArms, SaleType.Open,
            false, false, false, false, CountryStage.Active);
        var hotWar = BlowbackEngine.ComputeTraceChance(WeaponCategory.SmallArms, SaleType.Open,
            false, false, false, false, CountryStage.HotWar);
        hotWar.Should().BeApproximately(active + Balance.TraceModHotWar, 0.0001f);
    }

    [Fact]
    public void HumanitarianCrisis_IncreasesTraceChance()
    {
        var active  = BlowbackEngine.ComputeTraceChance(WeaponCategory.SmallArms, SaleType.Open,
            false, false, false, false, CountryStage.Active);
        var crisis  = BlowbackEngine.ComputeTraceChance(WeaponCategory.SmallArms, SaleType.Open,
            false, false, false, false, CountryStage.HumanitarianCrisis);
        crisis.Should().BeApproximately(active + Balance.TraceModCrisis, 0.0001f);
    }

    [Fact]
    public void BlowbackRepLoss_SmallArms_Open()
    {
        int loss = BlowbackEngine.ComputeRepLoss(WeaponCategory.SmallArms, SaleType.Open, isDualSupply: false);
        loss.Should().Be(Balance.RepLossBlowbackSmallArms);
    }

    [Fact]
    public void BlowbackRepLoss_Drones_CovertDualSupply()
    {
        int loss = BlowbackEngine.ComputeRepLoss(WeaponCategory.Drones, SaleType.Covert, isDualSupply: true);
        loss.Should().Be(Balance.RepLossBlowbackDrones
                       + Balance.RepLossBlowbackCovertBonus
                       + Balance.RepLossBlowbackDualSupply);
    }

    [Fact]
    public void IsTraced_DeterministicRng_BelowChance()
    {
        // rng always returns 0.0 → always traced when chance > 0
        var alwaysUnder = new AlwaysUnderRng();
        BlowbackEngine.IsTraced(0.5f, alwaysUnder).Should().BeTrue();
    }

    [Fact]
    public void IsTraced_DeterministicRng_AboveChance()
    {
        // rng always returns 1.0 → never traced
        var alwaysOver = new AlwaysOverRng();
        BlowbackEngine.IsTraced(0.5f, alwaysOver).Should().BeFalse();
    }

    private class AlwaysUnderRng : Random { public override double NextDouble() => 0.0; }
    private class AlwaysOverRng  : Random { public override double NextDouble() => 1.0; }
}
