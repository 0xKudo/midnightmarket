# The Arms Fair — Game Design Specification
### Version 0.2 | Design Document | Unity Edition

---

## 1. Overview

**The Arms Fair** is a multiplayer geopolitical simulation game for 2–6 players in which each player controls a private arms brokerage operating in a world seeded from real geopolitical data. Players procure weapons from a supplier network, sell to conflict zones across an interactive globe, manipulate political conditions, and compete for profit — while collectively managing a world that can spiral into total war or collapse into peace.

The game has no fixed round limit. It ends when the world reaches a breaking point — or the rare occasion when players choose not to let it.

The central thesis is mechanical, not didactic: the rules of the game make individually rational behavior collectively catastrophic. No text ever calls the player immoral. The world shows them what they did.

---

## 2. Core Design Principles

1. **Emergence over scripting.** After round 1, the world is entirely player-driven. Real-world data seeds the opening state. Everything after is simulation.
2. **Every decision has a cost.** There is no free action — selling openly risks exposure, selling secretly risks discovery, not selling costs income.
3. **Collective ruin is always possible.** Total War is a real outcome. Players feel its approach on the globe before it arrives.
4. **Consequences are named, not abstracted.** Blowback events name weapon types, countries, and players. Human cost events are specific, not numerical.
5. **Peace is hard but not irrational.** Reconstruction contracts and ceasefire bonuses make cooperation a genuine strategic option, not just a sacrifice.
6. **No moralizing.** The game never editorializes. It presents information and lets players feel the weight themselves.

---

## 3. Players and Setup

### 3.1 Player Count
- 2–6 players (4–5 optimal)
- AI fill-in opponents available with tunable behavior profiles: Pure Defector, Tit-for-Tat, Sanctimonious (performs peace publicly, defects privately), Opportunist (follows whoever is winning)

### 3.2 Company Setup
Each player creates an arms brokerage at game start:
- **Company name** (custom or generated)
- **Home nation** — determines starting supplier relationships and regulatory exposure. A US-based company has access to premium suppliers but faces stricter export license requirements. A UAE-based company has fewer restrictions but lower supplier trust.
- **Starting capital:** $50M
- **Starting reputation:** 75 / 100
- **Starting supplier relationships:** 2 unlocked suppliers (determined by home nation)
- **Starting stock portfolio:** empty

### 3.3 World Initialization
On game launch, the server fetches current data from ACLED and the Global Peace Index to seed:
- **Active conflict zones** — countries with ongoing armed conflict. Start at Stage 3 (Hot War) or Stage 4 (Humanitarian Crisis) on the escalation ladder.
- **Tension zones** — countries with political instability, intercommunal violence, or contested elections. Start at Stage 2 (Active).
- **Stable countries** — all remaining nations. Start at Stage 1 (Simmering) or Stage 0 (Dormant).

From round 2 onward, real-world data is no longer consulted. The simulation is fully autonomous.

---

## 4. The Globe

The primary game interface is a full-screen interactive world map rendered using GeoJSON country geometry (Natural Earth dataset, 1:50m resolution). All ~195 countries are present and selectable.

### 4.1 Country States (Escalation Ladder)

| Stage | Name | Visual | Arms Demand | Profit Multiplier |
|---|---|---|---|---|
| 0 | Dormant | No glow | None — must be manufactured | — |
| 1 | Simmering | Faint teal pulse | Covert only | 0.5× |
| 2 | Active | Amber pulse | Open or covert | 1.0× |
| 3 | Hot War | Red pulse, fast | Open or covert | 1.8× |
| 4 | Humanitarian Crisis | Red pulse + ring | Open or covert | 2.2× (civilian cost ×3) |
| 5 | Failed State | Gray, no pulse | None — market destroyed | 0× |

Stages only increase through player action or conflict spread. They only decrease through coordinated peace actions (see Section 9) or reconstruction investment. Stage 5 is permanent for the remainder of the game.

### 4.2 Conflict Spread
Each round, after reveals, every country at Stage 3 or above rolls a spread check against each bordering nation:
- Base spread chance: 8% per border
- Modified by: total weapons sold into the zone that round (+3% per sale), active treaties (-5% per signatory), peacekeeping investment (-4% flat)
- If spread triggers: the neighboring country moves up one stage. A new hotspot appears on the globe. Players did not cause this directly — but they fueled it.

### 4.3 Country Data Panel
Clicking any country opens a panel showing:
- Current stage and tension score
- Demand type (open / covert / none)
- Profit estimate by weapon category
- Civilian cost modifier
- Active treaties involving this country
- Known sales history (rounds where at least one player sold here openly)
- Covert sale history (only visible if whistleblown or blowback fired)

---

## 5. The Five World State Tracks

All tracks run 0–100. Track values are global — shared across all players. Individual player actions push them.

### 5.1 Market Heat
Measures overall global arms market activity. High market heat increases all profit margins but also accelerates the other tracks.
- Rises: with every sale (+1 to +4 depending on weapon tier and zone stage)
- Falls: with ceasefires (-3), Failed States (-1 per zone, market contraction), peacekeeping rounds (-2)
- Effect at 80+: profit multipliers increase by 25% but civilian cost from all sales doubles
- Effect at 100: market overheats — a crash event fires, resetting heat to 40 and freezing all profits for one round

### 5.2 Civilian Cost
Measures cumulative humanitarian harm. Drives international political pressure.
- Rises: with every sale (weighted by weapon tier and zone stage), with blowback events, with conflict spread
- Falls: with peace broker actions, ceasefire agreements, reconstruction investment
- Effect at 60: UN sanctions discussions begin — a random player's supplier relationship is investigated
- Effect at 75: automatic sanctions event fires (see Section 11)
- Effect at 100: Global Sanctions ending triggered (see Section 13)

### 5.3 Global Stability
Measures world conflict intensity. The doomsday clock.
- Rises: with every sale to Stage 3+ zones, with conflict spread, with coup events, with dual supply
- Falls: with peace broker actions, with ceasefire votes, with successful peacekeeping investment
- Effect at 80: all conflict spread chances double
- Effect at 100: Total War ending triggered (see Section 13)

### 5.4 Sanctions Risk
Measures international regulatory attention on the arms trade specifically.
- Rises: with open sales to Stage 4 zones, with exposed covert sales, with whistleblower events, with aid cover fraud
- Falls: with successful lobbying actions, with round passing without incidents, with ceasefire progress
- Effect at 60: export license costs double for all players
- Effect at 80: one random player per round receives a formal investigation notice (see Section 10.3)

### 5.5 Geopolitical Tension
Measures great-power rivalry and bloc politics. Affects supplier availability and export license complexity.
- Rises: with sales that arm one side of a great-power proxy conflict, with coup events in strategically important nations
- Falls: with multilateral peace agreements, with trade diplomacy actions
- Effect at 70: US and Russian supplier networks begin refusing sales to opposite-bloc customers — forces players toward gray market
- Effect at 90: a great-power confrontation event fires, creating a new high-tension zone anywhere on the globe

---

## 6. Round Structure

Each round represents one fiscal quarter. There is no fixed number of rounds — the game ends when an ending condition is triggered (Section 13).

