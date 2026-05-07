using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;

namespace ArmsFair.Shared.Models.Messages;

public record SubmitActionMessage(
    SaleType        SaleType,
    string?         TargetCountry,
    WeaponCategory? WeaponCategory,
    string?         SupplierId,
    bool            IsDualSupply,
    bool            IsProxyRouted,
    int             Quantity = 1);

public record ChatMessage(
    string  SenderId,
    string  Text,
    string? RecipientId,
    bool    IsPrivate,
    bool    IsSystem);

public record ProposeTreatyMessage(
    List<string> ParticipantIds,
    string       Terms,
    int          DurationRounds);

public record WhistleMessage(int Level, string TargetPlayerId);

public record FundCoupMessage(string TargetCountryIso);

public record ManufactureDemandMessage(string TargetCountryIso);

public record PeacekeepingMessage(string TargetCountryIso);

public record ProcurementMessage(List<WeaponCategory> SelectedWeapons);

public record LobbySettingsMessage(
    int          PlayerSlots,
    string       TimerPreset,
    bool         VoiceEnabled,
    bool         AiFillIn,
    bool         IsPrivate,
    GameMode     GameMode     = GameMode.Realistic,
    string?      ScenarioCode = null,           // Mode 5: load a saved scenario
    WorldTracks? CustomTracks = null,           // Mode 5: manual starting tracks
    List<CustomCountryConfig>? CustomCountries = null); // Mode 5: per-country overrides

// Mode 5 per-country configuration sent from the lobby map editor
public record CustomCountryConfig(string Iso, CountryStage Stage, int Tension);
