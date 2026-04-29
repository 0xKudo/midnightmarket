# The Arms Fair — Balance Math Sheet
### Version 0.2 | Design & Tuning Reference | Unity Edition

---

## How to Use This Document

Every numeric constant in the game lives here first, then gets copied into `Balance.cs` in the `ArmsFair.Shared` project. Because Shared is referenced by both the Unity client and the ASP.NET Core server, changing a number here propagates to both sides automatically. When playtesting reveals a problem — peace broker is never chosen, total war arrives too fast, covert sales are always dominant — come here first, change the number, document why, and note the version.

Each section explains the *reasoning* behind the numbers, not just the numbers themselves. That context is what lets you tune intelligently rather than randomly.

---

## 1. Starting Values

Starting values vary by game mode. The world tracks are set at game creation and do not change until player actions begin in round 1.

```typescript
export const STARTING_VALUES = {

  // Per-player starting values — identical across all modes
  player: {
    capital:        50,   // $50M
    reputation:     75,
    share_price:   100,
    peace_credits:   0,
    latent_risk:     0,
  },

  // World track starting values — varies by game mode
  tracks: {
    realistic: {
      market_heat:    30,
      civilian_cost:  20,
      stability:      25,
      sanctions_risk: 10,
      geo_tension:    35,
    },
    equal_world: {
      market_heat:    20,
      civilian_cost:  10,
      stability:      15,
      sanctions_risk:  5,
      geo_tension:    20,
    },
    blank_slate: {
      market_heat:    10,
      civilian_cost:   5,
      stability:      10,
      sanctions_risk:  0,
      geo_tension:    10,
    },
    hot_world: {
      market_heat:    55,
      civilian_cost:  45,
      stability:      50,
      sanctions_risk: 30,
      geo_tension:    55,
    },
    // Custom mode: host-defined — use equal_world as fallback default
  },

  // Country tension starting values — varies by mode
  country_tension: {
    realistic:   'acled_gpi_seeded',  // fetched from external APIs
    equal_world: 25,                  // all countries identical
    blank_slate:  5,                  // all countries identical
    hot_world:   'regional_preset',   // pre-defined regional groupings
    custom:      'host_defined',      // per-country in lobby editor
  },

} as const;
```

**Tuning notes by mode:**
- Realistic: if the game feels immediately overwhelming in round 1, consider capping the maximum stage seeded from ACLED at 3 (not 4) to give players one round to orient before crisis level events.
- Equal World: the lower starting tracks mean Total War is ~20% further away than Realistic, which gives newer players more time to understand the mechanics before the doomsday clock becomes urgent.
- Blank Slate: with no market at start, round 1 profit is zero for all players. Starting capital may need to increase to $65M in this mode so players aren't cash-starved before any market exists.
- Hot World: at these starting values, Global Sanctions (civilian cost 100) is only ~8 rounds away at average play. This mode should be communicated to players as a fast, high-pressure variant.

---

## 2. Track Movement — Per Action Per Round

This is the core of the balance sheet. Every number here is a *base* value before multipliers are applied.

### 2.1 Base Track Deltas by Sale Type and Weapon Category

```typescript
export const TRACK_DELTAS = {

  open_sale: {
    //                market  civilian  stability  sanctions  geo_tension
    small_arms:    [    +1,      +3,       +1,        0,          0    ],
    vehicles:      [    +2,      +2,       +1,       +1,          0    ],
    air_defense:   [    +3,      +1,       +2,       +1,         +1    ],
    drones:        [    +4,      +4,       +2,       +2,         +1    ],
  },

  // Covert sales have the same base values but apply with a 1-round delay
  // and do NOT affect sanctions_risk immediately (it's hidden)
  covert_sale: {
    small_arms:    [    +1,      +3,       +1,        0,          0    ],
    vehicles:      [    +2,      +2,       +1,        0,          0    ],  // sanctions: 0 until traced
    air_defense:   [    +3,      +1,       +2,        0,         +1    ],
    drones:        [    +4,      +4,       +2,        0,         +1    ],
    // sanctions_risk added retroactively if/when blowback traces the sale
    trace_sanctions_hit: +3,   // added to sanctions_risk when a covert sale is traced
  },

  aid_cover: {
    // Weapon is still delivered — stability/market heat same as open_sale
    // Only civilian_cost and sanctions_risk are suppressed by the cover story
    small_arms:    [    +1,      -1,       +1,        0,          0    ],  // civ cost reversed
    vehicles:      [    +2,      -1,       +1,        0,          0    ],
    air_defense:   [    +3,      -1,       +2,        0,         +1    ],
    drones:        [    +4,      -1,       +2,        0,         +1    ],
    // If exposed: civilian_cost penalty applied retroactively (+5 on top of normal)
    fraud_exposure_penalty: +5,
  },

  peace_broker: {
    // No weapon delivered — flat values regardless of weapon or country
    market_heat:    -1,
    civilian_cost:  -1,
    stability:      -2,
    sanctions_risk:  0,
    geo_tension:     0,
    cost_to_player:  2,    // $2M operational cost
    peace_credit:   +1,    // earns 1 peace credit
  },

  dual_supply_multiplier: 2.2,
  // Applied to ALL track deltas when is_dual_supply = true
  // Rationale: arming both sides creates more than double the harm
  // because weapons circulate between factions unpredictably

} as const;
```

