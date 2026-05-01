# Arms Fair — Solo Playability Design Spec
**Date:** 2026-04-30
**Scope:** Server + Unity client — everything required for one player to test all game mechanics solo
**Goal:** A single authenticated player can create a game, play through unlimited rounds (all 6 phases), exercise every mechanic in the spec (procurement, sales, coups, negotiations, endings, debrief), and verify correct behavior via a dev panel — without needing any other human players or AI opponents.

---

## 1. Critical Bugs — Game Won't Run At All

### 1a. StartGame never called from client
**File:** `ArmsFair/Assets/Scripts/UI/PreGame/PreGameLobbyScreen.cs`

`OnStart()` calls `CreateGameAsync(settings)` and has a comment "StartGame fires after that" — but there is no code that does it. `TickerService.StartGame(gameId)` is only invoked from `GameHub.StartGame`, which the client never calls. The game sits on WorldUpdate phase forever.

**Fix:** In `PreGameLobbyScreen.OnStateSync`, after the first `StateSync` arrives (which sets `GameClient.GameId`), call `await GameClient.Instance.StartGameAsync()` before navigating to the HUD. Guard with a `_gameStarted` flag so it only fires once.

---

### 1b. TickerService bypasses PhaseOrchestrator — state never mutates
**Files:** `ArmsFair.Server/Services/TickerService.cs`, `ArmsFair.Server/Hubs/GameHub.cs`

When a phase timer expires, `TickerService` sends a raw `PhaseStart` message directly to the SignalR group. It does NOT call `GameHub.AdvancePhase` or `PhaseOrchestrator.AdvanceAsync`. Result:
- WorldUpdate simulation (conflict spread, blowback, failed state checks) never runs
- Reveal logic (resolving submitted actions) never runs
- Consequences (profit/rep distribution) never runs
- Server `GameState` is never mutated after creation

**Root cause:** `TickerService` cannot call `GameHub.AdvancePhase` — hub methods are only invokable from connected clients, not from `IHubContext`. The `_games` dictionary lives as a static field on `GameHub`, inaccessible to the ticker.

**Fix:** See Section 1c (GameStateService). Once `_games` is extracted, `TickerService` calls `PhaseOrchestrator.AdvanceAsync` directly on phase expiry, then updates `GameStateService` with the returned state. `PhaseOrchestrator` already has `IHubContext` and broadcasts all messages.

---

### 1c. GameStateService — extract shared state from GameHub
**New file:** `ArmsFair.Server/Services/GameStateService.cs`

Extract the three static dictionaries from `GameHub` into an injectable singleton:
```csharp
public class GameStateService
{
    private readonly ConcurrentDictionary<string, GameState>       _games           = new();
    private readonly ConcurrentDictionary<string, PlayerAction>    _pending         = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _ceaseFireVoters = new();

    // Get/set game state
    public bool TryGet(string gameId, out GameState state) => _games.TryGetValue(gameId, out state);
    public void Set(string gameId, GameState state)         => _games[gameId] = state;
    public void Remove(string gameId)                       => _games.TryRemove(gameId, out _);

    // Pending actions
    public void SetPendingAction(string connectionId, PlayerAction action) => _pending[connectionId] = action;
    public List<PlayerAction> GetAndClearPending(IEnumerable<string> playerIds)
        => _pending.Values.Where(a => playerIds.Contains(a.PlayerId)).ToList();
    public void ClearPending() => _pending.Clear();

    // Cease-fire voters
    public HashSet<string> GetOrAddVoters(string gameId) => _ceaseFireVoters.GetOrAdd(gameId, _ => new());
    public void RemoveVoters(string gameId)               => _ceaseFireVoters.TryRemove(gameId, out _);
}
```

Register as singleton in `Program.cs`. `GameHub` and `TickerService` both inject it. `GameHub` drops all three static dictionaries.

---

### 1d. PhaseStart deserialization mismatch (auto-fixed by 1b)
`TickerService` sends `new { Phase = next.ToString(), ... }` — an anonymous object with `Phase` as a string. The client deserializes as `PhaseStartMessage` which has `Phase` as a `GamePhase` enum. This is fragile and likely failing silently. Once 1b is fixed, `TickerService` no longer sends `PhaseStart` directly — `PhaseOrchestrator` sends the correctly typed record — so this bug disappears automatically.

---

## 2. Core Loop — Phases Advance But Mechanics Don't Work

### 2a. Procurement confirmation not sent to server
**Files:** `ArmsFair/Assets/Scripts/UI/Game/ProcurementScreen.cs`, `ArmsFair.Server/Hubs/GameHub.cs`

`ProcurementScreen` stores purchases in the client-side `ProcurementManager` singleton. It never sends any message to the server. Capital is never deducted from `GameState.Players`. When Consequences runs, profit is added to stale capital.

**Fix:**
- Add `GameHub.ConfirmProcurement(string gameId, List<ProcurementConfirmItem> items)` — deducts total cost from player's `Capital` in `GameStateService`, sends back `StateSync`.
- `ProcurementScreen` confirm button calls `GameClient.ConfirmProcurementAsync(items)` instead of just closing the panel.
- Add `ProcurementConfirmItem` to client messages: `{ WeaponCategory Category, string SupplierId, int Quantity, float CostPerUnit }`.

