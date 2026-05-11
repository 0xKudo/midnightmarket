using ArmsFair.Shared.Enums;
using static ArmsFair.Shared.Enums.WeaponCategory;
using static ArmsFair.Shared.WeaponTab;

namespace ArmsFair.Shared;

public enum WeaponTab { Light, Aircraft, Missiles, Wmd }

public static class WeaponCatalog
{
    public static readonly IReadOnlyList<WeaponEntry> Items = new List<WeaponEntry>
    {
        new(SmallArms,         "SMALL ARMS",          Balance.CostSmallArms,          Balance.ProfitSmallArms,          Light),
        new(Vehicles,          "LIGHT VEHICLES",       Balance.CostVehicles,           Balance.ProfitVehicles,           Light),
        new(CombatHelicopters, "COMBAT HELICOPTERS",  Balance.CostCombatHelicopters,   Balance.ProfitCombatHelicopters,  Aircraft),
        new(FighterJets,       "FIGHTER JETS",         Balance.CostFighterJets,        Balance.ProfitFighterJets,        Aircraft),
        new(Drones,            "DRONES / UAS",         Balance.CostDrones,             Balance.ProfitDrones,             Aircraft),
        new(AirDefense,        "AIR DEFENSE SYSTEMS",  Balance.CostAirDefense,         Balance.ProfitAirDefense,         Missiles),
        new(CruiseMissiles,    "CRUISE MISSILES",      Balance.CostCruiseMissiles,     Balance.ProfitCruiseMissiles,     Missiles),
        new(IcbmComponents,    "ICBM COMPONENTS",      Balance.CostIcbmComponents,     Balance.ProfitIcbmComponents,     Wmd, IsWmd: true),
        new(FissileMaterials,  "FISSILE MATERIALS",    Balance.CostFissileMaterials,   Balance.ProfitFissileMaterials,   Wmd, IsWmd: true),
        new(NuclearWarhead,    "NUCLEAR WARHEAD",      Balance.CostNuclearWarhead,     Balance.ProfitNuclearWarhead,     Wmd, IsWmd: true),
    }.AsReadOnly();
}

public record WeaponEntry(
    WeaponCategory Category,
    string         DisplayName,
    int            BaseCostMillions,
    int            BaseProfitMillions,
    WeaponTab      Tab,
    bool           IsWmd = false);
