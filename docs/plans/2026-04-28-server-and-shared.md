# Arms Fair — Server & Shared Library Implementation Plan
**Date:** 2026-04-28  
**Scope:** ArmsFair.Shared (.NET 8 class library) + ArmsFair.Server (ASP.NET Core 8 + SignalR)  
**Excludes:** Unity client, GeoJSON preprocessing (separate plans)

---

## Solution Structure

```
ArmsFair/
├── ArmsFair.sln                          (replace existing Godot sln)
├── ArmsFair.Shared/
│   ├── ArmsFair.Shared.csproj
│   ├── Balance.cs                        all tunable constants
│   ├── Enums/
│   │   ├── SaleType.cs
│   │   ├── WeaponCategory.cs
│   │   ├── GamePhase.cs
│   │   └── CountryStage.cs
│   └── Models/
│       ├── GameState.cs
│       ├── PlayerAction.cs
│       ├── WorldTracks.cs
│       ├── CountryState.cs
│       ├── PlayerProfile.cs
│       ├── PlayerStats.cs
│       └── Messages/
│           ├── ServerMessages.cs
│           └── ClientMessages.cs
├── ArmsFair.Server/
│   ├── ArmsFair.Server.csproj
│   ├── Program.cs
│   ├── Hubs/
│   │   └── GameHub.cs
│   ├── Simulation/
│   │   ├── TrackEngine.cs
│   │   ├── SpreadEngine.cs
│   │   ├── BlowbackEngine.cs
│   │   ├── CoupEngine.cs
│   │   └── EndingChecker.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Repositories/
│   │       ├── IGameRepository.cs
│   │       └── GameRepository.cs
│   ├── Services/
│   │   ├── PhaseOrchestrator.cs
│   │   ├── SeedService.cs
│   │   └── TickerService.cs
│   └── Dockerfile
└── ArmsFair.Server.Tests/
    ├── ArmsFair.Server.Tests.csproj
    ├── Simulation/
    │   ├── TrackEngineTests.cs
    │   ├── SpreadEngineTests.cs
    │   ├── BlowbackEngineTests.cs
    │   ├── CoupEngineTests.cs
    │   └── EndingCheckerTests.cs
    └── Services/
        └── PhaseOrchestratorTests.cs
```

---

## Task 1: Scaffold the .NET Solution

**Goal:** Replace the Godot solution with a proper three-project .NET 8 solution.

- [ ] Delete the old Godot artifacts:
  ```bash
  cd "c:/Users/lsgra/Desktop/arms-fair"
  rm "Arms Fair.csproj" "Arms Fair.sln" test.cs test.cs.uid icon.svg icon.svg.import
  ```

- [ ] Create the solution and projects:
  ```bash
  dotnet new sln -n ArmsFair
  dotnet new classlib -n ArmsFair.Shared -f net8.0 -o ArmsFair.Shared
  dotnet new webapi -n ArmsFair.Server -f net8.0 -o ArmsFair.Server
  dotnet new xunit -n ArmsFair.Server.Tests -f net8.0 -o ArmsFair.Server.Tests
  dotnet sln add ArmsFair.Shared/ArmsFair.Shared.csproj
  dotnet sln add ArmsFair.Server/ArmsFair.Server.csproj
  dotnet sln add ArmsFair.Server.Tests/ArmsFair.Server.Tests.csproj
  dotnet add ArmsFair.Server/ArmsFair.Server.csproj reference ArmsFair.Shared/ArmsFair.Shared.csproj
  dotnet add ArmsFair.Server.Tests/ArmsFair.Server.Tests.csproj reference ArmsFair.Shared/ArmsFair.Shared.csproj
  dotnet add ArmsFair.Server.Tests/ArmsFair.Server.Tests.csproj reference ArmsFair.Server/ArmsFair.Server.csproj
  ```

- [ ] Add NuGet packages to Server:
  ```bash
  cd ArmsFair.Server
  dotnet add package Microsoft.AspNetCore.SignalR --version 1.1.0
  dotnet add package Microsoft.EntityFrameworkCore --version 8.0.4
  dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.4
  dotnet add package StackExchange.Redis --version 2.7.33
  dotnet add package FluentValidation.AspNetCore --version 11.3.0
  dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.4
  cd ..
  ```

- [ ] Add NuGet packages to Tests:
  ```bash
  cd ArmsFair.Server.Tests
  dotnet add package FluentAssertions --version 6.12.0
  dotnet add package Moq --version 4.20.70
  cd ..
  ```

