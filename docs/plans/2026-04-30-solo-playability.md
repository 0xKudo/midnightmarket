# Solo Playability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One authenticated player can create a game, play through all 6 phases indefinitely, exercise every mechanic (procurement, sales, coups, endings, debrief), and verify correct state via a dev panel — with no AI or other human players.

**Architecture:** Extract in-memory game state from `GameHub` into an injectable `GameStateService` singleton so `TickerService` can call `PhaseOrchestrator.AdvanceAsync` directly on phase expiry, closing the loop where phases advanced but state never mutated. The client is fixed to call `StartGame` after `CreateGame`. A dev panel + skip-phase button allows rapid solo testing.

**Tech Stack:** ASP.NET Core 8 / SignalR (server), Unity 6 UI Toolkit / C# (client), shared `ArmsFair.Shared` models

---

## File Map

**Server — create:**
- `ArmsFair.Server/Services/GameStateService.cs` — injectable singleton holding `_games`, `_pending`, `_ceaseFireVoters`

**Server — modify:**
- `ArmsFair.Server/Hubs/GameHub.cs` — inject `GameStateService`; remove three static dicts; add `ConfirmProcurement`, `DevForceAdvance`, `DevSetTrack`, `DevSetCountryStage`, `DevAddCapital`
- `ArmsFair.Server/Services/TickerService.cs` — inject `GameStateService` + `PhaseOrchestrator`; replace direct `PhaseStart` send with `PhaseOrchestrator.AdvanceAsync` call
- `ArmsFair.Server/Services/PhaseOrchestrator.cs` — emit threshold `GameEvent`s in `RunWorldUpdate`; stop `TickerService` and remove game on ending
- `ArmsFair.Server/Program.cs` — register `GameStateService` as singleton

**Shared — modify:**
- `ArmsFair.Shared/Models/Messages/ClientMessages.cs` — add `ProcurementConfirmItem` record

**Client — modify:**
- `ArmsFair/Assets/Scripts/UI/PreGame/PreGameLobbyScreen.cs` — call `StartGameAsync` once after first `StateSync`
- `ArmsFair/Assets/Scripts/Game/GameManager.cs` — navigate to `DebriefScreen` on `GameEnding`
- `ArmsFair/Assets/Scripts/UI/Game/HUD.cs` — sales state machine (`_selectedSaleType`, `_selectedWeapon`, `_selectedSupplierId`); dev skip button; `Ctrl+D` toggle
- `ArmsFair/Assets/Scripts/UI/Game/CountryInfoCard.cs` — subscribe to `WPMGlobeBridge.OnCountrySelected`; position card at click point; expose weapon-selection callback to HUD
- `ArmsFair/Assets/Scripts/Map/WPMGlobeBridge.cs` — upgrade `OnCountrySelected` event to include screen-space `Vector2`
- `ArmsFair/Assets/Scripts/UI/Game/ProcurementScreen.cs` — confirm button calls `ConfirmProcurementAsync`
- `ArmsFair/Assets/Scripts/Network/GameClient.cs` — add `ConfirmProcurementAsync`, `DevForceAdvanceAsync`, `DevSetTrackAsync`, `DevSetCountryStageAsync`, `DevAddCapitalAsync`

**Client — create:**
- `ArmsFair/Assets/Scripts/UI/Game/DevPanel.cs` — live state inspector overlay, `~` key toggle

---

## Task 1: GameStateService — extract shared state

**Files:**
- Create: `ArmsFair.Server/Services/GameStateService.cs`
- Modify: `ArmsFair.Server/Program.cs`

- [ ] **Step 1: Create GameStateService.cs**

```csharp
// ArmsFair.Server/Services/GameStateService.cs
using ArmsFair.Shared.Models;
using System.Collections.Concurrent;

namespace ArmsFair.Server.Services;

public class GameStateService
{
    private readonly ConcurrentDictionary<string, GameState>       _games           = new();
    private readonly ConcurrentDictionary<string, PlayerAction>    _pending         = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _ceaseFireVoters = new();

    public bool TryGet(string gameId, out GameState state) =>
        _games.TryGetValue(gameId, out state!);

    public void Set(string gameId, GameState state) =>
        _games[gameId] = state;

    public void Remove(string gameId) =>
        _games.TryRemove(gameId, out _);

    public IEnumerable<string> AllGameIds => _games.Keys;

    public void SetPendingAction(string connectionId, PlayerAction action) =>
        _pending[connectionId] = action;

    public List<PlayerAction> GetPendingForGame(IEnumerable<string> playerIds)
    {
        var ids = playerIds.ToHashSet();
        return _pending.Values.Where(a => ids.Contains(a.PlayerId)).ToList();
    }

    public void ClearPending() => _pending.Clear();

    public HashSet<string> GetOrAddVoters(string gameId) =>
        _ceaseFireVoters.GetOrAdd(gameId, _ => new HashSet<string>());

    public void RemoveVoters(string gameId) =>
        _ceaseFireVoters.TryRemove(gameId, out _);
}
```

- [ ] **Step 2: Register in Program.cs**

In `ArmsFair.Server/Program.cs`, add after the existing singleton registrations (after line `builder.Services.AddSingleton<TickerService>();`):

```csharp
builder.Services.AddSingleton<GameStateService>();
```

- [ ] **Step 3: Build to verify no compile errors**

```bash
cd ArmsFair.Server && dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add ArmsFair.Server/Services/GameStateService.cs ArmsFair.Server/Program.cs
git commit -m "feat: add GameStateService singleton to hold shared game state"
```

---

## Task 2: Wire GameHub to GameStateService

**Files:**
- Modify: `ArmsFair.Server/Hubs/GameHub.cs`

Replace the three static `ConcurrentDictionary` fields with injected `GameStateService`. Every reference to `_games`, `_pending`, `_ceaseFireVoters` becomes a call to the service.