**Design rationale:**
- Small arms have the highest civilian cost per unit because they proliferate uncontrollably — they're the most realistic driver of long-term civilian harm.
- Drones have maximum market heat AND civilian cost — they are the highest-value, highest-consequence weapon. Selling drones is a high-risk high-reward play.
- Air defense systems raise geo_tension because they change the strategic balance — a nation with good air defense can shelter other actors, escalating great-power interest.
- Peace broker costs $2M and earns -1 market heat — the player is actively investing in reducing demand, not just abstaining.

---

## 3. Stage Multipliers

Track deltas are multiplied by the target country's current stage. This makes selling into active war zones more impactful — more profitable AND more destabilizing.

```typescript
export const STAGE_MULTIPLIERS = {
  //  Stage 0   1     2     3     4     5
  track: [  0, 0.5,  1.0,  1.8,  2.2,  0  ],
  profit: [  0, 0.5,  1.0,  1.8,  2.2,  0  ],
  // Stage 5 (Failed State) = 0 — no market, no effect
  // Stage 4 multiplier is intentionally higher than stage 3
  // because Humanitarian Crisis zones are the most
  // visible — maximum profit but maximum blowback risk
} as const;
```

**Example calculation — full sale:**
Player sells drones via open sale into a Stage 3 (Hot War) country:
```
market_heat  delta = 4 × 1.8 = +7.2  → rounds to +7
civilian_cost delta = 4 × 1.8 = +7.2  → rounds to +7
stability    delta = 2 × 1.8 = +3.6  → rounds to +4
sanctions    delta = 2 × 1.8 = +3.6  → rounds to +4
```
In a 5-player game where all 5 sell drones into a Stage 3 zone:
```
market_heat  this round: +35
civilian_cost this round: +35
stability    this round: +20
```
Starting from track values of 30/25/25 — that would push civilian cost to 60 and trigger the UN discussion event in one round. Players will feel this immediately.

---

## 4. Profit Model

### 4.1 Base Profit by Weapon Category

```typescript
export const PROFIT = {
  base: {
    //  $M per sale
    small_arms:   4,
    vehicles:    11,
    air_defense: 22,
    drones:      34,
  },

  // Stage multiplier — same table as track multipliers
  stage_multiplier: STAGE_MULTIPLIERS.profit,

  // Market heat bonus — kicks in when heat > 80
  market_heat_bonus: (heat: number): number => {
    if (heat >= 80) return 1.25;   // +25% profit when market is hot
    return 1.0;
  },

  // Supplier relationship bonus
  relationship_bonus: {
    tier_1:  1.0,   // 0-4 points: no bonus
    tier_2:  1.08,  // 5-9 points: +8%
    tier_3:  1.15,  // 10-14 points: +15%
    tier_4:  1.22,  // 15+ points: +22%
  },

  // Dual supply multiplier on profit — slightly below track multiplier
  // rationale: dual supply is logistically complex — not quite 2x efficient
  dual_supply_profit_multiplier: 1.8,

  // Covert sale risk premium
  covert_premium: 1.3,   // +30% profit to compensate for blowback risk

  // Aid cover penalty — overhead costs of maintaining cover
  aid_cover_penalty: 0.8,  // -20% profit

  // Reconstruction contract flat rate — immune to all multipliers
  reconstruction_payout: 8,    // $M per round, for 3 rounds = $24M total
} as const;
```