- [ ] Verify build:
  ```bash
  dotnet build ArmsFair.sln
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] Commit:
  ```bash
  git init
  git add .
  git commit -m "feat: scaffold three-project .NET 8 solution"
  ```

---

## Task 2: Enums (ArmsFair.Shared)

- [ ] Create `ArmsFair.Shared/Enums/SaleType.cs`:
  ```csharp
  namespace ArmsFair.Shared.Enums;

  public enum SaleType { Open, Covert, AidCover, PeaceBroker }
  ```

- [ ] Create `ArmsFair.Shared/Enums/WeaponCategory.cs`:
  ```csharp
  namespace ArmsFair.Shared.Enums;

  public enum WeaponCategory { SmallArms, Vehicles, AirDefense, Drones }
  ```

- [ ] Create `ArmsFair.Shared/Enums/GamePhase.cs`:
  ```csharp
  namespace ArmsFair.Shared.Enums;

  public enum GamePhase { WorldUpdate, Procurement, Negotiation, Sales, Reveal, Consequences }
  ```

- [ ] Create `ArmsFair.Shared/Enums/CountryStage.cs`:
  ```csharp
  namespace ArmsFair.Shared.Enums;

  public enum CountryStage { Dormant = 0, Simmering = 1, Active = 2, HotWar = 3, HumanitarianCrisis = 4, FailedState = 5 }
  ```

- [ ] Delete the default `Class1.cs` that `dotnet new classlib` creates:
  ```bash
  rm ArmsFair.Shared/Class1.cs
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.sln
  git add ArmsFair.Shared/Enums/
  git commit -m "feat: add shared enums (SaleType, WeaponCategory, GamePhase, CountryStage)"
  ```

---

## Task 3: Balance.cs (ArmsFair.Shared)

All tunable constants live here — single source of truth for both server simulation and client display.

- [ ] Create `ArmsFair.Shared/Balance.cs`:
  ```csharp
  namespace ArmsFair.Shared;

  public static class Balance
  {
      // ── Starting values ──────────────────────────────────────────────────
      public const int StartingCapital       = 50;   // $M
      public const int StartingReputation    = 75;
      public const int StartingSharePrice    = 100;

      public const int StartMarketHeat       = 30;
      public const int StartCivilianCost     = 20;
      public const int StartStability        = 25;
      public const int StartSanctionsRisk    = 10;
      public const int StartGeoTension       = 35;

      // ── Procurement costs ($M) ────────────────────────────────────────────
      public const int CostSmallArms   = 2;
      public const int CostVehicles    = 6;
      public const int CostAirDefense  = 12;
      public const int CostDrones      = 18;

      // ── Base profits ($M) ────────────────────────────────────────────────
      public const int ProfitSmallArms   = 4;
      public const int ProfitVehicles    = 11;
      public const int ProfitAirDefense  = 22;
      public const int ProfitDrones      = 34;

      // ── Stage multipliers [0..5] ─────────────────────────────────────────
      public static readonly float[] StageMultiplier = { 0f, 0.5f, 1.0f, 1.8f, 2.2f, 0f };

      // ── Supplier price modifiers ──────────────────────────────────────────
      public const float SupplierApex        = 1.20f;
      public const float SupplierHorizon     = 1.00f;
      public const float SupplierLongwei     = 0.90f;
      public const float SupplierAlNoor      = 0.95f;
      public const float SupplierUral        = 0.75f;
      public const float SupplierGray        = 0.70f;
      public const float SupplierVostok      = 0.60f;

      // ── Relationship tiers ────────────────────────────────────────────────
      public const float RelTier1Discount = 0.00f;  // 0-4 pts
      public const float RelTier2Discount = 0.05f;  // 5-9 pts
      public const float RelTier3Discount = 0.10f;  // 10-14 pts
      public const float RelTier4Discount = 0.15f;  // 15+ pts
      public const float RelTier2ProfitBonus = 1.08f;
      public const float RelTier3ProfitBonus = 1.15f;
      public const float RelTier4ProfitBonus = 1.22f;

      // ── Sale type modifiers ───────────────────────────────────────────────
      public const float CovertProfitPremium    = 1.30f;
      public const float AidCoverProfitPenalty  = 0.80f;
      public const float DualSupplyTrackMul     = 2.20f;
      public const float DualSupplyProfitMul    = 1.80f;

      // ── Track deltas: [marketHeat, civilianCost, stability, sanctionsRisk, geoTension]
      // Open sale base deltas by weapon
      public static readonly int[] OpenSmallArms  = {  1,  3,  1,  0,  0 };
      public static readonly int[] OpenVehicles   = {  2,  2,  1,  1,  0 };
      public static readonly int[] OpenAirDefense = {  3,  1,  2,  1,  1 };
      public static readonly int[] OpenDrones     = {  4,  4,  2,  2,  1 };

      // Covert: same base, sanctions=0 until traced
      public static readonly int[] CovertSmallArms  = {  1,  3,  1,  0,  0 };
      public static readonly int[] CovertVehicles   = {  2,  2,  1,  0,  0 };
      public static readonly int[] CovertAirDefense = {  3,  1,  2,  0,  1 };
      public static readonly int[] CovertDrones     = {  4,  4,  2,  0,  1 };
      public const int CovertTraceSanctionsHit = 3;

      // Aid cover: civilian cost = -1 (suppressed), stability/heat same as open
      public static readonly int[] AidSmallArms  = {  1, -1,  1,  0,  0 };
      public static readonly int[] AidVehicles   = {  2, -1,  1,  0,  0 };
      public static readonly int[] AidAirDefense = {  3, -1,  2,  0,  1 };
      public static readonly int[] AidDrones     = {  4, -1,  2,  0,  1 };
      public const int AidFraudExposurePenalty = 5;

      // Peace broker flat deltas
      public const int PeaceMarketHeat    = -1;
      public const int PeaceCivilianCost  = -1;
      public const int PeaceStability     = -2;
      public const int PeaceCostToPlayer  = 2;   // $M
      public const int PeaceCreditEarned  = 1;

      // ── Blowback trace chances ────────────────────────────────────────────
      public const float TraceSmallArms   = 0.15f;
      public const float TraceVehicles    = 0.35f;
      public const float TraceAirDefense  = 0.50f;
      public const float TraceDrones      = 0.70f;

      public const float TraceModCovert           = +0.10f;
      public const float TraceModAidExposed       = +0.25f;
      public const float TraceModGrayChannel      = -0.15f;
      public const float TraceModVostok           = -0.20f;
      public const float TraceModHighLatentRisk   = +0.10f;
      public const float TraceModHotWar           = +0.05f;
      public const float TraceModCrisis           = +0.10f;

      // ── Reputation ───────────────────────────────────────────────────────
      public const int RepGainPeaceBroker              = +3;
      public const int RepGainCeasefire                = +8;
      public const int RepGainReconstruction           = +5;
      public const int RepGainCleanRound               = +1;
      public const int RepLossOpenSale                 = -1;
      public const int RepLossCovertDiscovered         = -10;
      public const int RepLossAidFraud                 = -15;
      public const int RepLossInvestigationSettled     = -10;
      public const int RepLossWhistleL3                = -5;
      public const int RepLossTreatyBroken             = -8;
      public const int RepLossCoupExposed              = -15;
      public const int RepLossManufactureDemandExposed = -20;
      public const int RepLossBlowbackSmallArms        = -8;
      public const int RepLossBlowbackVehicles         = -12;
      public const int RepLossBlowbackAirDefense       = -10;
      public const int RepLossBlowbackDrones           = -20;
      public const int RepLossBlowbackCovertBonus      = -5;
      public const int RepLossBlowbackDualSupply       = -8;
      public const int CollapseThreshold               = 0;

      // ── Latent risk accumulation ──────────────────────────────────────────
      public const int LatentPerCovert          = 3;
      public const int LatentPerGrayChannel     = 5;
      public const int LatentPerVostok          = 7;
      public const int LatentPerCoupConcealed   = 5;
      public const int LatentPerManufacture     = 4;
      public const float LatentConversionRate   = 0.5f;  // latent → reputation damage
      public const int LatentMaxPerInvestigation = 25;

      // ── Conflict spread ───────────────────────────────────────────────────
      public const float SpreadBaseChance          = 0.08f;
      public const float SpreadPerSale             = +0.03f;
      public const float SpreadPerTreaty           = -0.05f;
      public const float SpreadPeacekeepingFlat    = -0.04f;
      public const float SpreadStage4Bonus         = +0.04f;
      public const float SpreadMin                 = 0.00f;
      public const float SpreadMax                 = 0.60f;
      public const float SpreadHighStabilityMul    = 2.0f;
      public const int   SpreadHighStabilityThresh = 80;

      // ── Track thresholds ─────────────────────────────────────────────────
      public const int MarketHeatBonusThresh    = 80;
      public const float MarketHeatProfitBonus  = 1.25f;
      public const float MarketHeatCivCostMul   = 2.0f;
      public const int MarketCrashThresh        = 100;
      public const int MarketCrashResetTo       = 40;

      public const int CivCostUnDiscussionThresh = 60;
      public const int CivCostSanctionsThresh    = 75;
      public const int CivCostEndingThresh       = 100;

      public const int StabilitySpreadMulThresh  = 80;
      public const int StabilityEndingThresh     = 100;

      public const int SanctionsLicenseCostThresh = 60;
      public const int SanctionsInvestThresh      = 80;

      public const int GeoTensionBlocThresh       = 70;
      public const int GeoTensionCrisisThresh     = 90;
      public const int GeoTensionEndingThresh     = 100;

      // ── Whistle costs ─────────────────────────────────────────────────────
      public const int WhistleL1Cost          = 3;
      public const int WhistleL2Cost          = 8;
      public const int WhistleL2RepHit        = -3;
      public const int WhistleL3Cost          = 15;
      public const int WhistleL3RepHit        = -5;
      public const int WhistleL3TargetChangeCost = 10;
      public const int WhistleL3TargetChangeRepHit = -3;

      // ── Export licenses ───────────────────────────────────────────────────
      public const int LicenseStandardCost    = 3;
      public const int LicenseRestrictedCost  = 8;
      public const int LobbyingCost           = 5;
      public const float LobbyingSuccessChance = 0.40f;

      // ── Short selling ─────────────────────────────────────────────────────
      public const int ShortStake             = 5;   // $M
      public const float ShortPayoutMultiplier = 3.0f;
      public const int ShortRepThreshold      = 15;   // rep must fall by this much

      // ── Coup ─────────────────────────────────────────────────────────────
      public const float CoupSuccessChance          = 0.35f;
      public const float CoupPartialChance          = 0.25f;
      public const float CoupFailConcealedChance    = 0.20f;
      public const float CoupFailExposedChance      = 0.15f;
      public const float CoupBlowbackChance         = 0.05f;
      public const int   CoupSuccessFavorRounds     = 3;
      public const float CoupSuccessLicenseDiscount = 0.20f;
      public const int   CoupFailExposedRepHit      = -15;
      public const int   CoupFailExposedBanRounds   = 2;
      public const int   CoupFailExposedSanctions   = +8;
      public const int   CoupBlowbackRepHit         = -10;
      public const int   CoupBlowbackGeoTension     = +5;

      // ── Manufacture demand ────────────────────────────────────────────────
      public const int ManufactureCostStage0   = 25;  // $M
      public const int ManufactureCostStage1   = 15;
      public const int ManufactureTensionMin   = 15;
      public const int ManufactureTensionMax   = 20;
      public const int ManufactureToStage1     = 40;  // tension threshold
      public const int ManufactureToStage2     = 70;
      public const int ManufactureExposedRepHit   = -20;
      public const int ManufactureExposedBanRounds = 2;
      public const int ManufactureExposedSanctions = +5;

      // ── Peacekeeping investment ───────────────────────────────────────────
      public const int   PeacekeepingCost          = 10;  // $M
      public const int   PeacekeepingStabilityDelta = -3;
      public const int   PeacekeepingCivilianDelta  = -2;
      public const int   PeacekeepingCoSignatories  = 1;
      public const float PeacekeepingSpreadReduction = 0.04f;

      // ── Reconstruction contracts ──────────────────────────────────────────
      public const int ReconstructionPayoutPerRound = 8;   // $M
      public const int ReconstructionPayoutRounds   = 3;
      public const int ReconstructionRepOnComplete  = +5;

      // ── Phase durations (ms) ─────────────────────────────────────────────
      public const int PhaseWorldUpdate   = 30_000;
      public const int PhaseProcurement   = 60_000;
      public const int PhaseNegotiation   = 120_000;
      public const int PhaseSales         = 90_000;
      public const int PhaseReveal        = 30_000;
      public const int PhaseConsequences  = 60_000;

      // ── Ending conditions ─────────────────────────────────────────────────
      public const int   WorldPeaceStabilityThresh       = 15;
      public const int   WorldPeaceCivCostThresh         = 25;
      public const int   WorldPeaceMaxCountryStage       = 2;
      public const int   WorldPeaceMinReconstruction     = 3;
      public const int   WorldPeaceLegacyBonus           = 200;
      public const float MarketSaturationFailedPct       = 0.40f;
      public const int   NegotiatedPeaceStabilityThresh  = 20;
      public const int   NegotiatedPeaceCreditBonus      = 20;  // $M for 5+ peace credits
      public const int   NegotiatedPeaceMinCredits       = 5;
      public const int   GlobalSanctionsEndgameRounds    = 3;
      public const float GlobalSanctionsGrayLatentMul    = 3.0f;
      public const int   GreatPowerEndgameRounds         = 2;

      // ── Legacy score formula ──────────────────────────────────────────────
      public const int LegacyPerPeaceCredit         = 5;
      public const int LegacyPerCeasefire           = 20;
      public const int LegacyPerReconstruction      = 15;
      public const int LegacyMaxStabilityBonus      = 100;
      public const int LegacyStabilityBonusThresh   = 20;
      public const int LegacyPenaltyPerFailedState  = -10;
      public const float LegacyCivilianCostPenalty  = 0.5f;
  }
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.sln
  git add ArmsFair.Shared/Balance.cs
  git commit -m "feat: add Balance.cs with all tunable constants from spec"
  ```

---

## Task 4: Core Models (ArmsFair.Shared)

- [ ] Create `ArmsFair.Shared/Models/WorldTracks.cs`:
  ```csharp
  namespace ArmsFair.Shared.Models;

  public record WorldTracks
  {
      public int MarketHeat    { get; init; }
      public int CivilianCost  { get; init; }
      public int Stability     { get; init; }
      public int SanctionsRisk { get; init; }
      public int GeoTension    { get; init; }

      public static WorldTracks Initial() => new()
      {
          MarketHeat    = Balance.StartMarketHeat,
          CivilianCost  = Balance.StartCivilianCost,
          Stability     = Balance.StartStability,
          SanctionsRisk = Balance.StartSanctionsRisk,
          GeoTension    = Balance.StartGeoTension,
      };

      public WorldTracks Clamp() => this with
      {
          MarketHeat    = Math.Clamp(MarketHeat,    0, 100),
          CivilianCost  = Math.Clamp(CivilianCost,  0, 100),
          Stability     = Math.Clamp(Stability,     0, 100),
          SanctionsRisk = Math.Clamp(SanctionsRisk, 0, 100),
          GeoTension    = Math.Clamp(GeoTension,    0, 100),
      };
  }
  ```

- [ ] Create `ArmsFair.Shared/Models/CountryState.cs`:
  ```csharp
  using ArmsFair.Shared.Enums;

  namespace ArmsFair.Shared.Models;

  public record CountryState
  {
      public required string Iso        { get; init; }
      public required string Name       { get; init; }
      public CountryStage    Stage      { get; init; }
      public int             Tension    { get; init; }
      public string          DemandType { get; init; } = "none"; // "none" | "covert" | "open"
  }
  ```

- [ ] Create `ArmsFair.Shared/Models/PlayerProfile.cs`:
  ```csharp
  namespace ArmsFair.Shared.Models;

  public record PlayerProfile
  {
      public required string Id          { get; init; }
      public required string Username    { get; init; }
      public required string HomeNation  { get; init; }
      public string?         CompanyName { get; init; }
      public string          Status      { get; init; } = "active"; // "active" | "observer" | "collapsed"
      public int             Capital     { get; init; } = Balance.StartingCapital;
      public int             Reputation  { get; init; } = Balance.StartingReputation;
      public int             SharePrice  { get; init; } = Balance.StartingSharePrice;
      public int             PeaceCredits { get; init; }
      public int             LatentRisk  { get; init; }
  }
  ```

- [ ] Create `ArmsFair.Shared/Models/PlayerAction.cs`:
  ```csharp
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
  ```

- [ ] Create `ArmsFair.Shared/Models/GameState.cs`:
  ```csharp
  using ArmsFair.Shared.Enums;

  namespace ArmsFair.Shared.Models;

  public record GameState
  {
      public required string            GameId     { get; init; }
      public int                        Round      { get; init; }
      public GamePhase                  Phase      { get; init; }
      public required WorldTracks       Tracks     { get; init; }
      public required List<CountryState> Countries { get; init; }
      public required List<PlayerProfile> Players  { get; init; }
      public int                        CompletedReconstructionContracts { get; init; }
      public string                     EndingType { get; init; } = "none";
  }
  ```

- [ ] Create `ArmsFair.Shared/Models/PlayerStats.cs`:
  ```csharp
  namespace ArmsFair.Shared.Models;

  public record PlayerStats
  {
      public int   GamesPlayed              { get; init; }
      public int   GamesWon                 { get; init; }
      public int   WarsStarted              { get; init; }
      public int   FailedStatesCaused       { get; init; }
      public int   SmallArmsSold            { get; init; }
      public int   VehiclesSold             { get; init; }
      public int   AirDefenseSold           { get; init; }
      public int   DronesSold               { get; init; }
      public long  TotalProfitEarned        { get; init; }
      public long  TotalCivilianCost        { get; init; }
      public int   CeasefiresBrokered       { get; init; }
      public int   CoupsFunded              { get; init; }
      public int   CoupsSucceeded           { get; init; }
      public int   TimesWhistleblown        { get; init; }
      public int   TimesWhistleblower       { get; init; }
      public int   TotalWarParticipations   { get; init; }
      public int   WorldPeaceAchieved       { get; init; }
      public int   CompanyCollapses         { get; init; }
      public int   ReconstructionWins       { get; init; }
      public float AvgFinalReputation       { get; init; }
  }
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.sln
  git add ArmsFair.Shared/Models/
  git commit -m "feat: add shared models (WorldTracks, CountryState, PlayerProfile, PlayerAction, GameState, PlayerStats)"
  ```

---

## Task 5: Message Types (ArmsFair.Shared)

- [ ] Create `ArmsFair.Shared/Models/Messages/ServerMessages.cs`:
  ```csharp
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
      string         PlayerId,
      string         CompanyName,
      SaleType       SaleType,
      string?        TargetIso,
      WeaponCategory? WeaponCategory);

  public record RevealMessage(List<RevealedAction> Actions, List<ArcAnimation> Animations);

  public record ProfitUpdate(string PlayerId, int ProfitEarned, int NewCapital);

  public record ReputationUpdate(string PlayerId, int Delta, int NewReputation, string Reason);

  public record SharePriceUpdate(string PlayerId, int NewPrice);

  public record BlowbackEvent(string PlayerId, string CountryIso, WeaponCategory Weapon, bool Traced);

  public record HumanCostEvent(string Description, string CountryIso);

  public record TreatyResolution(string TreatyId, bool Honored, string? BreakerPlayerId);

  public record ConsequencesMessage(
      List<ProfitUpdate>       ProfitUpdates,
      List<ReputationUpdate>   ReputationUpdates,
      List<SharePriceUpdate>   SharePriceUpdates,
      List<BlowbackEvent>      BlowbackEvents,
      List<HumanCostEvent>     HumanCostEvents,
      List<TreatyResolution>   TreatyResolutions,
      WorldTracks              NewTracks);

  public record WhistleResultMessage(
      int             Level,
      string          TargetName,
      string?         WeaponCategory,
      object?         Procurement,
      object?         Action,
      bool            IsAidFraud);

  public record FinalScore(string PlayerId, string CompanyName, long Profit, int Reputation, long Composite, long Legacy);

  public record GameEndingMessage(string EndingType, string TriggerDescription, List<FinalScore>? FinalScores);

  public record StateSync(GameState FullState);

  public record ErrorMessage(string Code, string Message);
  ```

- [ ] Create `ArmsFair.Shared/Models/Messages/ClientMessages.cs`:
  ```csharp
  using ArmsFair.Shared.Enums;

  namespace ArmsFair.Shared.Models.Messages;

  public record SubmitActionMessage(
      SaleType       SaleType,
      string?        TargetCountry,
      WeaponCategory? WeaponCategory,
      string?        SupplierId,
      bool           IsDualSupply,
      bool           IsProxyRouted);

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
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.sln
  git add ArmsFair.Shared/Models/Messages/
  git commit -m "feat: add SignalR message types (server→client and client→server)"
  ```

---

## Task 6: TrackEngine with Tests

The track engine applies sale deltas to world tracks. Pure functions, no I/O — fully testable.

- [ ] Write failing tests first in `ArmsFair.Server.Tests/Simulation/TrackEngineTests.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Shared.Models;
  using ArmsFair.Server.Simulation;
  using FluentAssertions;

  namespace ArmsFair.Server.Tests.Simulation;

  public class TrackEngineTests
  {
      private static WorldTracks Base() => new()
      {
          MarketHeat = 30, CivilianCost = 20, Stability = 25,
          SanctionsRisk = 10, GeoTension = 35
      };

      [Fact]
      public void OpenDroneSaleIntoStage3_AppliesCorrectDeltas()
      {
          var tracks = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.Drones, CountryStage.HotWar, isDualSupply: false);
          // drones open: [4,4,2,2,1] × 1.8 (stage3) = [7,7,4,4,2] rounded
          tracks.MarketHeat.Should().Be(37);
          tracks.CivilianCost.Should().Be(27);
          tracks.Stability.Should().Be(29);
          tracks.SanctionsRisk.Should().Be(14);
          tracks.GeoTension.Should().Be(37);
      }

      [Fact]
      public void OpenSmallArmsSaleIntoStage1_AppliesHalfMultiplier()
      {
          var tracks = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.SmallArms, CountryStage.Simmering, isDualSupply: false);
          // smallarms open: [1,3,1,0,0] × 0.5 = [1,2,1,0,0] rounded
          tracks.MarketHeat.Should().Be(31);
          tracks.CivilianCost.Should().Be(22);
          tracks.Stability.Should().Be(26);
          tracks.SanctionsRisk.Should().Be(10);
          tracks.GeoTension.Should().Be(35);
      }

      [Fact]
      public void CovertSale_DoesNotImmediatelyRaiseSanctionsRisk()
      {
          var tracks = TrackEngine.ApplyCovertSale(Base(), WeaponCategory.Drones, CountryStage.HotWar, isDualSupply: false);
          tracks.SanctionsRisk.Should().Be(10); // unchanged
      }

      [Fact]
      public void AidCoverSale_SuppressesCivilianCost()
      {
          var tracks = TrackEngine.ApplyAidCoverSale(Base(), WeaponCategory.Drones, CountryStage.HotWar);
          // drones aid: [4,-1,2,0,1] × 1.8 = [7,-2,4,0,2]
          tracks.CivilianCost.Should().Be(18); // 20 - 2
      }

      [Fact]
      public void PeaceBrokerAction_AppliesFlatDeltas()
      {
          var tracks = TrackEngine.ApplyPeaceBroker(Base());
          tracks.MarketHeat.Should().Be(29);
          tracks.CivilianCost.Should().Be(19);
          tracks.Stability.Should().Be(23);
          tracks.SanctionsRisk.Should().Be(10);
          tracks.GeoTension.Should().Be(35);
      }

      [Fact]
      public void DualSupply_MultipliesAllDeltas()
      {
          var normal = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.SmallArms, CountryStage.Active, isDualSupply: false);
          var dual   = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.SmallArms, CountryStage.Active, isDualSupply: true);
          (dual.MarketHeat - Base().MarketHeat).Should().BeGreaterThan(normal.MarketHeat - Base().MarketHeat);
      }

      [Fact]
      public void Tracks_AreClampedTo100()
      {
          var high = Base() with { Stability = 99 };
          var result = TrackEngine.ApplyOpenSale(high, WeaponCategory.Drones, CountryStage.HumanitarianCrisis, isDualSupply: false);
          result.Stability.Should().BeLessOrEqualTo(100);
      }

      [Fact]
      public void Stage5FailedState_ZeroMultiplier_NoChange()
      {
          var tracks = TrackEngine.ApplyOpenSale(Base(), WeaponCategory.Drones, CountryStage.FailedState, isDualSupply: false);
          tracks.Should().Be(Base());
      }
  }
  ```

- [ ] Run tests to confirm they fail:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "TrackEngineTests" 2>&1 | tail -5
  ```
  Expected: compile error — `TrackEngine` doesn't exist yet.