Average game length: 12–20 rounds (45–90 minutes at standard timer settings).

### Phase 1 — World Update (Automatic, ~30 seconds)
The simulation engine resolves the previous round's consequences:
- Conflict spread rolls computed and applied
- Blowback events from last round's sales resolved and named
- Human cost events generated and displayed on globe
- Failed State checks applied to Stage 4 zones that received no peace investment
- Reconstruction contract payouts distributed
- Supplier sanctions and availability updated
- All five tracks updated with cumulative effects
- The globe visually updates — zones change color, new hotspots appear, failed states go dark

Players watch this unfold on the globe. It should feel like reading the news.

### Phase 2 — Procurement (60 seconds, private)
Players visit their supplier network and purchase weapons inventory for this round. This is a private phase — other players see only that you spent money, not what you bought.

Each player:
- Reviews available suppliers and their current inventory, pricing, and risk profiles
- Selects weapon categories to purchase within their budget
- Weapon purchases are held in a private inventory until Phase 4

Budget constraints force real tradeoffs: buying expensive guided munitions means fewer resources for the negotiation phase. Loading up on cheap small arms means flying under the blowback radar but contributing invisibly to civilian cost.

Procurement choices are not visible to other players unless a Level 1 or Level 2 whistle is used in Phase 3.

### Phase 3 — Negotiation (120 seconds, social)
The game's social engine. All players can:

**Communicate:**
- Open global chat (visible to all)
- Private bilateral messages (visible only to sender and recipient)
- Propose treaties (see Section 9)

**Spend resources:**
- Purchase intelligence (Level 1 whistle — see Section 8)
- Leak procurement data (Level 2 whistle)
- Lobby for export licenses on restricted country pairs
- Short another player's stock (see Section 12)
- Initiate a peacekeeping investment proposal (requires co-signatories)

**Strategic notes:**
The negotiation timer is visible to all. The last 30 seconds are a crunch — deals made in the first 90 seconds often collapse in the final 30. Players can make promises they will not keep. The game enforces mechanically binding treaties with a penalty clause but cannot enforce verbal agreements.

### Phase 4 — Sales (90 seconds, sealed)
Each player secretly configures their action for the round:
1. **Select target country** (any selectable country with demand, or a covert sale attempt into Dormant/Stable)
2. **Select weapon category** from their purchased inventory
3. **Select sale type:** Open Sale, Covert Sale, Aid Cover, or Peace Broker (see Section 7)
4. **Optional modifiers:** apply lobbied licenses, mark as dual supply, flag as proxy routing

All selections are sealed until Phase 5. No player can see another's selection during this phase. The timer creates urgency — players must act within 90 seconds or their action defaults to Peace Broker (no sale).

### Phase 5 — Reveal (~30 seconds, cinematic)
All sales animate simultaneously on the globe:
- Each player's sale appears as an animated line from their company HQ to the target country
- Line color identifies the player
- Line style identifies the sale type: solid (open), dashed (covert, revealed), dotted (aid cover)
- Peace Broker players show a different visual — a stabilizing ring around their HQ
- Dual supply shows two lines from one player to the same zone in different colors

This is the most dramatic moment of every round. Players see for the first time where everyone sold — and who lied about their intentions in negotiation.

### Phase 6 — Consequences (~60 seconds)
The engine applies all effects from the round's sales:
- All five tracks update
- Profit is distributed to player accounts
- Blowback events are queued for Phase 1 of the next round
- Human cost events display (specific, named, not numerical)
- Leaderboard updates — current profit ranking visible to all
- Whistleblower resolution: any Phase 4 exposes are settled
- Treaty compliance checked — penalties applied for any broken agreements
- Stock price updates applied

---

## 7. Sale Types

### 7.1 Open Sale
The player publicly declares they are selling arms to this country. The sale is visible in Phase 5 and recorded permanently in that country's sale history.

**Consequences:**
- Full profit at standard rate
- Standard track contributions (market heat, civilian cost, stability)
- No blowback multiplier — the sale is documented and expected
- Reputation impact is low — this is legal, above-board commerce
- Available only in countries with Stage 2+ (Active conflict or above)

### 7.2 Covert Sale
The player routes weapons through shell companies, third-party nations, or falsified end-user certificates. The sale does not appear on the globe during Phase 5. Other players do not see it.

**Consequences:**
- Profit at 1.3× multiplier (risk premium)
- Track contributions are the same as open sale but applied with a one-round delay — the harm materializes before anyone can react
- Blowback risk is higher: if a blowback event fires, the investigation has a chance to trace the sale back to the player even if no whistle was blown. Traceability depends on weapon tier (guided munitions: 70% trace chance; small arms: 15% trace chance)
- If traced: the covert sale is publicly revealed retroactively — full reputation hit, civilian cost modifier applied retroactively
- Available in any country with demand, including Stage 1 (Simmering) countries — covert sales to stable-ish countries are one of the documented real-world causes of conflict ignition
- Covert sales to Stage 0 (Dormant) countries require the "manufactured demand" mechanic (see Section 9.3)

### 7.3 Aid Cover
The player publicly announces a humanitarian aid shipment while covertly routing weapons inside it. On the globe, this appears as a green aid route, not an arms sale.

**Consequences:**
- Profit at 0.8× (reduced — the cover operation has overhead)
- Civilian cost track decreases by 2 (the public narrative suppresses international attention)
- Actual weapons delivered — stability and market heat still rise as normal
- If whistleblown and exposed as fraud: civilian cost modifier reverses (+5 instead of -2), major reputation hit, and a Humanitarian Fraud event is publicly named and displayed
- Aid Cover is the most hypocritical action — the game treats it as such when exposed
- Available in any country Stage 1 and above

### 7.4 Peace Broker
The player makes no sale this round. They instead invest in diplomatic pressure, ceasefire talks, or peacekeeping infrastructure in one country.

**Consequences:**
- No profit — player pays a $2M operational cost for the diplomatic effort
- Target country's tension score reduces by 5
- Global stability track decreases by 2
- Civilian cost decreases by 1
- Player earns 1 Peace Credit (tracked privately — used for reconstruction contract priority)
- If enough players choose Peace Broker on the same country in the same round (threshold: 50% of active players), a Ceasefire Event fires in that country — the zone drops one stage on the escalation ladder
- Peace Credits accumulate and become the reconstruction contract bidding currency when a ceasefire is reached (see Section 9.4)

---

## 8. Whistleblower System

Whistleblowing is an intelligence and disruption mechanic available during Phase 3 (Levels 1–2) and Phase 4 (Level 3). It always costs the whistleblower something. There is no free exposure.

### Level 1 — Intelligence Purchase ($3M, private)
Available: Phase 3
Effect: The purchasing player privately learns the weapon category purchased by one target player during Phase 2. They know the target's inventory type but not their intended target country or sale type.
Use: Negotiation leverage. The player can truthfully warn others ("I know what they're buying"), bluff about it, or hold it as private knowledge.
Traceability: Untraceable — purchased through your own intelligence network.