### 4.2 Full Profit Calculation

```typescript
function calculateProfit(action: ResolvedAction, state: GameState): number {
  const base    = PROFIT.base[action.weapon_category];
  const stage   = state.countries[action.target_country].stage;
  const stageMul = PROFIT.stage_multiplier[stage];
  const heatMul  = PROFIT.market_heat_bonus(state.tracks.market_heat);

  const relPts  = state.players[action.player_id]
                       .supplier_relationships[action.supplier_id] ?? 0;
  const relMul  = relPts >= 15 ? PROFIT.relationship_bonus.tier_4
                : relPts >= 10 ? PROFIT.relationship_bonus.tier_3
                : relPts >=  5 ? PROFIT.relationship_bonus.tier_2
                :                PROFIT.relationship_bonus.tier_1;

  let profit = base * stageMul * heatMul * relMul;

  if (action.is_dual_supply)              profit *= PROFIT.dual_supply_profit_multiplier;
  if (action.sale_type === 'covert')      profit *= PROFIT.covert_premium;
  if (action.sale_type === 'aid_cover')   profit *= PROFIT.aid_cover_penalty;

  return Math.round(profit);
}
```

### 4.3 Profit Reference Table

Expected profit per sale at standard conditions (no multipliers beyond stage):

| Weapon | Stage 0 | Stage 1 | Stage 2 | Stage 3 | Stage 4 | Stage 5 |
|---|---|---|---|---|---|---|
| Small arms | $0 | $2M | $4M | $7M | $9M | $0 |
| Vehicles | $0 | $6M | $11M | $20M | $24M | $0 |
| Air defense | $0 | $11M | $22M | $40M | $48M | $0 |
| Drones | $0 | $17M | $34M | $61M | $75M | $0 |

With covert premium (+30%) and market heat bonus (+25%): multiply by 1.625.
Drone sale into Stage 4 covert with hot market: $75M × 1.625 = **$122M** — one sale.
That's the ceiling. It should feel dangerous to reach it.

---

## 5. Procurement Costs and Budget

```typescript
export const PROCUREMENT = {
  // Cost to BUY weapons from suppliers ($M)
  cost: {
    small_arms:   2,
    vehicles:     6,
    air_defense: 12,
    drones:      18,
  },

  // Supplier price modifiers (applied to procurement cost)
  supplier_modifiers: {
    apex_defense:    1.20,   // +20% — premium US supplier
    horizon_arms:    1.00,   // baseline EU supplier
    longwei:         0.90,   // -10% Chinese supplier
    al_noor:         0.95,   // -5% Gulf supplier
    ural_export:     0.75,   // -25% Russian supplier
    gray_channel:    0.70,   // -30% untraceable — cheapest
    vostok_special:  0.60,   // -40% ultra-cheap — very high risk
  },

  // Relationship discount stacks with supplier modifier
  relationship_discount: {
    tier_1: 0.00,   // 0-4 pts: no discount
    tier_2: 0.05,   // 5-9 pts: -5%
    tier_3: 0.10,   // 10-14 pts: -10%
    tier_4: 0.15,   // 15+ pts: -15%
  },

  // Export license costs ($M)
  license_cost: {
    standard:   3,
    restricted: 8,
  },

  // Lobbying action cost and success chance
  lobbying: {
    cost:         5,    // $M
    success_chance: 0.40,
    // If successful: restricted → standard for 1 round
  },
} as const;
```

**Budget math — can a player survive round 1 without income?**
Starting capital: $50M
Cost of one drone purchase (Horizon Arms): $18M
Cost of one standard license: $3M
Remaining: $29M — yes, comfortable.

Cost of two air defense purchases (Apex Defense): $12M × 1.2 × 2 = $28.8M
Plus two licenses: $6M
Remaining: $15.2M — tight but viable.

This means players can afford meaningful procurement from day 1 without needing to have sold anything yet. After round 1, income from sales should more than cover procurement costs for aggressive players.

---

## 6. Special Action Costs

