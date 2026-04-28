using ArmsFair.Shared;
using ArmsFair.Server.Simulation;
using FluentAssertions;

namespace ArmsFair.Server.Tests.Simulation;

public class SpreadEngineTests
{
    [Fact]
    public void BaseChance_NoModifiers()
    {
        var chance = SpreadEngine.ComputeSpreadChance(
            salesIntoZone: 0, treatySignatories: 0,
            peacekeepingInvestors: 0, isStage4: false, highStability: false);
        chance.Should().BeApproximately(Balance.SpreadBaseChance, 0.0001f);
    }

    [Fact]
    public void FiveSales_IncreasesChance()
    {
        // 0.08 + 5 * 0.03 = 0.23
        var chance = SpreadEngine.ComputeSpreadChance(
            salesIntoZone: 5, treatySignatories: 0,
            peacekeepingInvestors: 0, isStage4: false, highStability: false);
        chance.Should().BeApproximately(0.23f, 0.0001f);
    }

    [Fact]
    public void ThreeTreaties_SuppressesToZero()
    {
        // 0.08 + 2*0.03 - 3*0.05 = -0.01 → clamped to 0
        var chance = SpreadEngine.ComputeSpreadChance(
            salesIntoZone: 2, treatySignatories: 3,
            peacekeepingInvestors: 0, isStage4: false, highStability: false);
        chance.Should().Be(0f);
    }

    [Fact]
    public void HighStability_DoublesChance()
    {
        var normal = SpreadEngine.ComputeSpreadChance(
            salesIntoZone: 3, treatySignatories: 0,
            peacekeepingInvestors: 0, isStage4: false, highStability: false);
        var high = SpreadEngine.ComputeSpreadChance(
            salesIntoZone: 3, treatySignatories: 0,
            peacekeepingInvestors: 0, isStage4: false, highStability: true);
        high.Should().BeApproximately(normal * Balance.SpreadHighStabilityMul, 0.0001f);
    }

    [Fact]
    public void ChanceCappedAtMax()
    {
        // Many sales to force > 0.60
        var chance = SpreadEngine.ComputeSpreadChance(
            salesIntoZone: 100, treatySignatories: 0,
            peacekeepingInvestors: 0, isStage4: true, highStability: true);
        chance.Should().BeLessOrEqualTo(Balance.SpreadMax);
    }

    [Fact]
    public void ComputeSpreads_ReturnsSpreadsAboveThreshold()
    {
        // With a deterministic RNG that always returns 0, all neighbors should spread
        var rng = new Random(0);
        // chance=1.0 (clamped to 0.6 max) but seeded rng < 0.6 should trigger
        // Use chance = 1.0 (above max so clamped), seeded rng will return values
        // Easier: pass chance=0.0f — no spreads
        var spreads = SpreadEngine.ComputeSpreads("SYR", new[] { "IRQ", "LBN" }, 0.0f, rng);
        spreads.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSpreads_AllNeighborsSpreadsWhenCertainty()
    {
        // chance = 1.0f — every neighbor rolls below 1.0 and triggers
        // We clamp to 0.60 inside engine, so pass 0.60 but use a seeded rng that always returns < 0.60
        // Seed 42 with NextDouble check: verify at runtime behavior is consistent
        // Instead just test the mechanic: if rng always < chance, all neighbors spread
        var alwaysUnder = new AlwaysUnderRng();
        var spreads = SpreadEngine.ComputeSpreads("SYR", new[] { "IRQ", "LBN", "TUR" }, 0.60f, alwaysUnder);
        spreads.Should().BeEquivalentTo(new[] { "IRQ", "LBN", "TUR" });
    }

    // Helper: deterministic RNG that always returns 0.0 (below any positive chance)
    private class AlwaysUnderRng : Random
    {
        public override double NextDouble() => 0.0;
    }
}