- [ ] Create `ArmsFair.Server/Simulation/TrackEngine.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Shared.Models;

  namespace ArmsFair.Server.Simulation;

  public static class TrackEngine
  {
      public static WorldTracks ApplyOpenSale(WorldTracks tracks, WeaponCategory weapon, CountryStage stage, bool isDualSupply)
      {
          var deltas = GetOpenDeltas(weapon);
          return Apply(tracks, deltas, stage, isDualSupply);
      }

      public static WorldTracks ApplyCovertSale(WorldTracks tracks, WeaponCategory weapon, CountryStage stage, bool isDualSupply)
      {
          var deltas = GetCovertDeltas(weapon);
          return Apply(tracks, deltas, stage, isDualSupply);
      }

      public static WorldTracks ApplyAidCoverSale(WorldTracks tracks, WeaponCategory weapon, CountryStage stage)
      {
          var deltas = GetAidDeltas(weapon);
          return Apply(tracks, deltas, stage, isDualSupply: false);
      }

      public static WorldTracks ApplyPeaceBroker(WorldTracks tracks) =>
          (tracks with
          {
              MarketHeat   = tracks.MarketHeat   + Balance.PeaceMarketHeat,
              CivilianCost = tracks.CivilianCost + Balance.PeaceCivilianCost,
              Stability    = tracks.Stability    + Balance.PeaceStability,
          }).Clamp();

      public static WorldTracks ApplyCovertTrace(WorldTracks tracks) =>
          (tracks with { SanctionsRisk = tracks.SanctionsRisk + Balance.CovertTraceSanctionsHit }).Clamp();

      private static WorldTracks Apply(WorldTracks tracks, int[] deltas, CountryStage stage, bool isDualSupply)
      {
          float mul = Balance.StageMultiplier[(int)stage];
          if (isDualSupply) mul *= Balance.DualSupplyTrackMul;

          return (tracks with
          {
              MarketHeat    = tracks.MarketHeat    + Round(deltas[0] * mul),
              CivilianCost  = tracks.CivilianCost  + Round(deltas[1] * mul),
              Stability     = tracks.Stability     + Round(deltas[2] * mul),
              SanctionsRisk = tracks.SanctionsRisk + Round(deltas[3] * mul),
              GeoTension    = tracks.GeoTension    + Round(deltas[4] * mul),
          }).Clamp();
      }

      private static int Round(float v) => (int)MathF.Round(v);

      private static int[] GetOpenDeltas(WeaponCategory w) => w switch
      {
          WeaponCategory.SmallArms   => Balance.OpenSmallArms,
          WeaponCategory.Vehicles    => Balance.OpenVehicles,
          WeaponCategory.AirDefense  => Balance.OpenAirDefense,
          WeaponCategory.Drones      => Balance.OpenDrones,
          _ => throw new ArgumentOutOfRangeException(nameof(w))
      };

      private static int[] GetCovertDeltas(WeaponCategory w) => w switch
      {
          WeaponCategory.SmallArms   => Balance.CovertSmallArms,
          WeaponCategory.Vehicles    => Balance.CovertVehicles,
          WeaponCategory.AirDefense  => Balance.CovertAirDefense,
          WeaponCategory.Drones      => Balance.CovertDrones,
          _ => throw new ArgumentOutOfRangeException(nameof(w))
      };

      private static int[] GetAidDeltas(WeaponCategory w) => w switch
      {
          WeaponCategory.SmallArms   => Balance.AidSmallArms,
          WeaponCategory.Vehicles    => Balance.AidVehicles,
          WeaponCategory.AirDefense  => Balance.AidAirDefense,
          WeaponCategory.Drones      => Balance.AidDrones,
          _ => throw new ArgumentOutOfRangeException(nameof(w))
      };
  }
  ```

