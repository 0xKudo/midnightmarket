using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared.Models;

public record PlayerAction
{
    public required string   PlayerId       { get; init; }
    public required SaleType SaleType       { get; init; }
    public string?           TargetCountry  { get; init; }   // ISO3
    public WeaponCategory?   WeaponCategory { get; init; }
    public string?           SupplierId     { get; init; }
    public bool              IsDualSupply   { get; init; }
    public bool              IsProxyRouted  { get; init; }
}
