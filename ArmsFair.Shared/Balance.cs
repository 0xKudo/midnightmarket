namespace ArmsFair.Shared;

public static class Balance
{
    // ── Starting values ──────────────────────────────────────────────────
    public const int StartingCapital       = 50;   // $M
    public const int StartingReputation    = 75;
    public const int StartingSharePrice    = 100;

    // Mode 1 — Realistic (default)
    public const int StartMarketHeat       = 30;
    public const int StartCivilianCost     = 20;
    public const int StartInstability        = 25;
    public const int StartSanctionsRisk    = 10;
    public const int StartGeoTension       = 35;

    // Mode 2 — Equal World
    public const int EqualWorldMarketHeat    = 20;
    public const int EqualWorldCivilianCost  = 10;
    public const int EqualWorldInstability     = 15;
    public const int EqualWorldSanctionsRisk = 5;
    public const int EqualWorldGeoTension    = 20;

    // Mode 3 — Blank Slate
    public const int BlankSlateMarketHeat    = 10;
    public const int BlankSlateCivilianCost  = 5;
    public const int BlankSlateInstability     = 10;
    public const int BlankSlateSanctionsRisk = 0;
    public const int BlankSlateGeoTension    = 10;

    // Mode 4 — Hot World
    public const int HotWorldMarketHeat      = 55;
    public const int HotWorldCivilianCost    = 45;
    public const int HotWorldInstability       = 50;
    public const int HotWorldSanctionsRisk   = 30;
    public const int HotWorldGeoTension      = 55;

    // ── Procurement costs ($M) ────────────────────────────────────────────
    public const int CostSmallArms          = 3;
    public const int CostVehicles           = 8;
    public const int CostCombatHelicopters  = 35;
    public const int CostFighterJets        = 75;
    public const int CostDrones             = 25;
    public const int CostAirDefense         = 20;
    public const int CostCruiseMissiles     = 50;
    public const int CostIcbmComponents     = 200;
    public const int CostNuclearWarhead     = 500;
    public const int CostFissileMaterials   = 150;

    // ── Base profits ($M) ────────────────────────────────────────────────
    public const int ProfitSmallArms          = 6;
    public const int ProfitVehicles           = 14;
    public const int ProfitCombatHelicopters  = 60;
    public const int ProfitFighterJets        = 120;
    public const int ProfitDrones             = 42;
    public const int ProfitAirDefense         = 35;
    public const int ProfitCruiseMissiles     = 82;
    public const int ProfitIcbmComponents     = 320;
    public const int ProfitNuclearWarhead     = 800;
    public const int ProfitFissileMaterials   = 260;

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
    public const float RelTier1Discount    = 0.00f;  // 0-4 pts
    public const float RelTier2Discount    = 0.05f;  // 5-9 pts
    public const float RelTier3Discount    = 0.10f;  // 10-14 pts
    public const float RelTier4Discount    = 0.15f;  // 15+ pts
    public const float RelTier2ProfitBonus = 1.08f;
    public const float RelTier3ProfitBonus = 1.15f;
    public const float RelTier4ProfitBonus = 1.22f;

    // ── Sale type modifiers ───────────────────────────────────────────────
    public const float CovertProfitPremium   = 1.30f;
    public const float AidCoverProfitPenalty = 0.80f;
    public const float DualSupplyTrackMul    = 2.20f;
    public const float DualSupplyProfitMul   = 1.80f;

    // ── Track deltas: [marketHeat, civilianCost, stability, sanctionsRisk, geoTension]
    // Open sale base deltas by weapon: [marketHeat, civilianCost, instability, sanctionsRisk, geoTension]
    public static readonly int[] OpenSmallArms          = {  1,  3,  1,  0,  0 };
    public static readonly int[] OpenVehicles           = {  2,  2,  1,  1,  0 };
    public static readonly int[] OpenAirDefense         = {  3,  1,  2,  1,  1 };
    public static readonly int[] OpenDrones             = {  4,  4,  2,  2,  1 };
    public static readonly int[] OpenCombatHelicopters  = {  4,  3,  3,  2,  2 };
    public static readonly int[] OpenFighterJets        = {  5,  2,  3,  3,  3 };
    public static readonly int[] OpenCruiseMissiles     = {  5,  4,  4,  3,  3 };
    public static readonly int[] OpenIcbmComponents     = {  6,  5,  5,  8, 10 };
    public static readonly int[] OpenNuclearWarhead     = {  8,  8,  8, 15, 20 };
    public static readonly int[] OpenFissileMaterials   = {  5,  4,  6, 12, 15 };

