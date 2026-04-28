using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared.Models.Messages;

public record PhaseStartMessage(GamePhase Phase, int Round, long EndsAt);

public record TrackDeltas(int MarketHeat, int CivilianCost, int Stability, int SanctionsRisk, int GeoTension);

public record SpreadEvent(string FromIso, string ToIso, int NewStage);

public record CountryChange(string Iso, int OldStage, int NewStage, int NewTension);

public record GameEvent(string EventType, string Description, string? PlayerId, string? CountryIso);

public record WorldUpdateMessage(
    TrackDeltas         TrackDeltas,
    WorldTracks         NewTracks,
    List<SpreadEvent>   SpreadEvents,
    List<CountryChange> CountryChanges,
    List<GameEvent>     Events);

public record ArcAnimation(string PlayerId, string TargetIso, string SaleType, int DelayMs);

public record RevealedAction(
    string          PlayerId,
    string          CompanyName,
    SaleType        SaleType,
    string?         TargetIso,
    WeaponCategory? WeaponCategory);

public record RevealMessage(List<RevealedAction> Actions, List<ArcAnimation> Animations);

public record ProfitUpdate(string PlayerId, int ProfitEarned, int NewCapital);

public record ReputationUpdate(string PlayerId, int Delta, int NewReputation, string Reason);

public record SharePriceUpdate(string PlayerId, int NewPrice);

public record BlowbackEvent(string PlayerId, string CountryIso, WeaponCategory Weapon, bool Traced);

public record HumanCostEvent(string Description, string CountryIso);

public record TreatyResolution(string TreatyId, bool Honored, string? BreakerPlayerId);

public record ConsequencesMessage(
    List<ProfitUpdate>     ProfitUpdates,
    List<ReputationUpdate> ReputationUpdates,
    List<SharePriceUpdate> SharePriceUpdates,
    List<BlowbackEvent>    BlowbackEvents,
    List<HumanCostEvent>   HumanCostEvents,
    List<TreatyResolution> TreatyResolutions,
    WorldTracks            NewTracks);

public record WhistleResultMessage(
    int    Level,
    string TargetName,
    string? WeaponCategory,
    object? Procurement,
    object? Action,
    bool   IsAidFraud);

public record FinalScore(string PlayerId, string CompanyName, long Profit, int Reputation, long Composite, long Legacy);

public record GameEndingMessage(string EndingType, string TriggerDescription, List<FinalScore>? FinalScores);

public record StateSync(GameState FullState);

public record ErrorMessage(string Code, string Message);