### Level 2 — Procurement Leak ($8M, public)
Available: Phase 3
Effect: One target player's full Phase 2 procurement is publicly revealed to all players in the global chat. Weapon categories, quantities, and supplier used are all shown.
Traceability: Partially traceable. If the whistleblower and target share a supplier, the target knows the leak likely came from a rival who has intelligence contacts inside that supplier. The target doesn't know who for certain — but suspicion is powerful.
Reputation impact: The whistleblower takes a minor reputation hit (–3) for conducting corporate espionage.
Use: Disrupts the target's planned strategy, alerts other players to a potential threat, and poisons negotiation trust.

### Level 3 — Full Expose ($15M + –5 Reputation, public)
Available: Phase 4 only (after sales are sealed, before reveal)
Effect: One target player's complete sealed action — target country, weapon category, and sale type — is publicly revealed to all players immediately.
The target has a 30-second window to change their action, but changing after a Level 3 expose costs them $10M and a –3 reputation hit.
If the target chose Aid Cover and is exposed: Humanitarian Fraud event fires immediately — their civilian cost modifier flips to +5 that round and they take –10 reputation.
Traceability: High. The expose is clearly deliberate and expensive. The target knows someone paid to expose them. The whistleblower's identity is not confirmed by the game — but in a 5-player game, other players can deduce it.
Use: The nuclear option. Disrupts a key play, exposes hypocrisy, destroys trust. The reputation cost to the whistleblower ensures it is used sparingly.

### Whistleblower Protections and Limits
- A player can only use each whistle level once per round
- Using Level 3 twice in three consecutive rounds triggers an "Investigative Scrutiny" status on the whistleblower — their own actions become slightly more visible (covert sales have a +10% trace chance for 2 rounds)
- Whistleblowing is not available in the first round — players need at least one round of established behavior before intelligence gathering begins

---

## 9. Special Actions

### 9.1 Open Sale vs. Covert Sale — Reputation Dynamics
The long-run strategic difference between open and covert selling is reputational:

Open sellers build a public record — they are known arms dealers operating within the law. Their reputation is stable but never excellent. They are visible targets for sanctions but not for criminal prosecution.

Covert sellers accumulate hidden exposure. Each undiscovered covert sale adds a Latent Risk point (hidden from the player themselves, tracked internally). If blowback fires and traces a covert sale, Latent Risk is partially converted to public reputation damage. A player who has covertly sold heavily for many rounds can experience a sudden, large reputation collapse when one sale is finally traced — disproportionate to the single event, because the investigation uncovers a pattern.

This models how real arms embargo violations are prosecuted: years of activity are often revealed at once.

### 9.2 Funding Coups
Available: Phase 3 (proposed) or Phase 4 (executed)
Cost: $20M–$40M depending on country stability and size
Requirement: The target country must be Stage 1 or Stage 2 (a coup in a Stage 0 country is an attempt to manufacture a conflict from scratch — see 9.3)

A coup attempt destabilizes a government and, if successful, installs a faction more favorable to arms imports. The player funding the coup does not control the outcome — they are rolling a weighted die.

**Coup outcomes (resolved in Phase 1 of next round):**

| Result | Probability | Effect |
|---|---|---|
| Success | 35% | Country moves up one stage. New government has –20% export license restrictions with funding player for 3 rounds. A named political event fires. |
| Partial success | 25% | Country enters civil conflict. Stage +1. No favor granted — the winning faction didn't know who funded it. |
| Failure, concealed | 20% | Nothing happens publicly. Player loses the investment. Internally, Latent Risk +5. |
| Failure, exposed | 15% | Player is publicly implicated. Major reputation hit (–15). Diplomatic fallout — that country and its allies become restricted markets for 2 rounds. |
| Blowback | 5% | The coup triggers a regional crisis. Conflict spread fires immediately in all bordering nations. Stage +1 to target country and one random neighbor. |

Coups can only be attempted once per country per game. A country that has experienced a successful coup cannot be coup-targeted again.

Coups are the most powerful action in the game but also the most dangerous. They are asymmetric — they can create enormous new markets from nothing, but the failure states can be catastrophic.

### 9.3 Manufacturing Demand
Available: Phase 3
Cost: $10M–$25M (sliding scale based on country stability)
Target: Any Stage 0 (Dormant) or Stage 1 (Simmering) country

The player funds destabilization operations — disinformation, sectarian incitement, funding of extremist factions, or support for separatist movements — to artificially create internal tension. This does not immediately create an arms market, but it raises the country's internal tension score.

**Mechanic:**
- Each manufactured demand action raises the target country's internal tension by 10–20 points
- At 40 tension: the country moves to Stage 1 (Simmering) — covert sales become possible
- At 70 tension: the country moves to Stage 2 (Active) — open sales become possible, conflict spread becomes possible
- Manufactured demand is entirely covert — nothing is visible on the globe unless the action is whistleblown
- If the operation is exposed (via whistle or blowback): a named Destabilization Exposed event fires, the player takes a major reputation hit (–20), and the target country's government imposes a permanent trade ban with that player's company

**Cumulative effect:** multiple players can fund destabilization in the same country simultaneously — their effects stack. A country that three players are destabilizing moves toward Active conflict significantly faster. Players don't know others are doing this unless they use Level 1 intelligence to check.

This mechanic is deliberately uncomfortable. It accurately models a documented real-world phenomenon.

### 9.4 Reconstruction Contracts
When a country drops from Stage 3 to Stage 2 or below through a Ceasefire Event, a Reconstruction Contract becomes available for that country.

**Bidding:** Players bid using Peace Credits accumulated through Peace Broker actions in prior rounds. The highest Peace Credit bidder wins the contract.

**Payout:** Reconstruction contracts pay out over 3 rounds at a flat rate of $8M per round, regardless of market conditions. They are immune to sanctions and track effects.

**Strategic significance:** Reconstruction money is the cleanest income in the game. It cannot be sanctioned, cannot be whistleblown, and carries zero civilian cost or stability impact. For a player who has accumulated Peace Credits through genuine diplomatic investment, reconstruction contracts can be highly competitive with arms income in the late game — making Peace Broker a real long-game strategy rather than a sacrifice.

---

## 10. Weapons and Suppliers

### 10.1 Weapon Categories

| Category | Base Cost | Base Profit | Civilian Cost | Blowback Trace % | Notes |
|---|---|---|---|---|---|
| Small arms & ammunition | $2M | $4M | +3 | 15% | Hardest to trace. Highest civilian impact per dollar. |
| Vehicles & artillery | $6M | $11M | +2 | 35% | Serial-numbered. Traceable if investigated. |
| Air defense systems | $12M | $22M | +1 | 50% | Escalatory — raises stability track significantly |
| Drones & guided munitions | $18M | $34M | +4 | 70% | Maximum profit. Named in blowback events. |

All profits are modified by: zone stage multiplier, market heat modifier, weapon-country demand match, and supplier relationship bonus.

### 10.2 Supplier Network