- [ ] Run tests:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "TrackEngineTests" -v
  ```
  Expected: all 8 tests PASS.

- [ ] Commit:
  ```bash
  git add ArmsFair.Server/Simulation/TrackEngine.cs ArmsFair.Server.Tests/Simulation/TrackEngineTests.cs
  git commit -m "feat: TrackEngine with full test coverage"
  ```

---

## Task 7: ProfitEngine with Tests

- [ ] Create `ArmsFair.Server.Tests/Simulation/ProfitEngineTests.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Server.Simulation;
  using FluentAssertions;

  namespace ArmsFair.Server.Tests.Simulation;

  public class ProfitEngineTests
  {
      [Fact]
      public void DroneOpenSaleStage3_BaseProfit()
      {
          // 34 * 1.8 = 61.2 → 61
          var profit = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.HotWar,
              SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          profit.Should().Be(61);
      }

      [Fact]
      public void CovertPremiumApplied()
      {
          var open   = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
              SaleType.Open,   isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          var covert = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
              SaleType.Covert, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          covert.Should().BeGreaterThan(open);
          covert.Should().Be((int)Math.Round(open * Balance.CovertProfitPremium));
      }

      [Fact]
      public void AidCoverPenaltyApplied()
      {
          var open    = ProfitEngine.Calculate(WeaponCategory.Vehicles, CountryStage.Active,
              SaleType.Open,     isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          var aid     = ProfitEngine.Calculate(WeaponCategory.Vehicles, CountryStage.Active,
              SaleType.AidCover, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          aid.Should().BeLessThan(open);
      }

      [Fact]
      public void MarketHeatOver80_AddsProfitBonus()
      {
          var normal = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
              SaleType.Open, isDualSupply: false, marketHeat: 79, relationshipPoints: 0);
          var hot    = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
              SaleType.Open, isDualSupply: false, marketHeat: 80, relationshipPoints: 0);
          hot.Should().BeGreaterThan(normal);
          hot.Should().Be((int)Math.Round(normal * Balance.MarketHeatProfitBonus));
      }

      [Fact]
      public void RelationshipTier4_AppliesProfitBonus()
      {
          var tier1 = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
              SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          var tier4 = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.Active,
              SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 15);
          tier4.Should().BeGreaterThan(tier1);
      }

      [Fact]
      public void FailedState_ZeroProfit()
      {
          var profit = ProfitEngine.Calculate(WeaponCategory.Drones, CountryStage.FailedState,
              SaleType.Open, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          profit.Should().Be(0);
      }

      [Fact]
      public void PeaceBroker_ZeroProfit()
      {
          var profit = ProfitEngine.Calculate(WeaponCategory.SmallArms, CountryStage.Active,
              SaleType.PeaceBroker, isDualSupply: false, marketHeat: 50, relationshipPoints: 0);
          profit.Should().Be(0);
      }
  }
  ```

- [ ] Run to confirm failure, then create `ArmsFair.Server/Simulation/ProfitEngine.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;

  namespace ArmsFair.Server.Simulation;

  public static class ProfitEngine
  {
      public static int Calculate(
          WeaponCategory weapon,
          CountryStage   stage,
          SaleType       saleType,
          bool           isDualSupply,
          int            marketHeat,
          int            relationshipPoints)
      {
          if (saleType == SaleType.PeaceBroker) return 0;

          float stageMul = Balance.StageMultiplier[(int)stage];
          if (stageMul == 0) return 0;

          float heatMul = marketHeat >= Balance.MarketHeatBonusThresh
              ? Balance.MarketHeatProfitBonus
              : 1.0f;

          float relMul = relationshipPoints >= 15 ? Balance.RelTier4ProfitBonus
              : relationshipPoints >= 10          ? Balance.RelTier3ProfitBonus
              : relationshipPoints >=  5          ? Balance.RelTier2ProfitBonus
              :                                     1.0f;

          float profit = BaseProfit(weapon) * stageMul * heatMul * relMul;

          if (isDualSupply)              profit *= Balance.DualSupplyProfitMul;
          if (saleType == SaleType.Covert)    profit *= Balance.CovertProfitPremium;
          if (saleType == SaleType.AidCover)  profit *= Balance.AidCoverProfitPenalty;

          return (int)MathF.Round(profit);
      }

      private static int BaseProfit(WeaponCategory w) => w switch
      {
          WeaponCategory.SmallArms  => Balance.ProfitSmallArms,
          WeaponCategory.Vehicles   => Balance.ProfitVehicles,
          WeaponCategory.AirDefense => Balance.ProfitAirDefense,
          WeaponCategory.Drones     => Balance.ProfitDrones,
          _ => throw new ArgumentOutOfRangeException(nameof(w))
      };
  }
  ```

- [ ] Run tests and commit:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "ProfitEngineTests" -v
  git add ArmsFair.Server/Simulation/ProfitEngine.cs ArmsFair.Server.Tests/Simulation/ProfitEngineTests.cs
  git commit -m "feat: ProfitEngine with full test coverage"
  ```

---

## Task 8: SpreadEngine with Tests

- [ ] Create `ArmsFair.Server.Tests/Simulation/SpreadEngineTests.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Server.Simulation;
  using FluentAssertions;

  namespace ArmsFair.Server.Tests.Simulation;

  public class SpreadEngineTests
  {
      [Fact]
      public void BaseChance_NoModifiers()
      {
          float chance = SpreadEngine.ComputeSpreadChance(
              salesIntoZone: 0, treatySignatories: 0,
              peacekeepingInvestors: 0, isStage4: false, highStability: false);
          chance.Should().Be(Balance.SpreadBaseChance);
      }

      [Fact]
      public void FiveSalesIntoZone_IncreasesChance()
      {
          float chance = SpreadEngine.ComputeSpreadChance(
              salesIntoZone: 5, treatySignatories: 0,
              peacekeepingInvestors: 0, isStage4: false, highStability: false);
          chance.Should().BeApproximately(0.08f + 5 * 0.03f, 0.001f);
      }

      [Fact]
      public void ThreeTreatySignatories_SuppressesSpreadToZero()
      {
          // 0.08 + 2*0.03 - 3*0.05 = -0.01 → clamped to 0
          float chance = SpreadEngine.ComputeSpreadChance(
              salesIntoZone: 2, treatySignatories: 3,
              peacekeepingInvestors: 0, isStage4: false, highStability: false);
          chance.Should().Be(0f);
      }

      [Fact]
      public void HighStability_DoublesChance()
      {
          float normal = SpreadEngine.ComputeSpreadChance(
              salesIntoZone: 2, treatySignatories: 0,
              peacekeepingInvestors: 0, isStage4: false, highStability: false);
          float high = SpreadEngine.ComputeSpreadChance(
              salesIntoZone: 2, treatySignatories: 0,
              peacekeepingInvestors: 0, isStage4: false, highStability: true);
          high.Should().BeApproximately(normal * 2, 0.001f);
      }

      [Fact]
      public void Chance_CappedAt60Percent()
      {
          float chance = SpreadEngine.ComputeSpreadChance(
              salesIntoZone: 50, treatySignatories: 0,
              peacekeepingInvestors: 0, isStage4: true, highStability: true);
          chance.Should().BeLessOrEqualTo(Balance.SpreadMax);
      }

      [Fact]
      public void ComputeSpreads_ReturnsCountriesAboveThreshold()
      {
          var rng = new Random(42);
          // With 100% chance, all neighbors should spread
          var results = SpreadEngine.ComputeSpreads(
              sourceIso: "TST",
              neighborIsos: new[] { "N1", "N2", "N3" },
              spreadChance: 1.0f,
              rng: rng);
          results.Should().BeEquivalentTo(new[] { "N1", "N2", "N3" });
      }
  }
  ```