```typescript
export const SPECIAL_ACTIONS = {

  whistle: {
    level_1: {
      cost:              3,    // $M — private intel
      reputation_hit:    0,
    },
    level_2: {
      cost:              8,    // $M — public procurement leak
      reputation_hit:   -3,   // corporate espionage stigma
    },
    level_3: {
      cost:             15,    // $M — full expose
      reputation_hit:   -5,   // significant — this is aggressive
      target_change_cost: 10, // $M penalty if target changes their action
      target_reputation_if_changed: -3,
    },
  },

  short_selling: {
    stake:              5,    // $M minimum
    payout_multiplier:  3.0,  // 3× if correct (target rep falls 15+)
    window_rounds:      1,    // resolves next round
  },

  coup: {
    cost_range:       [20, 40],   // $M — sliding by country stability
    cost_formula: (stability: number) => Math.round(20 + (stability / 100) * 20),
    // stable country (stability 80) costs $36M to coup
    // unstable country (stability 20) costs $24M to coup
    cooldown_rounds:  0,          // no cooldown — but each country only once per game
    latent_risk_on_failure: 5,
  },

  manufacture_demand: {
    cost_range:       [10, 25],   // $M — sliding by country stability
    cost_formula: (stage: number) => Math.round(10 + (stage === 0 ? 15 : 5)),
    // Stage 0 (Dormant): $25M — hard to destabilize truly stable nation
    // Stage 1 (Simmering): $15M — easier, tension already present
    tension_gain_per_action: [15, 20],  // random in range
    // At tension 40: moves to Stage 1
    // At tension 70: moves to Stage 2
    exposure_reputation_hit: -20,
    exposure_trade_ban_rounds: 2,
  },

  peacekeeping_investment: {
    cost:              10,   // $M per round
    stability_delta:   -3,   // better than peace broker (-2)
    civilian_delta:    -2,
    requires_cosignatories: 1,   // at least 1 other player must co-invest
    spread_reduction:  0.04,     // flat reduction to spread chance in target zone
  },

  reconstruction_bid: {
    currency: 'peace_credits',   // not $M — separate economy
    payout_per_round:  8,        // $M — flat, immune to all multipliers
    payout_rounds:     3,        // total: $24M over 3 rounds
    // Reconstruction is always worth less than a single drone sale into a hot war
    // but it's guaranteed, safe, and reputation-positive
    reputation_on_completion: +5,
  },

} as const;
```

---

## 7. Blowback and Reputation System

### 7.1 Blowback Trigger Probabilities

```typescript
export const BLOWBACK = {

  // Base trace probability by weapon tier
  base_trace_chance: {
    small_arms:   0.15,   // hard to trace — no serial numbers, proliferates widely
    vehicles:     0.35,   // serial-numbered, visible, traceable if investigated
    air_defense:  0.50,   // sophisticated — export records exist, traceable
    drones:       0.70,   // highest trace — precision weapons have identifiable signatures
  },

  // Modifiers to trace chance
  modifiers: {
    covert_sale:          +0.10,  // covert sales are investigated harder when found
    aid_cover_exposed:    +0.25,  // fraud triggers deep investigation
    gray_channel:         -0.15,  // untraceable supplier reduces chance
    vostok_special:       -0.20,  // very untraceable
    high_latent_risk:     +0.10,  // if latent_risk > 20, investigations are more thorough
    hot_war_zone:         +0.05,  // hot war zones get more media/NGO attention
    humanitarian_crisis:  +0.10,  // crisis zones: maximum scrutiny
  },

  // Reputation hits on blowback
  reputation_hit: {
    small_arms:   -8,
    vehicles:    -12,
    air_defense: -10,
    drones:      -20,   // named in headlines — maximum reputational damage
    covert_bonus: -5,   // additional hit if it was a covert sale
    dual_supply_bonus: -8,  // arming both sides — severe condemnation
  },

  // Track effects on blowback
  track_effects: {
    civilian_cost_addition:   2,
    sanctions_risk_addition:  3,
  },

  // Latent risk accumulation (hidden track per player)
  latent_risk: {
    per_covert_sale:          3,
    per_gray_channel_sale:    5,
    per_vostok_sale:          7,
    per_coup_concealed:       5,
    per_manufacture_demand:   4,
    // Latent risk converts to reputation damage when an investigation fires
    conversion_rate:          0.5,  // 10 latent risk → -5 reputation on investigation
    max_per_investigation:   -25,   // cap on single investigation damage
  },

} as const;
```

### 7.2 Reputation Effects Table

