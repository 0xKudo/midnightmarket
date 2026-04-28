using ArmsFair.Shared;

namespace ArmsFair.Server.Simulation;

public static class SpreadEngine
{
    public static float ComputeSpreadChance(
        int salesIntoZone,
        int treatySignatories,
        int peacekeepingInvestors,
        bool isStage4,
        bool highStability)
    {
        float chance = Balance.SpreadBaseChance
            + salesIntoZone      * Balance.SpreadPerSale
            + treatySignatories  * Balance.SpreadPerTreaty
            + peacekeepingInvestors * Balance.SpreadPeacekeepingFlat;

        if (isStage4) chance += Balance.SpreadStage4Bonus;

        chance = MathF.Max(Balance.SpreadMin, chance);

        if (highStability) chance *= Balance.SpreadHighStabilityMul;

        return MathF.Min(Balance.SpreadMax, chance);
    }

    public static IEnumerable<string> ComputeSpreads(
        string sourceIso,
        IEnumerable<string> neighborIsos,
        float spreadChance,
        Random rng)
    {
        foreach (var iso in neighborIsos)
            if (rng.NextDouble() < spreadChance)
                yield return iso;
    }
}