---

### 2b. Sales weapon/type selection is incomplete
**File:** `ArmsFair/Assets/Scripts/UI/Game/HUD.cs`

`HUD.OnSalesConfirm` always submits `inv[0]` with `SaleType.Open` hardcoded. No UI to select which weapon or sale type. The Open/Covert/Aid Cover buttons show when no target is set but only toast "click a country" — they don't set any state.

**Fix:**
- HUD maintains `_selectedSaleType` (default `Open`) and `_selectedWeaponCategory` / `_selectedSupplierId`.
- Sale type buttons (Open/Covert/Aid Cover/Peace Broker) set `_selectedSaleType` and update their visual state (active class).
- CountryInfoCard weapon rows show inventory items; tapping one sets `_selectedWeaponCategory` + `_selectedSupplierId` on HUD and calls `SetSalesTarget`.
- Confirm is enabled only when `_salesTargetIso != null && _selectedWeaponCategory != null`.
- `OnSalesConfirm` uses `_selectedSaleType` and `_selectedWeaponCategory` instead of hardcoded values.

---

### 2c. Globe clicks don't open CountryInfoCard
**Files:** `ArmsFair/Assets/Scripts/UI/Game/CountryInfoCard.cs`, `ArmsFair/Assets/Scripts/Map/WPMGlobeBridge.cs`

`CountrySelector` handles flat-map clicks. `CountryInfoCard` subscribes only to `CountrySelector.OnCountrySelected`. The globe uses `WPMGlobeBridge` which fires a separate event — `CountryInfoCard` doesn't subscribe to it.

**Fix:**
- `WPMGlobeBridge` exposes `public event Action<string, Vector2> OnCountrySelected` (iso + screen position).
- `CountryInfoCard.Start()` subscribes to both `CountrySelector.OnCountrySelected` and `WPMGlobeBridge.OnCountrySelected`.
- `PositionCard()` accepts a `Vector2 screenPos` argument; when called from globe click, uses the actual click position (offset 12px right/up, clamped to screen bounds). When called from flat map, retains current quadrant behaviour.

---

### 2d. Reveal and Consequences overlays show no data
**Files:** `ArmsFair/Assets/Scripts/UI/Game/RevealOverlay.cs`, `ArmsFair/Assets/Scripts/UI/Game/ConsequencesOverlay.cs`

Both overlays get pushed by HUD on phase change but don't subscribe to `GameClient.OnReveal` / `GameClient.OnConsequences`. Players see a blank overlay.

**Fix:**
- `RevealOverlay.OnEnable` subscribes to `GameClient.Instance.OnReveal`. On receipt, renders a list: `[PlayerName] → [Country] ([SaleType], [Weapon])` for each `RevealedAction`. Arc animations are out of scope for this spec.
- `ConsequencesOverlay.OnEnable` subscribes to `GameClient.Instance.OnConsequences`. On receipt, renders: profit earned, capital new total, reputation change, blowback events (if any), track delta summary.
- Both overlays auto-close after their phase timer expires (they already get a `PhaseChanged` event from `PhaseManager`).

---

## 3. Testable Mechanics

### 3a. Track threshold events never emitted
**File:** `ArmsFair.Server/Services/PhaseOrchestrator.cs`

`RunWorldUpdate` updates tracks but never checks thresholds. No events appear in `WorldUpdateMessage.Events` for UN discussion (CivCost 60), sanctions (CivCost 75), investigation (SanctionsRisk 80), spread multiplier (Stability 80), etc.

**Fix:** After track update in `RunWorldUpdate`, compare old vs new track values against threshold table. For each threshold crossed, add a `GameEvent(EventType: "threshold", Description: "...", PlayerId: null, CountryIso: null)` to the events list. Descriptions match the spec's human-readable strings (e.g. "UN Security Council opens debate on arms flows").

Thresholds to implement (per `Balance.cs` / spec):
- MarketHeat 80: "Market overheating — profits spike and civilian harm accelerates"
- MarketHeat 100: "Market crash — profits frozen for one round" + reset heat to 40
- CivilianCost 60: "UN Security Council opens debate on arms flows"
- CivilianCost 75: "International sanctions designated against [player]"
- CivilianCost 100: ending → GlobalSanctions
- Stability 80: "World on edge — conflicts spread twice as fast"
- Stability 100: ending → TotalWar
- SanctionsRisk 60: "Export license costs doubled"
- SanctionsRisk 80: "Formal investigation notice issued to [player]"
- GeoTension 70: "US/Russian suppliers restrict sales to opposite-bloc buyers"
- GeoTension 90: "Great-power confrontation — new crisis zone created"
- GeoTension 100: ending → GreatPowerConfrontation

---

### 3b. Ending conditions never checked
**File:** `ArmsFair.Server/Services/PhaseOrchestrator.cs`, `ArmsFair.Server/Simulation/EndingChecker.cs`

`EndingChecker.Check` is only called inside `VoteCeaseFire`. It is never called after track updates. Total War, Global Sanctions, Market Saturation, and Great Power Confrontation can never fire — the game loops forever.

