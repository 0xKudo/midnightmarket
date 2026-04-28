using ArmsFair.Shared;

namespace ArmsFair.Server.Simulation;

public enum CoupOutcome { Success, Partial, FailConcealed, FailExposed, Blowback }

public static class CoupEngine
{
    public static CoupOutcome Roll(Random rng)
    {
        double roll = rng.NextDouble();

        double t1 = Balance.CoupSuccessChance;
        double t2 = t1 + Balance.CoupPartialChance;
        double t3 = t2 + Balance.CoupFailConcealedChance;
        double t4 = 1.0 - Balance.CoupBlowbackChance;

        if (roll < t1) return CoupOutcome.Success;
        if (roll < t2) return CoupOutcome.Partial;
        if (roll < t3) return CoupOutcome.FailConcealed;
        if (roll < t4) return CoupOutcome.FailExposed;
        return CoupOutcome.Blowback;
    }

    public static int RepLoss(CoupOutcome outcome) => outcome switch
    {
        CoupOutcome.FailExposed => Balance.CoupFailExposedRepHit,
        CoupOutcome.Blowback    => Balance.CoupBlowbackRepHit,
        _                       => 0
    };
}