```typescript
export const REPUTATION = {

  gains: {
    peace_broker_round:           +3,
    ceasefire_contribution:       +8,    // contributed to a ceasefire event
    reconstruction_completion:    +5,
    clean_round:                  +1,    // round passes with no blowback traced to you
  },

  losses: {
    open_sale_standard:           -1,    // doing legal business — minor reputational cost
    covert_sale_discovered:      -10,    // base — modified by weapon tier
    aid_cover_fraud:             -15,
    investigation_settled:       -10,    // unavoidable even when settled
    whistle_level_3_used:         -5,    // you're seen as aggressive
    treaty_broken:               -8,
    coup_exposed:                -15,
    manufacture_demand_exposed:  -20,
    company_collapse:            -99,    // effectively terminal
  },

  // Final score multiplier: score = profit × (reputation / 100)
  // At rep 100: full profit counts
  // At rep 75:  75% of profit counts
  // At rep 50:  50% of profit counts (halved)
  // At rep 25:  25% of profit counts (devastating)
  // At rep 0:   company collapses — player enters observer mode
  collapse_threshold: 0,

} as const;
```

### 7.3 Reputation Decay and Recovery

Reputation does not recover passively — it requires active investment in peace actions or the passage of clean rounds. This creates meaningful choices: do you spend rounds doing peace broker to recover reputation, or keep selling and hope for no blowback?

```typescript
export const REPUTATION_RECOVERY = {
  clean_round_gain:   +1,   // per round with zero blowback traced to you
  max_passive_gain:    3,   // per game — passive recovery is very limited
  // Most recovery comes from active actions: peace broker, ceasefire votes
} as const;
```

---

## 8. Track Threshold Events

```typescript
export const THRESHOLDS = {

  market_heat: {
    80: {
      effect: 'profit_bonus_and_civ_cost_double',
      profit_bonus_pct: 25,
      civilian_cost_multiplier: 2.0,
      description: 'Market overheating — profits spike but harm accelerates',
    },
    100: {
      effect: 'market_crash',
      reset_to: 40,
      freeze_profits_rounds: 1,
      description: 'Market crashes — one round of zero income for all players',
    },
  },

  civilian_cost: {
    60: {
      effect: 'un_discussion',
      sanctions_risk_addition: 5,
      description: 'UN Security Council opens debate on arms flows',
    },
    75: {
      effect: 'sanctions_event',
      target: 'highest_civilian_contributor',  // weighted random
      suspension_rounds: 2,
      description: 'International sanctions designate one player',
    },
    100: {
      effect: 'ending_global_sanctions',
      endgame_rounds: 3,
    },
  },

  stability: {
    80: {
      effect: 'spread_multiplier',
      spread_multiplier: 2.0,
      description: 'World on edge — conflicts spread twice as fast',
    },
    100: {
      effect: 'ending_total_war',
    },
  },

  sanctions_risk: {
    60: {
      effect: 'license_cost_double',
      duration_rounds: 'permanent_until_track_drops',
      description: 'Export license scrutiny intensifies',
    },
    80: {
      effect: 'investigation',
      target: 'highest_latent_risk',  // weighted random
      description: 'Formal investigation notice issued to one player',
    },
  },

  geo_tension: {
    70: {
      effect: 'supplier_bloc_restriction',
      description: 'US/Russian suppliers refuse sales to opposite-bloc buyers',
    },
    90: {
      effect: 'great_power_crisis_zone',
      description: 'Great-power confrontation creates new high-tension zone',
      new_zone_stage: 3,
    },
    100: {
      effect: 'ending_great_power_confrontation',
      endgame_rounds: 2,
    },
  },

} as const;
```

---

## 9. Conflict Spread Math

```typescript
export const SPREAD = {
  // Base spread chance from a Stage 3+ country to each bordering nation per round
  base_chance_per_border: 0.08,    // 8%

  // Modifiers (additive)
  per_sale_into_zone:               +0.03,  // each sale this round adds 3%
  per_treaty_signatory:             -0.05,  // each treaty signatory reduces by 5%
  peacekeeping_flat_reduction:      -0.04,  // flat reduction per peacekeeping investor
  stage_4_bonus:                    +0.04,  // Humanitarian Crisis spreads faster

  // Hard caps
  min_chance:  0.00,   // can never be negative
  max_chance:  0.60,   // capped at 60% — even worst case not certain

  // When stability > 80: all spread chances doubled (applied after modifiers and cap)
  high_stability_multiplier: 2.0,

  // Stage increase on spread
  spread_stage_increase: 1,   // neighbor moves up one stage

} as const;
```

