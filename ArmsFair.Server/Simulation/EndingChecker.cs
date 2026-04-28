using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;

namespace ArmsFair.Server.Simulation;

public record EndingCondition(string Type);

public static class EndingChecker
{
    public static EndingCondition? Check(GameState state, HashSet<string> ceaseFireVoters)
    {
        var t = state.Tracks;

        if (t.Stability    >= 100) return new EndingCondition("total_war");
        if (t.CivilianCost >= 100) return new EndingCondition("global_sanctions");
        if (t.GeoTension   >= 100) return new EndingCondition("great_power_confrontation");

        if (IsMarketSaturation(state.Countries))
            return new EndingCondition("market_saturation");

        if (IsNegotiatedPeace(state, ceaseFireVoters))
            return new EndingCondition("negotiated_peace");

        return null;
    }

    private static bool IsMarketSaturation(List<CountryState> countries)
    {
        int eligible = countries.Count(c => c.InitialStage2Plus);
        if (eligible == 0) return false;
        int failed = countries.Count(c => c.InitialStage2Plus && c.Stage == CountryStage.FailedState);
        return (float)failed / eligible >= 0.40f;
    }

    private static bool IsNegotiatedPeace(GameState state, HashSet<string> ceaseFireVoters)
    {
        if (state.Tracks.Stability >= 20) return false;
        var activeIds = state.Players
            .Where(p => p.Status == "active")
            .Select(p => p.Id)
            .ToHashSet();
        return activeIds.Count > 0 && activeIds.IsSubsetOf(ceaseFireVoters);
    }
}