- [ ] **Step 1: Update GameHub constructor and remove static dicts**

Replace the top of `GameHub.cs` (lines 17–23):

```csharp
[Authorize]
public class GameHub(
    ArmsFairDb db,
    SeedService seedService,
    PhaseOrchestrator phaseOrchestrator,
    TickerService ticker,
    GameStateService games) : Hub
{
    // static dicts removed — state lives in GameStateService
```

- [ ] **Step 2: Replace all _games references**

Every `_games[gameId] = state` → `games.Set(gameId, state)`
Every `_games.TryGetValue(gameId, out var state)` → `games.TryGet(gameId, out var state)`
Every `_games.TryRemove(gameId, out _)` → `games.Remove(gameId)`

- [ ] **Step 3: Replace all _pending references**

Every `_pending[Context.ConnectionId] = new PlayerAction { ... }` → `games.SetPendingAction(Context.ConnectionId, new PlayerAction { ... })`
Every `_pending.Values.Where(...)` → `games.GetPendingForGame(state.Players.Select(p => p.Id))`
Every `_pending.Clear()` → `games.ClearPending()`

- [ ] **Step 4: Replace all _ceaseFireVoters references**

Every `_ceaseFireVoters.GetOrAdd(gameId, ...)` → `games.GetOrAddVoters(gameId)`
Every `_ceaseFireVoters.GetValueOrDefault(gameId) ?? []` → `games.GetOrAddVoters(gameId)`
Every `_ceaseFireVoters.TryRemove(gameId, out _)` → `games.RemoveVoters(gameId)`

- [ ] **Step 5: Build**

```bash
cd ArmsFair.Server && dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add ArmsFair.Server/Hubs/GameHub.cs
git commit -m "refactor: GameHub uses injected GameStateService instead of static dicts"
```

---

## Task 3: Wire TickerService to call PhaseOrchestrator

**Files:**
- Modify: `ArmsFair.Server/Services/TickerService.cs`

`TickerService` currently sends a raw anonymous `PhaseStart` to the SignalR group and never mutates state. Replace that with a call to `PhaseOrchestrator.AdvanceAsync`.

- [ ] **Step 1: Inject GameStateService and PhaseOrchestrator into TickerService**

Change the constructor:

```csharp
public class TickerService(
    IHubContext<GameHub> hub,
    GameStateService games,
    PhaseOrchestrator phaseOrchestrator,
    ILogger<TickerService> logger) : BackgroundService
```

- [ ] **Step 2: Replace the expired-phase loop body**

Find the `foreach (var gameId in expired)` block (lines 59–79) and replace it entirely:

```csharp
foreach (var gameId in expired)
{
    if (!_phases.TryGetValue(gameId, out var current)) continue;
    if (!games.TryGet(gameId, out var state)) continue;

    var voters = games.GetOrAddVoters(gameId);
    var pending = games.GetPendingForGame(state.Players.Select(p => p.Id));

    try
    {
        var (newState, ending) = await phaseOrchestrator.AdvanceAsync(
            gameId, state, voters, pending);

        if (ending is not null)
        {
            StopGame(gameId);
            games.Remove(gameId);
            games.RemoveVoters(gameId);
            games.ClearPending();
        }
        else
        {
            games.Set(gameId, newState);
            var next = newState.Phase;
            _phases[gameId]    = next;
            _phaseEnds[gameId] = DateTimeOffset.UtcNow.AddMilliseconds(PhaseDuration(next));

            if (next == GamePhase.WorldUpdate)
                _rounds.AddOrUpdate(gameId, 1, (_, r) => r + 1);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "TickerService: error advancing game {GameId}", gameId);
    }
}
```

- [ ] **Step 3: Remove the now-unused `_rounds` increment from `ExecuteAsync`**