**Spread scenario examples:**

*Worst case* — Stage 4 zone, 5 players all sold in, no treaties, stability > 80:
```
chance = (0.08 + 0.04 + 5×0.03) cap at 0.60 = 0.60 × 2.0 (stability) = capped at 0.60
```
60% chance of spreading to EACH neighbor. A country with 3 neighbors: ~93% chance at least one spreads.

*Managed case* — Stage 3 zone, 2 sales, 3 treaty signatories:
```
chance = 0.08 + 2×0.03 - 3×0.05 = 0.08 + 0.06 - 0.15 = -0.01 → clamped to 0.00
```
Treaties completely suppress spread. This is the mechanical payoff for cooperation.

---

## 10. Coup Outcomes

```typescript
export const COUP = {

  // Outcome probability distribution (must sum to 1.0)
  outcomes: {
    success:           0.35,
    partial:           0.25,
    failure_concealed: 0.20,
    failure_exposed:   0.15,
    blowback:          0.05,
  },

  // Cost formula: $20M base + $0-20M based on country stability score
  // A dormant stable country (tension 10) costs ~$22M to coup
  // A simmering unstable country (tension 60) costs ~$32M to coup
  cost_formula: (tension: number): number =>
    Math.round(20 + (tension / 100) * 20),

  // Outcome effects
  effects: {
    success: {
      stage_increase:      1,
      favor_rounds:        3,
      license_discount:    0.20,   // -20% on licenses in this country for 3 rounds
      reputation_hit:      0,      // success is concealed — no public knowledge
    },
    partial: {
      stage_increase:      1,
      favor_granted:       false,
      reputation_hit:      0,
    },
    failure_concealed: {
      stage_increase:      0,
      latent_risk:        +5,
      reputation_hit:      0,
    },
    failure_exposed: {
      stage_increase:      0,
      reputation_hit:     -15,
      trade_ban_rounds:    2,      // banned from selling to this country + allies
      sanctions_risk:     +8,
    },
    blowback: {
      stage_increase:      1,
      spread_fires:        true,   // immediate spread to all neighbors
      geo_tension:        +5,
      reputation_hit:     -10,
    },
  },

} as const;
```

---

## 11. Manufactured Demand

```typescript
export const MANUFACTURED_DEMAND = {

  // Cost to destabilize ($M)
  cost: {
    stage_0_dormant:    25,   // hardest — truly stable nation
    stage_1_simmering:  15,   // easier — tension already present
  },

  // Tension gain per action
  tension_gain: {
    min: 15,
    max: 20,
    // Random in range each time
  },

  // Stage thresholds from tension score
  stage_transitions: {
    40: 1,   // tension 40+ → Stage 1 (covert sales possible)
    70: 2,   // tension 70+ → Stage 2 (open sales possible)
  },

  // If multiple players destabilize the same country, effects stack additively
  // 3 players all doing manufacture_demand: +45-60 tension in one round
  // Can take a Stage 0 country to Stage 2 in a single round if coordinated

  // Exposure consequences
  exposure: {
    reputation_hit:     -20,
    trade_ban_rounds:    2,
    headline_generated:  true,
    sanctions_risk:     +5,
  },

} as const;
```

---

## 12. Company Collapse Conditions

```typescript
export const COLLAPSE = {

  trigger_threshold:  0,   // reputation hits 0 → collapse

  // What happens on collapse
  effects: {
    player_status:       'observer',
    inventory_auctioned: true,
    auction_rounds:      1,          // 1 round for other players to bid
    auction_currency:    'capital',  // bids in $M
    pending_reconstruction_forfeited: true,
    supplier_relationships_available: true,   // others can claim at reduced cost
    supplier_claim_cost_multiplier:   0.5,    // 50% of normal relationship cost
    score_locked_at_collapse:         true,   // final profit locked in
  },

  // Observer mode abilities (collapsed players stay engaged)
  observer_abilities: {
    can_chat:           true,
    can_short_stocks:   true,
    can_whistle_level_1: true,   // still has intelligence network
    can_whistle_level_2: false,
    can_whistle_level_3: false,
    can_sell:           false,
    can_procure:        false,
    gets_free_intel:    true,    // 1 free level-1 intel per round — keeps them invested
  },

  // How hard is it to engineer a collapse?
  // Reputation starts at 75. To reach 0:
  // Need: 75 reputation damage total
  // Fast collapse requires: multiple investigations + exposed covert sales + blowback
  // Rough minimum: ~5-6 rounds of coordinated targeting
  // Cannot be done in 1-2 rounds under any circumstances
  estimated_minimum_rounds_to_engineer: 5,

} as const;
```