| Supplier | Home | Price Modifier | Traceability | Availability Risk | Unlocked By |
|---|---|---|---|---|---|
| Apex Defense | USA | +20% | High | Low — unless sanctioned by OFAC | US/UK/EU home nation |
| Ural Export | Russia | –25% | Medium | High — sanctions-prone | RU/BY/IR home nation or gray channel purchase |
| Horizon Arms | EU | Base | Medium | Low | Any home nation |
| Longwei Industries | China | –10% | Low | Medium | CN/PK home nation or relationship-built |
| Gray Channel | Unknown | –30% | Very low | None — always available | Always available, always costs +5 Latent Risk |
| Al-Noor Trading | Gulf | –5% | Low | Low | ME home nation or lobbying |
| Vostok Special | Russia (unofficial) | –40% | Very low | Medium | Gray channel tier 2 — unlocked after 3 gray purchases |

**Relationship building:** Each purchase from a supplier earns 1 relationship point with them. At 5 points: 10% volume discount. At 10 points: priority access (never out of stock). At 15 points: exclusive inventory items (prototype weapon categories not available elsewhere).

**Supplier sanctions:** when a real-world-inspired event fires (e.g., a great-power sanctions event), a supplier may become temporarily unavailable. Players who relied heavily on that supplier are forced to pivot, often to the Gray Channel.

### 10.3 Export Licenses
Certain country pairs require a formal export license before open sales are permitted. Licenses are purchased during Phase 3.

- Standard license: $3M, available for most country pairs, approved automatically
- Restricted license: $8M, requires 60 seconds to process, may be blocked if sanctions risk is above 60
- Embargoed: sale is illegal via open channel — covert or gray channel only

Players can lobby during Phase 3 to downgrade a Restricted license to Standard for one round ($5M lobbying fee, 40% success chance).

---

## 11. Sanctions and Investigations

### 11.1 Sanctions Event (fires when Civilian Cost hits 75)
A randomly selected player — weighted toward highest recent civilian cost contributions — receives a formal sanctions designation:
- Their standard and restricted export licenses are suspended for 2 rounds
- Gray Channel sales are still available but at double the normal Latent Risk
- Other players may be offered a 1-round window to short their stock at a discount
- The sanctioned player can contest the designation by spending $15M on lobbying — 50% chance of suspension

### 11.2 Investigation Notice (fires when Sanctions Risk hits 80)
One random player — weighted toward highest Latent Risk — receives a formal investigation notice:
- Publicly visible to all players
- Latent Risk is partially converted to reputation damage (–10 immediate)
- If the investigation runs for 2 rounds without the player spending $20M to settle it: a full Exposure Event fires, revealing their covert sale history for the last 3 rounds
- Settling does not clear the reputation damage — it just stops the bleed

### 11.3 Humanitarian Fraud Event
Fires when an Aid Cover action is exposed via Level 3 whistle:
- The player's aid route on the globe visually changes from green to red
- A named event displays: "[Company] humanitarian shipment to [Country] confirmed to contain [Weapon category]"
- Civilian cost +5 (retroactive to that round)
- Reputation –15
- That player cannot use Aid Cover for 3 rounds

---

## 12. Stock Market and Reputation

### 12.1 Reputation Track (0–100 per player, private)
Each player's reputation is a private score that acts as a multiplier on final profit at game end.

**Final score = Total profit × (Reputation / 100)**

A player with $200M profit and 80 reputation scores 160. A player with $180M profit and 50 reputation scores 90. Reputation cannot be ignored in the long game.

Reputation changes:
- Open sale in Stage 2+: –1 (doing business)
- Covert sale discovered: –10 to –20
- Aid Cover fraud exposed: –15
- Peace Broker round: +3
- Reconstruction contract completion: +5
- Whistleblowing (Level 3): –5
- Successful ceasefire contribution: +8
- Investigation settled: –10 (cannot be avoided)
- Blowback event traced to player: –8 to –20 depending on weapon tier

### 12.2 Share Price (public)
Each company has a publicly visible share price, starting at $100.
Share price is driven by: profit growth rate, reputation score, and recent event history.
Share price is visible to all players on the leaderboard — it is a leading indicator of a company's trajectory.

### 12.3 Short Selling
During Phase 3, any player can short another player's stock:
- Cost: $5M stake
- Bet: target player's reputation will fall by 15+ points before end of next round
- Payout if correct: $15M (3×)
- Payout if wrong: stake lost
- A player can be shorted by multiple rivals simultaneously
- Short positions are visible to the target player — they know someone is betting against them

Short selling creates a financial incentive to actively damage rivals' reputations — through whistleblowing, through leaked procurement, through engineering blowback. It models activist short-selling and corporate warfare.

---

## 13. Ending Conditions

The game has no fixed round limit. It ends when one of the following conditions is met. Multiple conditions can be approached simultaneously — players must monitor all five tracks.

### 13.1 Negotiated Peace
**Trigger:** Global stability drops below 20 AND all active players simultaneously vote yes on a UN Ceasefire Resolution in the same round.
**Winner:** Player with highest (profit × reputation) composite score.
**Bonus:** Any player with 5+ Peace Credits receives a +$20M diplomatic bonus applied before final scoring.
**Frequency:** Rare. Requires genuine coordination across all players. Expected in fewer than 10% of games.
**Debrief:** Shows the full cooperation timeline — who played Peace Broker when, what it cost them, and how the world recovered.

### 13.2 Market Saturation
**Trigger:** 40% or more of all countries with initial Stage 2+ status have reached Stage 5 (Failed State).
**Winner:** Player with highest total profit.
**Reputation multiplier:** Still applied — a player who extracted maximum profit through covert sales and blowback accumulation may find their multiplier collapses the lead.
**Frequency:** Common in aggressive games (estimated 35% of games). The market destroyed itself.
**Debrief:** Shows the progression of Failed States across the globe — a map of grayed-out zones spreading round by round.

### 13.3 Global Sanctions Regime
**Trigger:** Civilian Cost hits 100.
**Effect:** International arms embargo declared. All standard and restricted export licenses are suspended indefinitely. Only Gray Channel sales are possible — at 3× normal Latent Risk.
**Endgame:** The game enters a 3-round Gray Market endgame. Players scramble to extract final income through covert channels. Blowback fires frequently. Investigations pile up.
**Winner:** After 3 rounds, highest (profit × reputation) composite score wins.
**Frequency:** Moderate (estimated 25% of games). Messy, paranoid endgame.

### 13.4 Total War
**Trigger:** Global Stability hits 100.
**Effect:** World-war-scale conflict erupts. All private arms markets are nationalized. Players are locked out of the market entirely.
**Winner:** Nobody. All players lose. This is a collective failure state.
**Debrief:** The game shows the exact sequence of sales and events that pushed stability to 100. The final 5 rounds are reconstructed round by round. The question is always: when was the last moment it could have been stopped?
**Frequency:** Less common but memorable (estimated 15% of games). The most politically resonant ending.
**Note:** The globe goes fully red. The news ticker fills with real-sounding atrocity coverage. No music.

