using ArmsFair.Shared.Enums;

namespace ArmsFair.Shared;

public static class WeaponCatalog
{
    public static readonly IReadOnlyList<WeaponEntry> Items = new List<WeaponEntry>
    {
        new WeaponEntry(WeaponCategory.SmallArms,  "SMALL ARMS",   Balance.CostSmallArms,  Balance.ProfitSmallArms),
        new WeaponEntry(WeaponCategory.Vehicles,   "VEHICLES",     Balance.CostVehicles,   Balance.ProfitVehicles),
        new WeaponEntry(WeaponCategory.AirDefense, "AIR DEFENSE",  Balance.CostAirDefense, Balance.ProfitAirDefense),
        new WeaponEntry(WeaponCategory.Drones,     "DRONES",       Balance.CostDrones,     Balance.ProfitDrones),
    }.AsReadOnly();
}

public record WeaponEntry(
    WeaponCategory Category,
    string         DisplayName,
    int            BaseCostMillions,
    int            BaseProfitMillions);
