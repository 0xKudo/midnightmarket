using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared.Models.Messages;

public record SubmitActionMessage(
    SaleType        SaleType,
    string?         TargetCountry,
    WeaponCategory? WeaponCategory,
    string?         SupplierId,
    bool            IsDualSupply,
    bool            IsProxyRouted);

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

public record ProcurementMessage(WeaponCategory Weapon, string SupplierId, int Quantity);

public record LobbySettingsMessage(
    int    PlayerSlots,
    string TimerPreset,
    bool   VoiceEnabled,
    bool   AiFillIn,
    bool   IsPrivate);