### 13.5 Great Power Confrontation
**Trigger:** Geopolitical Tension hits 100.
**Effect:** A direct confrontation between two great-power blocs erupts, consuming all proxy markets simultaneously. Every active conflict zone jumps to Stage 4. Arms demand theoretically skyrockets — but great powers cut out the brokers and supply their own factions directly.
**Endgame:** 2-round endgame where players can attempt one final covert sale per round at extreme blowback risk, or pivot entirely to reconstruction investment for peace credits.
**Winner:** Highest composite score after 2 rounds.
**Frequency:** Rare (estimated 10% of games). Requires systematic arming of proxy conflicts across multiple regions.

---

## 14. Player Elimination — Company Collapse

A player's company collapses if their reputation hits 0. This is designed to be rare and difficult to engineer deliberately — it mirrors the reality that arms dealers almost never face existential consequences.

### 14.1 Collapse Conditions
Reputation can only reach 0 through sustained, compounding damage over many rounds — a single bad round is insufficient. The most common path is:
- Multiple covert sales exposed across multiple rounds
- Multiple investigation events without settlement
- Targeted short selling combined with coordinated whistleblowing from multiple rivals

### 14.2 Collapse Effects
When a player's company collapses:
- They are removed from the active player list
- Their inventory is auctioned to remaining players (sealed bid, one round)
- Their supplier relationships become available to other players at reduced cost
- Their pending reconstruction contracts are forfeited
- They enter **Observer Mode** — they can still see all information, use the chat, and short stocks, but cannot make sales or procurement decisions
- **They are not eliminated from scoring** — their final profit at the time of collapse is locked in and they remain in the leaderboard. They may still win if other players collapse the market or trigger Total War after them.

### 14.3 Design Note
Monopoly Collapse is not an ending — it is an event. The game continues. The collapsed player watches the world they helped create continue without them. In Observer Mode, they often have the clearest view of the game's dynamics — and the most motivation to whisper in other players' ears.

---

## 15. The Debrief

The most important screen in the game. Shown after every ending condition.

### 15.1 Content
- **Globe timeline:** the world map shown at rounds 1, 4, 8, 12, and final — staged visual of how the world changed
- **Sales attribution:** every sale ever made, by whom, to which country, in which round — a complete transaction history
- **Causal chain:** for every blowback event and human cost event, the chain of sales that led to it is shown
- **Human cost summary:** specific, named events — not aggregate numbers. "A guided munitions strike on a market in Khartoum, traced to weapons sold by [company] in round 7." Sourced from ACLED event types where possible.
- **What-if moment:** the game identifies the single round where the most consequential choice was made — the pivot point where Total War became likely, or where peace was closest. Shown as a highlighted round in the timeline.
- **Player profiles:** each player's profit, reputation, and composite score. Their most profitable sale. Their most consequential sale (the one with the highest downstream impact on civilian cost or stability). Sometimes these are the same. Often they're not.

### 15.2 Tone
The debrief presents information factually, without accusatory framing. No grade. No judgment text. No "you caused X deaths." Just: here is what you did, here is what happened, here is the sequence.

The political point lands because the player already knows what they chose. Seeing it laid out removes the ambient justifications that made each individual decision feel reasonable at the time.

---

## 16. Technical Implementation Notes

### 16.1 Recommended Engine
Unity 2023 LTS with Universal Render Pipeline (URP). Supports all target platforms: Windows/Mac/Linux (Steam), WebGL (browser), iOS and Android (future mobile). C# throughout — shared models between client and server via ArmsFair.Shared class library referenced as a compiled DLL in Unity Assets/Plugins/.

### 16.2 Map Rendering
- GeoJSON source: Natural Earth 1:50m cultural vectors (free, public domain)
- Preprocessing: Python script using Shapely — simplifies geometry, builds adjacency graph, outputs to StreamingAssets
- Flat map: GeoJSON coordinates tessellated into Unity Mesh objects at runtime using Mercator projection
- 3D globe: URP custom HLSL shader on a UV-sphere mesh — country overlay driven by pre-baked country ID texture and per-country tension float array updated each round via MaterialPropertyBlock
- Arc lines: LineRenderer components with custom URP shader for glow effect
- Click detection: Physics2D.OverlapPoint (flat map) and Physics.Raycast (globe)

### 16.3 Data Pipeline
- **Game launch:** ASP.NET Core server fetches from ACLED API and Global Peace Index on first room creation, cached in Redis for 24 hours
- **Round 1 only:** real data used to seed country tension scores and stages
- **Round 2+:** fully simulation-driven — no external data fetched, world evolves from player actions alone
- **News ticker:** round 1 uses real headlines from NewsAPI filtered by active conflict country names. Round 2+ uses procedural templates populated with game-state data
- **GPI data:** downloaded annually from visionofhumanity.org, hosted as static JSON on Cloudflare R2