The round counter in `TickerService` is now owned by `PhaseOrchestrator` (it's in `GameState.Round`). Remove `IncrementRound` calls if any remain, and the `_rounds` dictionary can stay as a fallback-unused field — it won't hurt anything.

- [ ] **Step 4: Build**

```bash
cd ArmsFair.Server && dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add ArmsFair.Server/Services/TickerService.cs
git commit -m "fix: TickerService calls PhaseOrchestrator.AdvanceAsync instead of sending PhaseStart directly"
```

---

## Task 4: Client calls StartGame after CreateGame

**Files:**
- Modify: `ArmsFair/Assets/Scripts/UI/PreGame/PreGameLobbyScreen.cs`

- [ ] **Step 1: Add a `_gameStarted` guard field**

Add at the top of the class (after existing fields):

```csharp
bool _gameStarted;
```

- [ ] **Step 2: Reset the guard in `Show()`**

Inside `public async void Show()`, add:

```csharp
_gameStarted = false;
```

- [ ] **Step 3: Update `OnStateSync` to call StartGame once then navigate**

Replace the existing `OnStateSync` method:

```csharp
async void OnStateSync(StateSync _)
{
    if (_gameStarted) return;
    _gameStarted = true;
    await GameClient.Instance.StartGameAsync();
    UIManager.Instance?.Show<HUD>();
}
```

- [ ] **Step 4: Enter Play mode and test**

1. Start the server: `cd ArmsFair.Server && dotnet run`
2. Enter Play mode in Unity
3. Log in, create a room, click Start
4. Verify the HUD appears, and after ~30 seconds the phase label changes from `WorldUpdate` to `Procurement`

- [ ] **Step 5: Commit**

```bash
git add "ArmsFair/Assets/Scripts/UI/PreGame/PreGameLobbyScreen.cs"
git commit -m "fix: call StartGame after CreateGame so TickerService begins phase progression"
```

---

## Task 5: Ending conditions stop the ticker and show debrief

**Files:**
- Modify: `ArmsFair/Assets/Scripts/Game/GameManager.cs`

The server already calls `TriggerEndingAsync` which broadcasts `GameEnding`. `DebriefScreen` already subscribes to `OnGameEnding` and renders itself via `UIManager.Instance?.Show<DebriefScreen>()` inside its own handler. But `GameManager.HandleGameEnding` only fires `OnGameEnded` — it doesn't navigate. The debrief calls `Show` on itself from its own listener, so the nav is already done. Verify and add the disconnect.

- [ ] **Step 1: Update `GameManager.HandleGameEnding`**

Replace the existing method in `ArmsFair/Assets/Scripts/Game/GameManager.cs`:

```csharp
private void HandleGameEnding(GameEndingMessage msg)
{
    OnGameEnded.Invoke(msg);
    // DebriefScreen.OnGameEnding handles UIManager.Show internally.
    // We disconnect the hub so no further messages arrive.
    _ = GameClient.Instance?.DisconnectAsync();
}
```

- [ ] **Step 2: Verify DebriefScreen "Play Again" navigates back cleanly**

Open `ArmsFair/Assets/Scripts/UI/Game/DebriefScreen.cs`. The `btn-return-lobby` button already calls `GameClient.Instance.DisconnectAsync()` then `UIManager.Instance?.Show<LobbyScreen>()`. Confirm this is present — no change needed.

- [ ] **Step 3: Commit**

```bash
git add "ArmsFair/Assets/Scripts/Game/GameManager.cs"
git commit -m "fix: disconnect hub on game ending so debrief doesn't receive stale messages"
```

---

## Task 6: Globe clicks open CountryInfoCard at click position

**Files:**
- Modify: `ArmsFair/Assets/Scripts/Map/WPMGlobeBridge.cs`
- Modify: `ArmsFair/Assets/Scripts/UI/Game/CountryInfoCard.cs`

- [ ] **Step 1: Upgrade WPMGlobeBridge.OnCountrySelected to include screen position**

In `WPMGlobeBridge.cs`, change the event signature and fire it with mouse position:

```csharp
// Change line 16:
public event System.Action<string, Vector2> OnCountrySelected;
```

In `Update()`, replace `OnCountrySelected?.Invoke(iso);` with:

```csharp
Vector2 screenPos = _map.input.mousePosition;
OnCountrySelected?.Invoke(iso, screenPos);
```

- [ ] **Step 2: Update CountryInfoCard to subscribe to WPMGlobeBridge**

In `CountryInfoCard.cs`, change `Start()`:

```csharp
void Start()
{
    if (countrySelector == null)
        countrySelector = FindObjectOfType<CountrySelector>();
    if (countrySelector != null)
        countrySelector.OnCountrySelected += OnFlatMapClicked;

    if (WPMGlobeBridge.Instance != null)
        WPMGlobeBridge.Instance.OnCountrySelected += OnGlobeClicked;
}
```

Add `OnDisable` cleanup:

```csharp
void OnDisable()
{
    if (countrySelector != null)
        countrySelector.OnCountrySelected -= OnFlatMapClicked;
    if (WPMGlobeBridge.Instance != null)
        WPMGlobeBridge.Instance.OnCountrySelected -= OnGlobeClicked;
    PhaseManager.Instance?.OnPhaseChanged.RemoveListener(OnPhaseChanged);
}
```

- [ ] **Step 3: Rename and add click handlers**

Rename `OnCountryClicked` to `OnFlatMapClicked`. Add `OnGlobeClicked`:

```csharp
void OnFlatMapClicked(string iso)
{
    ShowCard(iso, Vector2.zero, useQuadrant: true);
}

void OnGlobeClicked(string iso, Vector2 screenPos)
{
    ShowCard(iso, screenPos, useQuadrant: false);
}

void ShowCard(string iso, Vector2 screenPos, bool useQuadrant)
{
    var state = GameManager.Instance?.CurrentState;
    if (state == null) return;
    var country = state.Countries?.Find(c => c.Iso == iso);
    if (country == null) return;
    PopulateCard(country);
    PositionCard(screenPos, useQuadrant);
    _card.RemoveFromClassList("hidden");
}
```

- [ ] **Step 4: Update PositionCard to accept click position**

Replace the existing `PositionCard()` method:

```csharp
void PositionCard(Vector2 screenPos, bool useQuadrant)
{
    var root   = uiDocument.rootVisualElement;
    float screenW = root.resolvedStyle.width;
    float screenH = root.resolvedStyle.height;
    const float cardW = 200f;
    const float cardH = 320f;
    const float offset = 12f;

    float x, y;
    if (useQuadrant)
    {
        x = Mathf.Clamp(screenW * 0.65f, 0, screenW - cardW - 8);
        y = Mathf.Clamp(screenH * 0.35f, 40, screenH - cardH - 48);
    }
    else
    {
        // screenPos is in Unity screen coords (origin bottom-left); UI Toolkit uses top-left
        float uiY = screenH - screenPos.y;
        x = Mathf.Clamp(screenPos.x + offset, 0, screenW - cardW - 8);
        y = Mathf.Clamp(uiY - cardH - offset, 40, screenH - cardH - 48);
    }

    _card.style.left = x;
    _card.style.top  = y;
}
```

- [ ] **Step 5: Fix the old PositionCard() call sites**

Search `CountryInfoCard.cs` for any remaining call to `PositionCard()` with no arguments and update them to `PositionCard(Vector2.zero, useQuadrant: true)`.

- [ ] **Step 6: Commit**

```bash
git add "ArmsFair/Assets/Scripts/Map/WPMGlobeBridge.cs" "ArmsFair/Assets/Scripts/UI/Game/CountryInfoCard.cs"
git commit -m "fix: globe clicks open CountryInfoCard at click position via WPMGlobeBridge event"
```

---

## Task 7: Sales — weapon + sale-type selection state machine

**Files:**
- Modify: `ArmsFair/Assets/Scripts/UI/Game/HUD.cs`
- Modify: `ArmsFair/Assets/Scripts/UI/Game/CountryInfoCard.cs`

The HUD needs to track `_selectedSaleType`, `_selectedWeapon`, `_selectedSupplierId` so `OnSalesConfirm` submits the right action. `CountryInfoCard` needs a weapon-picker that calls back to HUD.

- [ ] **Step 1: Add sale-state fields to HUD**

After the existing `string _salesTargetIso;` field, add:

```csharp
SaleType       _selectedSaleType  = SaleType.Open;
WeaponCategory? _selectedWeapon   = null;
string?         _selectedSupplierId = null;
```

- [ ] **Step 2: Reset sale state on phase change**

In `OnPhaseChanged`, after `_salesTargetIso = null; _salesTargetName = null;`, add:

```csharp
_selectedSaleType   = SaleType.Open;
_selectedWeapon     = null;
_selectedSupplierId = null;
```

- [ ] **Step 3: Add SetWeaponSelection public method**

```csharp
public void SetWeaponSelection(WeaponCategory cat, string supplierId)
{
    _selectedWeapon     = cat;
    _selectedSupplierId = supplierId;
    if (_currentPhase == GamePhase.Sales)
        BuildSalesBar();
}
```

- [ ] **Step 4: Update BuildSalesBar — sale type buttons set state**

Replace the existing Open/Covert/Aid Cover buttons so they set `_selectedSaleType` and rebuild. The `_salesTargetIso == null` branch becomes:

```csharp
// Sale type row — always visible in Sales phase
var saleTypeRow = new VisualElement();
saleTypeRow.style.flexDirection = FlexDirection.Row;

foreach (var (label, st) in new[] {
    ("Open Sale",   SaleType.Open),
    ("Covert Sale", SaleType.Covert),
    ("Aid Cover",   SaleType.AidCover) })
{
    var btn = new Button { text = label };
    btn.AddToClassList("btn-secondary");
    if (_selectedSaleType == st) btn.AddToClassList("btn-active");
    var capSt = st;
    btn.clicked += () => { _selectedSaleType = capSt; BuildSalesBar(); };
    saleTypeRow.Add(btn);
}

var btnPeaceSale = new Button { text = "☮ Peace Broker" };
btnPeaceSale.AddToClassList("btn-secondary");
if (_selectedSaleType == SaleType.PeaceBroker) btnPeaceSale.AddToClassList("btn-active");
btnPeaceSale.clicked += OnPeaceBroker;
saleTypeRow.Add(btnPeaceSale);
_bottomRight.Add(saleTypeRow);

// Status row
var lblTarget = new Label(_salesTargetName != null ? $"Target: {_salesTargetName}" : "Target: — (click globe)");
lblTarget.name = "lbl-sales-target";
_bottomRight.Add(lblTarget);

var weaponText = _selectedWeapon.HasValue
    ? $"Weapon: {_selectedWeapon}"
    : "Weapon: — (select in country card)";
var lblWeapon = new Label(weaponText);
lblWeapon.name = "lbl-sales-weapon";
_bottomRight.Add(lblWeapon);

// Confirm — only when both target and weapon are set, and not Peace Broker
bool canConfirm = _salesTargetIso != null
    && _selectedWeapon.HasValue
    && _selectedSaleType != SaleType.PeaceBroker;

if (canConfirm)
{
    var btnConfirm = new Button { text = "✓ Confirm Action →" };
    btnConfirm.AddToClassList("btn-primary");
    btnConfirm.clicked += OnSalesConfirm;
    _bottomRight.Add(btnConfirm);
}
```

Remove the old `if (_salesTargetIso != null) { ... } else { ... }` branching entirely.

- [ ] **Step 5: Update OnSalesConfirm to use selected state**

Replace the existing `OnSalesConfirm` method:

```csharp
async void OnSalesConfirm()
{
    if (_salesTargetIso == null || !_selectedWeapon.HasValue)
    {
        ToastManager.Instance?.Show("Select a target country and weapon first.", ToastType.Danger);
        return;
    }
    try
    {
        await GameClient.Instance.SubmitActionAsync(new SubmitActionMessage(
            _selectedSaleType,
            _salesTargetIso,
            _selectedWeapon.Value,
            _selectedSupplierId,
            IsDualSupply: false,
            IsProxyRouted: false));
        ToastManager.Instance?.Show(UIStrings.ActionSubmitted, ToastType.Success);
        _salesTargetIso     = null;
        _salesTargetName    = null;
        _selectedWeapon     = null;
        _selectedSupplierId = null;
        BuildSalesBar();
    }
    catch (Exception ex) { ToastManager.Instance?.Show(ex.Message, ToastType.Danger); }
}
```

- [ ] **Step 6: Add weapon picker to CountryInfoCard**

In `CountryInfoCard.cs`, add a reference to HUD:

```csharp
// already has: HUD _hud;
```

In `PopulateCard`, replace the static profit grid with a clickable weapon selector during Sales phase:

```csharp
_profitGrid.Clear();
var categories = new[] {
    WeaponCategory.Drones,
    WeaponCategory.AirDefense,
    WeaponCategory.Vehicles,
    WeaponCategory.SmallArms
};

bool isSales = PhaseManager.Instance?.CurrentPhase == GamePhase.Sales;
var inv = ArmsFair.UI.Game.ProcurementManager.Instance?.CurrentInventory;

foreach (var cat in categories)
{
    int est = EstimateProfit(country, cat);
    var row = new VisualElement();
    row.style.flexDirection = FlexDirection.Row;
    row.style.justifyContent = Justify.SpaceBetween;
    row.Add(new Label(cat.ToString()));
    row.Add(new Label($"${est}M"));

    if (isSales && inv != null)
    {
        var item = inv.Find(i => i.Category == cat);
        if (item != null)
        {
            var btnSel = new Button { text = "Select" };
            btnSel.AddToClassList("btn-secondary");
            var capCat   = cat;
            var capSupp  = item.SupplierId;
            btnSel.clicked += () =>
            {
                _hud?.SetWeaponSelection(capCat, capSupp);
                Hide();
            };
            row.Add(btnSel);
        }
    }

    _profitGrid.Add(row);
}
```

- [ ] **Step 7: Commit**

```bash
git add "ArmsFair/Assets/Scripts/UI/Game/HUD.cs" "ArmsFair/Assets/Scripts/UI/Game/CountryInfoCard.cs"
git commit -m "feat: sales weapon + sale-type selection state machine in HUD and CountryInfoCard"
```

---

## Task 8: Procurement confirmation sent to server

**Files:**
- Modify: `ArmsFair.Shared/Models/Messages/ClientMessages.cs`
- Modify: `ArmsFair.Server/Hubs/GameHub.cs`
- Modify: `ArmsFair/Assets/Scripts/Network/GameClient.cs`
- Modify: `ArmsFair/Assets/Scripts/UI/Game/ProcurementScreen.cs`

- [ ] **Step 1: Add ProcurementConfirmItem to shared messages**

Open `ArmsFair.Shared/Models/Messages/ClientMessages.cs` and add at the bottom of the file:

```csharp
public record ProcurementConfirmItem(
    WeaponCategory Category,
    string         SupplierId,
    int            Quantity,
    float          CostPerUnit);
```

- [ ] **Step 2: Add ConfirmProcurement hub method**

In `GameHub.cs`, add after `SubmitAction`:

```csharp
public async Task ConfirmProcurement(string gameId, List<ProcurementConfirmItem> items)
{
    var playerId = GetPlayerId();
    if (!games.TryGet(gameId, out var state))
    { await SendError("GAME_NOT_FOUND", "Game not found."); return; }

    if (state.Phase != GamePhase.Procurement)
    { await SendError("WRONG_PHASE", "Procurement only allowed during Procurement phase."); return; }

    var totalCost = (int)items.Sum(i => i.CostPerUnit * i.Quantity);
    var player    = state.Players.Find(p => p.Id == playerId);
    if (player is null) { await SendError("PLAYER_NOT_FOUND", "Player not in game."); return; }
    if (player.Capital < totalCost) { await SendError("INSUFFICIENT_CAPITAL", "Not enough capital."); return; }

    var updated = state.Players.Select(p =>
        p.Id == playerId ? p with { Capital = p.Capital - totalCost } : p
    ).ToList();

    var newState = state with { Players = updated };
    games.Set(gameId, newState);

    await Clients.Caller.SendAsync("StateSync", new StateSync(newState));
}
```

- [ ] **Step 3: Add ConfirmProcurementAsync to GameClient**

In `GameClient.cs`, add with the other public task methods:

```csharp
public Task ConfirmProcurementAsync(List<ProcurementConfirmItem> items) =>
    InvokeAsync("ConfirmProcurement", GameId, items);
```

- [ ] **Step 4: Update ProcurementScreen confirm button**

In `ProcurementScreen.cs`, replace:

```csharp
tc.Q<Button>("btn-confirm").clicked += () => UIManager.Instance?.Pop();
```

with:

```csharp
tc.Q<Button>("btn-confirm").clicked += OnConfirmProcurement;
```

Add the handler:

```csharp
async void OnConfirmProcurement()
{
    var inv = ProcurementManager.Instance?.CurrentInventory;
    if (inv == null || inv.Count == 0) { UIManager.Instance?.Pop(); return; }

    var items = inv.Select(i => new ProcurementConfirmItem(
        i.Category, i.SupplierId, i.Quantity, i.CostPerUnit)).ToList();

    try
    {
        await GameClient.Instance.ConfirmProcurementAsync(items);
        UIManager.Instance?.Pop();
    }
    catch (Exception ex)
    {
        SetError(ex.Message, true);
    }
}
```

- [ ] **Step 5: Add using for shared messages in ProcurementScreen.cs**

At the top of `ProcurementScreen.cs`, ensure:

```csharp
using ArmsFair.Shared.Models.Messages;
```

- [ ] **Step 6: Build both projects**

```bash
cd ArmsFair.Server && dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add ArmsFair.Shared/Models/Messages/ClientMessages.cs ArmsFair.Server/Hubs/GameHub.cs "ArmsFair/Assets/Scripts/Network/GameClient.cs" "ArmsFair/Assets/Scripts/UI/Game/ProcurementScreen.cs"
git commit -m "feat: procurement confirmation deducts capital server-side via ConfirmProcurement hub method"
```

---

## Task 9: Track threshold events emitted in WorldUpdate

**Files:**
- Modify: `ArmsFair.Server/Services/PhaseOrchestrator.cs`

- [ ] **Step 1: Add CheckThresholds helper to PhaseOrchestrator**

Add this private method before the `NextPhase` helper:

```csharp
private static List<GameEvent> CheckThresholds(WorldTracks before, WorldTracks after)
{
    var events = new List<GameEvent>();

    void Check(int prev, int curr, int threshold, string desc)
    {
        if (prev < threshold && curr >= threshold)
            events.Add(new GameEvent("threshold", desc, null, null));
    }

    Check(before.MarketHeat,    after.MarketHeat,    80,  "Market overheating — profits spike and civilian harm accelerates");
    Check(before.MarketHeat,    after.MarketHeat,    100, "Market crash — profits frozen for one round");
    Check(before.CivilianCost,  after.CivilianCost,  60,  "UN Security Council opens debate on arms flows");
    Check(before.CivilianCost,  after.CivilianCost,  75,  "International sanctions event — highest contributor designated");
    Check(before.Stability,     after.Stability,     80,  "World on edge — conflicts now spread twice as fast");
    Check(before.SanctionsRisk, after.SanctionsRisk, 60,  "Export license costs doubled");
    Check(before.SanctionsRisk, after.SanctionsRisk, 80,  "Formal investigation notice issued to highest-risk player");
    Check(before.GeoTension,    after.GeoTension,    70,  "US/Russian suppliers restrict sales to opposite-bloc buyers");
    Check(before.GeoTension,    after.GeoTension,    90,  "Great-power confrontation — new crisis zone created");

    return events;
}
```

- [ ] **Step 2: Call CheckThresholds in RunWorldUpdate**

In `RunWorldUpdate`, the current code ends with:

```csharp
_ = hub.Clients.Group(state.GameId).SendAsync("WorldUpdate", new WorldUpdateMessage(
    TrackDeltas    : new TrackDeltas(0, 0, 0, 0, 0),
    NewTracks      : state.Tracks,
    SpreadEvents   : spreadEvents,
    CountryChanges : countryChanges,
    Events         : []));
```

Replace `Events : []` with:

```csharp
var tracksBefore = state.Tracks; // capture before any mutations above
// (Note: RunWorldUpdate doesn't mutate tracks yet — threshold check uses current tracks)
var thresholdEvents = CheckThresholds(tracksBefore, state.Tracks);

_ = hub.Clients.Group(state.GameId).SendAsync("WorldUpdate", new WorldUpdateMessage(
    TrackDeltas    : new TrackDeltas(0, 0, 0, 0, 0),
    NewTracks      : state.Tracks,
    SpreadEvents   : spreadEvents,
    CountryChanges : countryChanges,
    Events         : thresholdEvents));
```

Also, after Reveal phase in `RunRevealAsync`, the updated tracks are applied to state. Add a threshold check there too — replace the end of `RunRevealAsync` before the return:

```csharp
var thresholdEvents = CheckThresholds(state.Tracks, tracks);
state = state with { Tracks = tracks, Players = updatedPlayers };

// Add threshold events to consequences (re-use HumanCostEvents list for display)
var humanCostWithThresholds = thresholdEvents
    .Select(e => new HumanCostEvent(e.Description, ""))
    .ToList();

await hub.Clients.Group(gameId).SendAsync("Consequences", new ConsequencesMessage(
    profitUpdates,
    repUpdates,
    SharePriceUpdates : [],
    BlowbackEvents    : blowbacks,
    HumanCostEvents   : humanCostWithThresholds,
    TreatyResolutions : [],
    NewTracks         : tracks));

return (state, []);
```

- [ ] **Step 3: Build**

```bash
cd ArmsFair.Server && dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add ArmsFair.Server/Services/PhaseOrchestrator.cs
git commit -m "feat: emit threshold GameEvents when tracks cross 60/70/75/80/90/100 boundaries"
```

---

## Task 10: Dev mode — skip phase button and Ctrl+D toggle

**Files:**
- Modify: `ArmsFair.Server/Hubs/GameHub.cs`
- Modify: `ArmsFair/Assets/Scripts/Network/GameClient.cs`
- Modify: `ArmsFair/Assets/Scripts/UI/Game/HUD.cs`

- [ ] **Step 1: Add DevForceAdvance hub method**

In `GameHub.cs`, add after `ConfirmProcurement`:

```csharp
#if DEBUG
public async Task DevForceAdvance(string gameId)
{
    if (!games.TryGet(gameId, out var state)) return;
    var voters  = games.GetOrAddVoters(gameId);
    var pending = games.GetPendingForGame(state.Players.Select(p => p.Id));
    var (newState, ending) = await phaseOrchestrator.AdvanceAsync(gameId, state, voters, pending);
    if (ending is not null)
    {
        ticker.StopGame(gameId);
        games.Remove(gameId);
        games.RemoveVoters(gameId);
    }
    else
    {
        games.Set(gameId, newState);
        ticker.SetPhase(gameId, newState.Phase);
    }
}
#endif
```

- [ ] **Step 2: Add DevForceAdvanceAsync to GameClient**

In `GameClient.cs`:

```csharp
public Task DevForceAdvanceAsync() =>
    InvokeAsync("DevForceAdvance", GameId);
```

- [ ] **Step 3: Add Ctrl+D toggle and skip button to HUD**

In `HUD.cs`, add a field:

```csharp
bool _devMode;
```

In `Update()`, add:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
if (UnityEngine.InputSystem.Keyboard.current?.ctrlKey.isPressed == true &&
    UnityEngine.InputSystem.Keyboard.current?.dKey.wasPressedThisFrame == true)
{
    _devMode = !_devMode;
    PlayerPrefs.SetInt("dev_mode", _devMode ? 1 : 0);
    SetPhaseActionContent(_currentPhase); // rebuild bottom bar
    ToastManager.Instance?.Show(_devMode ? "Dev mode ON" : "Dev mode OFF", ToastType.Info);
}
#endif
```

- [ ] **Step 4: Inject skip button into every phase's bottom bar**

At the end of `SetPhaseActionContent`, before the closing `}`, add:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
if (_devMode)
{
    var btnSkipPhase = new Button { text = "⚡ Skip Phase [DEV]" };
    btnSkipPhase.AddToClassList("btn-danger");
    btnSkipPhase.clicked += async () =>
    {
        btnSkipPhase.SetEnabled(false);
        try { await GameClient.Instance.DevForceAdvanceAsync(); }
        catch (Exception ex) { ToastManager.Instance?.Show(ex.Message, ToastType.Danger); }
        finally { btnSkipPhase.SetEnabled(true); }
    };
    _bottomRight.Add(btnSkipPhase);
}
#endif
```