**Fix:** At the end of `PhaseOrchestrator.AdvanceAsync` (after all simulation and broadcast), call `EndingChecker.Check(state, ceaseFireVoters)`. If an ending is returned:
- Broadcast `GameEnding` message to the group
- Call `TickerService.StopGame(gameId)`
- Remove game from `GameStateService`

---

### 3c. Debrief never shown
**File:** `ArmsFair/Assets/Scripts/Game/GameManager.cs`, `ArmsFair/Assets/Scripts/UI/Game/DebriefScreen.cs`

`GameManager.HandleGameEnding` fires `OnGameEnded` event but nothing navigates to `DebriefScreen`.

**Fix:** `GameManager.HandleGameEnding` calls `UIManager.Instance?.Show<DebriefScreen>()` passing the `GameEndingMessage`. `DebriefScreen.Show()` renders: ending type, trigger description, final scores table (profit, reputation, composite), and a "Play Again" button that returns to the lobby.

---

### 3d. Negotiation actions wired (Intel only — others are stretch)
**File:** `ArmsFair/Assets/Scripts/UI/Game/NegotiationPanel.cs`, `ArmsFair/Assets/Scripts/UI/Game/HUD.cs`

`NegotiationPanel` already has whistle and treaty UI. Intel button in HUD bottom bar already pushes `NegotiationPanel`. The panel's whistle flow calls `GameClient.WhistleAsync` — the hub method exists.

**For this spec:** Wire Intel (already partially done) and Peace Proposal (calls `GameClient.InvestInPeacekeepingAsync` with a country selector). Leak, Short, and Lobby remain "not implemented" toasts — they are not blocking for solo mechanics testing.

---

## 4. Solo Test Quality of Life

### 4a. Dev mode toggle + skip phase button
**Trigger:** `Ctrl+D` in editor/dev builds toggles dev mode. Dev mode state stored in `PlayerPrefs["dev_mode"]`.

**Skip Phase button:** Visible in bottom bar during all phases when dev mode is on. Calls new `GameHub.DevForceAdvance(gameId)` — identical to `AdvancePhase` but skips the phase-gate checks. This lets you burn through a full round in under 10 seconds.

**Dev mode only, non-shipping:** Gate behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD` on the client. The server method is gated by a `isDevelopment` config flag checked from `IWebHostEnvironment`.

---

### 4b. Dev panel — live game state inspector
**Trigger:** `~` key when dev mode is on. Overlay panel (UI Toolkit, sort order 10) showing:

```
── GAME STATE ────────────────────
GameId: abc-123    Phase: Sales    Round: 3
── TRACKS ────────────────────────
MarketHeat: 42   CivilianCost: 31   Stability: 28
SanctionsRisk: 14   GeoTension: 19
── YOUR PROFILE ──────────────────
Capital: $47M   Rep: 72   LatentRisk: 3   PeaceCredits: 0
── LAST EVENTS (5) ───────────────
[WorldUpdate] Conflict spread: IRQ → SYR
[Threshold] UN Security Council opens debate
[Blowback] Drones traced to you in ETH (-20 rep)
── DEV CONTROLS ──────────────────
[Set Track…]  [Force Stage…]  [Force Blowback]  [Add Capital]
```

The "Set Track", "Force Stage", "Force Blowback", "Add Capital" buttons call new `GameHub.DevSetTrack`, `GameHub.DevSetCountryStage`, `GameHub.DevTriggerBlowback`, `GameHub.DevAddCapital` — each takes a gameId and the mutation to apply, broadcasts a `StateSync` after.

---

## 5. Implementation Priority Order

| # | Item | Why this order |
|---|---|---|
| 1 | `GameStateService` extraction | Unblocks everything; TickerService and GameHub need shared state |
| 2 | TickerService → PhaseOrchestrator wiring | Phases advance with real state mutation |
| 3 | Client calls `StartGame` after `CreateGame` | Game actually starts |
| 4 | Ending conditions checked each phase | Game can end; prevents infinite loop |
| 5 | Debrief shown on `GameEnding` | Game loop closes cleanly |
| 6 | Globe → CountryInfoCard click | Can target countries |
| 7 | Sales type + weapon selection in HUD | Full sale flow works |
| 8 | Procurement confirms to server | Capital tracks correctly |
| 9 | Reveal overlay shows action list | See what happened |
| 10 | Consequences overlay shows deltas | See profit/rep/blowback |
| 11 | Track threshold events emitted | Test sanctions, investigations |
| 12 | Dev mode + skip phase button | Efficient solo testing |
| 13 | Dev state inspector panel | Verify mechanics numerically |
| 14 | Negotiation Intel + Peace Proposal wired | Test whistle and peace mechanics |

---

## 6. Out of Scope (This Spec)

- AI opponents
- Real multiplayer (second human player joining)
- Arc line animations in Reveal phase
- Coup / Manufacture Demand UI (hub methods exist; UI entry points deferred)
- Short selling / Procurement Leak / Lobby negotiation actions
- Voice chat
- Observer mode
- Mobile layout

---

*End of spec*