    // Covert: same base, sanctions=0 until traced
    public static readonly int[] CovertSmallArms          = {  1,  3,  1,  0,  0 };
    public static readonly int[] CovertVehicles           = {  2,  2,  1,  0,  0 };
    public static readonly int[] CovertAirDefense         = {  3,  1,  2,  0,  1 };
    public static readonly int[] CovertDrones             = {  4,  4,  2,  0,  1 };
    public static readonly int[] CovertCombatHelicopters  = {  4,  3,  3,  0,  2 };
    public static readonly int[] CovertFighterJets        = {  5,  2,  3,  0,  3 };
    public static readonly int[] CovertCruiseMissiles     = {  5,  4,  4,  0,  3 };
    public static readonly int[] CovertIcbmComponents     = {  6,  5,  5,  0, 10 };
    public static readonly int[] CovertNuclearWarhead     = {  8,  8,  8,  0, 20 };
    public static readonly int[] CovertFissileMaterials   = {  5,  4,  6,  0, 15 };
    public const int CovertTraceSanctionsHit = 3;

    // Aid cover: civilian cost suppressed to -1, stability/heat same as open
    public static readonly int[] AidSmallArms          = {  1, -1,  1,  0,  0 };
    public static readonly int[] AidVehicles           = {  2, -1,  1,  0,  0 };
    public static readonly int[] AidAirDefense         = {  3, -1,  2,  0,  1 };
    public static readonly int[] AidDrones             = {  4, -1,  2,  0,  1 };
    public static readonly int[] AidCombatHelicopters  = {  4, -1,  3,  0,  2 };
    public static readonly int[] AidFighterJets        = {  5, -1,  3,  0,  3 };
    public static readonly int[] AidCruiseMissiles     = {  5, -1,  4,  0,  3 };
    // WMD cannot be Aid Cover (UI blocks it); fallback to covert deltas for safety
    public static readonly int[] AidIcbmComponents     = {  6,  5,  5,  0, 10 };
    public static readonly int[] AidNuclearWarhead     = {  8,  8,  8,  0, 20 };
    public static readonly int[] AidFissileMaterials   = {  5,  4,  6,  0, 15 };
    public const int AidFraudExposurePenalty = 5;

    // Peace broker flat deltas
    public const int PeaceMarketHeat   = -1;
    public const int PeaceCivilianCost = -1;
    public const int PeaceInstability    = -2;
    public const int PeaceCostToPlayer = 2;   // $M
    public const int PeaceCreditEarned = 1;

    // ── Blowback trace chances ────────────────────────────────────────────
    public const float TraceSmallArms          = 0.15f;
    public const float TraceVehicles           = 0.35f;
    public const float TraceCombatHelicopters  = 0.45f;
    public const float TraceFighterJets        = 0.55f;
    public const float TraceDrones             = 0.70f;
    public const float TraceAirDefense         = 0.50f;
    public const float TraceCruiseMissiles     = 0.60f;
    public const float TraceIcbmComponents     = 0.75f;
    public const float TraceNuclearWarhead     = 0.90f;
    public const float TraceFissileMaterials   = 0.85f;

    public const float TraceModCovert         = +0.10f;
    public const float TraceModAidExposed     = +0.25f;
    public const float TraceModGrayChannel    = -0.15f;
    public const float TraceModVostok         = -0.20f;
    public const float TraceModHighLatentRisk = +0.10f;
    public const float TraceModHotWar         = +0.05f;
    public const float TraceModCrisis         = +0.10f;

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
    public const int RepLossBlowbackSmallArms          = -8;
    public const int RepLossBlowbackVehicles           = -12;
    public const int RepLossBlowbackCombatHelicopters  = -18;
    public const int RepLossBlowbackFighterJets        = -22;
    public const int RepLossBlowbackDrones             = -20;
    public const int RepLossBlowbackAirDefense         = -10;
    public const int RepLossBlowbackCruiseMissiles     = -25;
    public const int RepLossBlowbackIcbmComponents     = -35;
    public const int RepLossBlowbackNuclearWarhead     = -60;
    public const int RepLossBlowbackFissileMaterials   = -45;
    public const int RepLossBlowbackCovertBonus      = -5;
    public const int RepLossBlowbackDualSupply       = -8;
    public const int CollapseThreshold               = 0;

    // ── Latent risk accumulation ──────────────────────────────────────────
    public const int   LatentPerCovert           = 3;
    public const int   LatentPerGrayChannel      = 5;
    public const int   LatentPerVostok           = 7;
    public const int   LatentPerCoupConcealed    = 5;
    public const int   LatentPerManufacture      = 4;
    public const float LatentConversionRate      = 0.5f;  // latent → reputation damage
    public const int   LatentMaxPerInvestigation = 25;

    // ── Conflict spread ───────────────────────────────────────────────────
    public const float SpreadBaseChance         = 0.08f;
    public const float SpreadPerSale            = +0.03f;
    public const float SpreadPerTreaty          = -0.05f;
    public const float SpreadPeacekeepingFlat   = -0.04f;
    public const float SpreadStage4Bonus        = +0.04f;
    public const float SpreadMin                = 0.00f;
    public const float SpreadMax                = 0.60f;
    public const float SpreadHighInstabilityMul   = 2.0f;
    public const int   SpreadHighInstabilityThresh = 80;