- [ ] Create `ArmsFair.Server/Simulation/SpreadEngine.cs`:
  ```csharp
  using ArmsFair.Shared;

  namespace ArmsFair.Server.Simulation;

  public static class SpreadEngine
  {
      public static float ComputeSpreadChance(
          int  salesIntoZone,
          int  treatySignatories,
          int  peacekeepingInvestors,
          bool isStage4,
          bool highStability)
      {
          float chance = Balance.SpreadBaseChance
              + salesIntoZone        * Balance.SpreadPerSale
              + treatySignatories    * Balance.SpreadPerTreaty
              + peacekeepingInvestors * Balance.SpreadPeacekeepingFlat
              + (isStage4 ? Balance.SpreadStage4Bonus : 0);

          chance = Math.Clamp(chance, Balance.SpreadMin, Balance.SpreadMax);

          if (highStability)
              chance = Math.Min(chance * Balance.SpreadHighStabilityMul, Balance.SpreadMax);

          return chance;
      }

      public static IEnumerable<string> ComputeSpreads(
          string   sourceIso,
          IEnumerable<string> neighborIsos,
          float    spreadChance,
          Random   rng)
      {
          foreach (var iso in neighborIsos)
              if (rng.NextSingle() < spreadChance)
                  yield return iso;
      }
  }
  ```

- [ ] Run tests and commit:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "SpreadEngineTests" -v
  git add ArmsFair.Server/Simulation/SpreadEngine.cs ArmsFair.Server.Tests/Simulation/SpreadEngineTests.cs
  git commit -m "feat: SpreadEngine with full test coverage"
  ```

---

## Task 9: BlowbackEngine with Tests

- [ ] Create `ArmsFair.Server.Tests/Simulation/BlowbackEngineTests.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Server.Simulation;
  using FluentAssertions;

  namespace ArmsFair.Server.Tests.Simulation;

  public class BlowbackEngineTests
  {
      [Fact]
      public void Drones_HighBaseTraceChance()
      {
          float chance = BlowbackEngine.ComputeTraceChance(
              WeaponCategory.Drones, SaleType.Open, supplierId: "horizon_arms",
              latentRisk: 0, stage: CountryStage.Active);
          chance.Should().Be(Balance.TraceDrones);
      }

      [Fact]
      public void SmallArms_LowBaseTraceChance()
      {
          float chance = BlowbackEngine.ComputeTraceChance(
              WeaponCategory.SmallArms, SaleType.Open, supplierId: "horizon_arms",
              latentRisk: 0, stage: CountryStage.Active);
          chance.Should().Be(Balance.TraceSmallArms);
      }

      [Fact]
      public void CovertModifier_IncreasesTraceChance()
      {
          float open   = BlowbackEngine.ComputeTraceChance(WeaponCategory.Drones, SaleType.Open,   "horizon_arms", 0, CountryStage.Active);
          float covert = BlowbackEngine.ComputeTraceChance(WeaponCategory.Drones, SaleType.Covert, "horizon_arms", 0, CountryStage.Active);
          covert.Should().BeGreaterThan(open);
      }

      [Fact]
      public void GrayChannel_DecreasesTraceChance()
      {
          float normal = BlowbackEngine.ComputeTraceChance(WeaponCategory.Drones, SaleType.Open, "horizon_arms", 0, CountryStage.Active);
          float gray   = BlowbackEngine.ComputeTraceChance(WeaponCategory.Drones, SaleType.Open, "gray_channel", 0, CountryStage.Active);
          gray.Should().BeLessThan(normal);
      }

      [Fact]
      public void HighLatentRisk_IncreasesTraceChance()
      {
          float low  = BlowbackEngine.ComputeTraceChance(WeaponCategory.SmallArms, SaleType.Open, "horizon_arms", 5,  CountryStage.Active);
          float high = BlowbackEngine.ComputeTraceChance(WeaponCategory.SmallArms, SaleType.Open, "horizon_arms", 25, CountryStage.Active);
          high.Should().BeGreaterThan(low);
      }

      [Fact]
      public void ReputationHit_Drones_IsHighest()
      {
          int hit = BlowbackEngine.ComputeRepHit(WeaponCategory.Drones, isCovert: false, isDualSupply: false);
          hit.Should().Be(Balance.RepLossBlowbackDrones);
      }

      [Fact]
      public void ReputationHit_CovertBonus_Applied()
      {
          int open   = BlowbackEngine.ComputeRepHit(WeaponCategory.SmallArms, isCovert: false, isDualSupply: false);
          int covert = BlowbackEngine.ComputeRepHit(WeaponCategory.SmallArms, isCovert: true,  isDualSupply: false);
          covert.Should().BeLessThan(open); // more negative
          (covert - open).Should().Be(Balance.RepLossBlowbackCovertBonus);
      }
  }
  ```

- [ ] Create `ArmsFair.Server/Simulation/BlowbackEngine.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;

  namespace ArmsFair.Server.Simulation;

  public static class BlowbackEngine
  {
      public static float ComputeTraceChance(
          WeaponCategory weapon,
          SaleType       saleType,
          string         supplierId,
          int            latentRisk,
          CountryStage   stage)
      {
          float chance = weapon switch
          {
              WeaponCategory.SmallArms  => Balance.TraceSmallArms,
              WeaponCategory.Vehicles   => Balance.TraceVehicles,
              WeaponCategory.AirDefense => Balance.TraceAirDefense,
              WeaponCategory.Drones     => Balance.TraceDrones,
              _ => 0f
          };

          if (saleType == SaleType.Covert)   chance += Balance.TraceModCovert;
          if (supplierId == "gray_channel")   chance += Balance.TraceModGrayChannel;
          if (supplierId == "vostok_special") chance += Balance.TraceModVostok;
          if (latentRisk > 20)               chance += Balance.TraceModHighLatentRisk;
          if (stage == CountryStage.HotWar)               chance += Balance.TraceModHotWar;
          if (stage == CountryStage.HumanitarianCrisis)   chance += Balance.TraceModCrisis;

          return Math.Clamp(chance, 0f, 1f);
      }

      public static int ComputeRepHit(WeaponCategory weapon, bool isCovert, bool isDualSupply)
      {
          int hit = weapon switch
          {
              WeaponCategory.SmallArms  => Balance.RepLossBlowbackSmallArms,
              WeaponCategory.Vehicles   => Balance.RepLossBlowbackVehicles,
              WeaponCategory.AirDefense => Balance.RepLossBlowbackAirDefense,
              WeaponCategory.Drones     => Balance.RepLossBlowbackDrones,
              _ => 0
          };
          if (isCovert)    hit += Balance.RepLossBlowbackCovertBonus;
          if (isDualSupply) hit += Balance.RepLossBlowbackDualSupply;
          return hit;
      }

      public static int ComputeLatentRiskGain(SaleType saleType, string supplierId) =>
          saleType == SaleType.Covert
              ? supplierId switch
              {
                  "vostok_special" => Balance.LatentPerVostok,
                  "gray_channel"   => Balance.LatentPerGrayChannel,
                  _                => Balance.LatentPerCovert,
              }
              : 0;
  }
  ```

- [ ] Run tests and commit:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "BlowbackEngineTests" -v
  git add ArmsFair.Server/Simulation/BlowbackEngine.cs ArmsFair.Server.Tests/Simulation/BlowbackEngineTests.cs
  git commit -m "feat: BlowbackEngine with test coverage"
  ```

---

## Task 10: CoupEngine with Tests

- [ ] Create `ArmsFair.Server.Tests/Simulation/CoupEngineTests.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Server.Simulation;
  using FluentAssertions;

  namespace ArmsFair.Server.Tests.Simulation;

  public class CoupEngineTests
  {
      [Fact]
      public void Cost_ScalesWithTension()
      {
          int low  = CoupEngine.ComputeCost(tension: 10);
          int high = CoupEngine.ComputeCost(tension: 90);
          high.Should().BeGreaterThan(low);
          low.Should().BeGreaterOrEqualTo(20);
          high.Should().BeLessOrEqualTo(40);
      }

      [Fact]
      public void OutcomeProbabilities_SumToOne()
      {
          float total = Balance.CoupSuccessChance + Balance.CoupPartialChance
              + Balance.CoupFailConcealedChance + Balance.CoupFailExposedChance
              + Balance.CoupBlowbackChance;
          total.Should().BeApproximately(1.0f, 0.001f);
      }

      [Theory]
      [InlineData(0.00f, "success")]
      [InlineData(0.34f, "success")]
      [InlineData(0.35f, "partial")]
      [InlineData(0.59f, "partial")]
      [InlineData(0.60f, "failure_concealed")]
      [InlineData(0.79f, "failure_concealed")]
      [InlineData(0.80f, "failure_exposed")]
      [InlineData(0.94f, "failure_exposed")]
      [InlineData(0.95f, "blowback")]
      [InlineData(0.99f, "blowback")]
      public void Roll_MapsToCorrectOutcome(float roll, string expected)
      {
          CoupEngine.RollOutcome(roll).Should().Be(expected);
      }
  }
  ```

- [ ] Create `ArmsFair.Server/Simulation/CoupEngine.cs`:
  ```csharp
  using ArmsFair.Shared;

  namespace ArmsFair.Server.Simulation;

  public static class CoupEngine
  {
      public static int ComputeCost(int tension) =>
          (int)Math.Round(20 + (tension / 100f) * 20);

      public static string RollOutcome(float roll)
      {
          if (roll < Balance.CoupSuccessChance)                                   return "success";
          if (roll < Balance.CoupSuccessChance + Balance.CoupPartialChance)       return "partial";
          if (roll < Balance.CoupSuccessChance + Balance.CoupPartialChance
                   + Balance.CoupFailConcealedChance)                             return "failure_concealed";
          if (roll < Balance.CoupSuccessChance + Balance.CoupPartialChance
                   + Balance.CoupFailConcealedChance + Balance.CoupFailExposedChance) return "failure_exposed";
          return "blowback";
      }
  }
  ```