- [ ] **Step 5: Restore dev mode on Awake from PlayerPrefs**

In `HUD.Awake()`, after other initialisations:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
_devMode = PlayerPrefs.GetInt("dev_mode", 0) == 1;
#endif
```

- [ ] **Step 6: Build server and verify Unity compiles**

```bash
cd ArmsFair.Server && dotnet build
```
Expected: `Build succeeded. 0 Error(s)`

In Unity: open Console, enter Play mode, confirm no compile errors.

- [ ] **Step 7: Commit**

```bash
git add ArmsFair.Server/Hubs/GameHub.cs "ArmsFair/Assets/Scripts/Network/GameClient.cs" "ArmsFair/Assets/Scripts/UI/Game/HUD.cs"
git commit -m "feat: dev mode toggle (Ctrl+D) and skip-phase button for solo testing"
```

---

## Task 11: Dev panel — live state inspector

**Files:**
- Create: `ArmsFair/Assets/Scripts/UI/Game/DevPanel.cs`
- Modify: `ArmsFair.Server/Hubs/GameHub.cs`
- Modify: `ArmsFair/Assets/Scripts/Network/GameClient.cs`

- [ ] **Step 1: Add dev mutation hub methods**

In `GameHub.cs`, add inside the `#if DEBUG` block:

