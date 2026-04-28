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

    // Aid cover: civilian cost suppressed to -1, stability/heat same as open
    public static readonly int[] AidSmallArms  = {  1, -1,  1,  0,  0 };
    public static readonly int[] AidVehicles   = {  2, -1,  1,  0,  0 };
    public static readonly int[] AidAirDefense = {  3, -1,  2,  0,  1 };
    public static readonly int[] AidDrones     = {  4, -1,  2,  0,  1 };
    public const int AidFraudExposurePenalty = 5;

    // Peace broker flat deltas
    public const int PeaceMarketHeat   = -1;
    public const int PeaceCivilianCost = -1;
    public const int PeaceStability    = -2;
    public const int PeaceCostToPlayer = 2;   // $M
    public const int PeaceCreditEarned = 1;

    // ── Blowback trace chances ────────────────────────────────────────────
    public const float TraceSmallArms   = 0.15f;
    public const float TraceVehicles    = 0.35f;
    public const float TraceAirDefense  = 0.50f;
    public const float TraceDrones      = 0.70f;

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
    public const int RepLossBlowbackSmallArms        = -8;
    public const int RepLossBlowbackVehicles         = -12;
    public const int RepLossBlowbackAirDefense       = -10;
    public const int RepLossBlowbackDrones           = -20;
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
    public const float SpreadHighStabilityMul   = 2.0f;
    public const int   SpreadHighStabilityThresh = 80;

    // ── Track thresholds ─────────────────────────────────────────────────
    public const int   MarketHeatBonusThresh   = 80;
    public const float MarketHeatProfitBonus   = 1.25f;
    public const float MarketHeatCivCostMul    = 2.0f;
    public const int   MarketCrashThresh       = 100;
    public const int   MarketCrashResetTo      = 40;

    public const int CivCostUnDiscussionThresh = 60;
    public const int CivCostSanctionsThresh    = 75;
    public const int CivCostEndingThresh       = 100;

    public const int StabilitySpreadMulThresh  = 80;
    public const int StabilityEndingThresh     = 100;

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
    public const int   PeacekeepingStabilityDelta  = -3;
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
    public const int   WorldPeaceStabilityThresh      = 15;
    public const int   WorldPeaceCivCostThresh        = 25;
    public const int   WorldPeaceMaxCountryStage      = 2;
    public const int   WorldPeaceMinReconstruction    = 3;
    public const int   WorldPeaceLegacyBonus          = 200;
    public const float MarketSaturationFailedPct      = 0.40f;
    public const int   NegotiatedPeaceStabilityThresh = 20;
    public const int   NegotiatedPeaceCreditBonus     = 20;  // $M for 5+ peace credits
    public const int   NegotiatedPeaceMinCredits      = 5;
    public const int   GlobalSanctionsEndgameRounds   = 3;
    public const float GlobalSanctionsGrayLatentMul   = 3.0f;
    public const int   GreatPowerEndgameRounds        = 2;

    // ── Legacy score formula ──────────────────────────────────────────────
    public const int   LegacyPerPeaceCredit        = 5;
    public const int   LegacyPerCeasefire          = 20;
    public const int   LegacyPerReconstruction     = 15;
    public const int   LegacyMaxStabilityBonus     = 100;
    public const int   LegacyStabilityBonusThresh  = 20;
    public const int   LegacyPenaltyPerFailedState = -10;
    public const float LegacyCivilianCostPenalty   = 0.5f;
}