- [ ] Run tests and commit:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "CoupEngineTests" -v
  git add ArmsFair.Server/Simulation/CoupEngine.cs ArmsFair.Server.Tests/Simulation/CoupEngineTests.cs
  git commit -m "feat: CoupEngine with test coverage"
  ```

---

## Task 11: EndingChecker with Tests

- [ ] Create `ArmsFair.Server.Tests/Simulation/EndingCheckerTests.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Shared.Models;
  using ArmsFair.Server.Simulation;
  using FluentAssertions;

  namespace ArmsFair.Server.Tests.Simulation;

  public class EndingCheckerTests
  {
      private static GameState BaseState(int stability = 25, int civCost = 20, int geoTension = 35,
          int failedStates = 0, int initialActiveZones = 10, int completedReconstruction = 0) =>
          new()
          {
              GameId = "test",
              Round  = 5,
              Phase  = GamePhase.Consequences,
              Tracks = new WorldTracks
              {
                  MarketHeat = 30, CivilianCost = civCost, Stability = stability,
                  SanctionsRisk = 10, GeoTension = geoTension
              },
              Countries = Enumerable.Range(0, 10).Select(i => new CountryState
              {
                  Iso = $"C{i}", Name = $"Country{i}",
                  Stage = i < failedStates ? CountryStage.FailedState : CountryStage.Active,
                  Tension = 50, DemandType = "open"
              }).ToList(),
              Players = new List<PlayerProfile>(),
              CompletedReconstructionContracts = completedReconstruction,
          };

      [Fact]
      public void TotalWar_WhenStabilityHits100()
      {
          var state = BaseState(stability: 100);
          EndingChecker.Check(state, allVotedPeace: false, initialActiveZoneCount: 10)
              .Should().Be("total_war");
      }

      [Fact]
      public void GlobalSanctions_WhenCivCostHits100()
      {
          var state = BaseState(civCost: 100);
          EndingChecker.Check(state, allVotedPeace: false, initialActiveZoneCount: 10)
              .Should().Be("global_sanctions");
      }

      [Fact]
      public void GreatPowerConfrontation_WhenGeoTensionHits100()
      {
          var state = BaseState(geoTension: 100);
          EndingChecker.Check(state, allVotedPeace: false, initialActiveZoneCount: 10)
              .Should().Be("great_power_confrontation");
      }

      [Fact]
      public void MarketSaturation_When40PctFailedStates()
      {
          // 4 of 10 initial active zones are failed states = 40%
          var state = BaseState(failedStates: 4, initialActiveZones: 10);
          EndingChecker.Check(state, allVotedPeace: false, initialActiveZoneCount: 10)
              .Should().Be("market_saturation");
      }

      [Fact]
      public void NegotiatedPeace_WhenAllVoteAndStabilityBelow20()
      {
          var state = BaseState(stability: 18);
          EndingChecker.Check(state, allVotedPeace: true, initialActiveZoneCount: 10)
              .Should().Be("negotiated_peace");
      }

      [Fact]
      public void NoEnding_WhenAllTracksNormal()
      {
          var state = BaseState();
          EndingChecker.Check(state, allVotedPeace: false, initialActiveZoneCount: 10)
              .Should().BeNull();
      }
  }
  ```

- [ ] Create `ArmsFair.Server/Simulation/EndingChecker.cs`:
  ```csharp
  using ArmsFair.Shared;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Shared.Models;

  namespace ArmsFair.Server.Simulation;

  public static class EndingChecker
  {
      public static string? Check(GameState state, bool allVotedPeace, int initialActiveZoneCount)
      {
          if (state.Tracks.Stability     >= 100) return "total_war";
          if (state.Tracks.CivilianCost  >= 100) return "global_sanctions";
          if (state.Tracks.GeoTension    >= 100) return "great_power_confrontation";

          int failedCount = state.Countries.Count(c => c.Stage == CountryStage.FailedState);
          if (initialActiveZoneCount > 0 &&
              (float)failedCount / initialActiveZoneCount >= Balance.MarketSaturationFailedPct)
              return "market_saturation";

          if (allVotedPeace && state.Tracks.Stability < Balance.NegotiatedPeaceStabilityThresh)
              return "negotiated_peace";

          return null;
      }

      public static bool CheckWorldPeace(GameState state, bool allVotedPeace)
      {
          if (!allVotedPeace) return false;
          return state.Tracks.Stability    < Balance.WorldPeaceStabilityThresh
              && state.Tracks.CivilianCost < Balance.WorldPeaceCivCostThresh
              && state.Countries.All(c => (int)c.Stage <= Balance.WorldPeaceMaxCountryStage)
              && state.CompletedReconstructionContracts >= Balance.WorldPeaceMinReconstruction;
      }
  }
  ```

- [ ] Run tests and commit:
  ```bash
  dotnet test ArmsFair.Server.Tests --filter "EndingCheckerTests" -v
  git add ArmsFair.Server/Simulation/EndingChecker.cs ArmsFair.Server.Tests/Simulation/EndingCheckerTests.cs
  git commit -m "feat: EndingChecker with all 5 ending conditions tested"
  ```

---

## Task 12: Database Setup (EF Core)

- [ ] Create `ArmsFair.Server/Data/AppDbContext.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using ArmsFair.Shared.Models;

  namespace ArmsFair.Server.Data;

  public class AppDbContext : DbContext
  {
      public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

      public DbSet<PlayerProfileEntity> Players        => Set<PlayerProfileEntity>();
      public DbSet<PlayerStatsEntity>   PlayerStats    => Set<PlayerStatsEntity>();
      public DbSet<GameRoomEntity>      GameRooms      => Set<GameRoomEntity>();
      public DbSet<GamePlayerEntity>    GamePlayers    => Set<GamePlayerEntity>();
      public DbSet<PlayerActionEntity>  PlayerActions  => Set<PlayerActionEntity>();

      protected override void OnModelCreating(ModelBuilder b)
      {
          b.Entity<PlayerProfileEntity>(e =>
          {
              e.HasKey(p => p.Id);
              e.HasIndex(p => p.Username).IsUnique();
              e.HasIndex(p => p.Email).IsUnique();
          });
          b.Entity<GameRoomEntity>(e =>
          {
              e.HasKey(g => g.Id);
              e.HasIndex(g => g.RoomCode).IsUnique();
          });
          b.Entity<GamePlayerEntity>(e =>
          {
              e.HasKey(gp => new { gp.GameId, gp.PlayerId });
          });
          b.Entity<PlayerActionEntity>(e =>
          {
              e.HasKey(a => a.Id);
              e.HasIndex(a => new { a.GameId, a.Round, a.PlayerId });
          });
      }
  }
  ```

- [ ] Create `ArmsFair.Server/Data/Entities.cs` (all EF entity classes):
  ```csharp
  namespace ArmsFair.Server.Data;

  public class PlayerProfileEntity
  {
      public string  Id           { get; set; } = Guid.NewGuid().ToString();
      public string  Username     { get; set; } = "";
      public string  Email        { get; set; } = "";
      public string  PasswordHash { get; set; } = "";
      public string? SteamId      { get; set; }
      public string  HomeNation   { get; set; } = "";
      public int     Reputation   { get; set; } = 75;
      public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
  }

  public class PlayerStatsEntity
  {
      public string Id             { get; set; } = Guid.NewGuid().ToString();
      public string PlayerId       { get; set; } = "";
      public int    GamesPlayed    { get; set; }
      public int    GamesWon       { get; set; }
      public int    WarsStarted    { get; set; }
      public int    FailedStatesCaused { get; set; }
      public int    SmallArmsSold  { get; set; }
      public int    VehiclesSold   { get; set; }
      public int    AirDefenseSold { get; set; }
      public int    DronesSold     { get; set; }
      public long   TotalProfit    { get; set; }
      public long   TotalCivCost   { get; set; }
      public int    CeasefiresBrokered { get; set; }
      public int    CoupsFunded    { get; set; }
      public int    CoupsSucceeded { get; set; }
      public int    TimesWhistleblown  { get; set; }
      public int    TimesWhistleblower { get; set; }
      public int    TotalWarParticipations { get; set; }
      public int    WorldPeaceAchieved { get; set; }
      public int    CompanyCollapses   { get; set; }
      public int    ReconstructionWins { get; set; }
      public float  AvgFinalReputation { get; set; }
  }

  public class GameRoomEntity
  {
      public string Id              { get; set; } = Guid.NewGuid().ToString();
      public string RoomCode        { get; set; } = "";
      public string HostPlayerId    { get; set; } = "";
      public int    PlayerSlots     { get; set; } = 4;
      public string TimerPreset     { get; set; } = "standard";
      public bool   IsPrivate       { get; set; }
      public bool   AiFillIn        { get; set; }
      public string Status          { get; set; } = "lobby"; // lobby | active | ended
      public int    CurrentRound    { get; set; }
      public string CurrentPhase    { get; set; } = "lobby";
      public int    InitialActiveZones { get; set; }
      public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
  }

  public class GamePlayerEntity
  {
      public string GameId       { get; set; } = "";
      public string PlayerId     { get; set; } = "";
      public string CompanyName  { get; set; } = "";
      public string HomeNation   { get; set; } = "";
      public int    Capital      { get; set; } = 75;
      public int    Reputation   { get; set; } = 75;
      public int    SharePrice   { get; set; } = 100;
      public int    PeaceCredits { get; set; }
      public int    LatentRisk   { get; set; }
      public string Status       { get; set; } = "active";
      public long   LockedProfit { get; set; }
  }

  public class PlayerActionEntity
  {
      public string  Id            { get; set; } = Guid.NewGuid().ToString();
      public string  GameId        { get; set; } = "";
      public string  PlayerId      { get; set; } = "";
      public int     Round         { get; set; }
      public string  SaleType      { get; set; } = "";
      public string? TargetCountry { get; set; }
      public string? WeaponCategory { get; set; }
      public string? SupplierId    { get; set; }
      public bool    IsDualSupply  { get; set; }
      public bool    IsSealed      { get; set; }
      public bool    IsRevealed    { get; set; }
      public int     ProfitEarned  { get; set; }
      public DateTime SubmittedAt  { get; set; } = DateTime.UtcNow;
  }
  ```

- [ ] Run build to confirm no errors:
  ```bash
  dotnet build ArmsFair.Server
  ```

- [ ] Commit:
  ```bash
  git add ArmsFair.Server/Data/
  git commit -m "feat: EF Core DbContext and entity classes"
  ```

---

## Task 13: Program.cs (Server Wiring)

- [ ] Replace the default `ArmsFair.Server/Program.cs` with:
  ```csharp
  using ArmsFair.Server.Data;
  using ArmsFair.Server.Hubs;
  using Microsoft.EntityFrameworkCore;

  var builder = WebApplication.CreateBuilder(args);

  // Database
  builder.Services.AddDbContext<AppDbContext>(opts =>
      opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

  // SignalR
  builder.Services.AddSignalR();

  // CORS — allow Unity WebGL and dev origins
  builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
      p.WithOrigins(
          builder.Configuration["AllowedOrigins"]?.Split(',') ?? Array.Empty<string>())
       .AllowAnyHeader()
       .AllowAnyMethod()
       .AllowCredentials()));

  // Auth (JWT)
  builder.Services.AddAuthentication("Bearer")
      .AddJwtBearer("Bearer", opts =>
      {
          opts.Authority = builder.Configuration["Auth:Authority"];
          opts.Audience  = builder.Configuration["Auth:Audience"];
      });

  builder.Services.AddAuthorization();
  builder.Services.AddControllers();

  var app = builder.Build();

  // Auto-apply migrations on startup (dev convenience — remove for prod)
  if (app.Environment.IsDevelopment())
  {
      using var scope = app.Services.CreateScope();
      scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
  }

  app.UseCors();
  app.UseAuthentication();
  app.UseAuthorization();
  app.MapControllers();
  app.MapHub<GameHub>("/gamehub");
  app.MapGet("/health", () => "ok");

  app.Run();
  ```

- [ ] Create a minimal `ArmsFair.Server/Hubs/GameHub.cs` stub (full implementation in next task):
  ```csharp
  using Microsoft.AspNetCore.SignalR;

  namespace ArmsFair.Server.Hubs;

  public class GameHub : Hub
  {
      public async Task Ping() =>
          await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
  }
  ```

- [ ] Build:
  ```bash
  dotnet build ArmsFair.Server
  ```

- [ ] Commit:
  ```bash
  git add ArmsFair.Server/Program.cs ArmsFair.Server/Hubs/GameHub.cs
  git commit -m "feat: wire up ASP.NET Core server with SignalR, EF Core, CORS, auth"
  ```

---

## Task 14: GameHub (SignalR)

- [ ] Replace `ArmsFair.Server/Hubs/GameHub.cs` with the full implementation:
  ```csharp
  using ArmsFair.Server.Data;
  using ArmsFair.Server.Simulation;
  using ArmsFair.Shared.Models.Messages;
  using Microsoft.AspNetCore.SignalR;
  using Microsoft.EntityFrameworkCore;

  namespace ArmsFair.Server.Hubs;

  public class GameHub : Hub
  {
      private readonly AppDbContext _db;
      private readonly ILogger<GameHub> _log;

      public GameHub(AppDbContext db, ILogger<GameHub> log)
      {
          _db  = db;
          _log = log;
      }

      public override async Task OnConnectedAsync()
      {
          var roomCode = Context.GetHttpContext()?.Request.Query["room"].ToString();
          if (!string.IsNullOrEmpty(roomCode))
              await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
          await base.OnConnectedAsync();
      }

      // ── Client → Server ───────────────────────────────────────────────────

      public async Task SubmitAction(SubmitActionMessage msg)
      {
          var roomCode = GetRoomCode();
          if (roomCode is null) return;

          var room = await _db.GameRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
          if (room is null) return;

          var playerId = Context.UserIdentifier ?? Context.ConnectionId;

          var action = new PlayerActionEntity
          {
              GameId        = room.Id,
              PlayerId      = playerId,
              Round         = room.CurrentRound,
              SaleType      = msg.SaleType.ToString(),
              TargetCountry = msg.TargetCountry,
              WeaponCategory = msg.WeaponCategory?.ToString(),
              SupplierId    = msg.SupplierId,
              IsDualSupply  = msg.IsDualSupply,
              IsSealed      = true,
          };

          _db.PlayerActions.Add(action);
          await _db.SaveChangesAsync();

          _log.LogInformation("Player {P} submitted action for room {R} round {Rd}",
              playerId, roomCode, room.CurrentRound);
      }

      public async Task SendChat(ChatMessage msg)
      {
          var roomCode = GetRoomCode();
          if (roomCode is null) return;

          var outbound = msg with { IsSystem = false };

          if (msg.RecipientId is not null)
              await Clients.User(msg.RecipientId).SendAsync("ChatMsg", outbound with { IsPrivate = true });
          else
              await Clients.Group(roomCode).SendAsync("ChatMsg", outbound);
      }

      public async Task VotePeace(bool vote)
      {
          var roomCode = GetRoomCode();
          if (roomCode is null) return;

          _log.LogInformation("Player {P} voted peace={V} in room {R}",
              Context.UserIdentifier, vote, roomCode);

          // Peace vote tracking stored in Redis by PhaseOrchestrator (wired in Task 15)
          await Clients.Group(roomCode).SendAsync("PeaceVote",
              new { PlayerId = Context.UserIdentifier, Vote = vote });
      }

      public async Task FundCoup(FundCoupMessage msg)
      {
          var roomCode = GetRoomCode();
          if (roomCode is null) return;

          _log.LogInformation("Player {P} funding coup in {C}", Context.UserIdentifier, msg.TargetCountryIso);
          // Full resolution in PhaseOrchestrator during WorldUpdate phase
          await Clients.Group(roomCode).SendAsync("CoupQueued",
              new { PlayerId = Context.UserIdentifier, msg.TargetCountryIso });
      }

      public async Task ManufactureDemand(ManufactureDemandMessage msg)
      {
          var roomCode = GetRoomCode();
          if (roomCode is null) return;

          _log.LogInformation("Player {P} manufacturing demand in {C}",
              Context.UserIdentifier, msg.TargetCountryIso);
      }

      public async Task Whistle(WhistleMessage msg)
      {
          var roomCode = GetRoomCode();
          if (roomCode is null) return;

          _log.LogInformation("Player {P} blew whistle L{L} on {T}",
              Context.UserIdentifier, msg.Level, msg.TargetPlayerId);
          // Full resolution handled by PhaseOrchestrator
      }

      public async Task Ping() =>
          await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);

      // ── Helpers ───────────────────────────────────────────────────────────

      private string? GetRoomCode() =>
          Context.GetHttpContext()?.Request.Query["room"].ToString();
  }
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.Server
  git add ArmsFair.Server/Hubs/GameHub.cs
  git commit -m "feat: full GameHub with action submission, chat, coup, whistle, peace vote"
  ```

---

## Task 15: SeedService

- [ ] Create `ArmsFair.Server/Services/SeedService.cs`:
  ```csharp
  using System.Text.Json;
  using ArmsFair.Shared.Enums;
  using ArmsFair.Shared.Models;

  namespace ArmsFair.Server.Services;

  public class SeedService
  {
      private readonly HttpClient _http;
      private readonly IConfiguration _config;
      private readonly ILogger<SeedService> _log;

      public SeedService(HttpClient http, IConfiguration config, ILogger<SeedService> log)
      {
          _http   = http;
          _config = config;
          _log    = log;
      }

      public async Task<List<CountryState>> FetchWorldSeedAsync()
      {
          _log.LogInformation("Fetching world seed from ACLED + GPI");
          var acledTask = FetchAcledAsync();
          var gpiTask   = FetchGpiAsync();
          await Task.WhenAll(acledTask, gpiTask);

          return BuildCountryStates(await acledTask, await gpiTask);
      }

      private async Task<Dictionary<string, float>> FetchAcledAsync()
      {
          try
          {
              var key   = _config["ACLED:ApiKey"];
              var email = _config["ACLED:Email"];
              var since = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-dd");
              var until = DateTime.UtcNow.ToString("yyyy-MM-dd");

              var url = $"https://api.acleddata.com/acled/read" +
                        $"?key={key}&email={email}" +
                        $"&event_date={since}|{until}&event_date_where=BETWEEN" +
                        $"&fields=iso3,fatalities&limit=5000";

              using var resp = await _http.GetAsync(url);
              resp.EnsureSuccessStatusCode();

              var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
              var byIso = new Dictionary<string, float>();

              foreach (var item in json.GetProperty("data").EnumerateArray())
              {
                  var iso = item.GetProperty("iso3").GetString() ?? "";
                  var fat = item.GetProperty("fatalities").GetSingle();
                  byIso[iso] = byIso.GetValueOrDefault(iso) + fat;
              }

              return byIso;
          }
          catch (Exception ex)
          {
              _log.LogWarning(ex, "ACLED fetch failed — using empty baseline");
              return new();
          }
      }

      private async Task<Dictionary<string, float>> FetchGpiAsync()
      {
          try
          {
              var url = _config["GPI:DataUrl"];
              using var resp = await _http.GetAsync(url);
              resp.EnsureSuccessStatusCode();

              var records = await resp.Content.ReadFromJsonAsync<List<GpiRecord>>();
              return records?.ToDictionary(r => r.Iso3, r => r.Score) ?? new();
          }
          catch (Exception ex)
          {
              _log.LogWarning(ex, "GPI fetch failed — using empty baseline");
              return new();
          }
      }

      private static List<CountryState> BuildCountryStates(
          Dictionary<string, float> acled,
          Dictionary<string, float> gpi)
      {
          var tensions = new Dictionary<string, float>();

          foreach (var (iso, score) in gpi)
              tensions[iso] = (score - 1f) / 2f * 60f;

          foreach (var (iso, fatalities) in acled)
          {
              float bonus = Math.Min(40f, MathF.Log(fatalities + 1) * 5f);
              tensions[iso] = Math.Min(100f, tensions.GetValueOrDefault(iso) + bonus);
          }

          return tensions.Select(kvp => new CountryState
          {
              Iso        = kvp.Key,
              Name       = kvp.Key,
              Tension    = (int)kvp.Value,
              Stage      = TensionToStage(kvp.Value),
              DemandType = kvp.Value > 40 ? (kvp.Value > 70 ? "open" : "covert") : "none",
          }).ToList();
      }

      private static CountryStage TensionToStage(float t) =>
          t >= 85 ? CountryStage.HumanitarianCrisis :
          t >= 65 ? CountryStage.HotWar :
          t >= 40 ? CountryStage.Active :
          t >= 20 ? CountryStage.Simmering :
                    CountryStage.Dormant;

      private record GpiRecord(string Iso3, float Score);
  }
  ```

- [ ] Register in `Program.cs` — add before `var app = builder.Build();`:
  ```csharp
  builder.Services.AddHttpClient<SeedService>();
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.Server
  git add ArmsFair.Server/Services/SeedService.cs ArmsFair.Server/Program.cs
  git commit -m "feat: SeedService fetches ACLED + GPI to seed world state"
  ```

---

## Task 16: TickerService

- [ ] Create `ArmsFair.Server/Services/TickerService.cs`:
  ```csharp
  using ArmsFair.Shared.Models;
  using System.Text.Json;

  namespace ArmsFair.Server.Services;

  public class TickerService
  {
      private readonly HttpClient _http;
      private readonly IConfiguration _config;

      public TickerService(HttpClient http, IConfiguration config)
      {
          _http   = http;
          _config = config;
      }

      private static readonly string[] Templates =
      {
          "{company} shipment to {country} under investigation by UN panel",
          "Conflict in {country} enters month {duration} — civilian toll mounting",
          "Arms embargo on {country} discussed at Security Council",
          "{country} government forces advance in contested territory",
          "Red Cross suspended operations in {country} citing security deterioration",
          "Reconstruction tenders open in {country} — international firms invited to bid",
          "Market analysts: defense sector earnings up {percent}% — attributed to regional instability",
          "{company} stock falls following {country} blowback reports",
          "Peace talks in {country} stall for the {number}th consecutive round",
          "Weapons manufacturer reports record quarterly profit amid global unrest",
      };

      public async Task<List<string>> GetRound1HeadlinesAsync(List<string> countryNames)
      {
          var key = _config["NewsAPI:ApiKey"];
          if (string.IsNullOrEmpty(key)) return GenerateProcedural(3, null);

          try
          {
              var query = Uri.EscapeDataString(string.Join(" OR ", countryNames.Take(5)));
              var url = $"https://newsapi.org/v2/everything?q={query}&sortBy=publishedAt&pageSize=10&apiKey={key}";
              using var resp = await _http.GetAsync(url);
              resp.EnsureSuccessStatusCode();

              var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
              return json.GetProperty("articles")
                         .EnumerateArray()
                         .Select(a => a.GetProperty("title").GetString() ?? "")
                         .Where(t => !string.IsNullOrEmpty(t))
                         .Take(5)
                         .ToList();
          }
          catch
          {
              return GenerateProcedural(3, null);
          }
      }

      public string GenerateTicker(GameState state)
      {
          var template = Templates[Random.Shared.Next(Templates.Length)];
          var hotCountry = state.Countries
              .Where(c => (int)c.Stage >= 3)
              .OrderByDescending(c => c.Tension)
              .FirstOrDefault();

          var company = state.Players.Count > 0
              ? state.Players[Random.Shared.Next(state.Players.Count)].CompanyName ?? "An unnamed firm"
              : "A major arms firm";

          return template
              .Replace("{country}", hotCountry?.Name ?? "an unnamed region")
              .Replace("{company}", company)
              .Replace("{duration}", Random.Shared.Next(3, 24).ToString())
              .Replace("{percent}", Random.Shared.Next(8, 40).ToString())
              .Replace("{number}", Random.Shared.Next(3, 12).ToString());
      }

      private static List<string> GenerateProcedural(int count, GameState? state) =>
          Enumerable.Range(0, count).Select(_ => "Global arms markets remain active amid ongoing conflicts.").ToList();
  }
  ```

- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddHttpClient<TickerService>();
  ```