---

## 13. Ending Condition Triggers

```typescript
export const ENDINGS = {

  total_war: {
    trigger: { stability: 100 },
    winner: null,   // collective loss
  },

  global_sanctions: {
    trigger: { civilian_cost: 100 },
    endgame_rounds: 3,
    endgame_rules: {
      only_gray_channel: true,
      gray_latent_risk_multiplier: 3.0,
      winner_by: 'composite_score',   // profit × (reputation/100)
    },
  },

  great_power_confrontation: {
    trigger: { geo_tension: 100 },
    endgame_rounds: 2,
    endgame_rules: {
      all_zones_jump_to_stage_4: true,
      private_brokers_locked_out: false,  // they can still sell, unlike total war
      covert_only: true,
      winner_by: 'composite_score',
    },
  },

  market_saturation: {
    trigger_condition: 'failed_states_pct >= 0.40',
    // 40% of initially-active zones (Stage 2+) have reached Stage 5
    winner_by: 'composite_score',
  },

  negotiated_peace: {
    trigger_condition: 'all_players_vote_yes AND stability < 20',
    winner_by: 'composite_score',
    peace_bonus: 20,   // $M bonus for players with 5+ peace credits
    reputation_bonus: +10,  // all players get a reputation boost
  },

  // Composite score formula
  composite_score: (profit: number, reputation: number): number =>
    Math.round(profit * (reputation / 100)),

} as const;
```

**Ending likelihood by playstyle:**

| Dominant player behavior | Most likely ending |
|---|---|
| All players sell aggressively, dual supply common | Total War or Market Saturation |
| All players covert-only, minimal peace | Global Sanctions |
| Heavy proxy conflict arming | Great Power Confrontation |
| Mixed — some peace brokers, some sellers | Market Saturation |
| Genuine coordination from round 4+ | Negotiated Peace |

---

## 14. Expected Game Length Model

```typescript
export const PACING = {

  // Track rise per round — average across all sale types and player counts
  // Assumes 4 active players, mixed strategies, Stage 2-3 zones
  average_track_rise_per_round: {
    market_heat:    8,    // hits 80 around round 7 — market overheats mid-game
    civilian_cost:  7,    // hits 75 (sanctions) around round 8
    stability:      5,    // hits 100 (total war) around round 15 if unchecked
    sanctions_risk: 4,    // hits 80 (investigation) around round 13
    geo_tension:    3,    // hits 70 (bloc restriction) around round 12
  },

  // These are AVERAGES — cooperative play slows them, aggressive play accelerates
  // Target: players should feel pressure by round 6-8, crisis by round 10-12
  // Total War should feel like a genuine threat, not a certainty

  // Phase durations (ms) — standard settings
  phase_durations: {
    world_update:   30_000,    // 30s
    procurement:    60_000,    // 60s
    negotiation:   120_000,    // 2 min
    sales:          90_000,    // 90s
    reveal:         30_000,    // 30s
    consequences:   60_000,    // 60s
    total_per_round: 390_000,  // 6.5 min per round
  },

  // Expected game length at standard timers
  expected_rounds:  { min: 10, max: 20, typical: 14 },
  expected_minutes: { min: 65, max: 130, typical: 91 },

  // Async mode — same phases, extended timers
  async_phase_durations: {
    world_update:    0,           // instant
    procurement:   3_600_000,    // 1 hour
    negotiation:  86_400_000,    // 24 hours
    sales:        43_200_000,    // 12 hours
    reveal:            0,        // instant
    consequences:      0,        // instant
  },
  async_expected_days: { min: 5, max: 15, typical: 8 },

} as const;
```

---

## 15. Balance Tuning Guide

When playtesting reveals a problem, use this guide to identify which constant to change.

