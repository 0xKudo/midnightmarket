using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Server.Simulation;
using FluentAssertions;

namespace ArmsFair.Server.Tests.Simulation;

public class TrackEngineTests
{
    private static WorldTracks Base() => new()
    {
        MarketHeat = 30, CivilianCost = 20, Stability = 25,
        SanctionsRisk = 10, GeoTension = 35
    };

    [Fact]
    public void OpenDroneSaleIntoStage3_AppliesCorrectDeltas()
    {
        var tracks = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.Drones, CountryStage.HotWar, isDualSupply: false);
        // drones open: [4,4,2,2,1] × 1.8 (stage3) = [7.2,7.2,3.6,3.6,1.8] → rounded = [7,7,4,4,2]
        tracks.MarketHeat.Should().Be(37);
        tracks.CivilianCost.Should().Be(27);
        tracks.Stability.Should().Be(29);
        tracks.SanctionsRisk.Should().Be(14);
        tracks.GeoTension.Should().Be(37);
    }

    [Fact]
    public void OpenSmallArmsSaleIntoStage1_AppliesHalfMultiplier()
    {
        var tracks = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.SmallArms, CountryStage.Simmering, isDualSupply: false);
        // smallarms open: [1,3,1,0,0] × 0.5 = [0.5,1.5,0.5,0,0] → rounded = [1,2,1,0,0]
        tracks.MarketHeat.Should().Be(31);
        tracks.CivilianCost.Should().Be(22);
        tracks.Stability.Should().Be(26);
        tracks.SanctionsRisk.Should().Be(10);
        tracks.GeoTension.Should().Be(35);
    }

    [Fact]
    public void CovertSale_DoesNotImmediatelyRaiseSanctionsRisk()
    {
        var tracks = TrackEngine.ApplyCovertSale(Base(), WeaponCategory.Drones, CountryStage.HotWar, isDualSupply: false);
        tracks.SanctionsRisk.Should().Be(10); // unchanged
    }

    [Fact]
    public void AidCoverSale_SuppressesCivilianCost()
    {
        var tracks = TrackEngine.ApplyAidCoverSale(Base(), WeaponCategory.Drones, CountryStage.HotWar);
        // drones aid: [4,-1,2,0,1] × 1.8 = [7.2,-1.8,3.6,0,1.8] → rounded = [7,-2,4,0,2]
        tracks.CivilianCost.Should().Be(18); // 20 - 2
    }

    [Fact]
    public void PeaceBrokerAction_AppliesFlatDeltas()
    {
        var tracks = TrackEngine.ApplyPeaceBroker(Base());
        tracks.MarketHeat.Should().Be(29);
        tracks.CivilianCost.Should().Be(19);
        tracks.Stability.Should().Be(23);
        tracks.SanctionsRisk.Should().Be(10);
        tracks.GeoTension.Should().Be(35);
    }

    [Fact]
    public void DualSupply_MultipliesAllDeltas()
    {
        var normal = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.SmallArms, CountryStage.Active, isDualSupply: false);
        var dual   = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.SmallArms, CountryStage.Active, isDualSupply: true);
        (dual.MarketHeat - Base().MarketHeat).Should().BeGreaterThan(normal.MarketHeat - Base().MarketHeat);
    }

    [Fact]
    public void Tracks_AreClampedTo100()
    {
        var high = Base() with { Stability = 99 };
        var result = TrackEngine.ApplyOpenSale(high, WeaponCategory.Drones, CountryStage.HumanitarianCrisis, isDualSupply: false);
        result.Stability.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public void Stage5FailedState_ZeroMultiplier_NoChange()
    {
        var tracks = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.Drones, CountryStage.FailedState, isDualSupply: false);
        tracks.Should().Be(Base());
    }

    [Fact]
    public void CovertTrace_RaisesSanctionsRisk()
    {
        var tracks = TrackEngine.ApplyCovertTrace(Base());
        tracks.SanctionsRisk.Should().Be(10 + Balance.CovertTraceSanctionsHit);
    }
}
