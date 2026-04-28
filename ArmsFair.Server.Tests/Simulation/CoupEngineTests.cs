using ArmsFair.Shared;
using ArmsFair.Server.Simulation;
using FluentAssertions;

namespace ArmsFair.Server.Tests.Simulation;

public class CoupEngineTests
{
    // Outcome thresholds (cumulative):
    // [0.00, 0.35) → Success
    // [0.35, 0.60) → Partial
    // [0.60, 0.80) → FailConcealed
    // [0.80, 0.95) → FailExposed
    // [0.95, 1.00) → Blowback

    [Fact]
    public void Roll_BelowSuccessThresh_ReturnsSuccess()
    {
        var result = CoupEngine.Roll(new FixedRng(0.0));
        result.Should().Be(CoupOutcome.Success);
    }

    [Fact]
    public void Roll_AtSuccessThresh_ReturnsPartial()
    {
        var result = CoupEngine.Roll(new FixedRng(0.35));
        result.Should().Be(CoupOutcome.Partial);
    }

    [Fact]
    public void Roll_AtPartialThresh_ReturnsFailConcealed()
    {
        var result = CoupEngine.Roll(new FixedRng(0.60));
        result.Should().Be(CoupOutcome.FailConcealed);
    }

    [Fact]
    public void Roll_AtFailConcealedThresh_ReturnsFailExposed()
    {
        var result = CoupEngine.Roll(new FixedRng(0.80));
        result.Should().Be(CoupOutcome.FailExposed);
    }

    [Fact]
    public void Roll_AtFailExposedThresh_ReturnsBlowback()
    {
        var result = CoupEngine.Roll(new FixedRng(0.95));
        result.Should().Be(CoupOutcome.Blowback);
    }

    [Fact]
    public void AllThresholdsSumToOne()
    {
        float total = Balance.CoupSuccessChance
                    + Balance.CoupPartialChance
                    + Balance.CoupFailConcealedChance
                    + Balance.CoupFailExposedChance
                    + Balance.CoupBlowbackChance;
        total.Should().BeApproximately(1.0f, 0.0001f);
    }

    [Fact]
    public void SuccessRepLoss_IsZero()
    {
        CoupEngine.RepLoss(CoupOutcome.Success).Should().Be(0);
    }

    [Fact]
    public void FailExposed_RepLossMatchesBalance()
    {
        CoupEngine.RepLoss(CoupOutcome.FailExposed).Should().Be(Balance.CoupFailExposedRepHit);
    }

    [Fact]
    public void Blowback_RepLossMatchesBalance()
    {
        CoupEngine.RepLoss(CoupOutcome.Blowback).Should().Be(Balance.CoupBlowbackRepHit);
    }

    private class FixedRng(double value) : Random
    {
        public override double NextDouble() => value;
    }
}