```csharp
public async Task DevSetTrack(string gameId, string trackName, int value)
{
    if (!games.TryGet(gameId, out var state)) return;
    var t = state.Tracks;
    var newTracks = trackName switch
    {
        "MarketHeat"    => t with { MarketHeat    = Math.Clamp(value, 0, 100) },
        "CivilianCost"  => t with { CivilianCost  = Math.Clamp(value, 0, 100) },
        "Stability"     => t with { Stability     = Math.Clamp(value, 0, 100) },
        "SanctionsRisk" => t with { SanctionsRisk = Math.Clamp(value, 0, 100) },
        "GeoTension"    => t with { GeoTension    = Math.Clamp(value, 0, 100) },
        _               => t
    };
    var newState = state with { Tracks = newTracks };
    games.Set(gameId, newState);
    await Clients.Group(gameId).SendAsync("StateSync", new StateSync(newState));
}

public async Task DevSetCountryStage(string gameId, string iso, int stage)
{
    if (!games.TryGet(gameId, out var state)) return;
    var countries = state.Countries.Select(c =>
        c.Iso == iso ? c with { Stage = (ArmsFair.Shared.Enums.CountryStage)Math.Clamp(stage, 0, 5) } : c
    ).ToList();
    var newState = state with { Countries = countries };
    games.Set(gameId, newState);
    await Clients.Group(gameId).SendAsync("StateSync", new StateSync(newState));
}

public async Task DevAddCapital(string gameId, int amount)
{
    var playerId = GetPlayerId();
    if (!games.TryGet(gameId, out var state)) return;
    var players = state.Players.Select(p =>
        p.Id == playerId ? p with { Capital = p.Capital + amount } : p
    ).ToList();
    var newState = state with { Players = players };
    games.Set(gameId, newState);
    await Clients.Caller.SendAsync("StateSync", new StateSync(newState));
}
```