### 16.4 Multiplayer
- Unity client connects to ASP.NET Core server via SignalR WebSocket (wss://)
- Same server handles WebGL browser clients and Steam desktop clients simultaneously
- Async mode supported: phase timers stored in PostgreSQL, survives server restarts

---

## 17. Open Design Questions (To Resolve in Playtesting)

1. **Optimal track movement rates.** The numbers in this document are starting estimates. Playtesting will determine whether Total War arrives too quickly (players have no time to react) or too slowly (doomsday feels toothless). Target: players should feel stability pressure by round 6–8.

2. **Peace Broker viability.** Is reconstruction contract income genuinely competitive with arms income in playtests? If no player ever chooses Peace Broker except as a last resort, the incentive structure needs rebalancing.

3. **Coup frequency.** Coups are high-variance. In playtests, do they destabilize the game balance? Consider whether the $20–40M cost is sufficient deterrence or needs to be higher.

4. **Observer Mode engagement.** Does a collapsed player remain engaged in Observer Mode, or do they disengage entirely? Consider whether collapsed players should have additional active tools (e.g., free Level 1 intelligence every round) to keep them invested.

5. **AI opponent behavior in mixed games.** What is the right AI aggression default? A default Opportunist AI seems most realistic — it should feel like a real rival, not a punching bag or an unbeatable engine.

6. **The debrief length.** How long should the debrief take? There is a risk that a 10-minute debrief feels like homework. Target: 3–5 minutes of curated information, with a deeper drill-down available for players who want it.

---

*End of original sections — see Sections 18–25 for new features added in v0.3*

---

## 18. Player Accounts and Username System

### 18.1 Account Creation
Players create a persistent account with a unique username before their first game. Accounts persist across sessions and track lifetime statistics.

**Account fields:**
- **Username** — unique, 3–20 characters, alphanumeric plus underscore and hyphen. Shown to all players in lobbies, game rooms, leaderboards, and the debrief. Cannot be changed more than once per 30 days.
- **Company name** — set per game at lobby creation. Can be different every game. The company name appears on the globe (arc lines, leaderboard) during gameplay. The username appears in menus and social contexts.
- **Avatar / flag** — a home nation flag selected at account creation. Represents the player's brokerage origin and determines starting supplier relationships.
- **Account created date** — shown on profile.
- **Steam ID link** — optional, links Steam account for Steam-specific features (achievements, friends).

**Authentication:**
- Browser players: email + password, with optional Google OAuth
- Steam players: Steam authentication (no password required — Steam handles identity)
- Both authentication paths create the same account type — a Steam player can also log in via email if they set a password

### 18.2 Unity Implementation

```csharp
// AccountManager.cs — handles auth and profile persistence
public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }
    public PlayerProfile CurrentProfile { get; private set; }

    // Called on app start — restore session from PlayerPrefs JWT
    public async Task<bool> TryRestoreSessionAsync()
    {
        var token = PlayerPrefs.GetString("auth_token", null);
        if (string.IsNullOrEmpty(token)) return false;

        try
        {
            var profile = await ApiClient.GetAsync<PlayerProfile>("/api/auth/me", token);
            CurrentProfile = profile;
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var response = await ApiClient.PostAsync<AuthResponse>("/api/auth/login",
            new { username, password });

        if (response?.Token == null) return false;

        PlayerPrefs.SetString("auth_token", response.Token);
        CurrentProfile = response.Profile;
        return true;
    }

    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        var response = await ApiClient.PostAsync<AuthResponse>("/api/auth/register",
            new { username, email, password });

        if (response?.Token == null) return false;

        PlayerPrefs.SetString("auth_token", response.Token);
        CurrentProfile = response.Profile;
        return true;
    }

    // Steam auth — exchanges Steam session ticket for JWT
    public async Task<bool> LoginSteamAsync()
    {
#if UNITY_STANDALONE && STEAM_BUILD
        var ticket = SteamUser.GetAuthSessionTicket();
        var response = await ApiClient.PostAsync<AuthResponse>("/api/auth/steam",
            new { ticket = ticket.ToString() });
        CurrentProfile = response.Profile;
        return true;
#else
        return false;
#endif
    }
}
```

---

## 19. Player Statistics

### 19.1 Lifetime Stats Tracked
Every completed game updates the player's lifetime statistics, stored in PostgreSQL against their account.

| Stat | Description |
|---|---|
| Games played | Total completed games |
| Games won | Games where player achieved highest composite score in a won ending |
| Wars started | Number of countries the player's sales directly caused to escalate to Stage 3+ for the first time |
| Failed states caused | Number of Stage 5 countries where the player was the primary seller |
| Total weapons sold | Lifetime count of sales by category (small arms, vehicles, air defense, drones) |
| Total profit earned | Lifetime $M across all games |
| Total civilian cost contributed | Lifetime civilian cost track contributions — the game's most uncomfortable stat |
| Ceasefires brokered | Number of ceasefire events the player contributed to with Peace Broker actions |
| Coups funded | Number of coup attempts, and how many succeeded |
| Times whistleblown | Number of times other players exposed the player |
| Times whistleblower | Number of times the player exposed others |
| Total War participations | Number of games ending in Total War — never a good look |
| World Peace achievements | Number of World Peace endings the player participated in — the rarest stat |
| Average reputation at game end | Lifetime average reputation score |
| Favorite weapon | Most frequently sold weapon category |
| Favorite region | Region where the player sells most frequently |
| Blowback events traced | Number of blowback events successfully traced back to the player |
| Company collapses | Number of times the player's company collapsed |
| Reconstruction contracts won | Lifetime reconstruction contract wins |

### 19.2 Per-Game Stats (shown in debrief)
After each game, before lifetime stats are updated, the debrief shows a per-game stat card:

- Profit earned this game
- Reputation final score
- Composite score (profit × reputation/100)
- Weapons sold by category (pie chart)
- Countries sold into (map highlight)
- Biggest single sale (country, weapon, profit)
- Most consequential sale (the one with highest downstream track impact)
- Peace Broker rounds taken
- Whistleblower actions used
- Net worth change (profit minus procurement costs)

### 19.3 Unity Implementation

```csharp
// PlayerStats.cs — in ArmsFair.Shared, used by both server and client
public record PlayerStats
{
    public int GamesPlayed          { get; init; }
    public int GamesWon             { get; init; }
    public int WarsStarted          { get; init; }
    public int FailedStatesCaused   { get; init; }
    public int SmallArmsSold        { get; init; }
    public int VehiclesSold         { get; init; }
    public int AirDefenseSold       { get; init; }
    public int DronesSold           { get; init; }
    public long TotalProfitEarned   { get; init; }   // $M lifetime
    public long TotalCivilianCost   { get; init; }   // lifetime civ cost contribution
    public int CeasefiresBrokered   { get; init; }
    public int CoupsFunded          { get; init; }
    public int CoupsSucceeded       { get; init; }
    public int TimesWhistleblown    { get; init; }
    public int TimesWhistleblower   { get; init; }
    public int TotalWarParticipations { get; init; }
    public int WorldPeaceAchieved   { get; init; }
    public int CompanyCollapses     { get; init; }
    public int ReconstructionWins   { get; init; }
    public float AvgFinalReputation { get; init; }
}

// Server updates stats at game end
public class StatsService
{
    public async Task UpdateLifetimeStatsAsync(string playerId, GameResult result)
    {
        var existing = await _repo.GetStatsAsync(playerId);

        var updated = existing with {
            GamesPlayed        = existing.GamesPlayed + 1,
            GamesWon           = existing.GamesWon + (result.IsWinner ? 1 : 0),
            WarsStarted        = existing.WarsStarted + result.WarsStartedThisGame,
            SmallArmsSold      = existing.SmallArmsSold + result.SmallArmsSoldThisGame,
            TotalProfitEarned  = existing.TotalProfitEarned + result.ProfitThisGame,
            TotalCivilianCost  = existing.TotalCivilianCost + result.CivilianCostThisGame,
            // ... all other fields
        };

        await _repo.SaveStatsAsync(playerId, updated);
    }
}
```

---

## 20. Winning the Game

### 20.1 Two Ways to Win

The game has two parallel win conditions that almost never align. Players choose — consciously or not — which kind of winner they want to be.

**The Profit Winner** — highest composite score (profit × reputation/100) when any non-Total-War ending is reached. This is the default "winner" displayed on the leaderboard. It is possible to be the profit winner in a game where the world is destroyed. The game does not hide this.

**The Legacy Winner** — the player with the highest Legacy Score at game end. Legacy Score is a separate calculation that rewards contribution to world stability, peace brokering, and reconstruction. A player who never sold a single weapon but invested in peace throughout can have the highest Legacy Score while having zero profit. Both scores are shown in the debrief side by side without editorial comment.

```
Legacy Score = (Peace Credits × 5) + (Ceasefires contributed to × 20)
             + (Reconstruction contracts completed × 15)
             + (Final world stability bonus: max 100 points if stability < 20 at game end)
             - (Failed States caused × 10)
             - (Total civilian cost contributed × 0.5)
```

The debrief shows both winners. Sometimes they are the same person. Usually they are not.

### 20.2 Winning Conditions by Ending

| Ending | Profit Winner | Legacy Winner | Notes |
|---|---|---|---|
| World Peace | Highest composite | Highest legacy | Both scores shown — peace players dominate legacy |
| Negotiated Peace | Highest composite | Highest legacy | Peace credits bonus applied |
| Market Saturation | Highest composite | Highest legacy | Often no one has a good legacy score |
| Global Sanctions | Highest composite (3-round endgame) | Highest legacy | Chaotic — reputation collapses fast |
| Great Power Confrontation | Highest composite (2-round endgame) | Highest legacy | |
| Total War | No winner | No winner | Both scores are shown but marked void — nobody wins |

---

## 21. World Peace Ending

### 21.1 Overview
World Peace is the rarest and most rewarding ending in the game. Unlike Negotiated Peace — which just requires stopping active fighting — World Peace requires genuinely rebuilding the world. It is the only ending where the Legacy Winner receives a larger score bonus than the Profit Winner.

Expected frequency: less than 5% of games. In practice it will be a celebrated achievement. Players who achieve it should feel they earned something rare.

### 21.2 Trigger Conditions
World Peace fires when ALL of the following are simultaneously true:

1. **Global Stability is below 15** (lower than Negotiated Peace threshold of 20)
2. **Civilian Cost is below 25** (world harm has been actively reduced, not just stopped)
3. **No country is above Stage 3** (no active Hot War or Humanitarian Crisis zones remain)
4. **At least 3 reconstruction contracts have been completed** (active rebuilding has occurred)
5. **All active players vote yes** on a World Peace Resolution in the same round

This requires sustained cooperative play across many rounds — it cannot be rushed. A group that has been aggressively selling arms cannot pivot to World Peace in two rounds. The civilian cost and country stage requirements mean genuine effort over time.

### 21.3 Effects and Scoring

**Immediate effects:**
- All remaining conflict zones begin a staged de-escalation over 3 rounds (visual — for the debrief)
- The globe turns from red/amber to teal/green over a 60-second cinematic
- A special World Peace theme plays — distinct from all other game music
- All players receive a base Legacy Score bonus of +200

**Final scoring:**
- Profit Winner: highest composite score — but the profit ceiling in a World Peace game is naturally lower (less selling = less profit), so the gap between players is compressed
- Legacy Winner: highest Legacy Score — this winner often matters more in community conversation than the profit winner
- **World Peace Achievement unlocked for all active players** — the rarest achievement in the game

### 21.4 The World Peace Paradox
The design tension is intentional: achieving World Peace means forgoing enormous amounts of profit. The player who tried hardest for peace probably has the lowest profit score. The player who sold aggressively for 8 rounds and then pivoted to peace in round 9 might win on profit. The game shows both outcomes without judging them.

The World Peace ending is also the only ending where the debrief includes a forward-looking section: "What the world looked like when you left it." A snapshot of the globe, the remaining country stages, the tracks — not a trophy, just a record.

### 21.5 Unity Implementation

```csharp
// EndingChecker.cs — World Peace check added
public EndingCondition? CheckWorldPeace(GameState state, HashSet<string> peacevotePlayerIds)
{
    var activePlayers = state.Players.Where(p => p.Status == "active").ToList();
    var allVotedYes = activePlayers.All(p => peacevotePlayerIds.Contains(p.Id));

    if (!allVotedYes) return null;

    var noHotWar = state.Countries.All(c => c.Stage <= 2);
    var stabilityOk = state.Tracks.Stability < 15;
    var civCostOk = state.Tracks.CivilianCost < 25;
    var reconstructionOk = state.CompletedReconstructionContracts >= 3;

    if (noHotWar && stabilityOk && civCostOk && reconstructionOk)
        return new EndingCondition { Type = "world_peace", Winner = null };

    // If conditions not met but all voted yes — tell players what's missing
    return new EndingCondition
    {
        Type = "peace_vote_failed",
        MissingConditions = BuildMissingList(state, noHotWar, stabilityOk, civCostOk, reconstructionOk)
    };
}

private List<string> BuildMissingList(GameState state, bool noHotWar,
    bool stabilityOk, bool civCostOk, bool reconstructionOk)
{
    var missing = new List<string>();
    if (!noHotWar)        missing.Add($"{state.Countries.Count(c => c.Stage > 2)} countries still in active conflict");
    if (!stabilityOk)     missing.Add($"Global stability too high ({state.Tracks.Stability}/15 needed)");
    if (!civCostOk)       missing.Add($"Civilian cost too high ({state.Tracks.CivilianCost}/25 needed)");
    if (!reconstructionOk) missing.Add($"Only {state.CompletedReconstructionContracts}/3 reconstruction contracts completed");
    return missing;
}
```

---

## 22. Text Chat

### 22.1 Chat Scope
Three chat channels operate simultaneously during the game. Players can switch between them at any time.

- **Global chat** — visible to all players in the room. Used for negotiation, accusations, deal proposals, and general table talk. Persists for the lifetime of the game room and is included in the debrief.
- **Private (DM) chat** — bilateral between any two players. Invisible to others. Used for secret deals, intelligence sharing, and coordination. Private chats are not included in the debrief — what was said privately stays private.
- **Observer chat** — visible only to collapsed players in Observer Mode. They can communicate with each other but not with active players unless specifically addressed in global chat.

### 22.2 Chat Rules and Moderation
- Messages are capped at 280 characters
- No profanity filter by default — this is a game about arms dealing, not a children's platform. Hosts can enable a filter in room settings.
- Players can mute specific other players — muted players' messages are hidden only for the muting player
- Chat history is saved server-side for the duration of the game and accessible in the debrief
- Rate limiting: maximum 5 messages per 10 seconds to prevent spam

### 22.3 Unity Implementation

```csharp
// ChatPanel.cs — UI controller for the chat system
public class ChatPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Transform messageContainer;
    [SerializeField] private GameObject messagePrefab;
    [SerializeField] private TMP_Dropdown channelDropdown;

    private string _selectedRecipientId = null; // null = global
    private HashSet<string> _mutedPlayerIds = new();

    public void SendMessage()
    {
        var text = messageInput.text.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 280) return;

        SignalRClient.Instance.SendChatAsync(new ChatMessage
        {
            Text = text,
            RecipientId = _selectedRecipientId  // null = global broadcast
        });

        messageInput.text = string.Empty;
    }

    public void ReceiveMessage(ChatMessage msg)
    {
        // Don't display messages from muted players
        if (_mutedPlayerIds.Contains(msg.SenderId)) return;

        var go = Instantiate(messagePrefab, messageContainer);
        var display = go.GetComponent<ChatMessageDisplay>();

        display.SetMessage(
            senderName: GameManager.Instance.GetPlayerName(msg.SenderId),
            text: msg.Text,
            isPrivate: msg.IsPrivate,
            isSystem: msg.IsSystem,
            senderColor: GameManager.Instance.GetPlayerColor(msg.SenderId)
        );

        // Auto-scroll to bottom
        Canvas.ForceUpdateCanvases();
        var scroll = messageContainer.GetComponentInParent<ScrollRect>();
        scroll.verticalNormalizedPosition = 0f;
    }

    public void MutePlayer(string playerId)
    {
        _mutedPlayerIds.Add(playerId);
    }

    public void SetPrivateChannel(string recipientId, string recipientName)
    {
        _selectedRecipientId = recipientId;
        channelDropdown.captionText.text = $"DM: {recipientName}";
    }

    public void SetGlobalChannel()
    {
        _selectedRecipientId = null;
        channelDropdown.captionText.text = "Global";
    }
}
```

---

## 23. Voice Chat

### 23.1 Overview
Voice chat is optional and off by default. Players enable it per session in the room settings. It uses Unity's Vivox service — Unity's first-party voice solution, free for games under a revenue threshold, fully integrated with Unity Gaming Services.

Voice chat is available during Phase 3 (Negotiation) only by default. It is automatically muted during Phase 4 (sealed action selection) to prevent real-time coordination during the sealed phase. This mirrors the game's design: talk all you want during negotiation, but when you place your order, you do it alone.

### 23.2 Voice Settings
- **Push-to-talk** (default) — player must hold a key/button to transmit. Recommended for this game as background noise during the debrief or reveal phase would be disruptive.
- **Voice activation** (optional) — toggleable in room settings. Host can override to force push-to-talk.
- **Individual volume controls** — each player can adjust the volume of any other player independently
- **Mute self** — available at all times via a dedicated UI button
- **Mute others** — individual mutes, same as text chat mute
- **Spatial audio** — disabled by default. The game is not spatial — all players are "at the same table."

### 23.3 Phase Behavior
| Phase | Voice Status | Reason |
|---|---|---|
| World Update | Muted | Cinematic — let the world update speak |
| Procurement | Optional (room setting) | Private phase — voice adds nothing |
| Negotiation | Active — full voice | The social heart of the game |
| Sales (sealed) | Forced mute | Sealed phase integrity |
| Reveal | Muted (15s) then active | Let the reveal animation land first |
| Consequences | Active | Reaction and discussion |

### 23.4 Unity Vivox Implementation

```csharp
// VoiceChatManager.cs
using Unity.Services.Vivox;
using UnityEngine;

public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    private IVivoxService _vivox;
    private bool _isConnected;
    public bool IsMuted { get; private set; }
    public bool IsVoiceEnabled { get; private set; }

    async void Start()
    {
        Instance = this;
        // Vivox is part of Unity Gaming Services — initialized alongside UGS
        await UnityServices.InitializeAsync();
        _vivox = VivoxService.Instance;
        await _vivox.InitializeAsync();
    }

    public async Task JoinRoomChannelAsync(string roomCode, bool pushToTalk)
    {
        IsVoiceEnabled = true;
        var channelName = $"armsfair_{roomCode}_negotiation";

        await _vivox.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);

        // Default to muted — player must push-to-talk or manually unmute
        if (pushToTalk) await _vivox.SetSelfMutedAsync(true);

        IsMuted = pushToTalk;
    }

    // Called by PhaseManager when phase changes
    public async Task SetPhaseVoiceStateAsync(GamePhase phase, bool voiceEnabled)
    {
        if (!IsVoiceEnabled) return;

        bool shouldMute = phase switch
        {
            GamePhase.WorldUpdate  => true,
            GamePhase.Procurement  => !voiceEnabled,  // room setting
            GamePhase.Negotiation  => false,           // always open
            GamePhase.Sales        => true,            // always muted — sealed phase
            GamePhase.Reveal       => true,            // muted for first 15s
            GamePhase.Consequences => false,
            _ => true
        };

        await _vivox.SetSelfMutedAsync(shouldMute);
        IsMuted = shouldMute;

        // Reveal phase: unmute after animation delay
        if (phase == GamePhase.Reveal)
        {
            await Task.Delay(15_000);
            await _vivox.SetSelfMutedAsync(false);
            IsMuted = false;
        }
    }

    public async Task PushToTalkAsync(bool pressed)
    {
        if (!IsVoiceEnabled) return;
        await _vivox.SetSelfMutedAsync(!pressed);
        IsMuted = !pressed;
    }

    public async Task LeaveChannelAsync()
    {
        await _vivox.LeaveAllChannelsAsync();
        IsVoiceEnabled = false;
    }
}
```

### 23.5 Voice Chat UI Elements
- **Mic indicator per player** — a small icon next to each player's name in the leaderboard panel pulses when they are transmitting
- **Push-to-talk button** — prominent, assignable to keyboard key (default: V) or controller button
- **Master voice volume slider** — in room settings
- **Per-player volume** — accessible by clicking a player's name in the leaderboard
- **Voice disabled indicator** — during sealed phases, a lock icon appears over the mic button

---

## 24. Lobby and Room System

### 24.1 Creating a Room
Any authenticated player can create a game room. On creation, the host sets:
- **Room name** (optional — defaults to host's username + "room")
- **Player slots:** 2–6
- **Timer preset:** Standard (6.5 min/round), Fast (4 min/round), Async (24hr/round)
- **Voice chat:** enabled/disabled, push-to-talk enforced or optional
- **Profanity filter:** on/off
- **AI fill-in:** enabled/disabled — if enabled, empty slots are filled with AI opponents
- **Private room:** if checked, generates an invite code rather than appearing in the public lobby

### 24.2 Joining a Room
- **Public lobby browser** — shows open rooms with player count, timer setting, and host username
- **Invite code** — 8-character code (e.g. ARMS-7X4Q) entered directly
- **Friends list** (Steam builds) — join a friend's room directly via Steam overlay

### 24.3 Pre-game Lobby Screen
While waiting for players:
- Shows connected players with their username, avatar flag, and "Ready" status
- Company name entry field — players set their company name for this game
- Home nation selector — determines starting suppliers
- Chat is active in the lobby — players can negotiate before the game even starts
- Host can kick players or start the game early once minimum player count is met

---

## 25. Updated Design Questions (v0.3)

1. **Dual winner communication.** How clearly should the game communicate that there are two win conditions? Players unfamiliar with the design may feel confused when the "profit winner" and "legacy winner" are different people. Consider a brief onboarding tooltip explaining the dual score system before the first game.

2. **World Peace difficulty calibration.** The conditions for World Peace (stability < 15, civilian cost < 25, no Stage 3+ countries, 3+ reconstruction contracts, unanimous vote) may be too strict for typical 4-player games where at least one player is always selling. Playtesting should determine whether the reconstruction contract requirement should drop to 2, or whether the stability/civilian cost thresholds need relaxing.

3. **Voice chat during async games.** Voice chat is not meaningful in async mode (24-hour phases). Should it be automatically disabled for async rooms, or should a Discord-style persistent voice channel be available for players who want to coordinate between phases?

4. **Username profanity and squatting.** Usernames need a server-side profanity check on registration. Username squatting (registering many usernames to block others) needs rate limiting — maximum 1 account per email address.

5. **Stats display in lobby.** Should lifetime stats be visible to other players in the lobby before a game starts? Knowing a player has caused 47 wars and never brokered a ceasefire is strategically relevant information — but it may also deter new players from joining experienced lobbies. Consider a toggle: stats visible / hidden in pre-game lobby.

6. **Legacy Score transparency.** The Legacy Score formula is complex. Should it be shown to players during the game (as a running tally) or only revealed in the debrief? Showing it during the game may encourage peace-broker behavior more effectively, but it also telegraphs strategy.

---

*End of specification v0.3*
*Engine: Unity 2023 LTS | New sections: 18 (Accounts), 19 (Stats), 20 (Winning), 21 (World Peace), 22 (Text Chat), 23 (Voice Chat), 24 (Lobby)*