    // ── Track thresholds ─────────────────────────────────────────────────
    public const int   MarketHeatBonusThresh   = 80;
    public const float MarketHeatProfitBonus   = 1.25f;
    public const float MarketHeatCivCostMul    = 2.0f;
    public const int   MarketCrashThresh       = 100;
    public const int   MarketCrashResetTo      = 40;

    public const int CivCostUnDiscussionThresh = 60;
    public const int CivCostSanctionsThresh    = 75;
    public const int CivCostEndingThresh       = 100;

    public const int InstabilitySpreadMulThresh  = 80;
    public const int InstabilityEndingThresh     = 100;

    public const int SanctionsLicenseCostThresh = 60;
    public const int SanctionsInvestThresh      = 80;

    public const int GeoTensionBlocThresh    = 70;
    public const int GeoTensionCrisisThresh  = 90;
    public const int GeoTensionEndingThresh  = 100;

    // ── Whistle costs ─────────────────────────────────────────────────────
    public const int WhistleL1Cost               = 3;
    public const int WhistleL2Cost               = 8;
    public const int WhistleL2RepHit             = -3;
    public const int WhistleL3Cost               = 15;
    public const int WhistleL3RepHit             = -5;
    public const int WhistleL3TargetChangeCost   = 10;
    public const int WhistleL3TargetChangeRepHit = -3;

    // ── Export licenses ───────────────────────────────────────────────────
    public const int   LicenseStandardCost    = 3;
    public const int   LicenseRestrictedCost  = 8;
    public const int   LobbyingCost           = 5;
    public const float LobbyingSuccessChance  = 0.40f;

    // ── Short selling ─────────────────────────────────────────────────────
    public const int   ShortStake            = 5;   // $M
    public const float ShortPayoutMultiplier = 3.0f;
    public const int   ShortRepThreshold     = 15;

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
    public const int ManufactureCostStage0       = 25;  // $M
    public const int ManufactureCostStage1       = 15;
    public const int ManufactureTensionMin       = 15;
    public const int ManufactureTensionMax       = 20;
    public const int ManufactureToStage1         = 40;  // tension threshold
    public const int ManufactureToStage2         = 70;
    public const int ManufactureExposedRepHit    = -20;
    public const int ManufactureExposedBanRounds = 2;
    public const int ManufactureExposedSanctions = +5;

    // ── Peacekeeping investment ───────────────────────────────────────────
    public const int   PeacekeepingCost            = 10;  // $M
    public const int   PeacekeepingInstabilityDelta  = -3;
    public const int   PeacekeepingCivilianDelta   = -2;
    public const int   PeacekeepingCoSignatories   = 1;
    public const float PeacekeepingSpreadReduction = 0.04f;

    // ── Reconstruction contracts ──────────────────────────────────────────
    public const int ReconstructionPayoutPerRound = 8;   // $M
    public const int ReconstructionPayoutRounds   = 3;
    public const int ReconstructionRepOnComplete  = +5;

    // ── Phase durations (ms) ─────────────────────────────────────────────
    public const int PhaseWorldUpdate  = 30_000;
    public const int PhaseProcurement  = 60_000;
    public const int PhaseNegotiation  = 120_000;
    public const int PhaseSales        = 90_000;
    public const int PhaseReveal       = 30_000;
    public const int PhaseConsequences = 60_000;

    // ── Ending conditions ─────────────────────────────────────────────────
    public const int   WorldPeaceInstabilityThresh      = 15;
    public const int   WorldPeaceCivCostThresh        = 25;
    public const int   WorldPeaceMaxCountryStage      = 2;
    public const int   WorldPeaceMinReconstruction    = 3;
    public const int   WorldPeaceLegacyBonus          = 200;
    public const float MarketSaturationFailedPct      = 0.40f;
    public const int   NegotiatedPeaceInstabilityThresh = 20;
    public const int   NegotiatedPeaceCreditBonus     = 20;  // $M for 5+ peace credits
    public const int   NegotiatedPeaceMinCredits      = 5;
    public const int   GlobalSanctionsEndgameRounds   = 3;
    public const float GlobalSanctionsGrayLatentMul   = 3.0f;
    public const int   GreatPowerEndgameRounds        = 2;

    // ── Legacy score formula ──────────────────────────────────────────────
    public const int   LegacyPerPeaceCredit        = 5;
    public const int   LegacyPerCeasefire          = 20;
    public const int   LegacyPerReconstruction     = 15;
    public const int   LegacyMaxInstabilityBonus     = 100;
    public const int   LegacyInstabilityBonusThresh  = 20;
    public const int   LegacyPenaltyPerFailedState = -10;
    public const float LegacyCivilianCostPenalty   = 0.5f;
}