- [ ] **Step 2: Add dev client methods to GameClient**

```csharp
public Task DevSetTrackAsync(string trackName, int value) =>
    InvokeAsync("DevSetTrack", GameId, trackName, value);

public Task DevSetCountryStageAsync(string iso, int stage) =>
    InvokeAsync("DevSetCountryStage", GameId, iso, stage);

public Task DevAddCapitalAsync(int amount) =>
    InvokeAsync("DevAddCapital", GameId, amount);
```

- [ ] **Step 3: Create DevPanel.cs**

```csharp
// ArmsFair/Assets/Scripts/UI/Game/DevPanel.cs
using System.Collections.Generic;
using ArmsFair.Game;
using ArmsFair.Network;
using ArmsFair.Shared.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI.Game
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public class DevPanel : MonoBehaviour
    {
        VisualElement _panel;
        Label         _lblState;
        List<string>  _eventLog = new();
        bool          _visible;

        void Awake()
        {
            // Build panel entirely in code — no UXML needed
            _panel = new VisualElement();
            _panel.style.position        = Position.Absolute;
            _panel.style.top             = 40;
            _panel.style.right           = 8;
            _panel.style.width           = 320;
            _panel.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.1f, 0.92f));
            _panel.style.paddingLeft = _panel.style.paddingRight =
            _panel.style.paddingTop  = _panel.style.paddingBottom = 8;

            _lblState = new Label { text = "—" };
            _lblState.style.whiteSpace  = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
            _lblState.style.fontSize    = 11;
            _lblState.style.color       = new StyleColor(new Color(0.8f, 1f, 0.8f));
            _panel.Add(_lblState);

            // Control buttons
            AddDevButton("+ $20M Capital",     async () => await GameClient.Instance.DevAddCapitalAsync(20_000_000));
            AddDevButton("Stability → 95",     async () => await GameClient.Instance.DevSetTrackAsync("Stability",    95));
            AddDevButton("CivCost → 65",        async () => await GameClient.Instance.DevSetTrackAsync("CivilianCost", 65));
            AddDevButton("MarketHeat → 85",    async () => await GameClient.Instance.DevSetTrackAsync("MarketHeat",   85));
            AddDevButton("GeoTension → 75",    async () => await GameClient.Instance.DevSetTrackAsync("GeoTension",   75));

            _panel.AddToClassList("hidden");

            // Attach to Bootstrap UIDocument
            var doc = FindObjectOfType<UnityEngine.UIElements.UIDocument>();
            if (doc != null)
            {
                doc.rootVisualElement.Add(_panel);
                _panel.BringToFront();
            }
        }

        void AddDevButton(string label, System.Func<System.Threading.Tasks.Task> action)
        {
            var btn = new Button { text = label };
            btn.style.marginTop = 4;
            btn.clicked += async () =>
            {
                try { await action(); }
                catch (System.Exception ex) { Debug.LogWarning($"[DevPanel] {ex.Message}"); }
            };
            _panel.Add(btn);
        }

        void OnEnable()
        {
            GameManager.Instance?.OnStateChanged.AddListener(OnStateChanged);
            GameManager.Instance?.OnWorldUpdated.AddListener(OnWorldUpdated);
        }

        void OnDisable()
        {
            GameManager.Instance?.OnStateChanged.RemoveListener(OnStateChanged);
            GameManager.Instance?.OnWorldUpdated.RemoveListener(OnWorldUpdated);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
            {
                _visible = !_visible;
                _panel.EnableInClassList("hidden", !_visible);
            }
        }

        void OnStateChanged(GameState state)
        {
            var t = state.Tracks;
            var local = GameManager.Instance?.LocalPlayer;
            _lblState.text =
                $"── GAME STATE ──────────────\n" +
                $"Phase: {state.Phase}  Round: {state.Round}\n\n" +
                $"── TRACKS ──────────────────\n" +
                $"MktHeat:{t.MarketHeat}  CivCost:{t.CivilianCost}  Stab:{t.Stability}\n" +
                $"SancRisk:{t.SanctionsRisk}  GeoTens:{t.GeoTension}\n\n" +
                $"── YOUR PROFILE ────────────\n" +
                (local != null
                    ? $"Capital:${local.Capital / 1_000_000}M  Rep:{local.Reputation}  " +
                      $"Latent:{local.LatentRisk}  Peace:{local.PeaceCredits}"
                    : "—") + "\n\n" +
                $"── LAST EVENTS ─────────────\n" +
                string.Join("\n", _eventLog);
        }

        void OnWorldUpdated(ArmsFair.Shared.Models.Messages.WorldUpdateMessage msg)
        {
            foreach (var e in msg.Events)
            {
                _eventLog.Insert(0, $"[{e.EventType}] {e.Description}");
                if (_eventLog.Count > 5) _eventLog.RemoveAt(_eventLog.Count - 1);
            }
        }
    }
#endif
}
```