- [ ] Build and commit:
  ```bash
  dotnet build ArmsFair.Server
  git add ArmsFair.Server/Services/TickerService.cs ArmsFair.Server/Program.cs
  git commit -m "feat: TickerService generates news headlines (real round 1, procedural round 2+)"
  ```

---

## Task 17: Dockerfile + appsettings

- [ ] Create `ArmsFair.Server/Dockerfile`:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
  WORKDIR /app
  EXPOSE 80

  FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
  WORKDIR /src
  COPY ["ArmsFair.Server/ArmsFair.Server.csproj", "ArmsFair.Server/"]
  COPY ["ArmsFair.Shared/ArmsFair.Shared.csproj", "ArmsFair.Shared/"]
  RUN dotnet restore "ArmsFair.Server/ArmsFair.Server.csproj"
  COPY . .
  RUN dotnet build "ArmsFair.Server/ArmsFair.Server.csproj" -c Release -o /app/build

  FROM build AS publish
  RUN dotnet publish "ArmsFair.Server/ArmsFair.Server.csproj" -c Release -o /app/publish

  FROM base AS final
  WORKDIR /app
  COPY --from=publish /app/publish .
  ENTRYPOINT ["dotnet", "ArmsFair.Server.dll"]
  ```

- [ ] Create `ArmsFair.Server/railway.toml`:
  ```toml
  [build]
  builder = "dockerfile"
  dockerfilePath = "ArmsFair.Server/Dockerfile"

  [deploy]
  healthcheckPath = "/health"
  restartPolicyType = "always"
  ```

- [ ] Update `ArmsFair.Server/appsettings.json`:
  ```json
  {
    "ConnectionStrings": {
      "Default": ""
    },
    "AllowedOrigins": "http://localhost:3000,http://localhost:8080",
    "Auth": {
      "Authority": "",
      "Audience": "armsfair"
    },
    "ACLED": {
      "ApiKey": "",
      "Email": ""
    },
    "GPI": {
      "DataUrl": ""
    },
    "NewsAPI": {
      "ApiKey": ""
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "AllowedHosts": "*"
  }
  ```

- [ ] Create `.gitignore` at root:
  ```
  bin/
  obj/
  .vs/
  *.user
  appsettings.Development.json
  appsettings.local.json
  .env
  ```

- [ ] Build final check and commit:
  ```bash
  dotnet build ArmsFair.sln
  dotnet test ArmsFair.Server.Tests -v
  git add ArmsFair.Server/Dockerfile ArmsFair.Server/railway.toml ArmsFair.Server/appsettings.json .gitignore
  git commit -m "feat: Dockerfile, railway.toml, appsettings, .gitignore"
  ```

---

## Task 18: GeoJSON Preprocessing Script

- [ ] Create `tools/preprocess_geojson.py` (run once at build time):
  ```python
  #!/usr/bin/env python3
  """
  Preprocesses Natural Earth 1:50m GeoJSON into two files for Unity:
    - countries.json  (simplified geometries, ~3MB)
    - adjacency.json  (border adjacency graph)

  Usage:
    pip install shapely
    python tools/preprocess_geojson.py <path-to-ne_50m_admin_0_countries.geojson>

  Download source from: https://www.naturalearthdata.com/downloads/50m-cultural-vectors/
  """
  import json
  import sys
  from pathlib import Path
  from shapely.geometry import shape
  from shapely.ops import unary_union

  def main():
      if len(sys.argv) < 2:
          print("Usage: python preprocess_geojson.py <input.geojson>")
          sys.exit(1)

      source = Path(sys.argv[1])
      out_dir = Path("ArmsFair.Unity/Assets/StreamingAssets/GeoData")
      out_dir.mkdir(parents=True, exist_ok=True)

      with open(source) as f:
          data = json.load(f)

      features = data["features"]
      countries_out = []
      iso_shapes = {}

      for feat in features:
          props = feat["properties"]
          iso   = props.get("ADM0_A3", "")
          name  = props.get("NAME", iso)
          geom  = shape(feat["geometry"])
          simp  = geom.simplify(0.1, preserve_topology=True)

          countries_out.append({
              "iso":      iso,
              "name":     name,
              "geometry": simp.__geo_interface__,
          })
          iso_shapes[iso] = geom.buffer(0.05)  # small buffer catches near-borders

      # Build adjacency graph
      adjacency = {}
      isos = list(iso_shapes.keys())
      for i, iso_a in enumerate(isos):
          adjacency[iso_a] = []
          for iso_b in isos:
              if iso_a != iso_b and iso_shapes[iso_a].intersects(iso_shapes[iso_b]):
                  adjacency[iso_a].append(iso_b)

      # Write output
      countries_path = out_dir / "countries.json"
      adjacency_path = out_dir / "adjacency.json"

      with open(countries_path, "w") as f:
          json.dump(countries_out, f, separators=(",", ":"))

      with open(adjacency_path, "w") as f:
          json.dump(adjacency, f, separators=(",", ":"))

      print(f"Processed {len(countries_out)} countries")
      print(f"  → {countries_path}")
      print(f"  → {adjacency_path}")

  if __name__ == "__main__":
      main()
  ```

- [ ] Create `tools/requirements.txt`:
  ```
  shapely>=2.0.0
  ```

- [ ] Commit:
  ```bash
  git add tools/
  git commit -m "feat: GeoJSON preprocessing script for Unity StreamingAssets"
  ```

---

## Task 19: Final Test Run + Handoff

- [ ] Run full test suite:
  ```bash
  dotnet test ArmsFair.sln -v
  ```
  Expected: all tests in TrackEngineTests, ProfitEngineTests, SpreadEngineTests, BlowbackEngineTests, CoupEngineTests, EndingCheckerTests pass.

- [ ] Verify build with no warnings:
  ```bash
  dotnet build ArmsFair.sln -warnaserror
  ```

- [ ] Update `CLAUDE.md` to reflect the new solution structure (replace the existing Godot content):
  The spec docs references stay the same. Add:
  ```markdown
  ## Solution structure
  - ArmsFair.Shared  — enums, models, Balance.cs, message types. No Unity or server deps.
  - ArmsFair.Server  — ASP.NET Core 8 + SignalR. Simulation engines in Server/Simulation/.
  - ArmsFair.Server.Tests — xUnit tests. Run: `dotnet test`
  - ArmsFair.Unity   — Unity 2023 LTS project (created separately once Unity is installed)
  - tools/           — Python preprocessing scripts (GeoJSON pipeline)
  ```

- [ ] Final commit:
  ```bash
  git add CLAUDE.md
  git commit -m "docs: update CLAUDE.md with solution structure"
  ```

---

## Handoff Status

Track completion here as tasks finish:

| Task | Description | Status |
|------|-------------|--------|
| 1 | Scaffold solution | ⬜ |
| 2 | Enums | ⬜ |
| 3 | Balance.cs | ⬜ |
| 4 | Core models | ⬜ |
| 5 | Message types | ⬜ |
| 6 | TrackEngine + tests | ⬜ |
| 7 | ProfitEngine + tests | ⬜ |
| 8 | SpreadEngine + tests | ⬜ |
| 9 | BlowbackEngine + tests | ⬜ |
| 10 | CoupEngine + tests | ⬜ |
| 11 | EndingChecker + tests | ⬜ |
| 12 | EF Core DbContext + entities | ⬜ |
| 13 | Program.cs wiring | ⬜ |
| 14 | GameHub (SignalR) | ⬜ |
| 15 | SeedService | ⬜ |
| 16 | TickerService | ⬜ |
| 17 | Dockerfile + config | ⬜ |
| 18 | GeoJSON script | ⬜ |
| 19 | Final tests + handoff | ⬜ |