| Symptom | Likely cause | Tuning action |
|---|---|---|
| Total War arrives by round 5 | stability deltas too high | Reduce `TRACK_DELTAS.open_sale.*.stability` by 20% |
| Total War never feels threatening | stability deltas too low | Raise stage_multiplier[3] and [4] |
| Nobody ever chooses Peace Broker | peace broker ROI too low | Raise `reconstruction_payout` or reduce `peace_broker.cost_to_player` |
| Covert is always dominant | covert premium too high vs. risk | Raise `blowback.base_trace_chance.drones` to 0.85+ |
| Open sale is always dominant | covert risk too punishing | Lower trace chances or blowback reputation hits |
| Players never blow the whistle | whistle costs too high | Lower level_2 cost to $5M, level_3 to $10M |
| Sanctions are ignored | sanctions effect too weak | Raise `suspension_rounds` to 3 or increase license cost multiplier |
| Small arms underused | profit/risk ratio off | Raise small arms base profit to $6M |
| Drones always dominant | profit ceiling too high vs. risk | Raise drones trace chance to 0.85, raise reputation hit to -25 |
| Market never crashes | heat ceiling rarely reached | Lower market crash threshold from 100 to 90 |
| Coup never attempted | cost too high | Lower cost formula base from $20M to $15M |
| Reputation collapses too fast | reputation loss too steep | Reduce covert_sale_discovered to -7 |
| Reputation never matters | multiplier effect too weak | Already steep — check whether players understand the mechanic |
| Games all end in market saturation | failed state threshold too low | Raise trigger from 40% to 50% of initial active zones |
| Negotiated peace never happens | stability drop rate too slow | Raise peace_broker stability delta to -3 |
| Geo tension irrelevant | rises too slowly | Raise air_defense geo_tension contribution from +1 to +2 |

### Version History
| Version | Change | Reason |
|---|---|---|
| 0.1 | Initial values | Baseline — untested |

---

*End of Balance Math Sheet v0.1*
*Next: playtest with these values, log symptoms, return to tuning guide above*

---

## 16. Legacy Score Math

```typescript
export const LEGACY_SCORE = {

  // Base formula
  // Legacy = (peace_credits × 5) + (ceasefires × 20) + (reconstruction × 15)
  //        + stability_bonus - failed_states_penalty - civilian_cost_penalty

  multipliers: {
    peace_credit:              5,
    ceasefire_contribution:   20,
    reconstruction_contract:  15,
  },

  // Bonus for world stability at game end
  // Only applies if stability < 50 at game end
  stability_bonus: {
    stability_0_to_15:   100,   // World Peace threshold — max bonus
    stability_16_to_30:   60,
    stability_31_to_50:   20,
    stability_above_50:    0,
  },

  penalties: {
    per_failed_state_caused:     -10,
    per_civilian_cost_point:     -0.5,  // lifetime civ cost contribution
  },

  // World Peace ending — bonus applied to ALL active players
  world_peace_bonus:           200,

  // Minimum legacy score is 0 — cannot go negative
  minimum:                       0,

} as const;
```

---

## 17. World Peace Track Requirements

```typescript
export const WORLD_PEACE = {

  // ALL conditions must be true simultaneously for World Peace vote to succeed
  required_conditions: {
    max_stability:              15,   // lower than Negotiated Peace (20)
    max_civilian_cost:          25,   // world harm must be actively reduced
    max_country_stage:           2,   // no Stage 3+ (Hot War) zones anywhere
    min_reconstruction_complete: 3,   // genuine rebuilding must have occurred
    all_players_vote_yes:      true,  // unanimous — no abstentions
  },

  // If vote fails, server returns missing conditions to display to players
  // This tells players exactly how far they are from World Peace

  // Negotiated Peace (existing) — easier threshold for comparison
  negotiated_peace: {
    max_stability:    20,
    all_vote_yes:    true,
    // No civilian cost, country stage, or reconstruction requirements
  },

} as const;
```

---

## 18. Version History (updated)

| Version | Change | Reason |
|---|---|---|
| 0.1 | Initial values | Baseline — untested |
| 0.2 | Engine changed to Unity | Platform requirements |
| 0.3 | Added Legacy Score math, World Peace conditions | New feature additions |
| 0.4 | Starting values expanded to cover all five game modes | Game mode feature |