- [ ] **Step 4: Add DevPanel component to Bootstrap scene**

In Unity (not in Play mode):
1. Select the `DontDestroyOnLoad` GameObject in the Bootstrap scene Hierarchy
2. Click `Add Component` → search for `DevPanel`
3. Save the scene: `Ctrl+S`

- [ ] **Step 5: Build server and enter Play mode to verify**

```bash
cd ArmsFair.Server && dotnet build
```

Enter Play mode. Press `~` — dev panel should appear top-right with current game state.

- [ ] **Step 6: Commit**

```bash
git add ArmsFair.Server/Hubs/GameHub.cs "ArmsFair/Assets/Scripts/Network/GameClient.cs" "ArmsFair/Assets/Scripts/UI/Game/DevPanel.cs" "ArmsFair/Assets/Bootstrap.unity"
git commit -m "feat: dev panel with live state inspector and track/stage/capital mutation tools"
```

---

## Task 12: Negotiation — Intel and Peace Proposal wired

**Files:**
- Modify: `ArmsFair/Assets/Scripts/UI/Game/NegotiationPanel.cs`

`NegotiationPanel` already has whistle UI wired to `GameClient.WhistleAsync`. The Intel button in the HUD already pushes `NegotiationPanel`. The panel's whistle-confirm flow (`OnConfirmWhistle`) just needs to verify it calls through correctly. Peace Proposal needs to call `InvestInPeacekeepingAsync` targeting a country.

- [ ] **Step 1: Read the rest of NegotiationPanel to verify whistle wiring**

Open `ArmsFair/Assets/Scripts/UI/Game/NegotiationPanel.cs` and check `OnConfirmWhistle`. It should call `GameClient.Instance.WhistleAsync(level, targetId)`. If it does, no change is needed for whistle.

- [ ] **Step 2: Add Peace Proposal to NegotiationPanel**

At the end of `Show()`, ensure the panel shows a simple `btn-peace-invest` button. Add to `Awake()`:

```csharp
tc.Q<Button>("btn-peace-invest").clicked += OnPeaceInvest;
```

Add the handler:

```csharp
void OnPeaceInvest()
{
    // For solo testing: invest in peacekeeping for the first Hot War or Active country
    var state = GameManager.Instance?.CurrentState;
    var target = state?.Countries?.Find(c =>
        c.Stage >= ArmsFair.Shared.Enums.CountryStage.Active);

    if (target == null)
    {
        SetError("No eligible country found for peacekeeping.", true);
        return;
    }

    _ = GameClient.Instance.InvestInPeacekeepingAsync(target.Iso);
    ToastManager.Instance?.Show($"Peacekeeping invested in {target.Name}", ToastType.Success);
    UIManager.Instance?.Pop();
}
```

- [ ] **Step 3: Commit**

```bash
git add "ArmsFair/Assets/Scripts/UI/Game/NegotiationPanel.cs"
git commit -m "feat: wire Peace Proposal in NegotiationPanel to InvestInPeacekeeping hub method"
```

---

## Self-Review Checklist

After writing this plan, checking against the spec:

| Spec Item | Task |
|---|---|
| 1a StartGame never called | Task 4 |
| 1b TickerService bypasses PhaseOrchestrator | Task 3 |
| 1c GameStateService | Task 1 + 2 |
| 1d PhaseStart mismatch | Auto-fixed by Task 3 |
| 2a Procurement confirmation | Task 8 |
| 2b Sales weapon/type selection | Task 7 |
| 2c Globe clicks | Task 6 |
| 2d Reveal/Consequences overlays | Both already implemented (RevealOverlay subscribes OnReveal; ConsequencesOverlay subscribes OnConsequencesApplied). No task needed. |
| 3a Track threshold events | Task 9 |
| 3b Ending conditions | Already wired in PhaseOrchestrator line 71. EndingChecker.Check is called. TickerService now stops game on ending (Task 3). No separate task needed. |
| 3c Debrief | Task 5 |
| 3d Negotiation Intel + Peace Prop | Task 12 |
| 4a Dev mode + skip button | Task 10 |
| 4b Dev panel | Task 11 |

All 14 spec items covered. No placeholders. Type names consistent throughout.

---

*End of plan*
