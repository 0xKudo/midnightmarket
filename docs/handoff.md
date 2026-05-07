# Arms Fair — Session Handoff

## Last updated: 2026-05-06 (Phase 13 complete — HUD live, phases auto-advance)

---

## Current State

### Server (ArmsFair.Server) — builds clean, deployed to VPS

**Done:**
- GameStateService singleton — owns all in-memory game state
- PhaseOrchestrator — single authority for phase transitions, uses IServiceScopeFactory for scoped DB access
- TickerService — calls PhaseOrchestrator.AdvanceForGameAsync on expiry
- GameHub — all state via GameStateService, no static fields
- AuthService — JWT + BCrypt, register/login/me endpoints
- LobbyService — create/join/list rooms
- SeedService — ACLED + GPI, all 5 game modes, Redis cache. Falls back to EqualWorld on Redis failure.
- Program.cs — correct singleton/scoped lifetimes. Redis registered with `AbortOnConnectFail=false`, default host `redis:6379` for Docker.
- `POST /api/auth/profile` — updates HomeNationIso + CompanyName, reads player from `ctx.User` claims (not manual token re-validation)
- `CompanyName` added to PlayerEntity + all auth responses (register/login/me)
- `AddCompanyName` EF migration run on VPS — column exists in DB

**Known gaps (server):**
- StatsService — lifetime stat updates at game end not wired
- ChatRepository — chat not persisted to DB
- Treaty system stubbed (0 values in PhaseOrchestrator)
- No `JsonStringEnumConverter` registered — enum fields in request bodies must be sent as integers, not strings

**Critical server gotcha — profile endpoint pattern:**
- Auth'd endpoints must read player identity from `ctx.User` claims, NOT by manually re-reading the Authorization header and calling `ValidateTokenAsync`
- Manual re-validation conflicts with `.RequireAuthorization()` JWT middleware and causes 401
- Correct pattern: `ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")?.Value` → parse Guid → `db.Players.FindAsync(id)`
- See `/api/rooms` POST and `/api/auth/profile` POST for reference implementations

**Critical server gotcha — HTTP methods:**
- `UnityWebRequest` does NOT reliably send `Authorization` headers with `PATCH` requests — use `POST` for all profile/update endpoints
- The profile update endpoint is `POST /api/auth/profile` (not PATCH)

**Critical server gotcha — claim name remapping:**
- ASP.NET Core JWT middleware silently remaps `sub` → `ClaimTypes.NameIdentifier` (`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`)
- `ctx.User.FindFirst("sub")` always returns null in handlers — causes silent 401 with empty body
- Always use the fallback pattern for player ID: `ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")?.Value`
- See `/api/auth/profile`, `/api/rooms` POST, and `/api/rooms/{id}/join` for correct implementations

---

### Unity Client — Bootstrap scene, all pre-game screens complete

**Done:**
- AuthApiClient.cs — login, register, getMe, PatchProfileAsync (POST)
- AccountManager.cs — singleton, PlayerPrefs token persistence, SaveProfileAsync
- GameClient.cs — SignalR singleton, all server→client events wired
- UnityMainThreadDispatcher.cs — marshals SignalR callbacks to main thread
- UIManager.cs — singleton navigation (GoTo/Push/Pop), DefaultExecutionOrder(-100)
- IScreen.cs — interface all screens implement
- NetworkManagerBootstrap.cs — TryAutoLoginAsync, routes to Login or MainMenu, DefaultExecutionOrder(100)
- LobbyApiClient.cs — create/list/get/join rooms, instance methods, Bearer token
- NationsList.cs — shared static ~155-country list, ISO 3166-1 alpha-3 format
- LoginScreen.uxml + LoginScreen.cs — registers as "Login"
- RegisterScreen.uxml + RegisterScreen.cs — registers as "Register"
- MainMenuScreen.uxml + MainMenuScreen.cs — registers as "MainMenu"
- CreateRoomScreen.uxml + CreateRoomScreen.cs — registers as "CreateRoom"
- RoomListScreen.uxml + RoomListScreen.cs — registers as "RoomList"
- ProfileScreen.uxml + ProfileScreen.cs — registers as "Profile"
- LobbyState.cs — static `PendingRoomId` property, used to pass roomId between screens (UIManager has no param system)
- PreGameLobbyScreen.uxml + PreGameLobbyScreen.cs — registers as "PreGameLobby"

**Bootstrap scene hierarchy:**
```
NetworkManager
  ├── AccountManager
  ├── UIManager
  ├── GameClient
  ├── UnityMainThreadDispatcher
  ├── NetworkManagerBootstrap
  ├── ViewToggleManager
  ├── LoginScreen          (UIDocument → LoginScreen.uxml + PanelSettings, LoginScreen.cs)
  ├── RegisterScreen       (UIDocument → RegisterScreen.uxml + PanelSettings, RegisterScreen.cs)
  ├── MainMenuScreen       (UIDocument → MainMenuScreen.uxml + PanelSettings, MainMenuScreen.cs)
  ├── CreateRoomScreen     (UIDocument → CreateRoomScreen.uxml + PanelSettings, CreateRoomScreen.cs)
  ├── RoomListScreen       (UIDocument → RoomListScreen.uxml + PanelSettings, RoomListScreen.cs)
  ├── ProfileScreen        (UIDocument → ProfileScreen.uxml + PanelSettings, ProfileScreen.cs)
  ├── PreGameLobbyScreen   (UIDocument → PreGameLobbyScreen.uxml + PanelSettings, PreGameLobbyScreen.cs)
  └── HUDScreen            (UIDocument → HUDScreen.uxml + PanelSettings, HUDScreen.cs)
```

**CRITICAL — one UIDocument per screen:**
Each screen is its own child GameObject with its own UIDocument. Do NOT share a UIDocument between screens. Always use `GetComponent<UIDocument>()` — never `FindFirstObjectByType<UIDocument>()`.

**Adding a new screen (follow every time):**
1. Create `UXML/XxxScreen.uxml` — include `<ui:Style>` for variables.uss + terminal.uss + scrollbar.uss, hardcoded RGB inline styles, root element `display:none; position:absolute; left:0; top:0; width:100%; height:100%`
2. Create `Screens/XxxScreen.cs` — copy LoginScreen.cs pattern (docRoot fill to 100%, StyleButton, StyleLabels, Register)
3. Add child GameObject `XxxScreen` under NetworkManager via Unity MCP (`manage_gameobject create`)
4. Add UIDocument component → `manage_components add` → set `sourceAsset` + `m_PanelSettings` (NOT `panelSettings`)
5. Add XxxScreen MonoBehaviour via `manage_components add`
6. Save scene via `manage_scene save`, check console for errors

**USS / Styling:**
- variables.uss — CSS custom properties (NOTE: Unity UI Toolkit does NOT reliably inherit CSS vars — reference only)
- terminal.uss — all hardcoded RGB values, no var() references. Top rule sets font via `* { -unity-font: resource("Fonts & Materials/SourceCodePro-Medium"); }`
- scrollbar.uss — hardcoded RGB scrollbar selectors. Arrow buttons hidden via `display:none`. Import in any UXML that uses ScrollView.
- ArmsFair.tss — imports all three USS files, assigned to PanelSettings.themeUss

**DO NOT use DropdownField for styled menus.** Unity's DropdownField popup renders in a separate overlay panel outside the USS scope. Instead use:
- A `Button` showing the current selection
- A modal overlay `VisualElement` (`position:absolute; left:0; top:0; right:0; bottom:0`) containing a `ScrollView` with choice buttons
- For long lists (nations): show a search `TextField` above the ScrollView, filter with `FindAll` on keystroke
- For short lists (slots, timer, game mode): hide the search field

**Modal overlay pattern (confirmed working):**
- Overlay: `position:absolute; left:0; top:0; right:0; bottom:0; background-color:rgba(0,0,0,0.85)`
- Panel inside: explicit `width` + `height` px + `overflow:hidden`
- ScrollView height set in BOTH UXML and C# when opening (`_choiceList.style.height = new StyleLength(Xf)`)
- `flex-grow:1` on ScrollView is unreliable — always set explicit pixel height

**Success/confirmation modals:**
- Use a full-screen overlay modal with an OK button — not an inline label
- Same overlay pattern as choice modals: `position:absolute; left:0; top:0; right:0; bottom:0`
- See ProfileScreen SuccessModal for reference

**Font setup (critical):**
- UI Toolkit requires font set via USS `* { -unity-font: resource("Fonts & Materials/SourceCodePro-Medium"); }` in terminal.uss
- rootVisualElement height fix: set `docRoot.style.height = Length.Percent(100)` in code

**Unity MCP — UIDocument property names:**
- `panelSettings` serialized property is named `m_PanelSettings` — use that when calling `manage_components set_property`
- Never use `manage_components get_components` on UIDocument or Transform — causes StackOverflow crash

---

## Screen Status

**LoginScreen: WORKING** ✓
**RegisterScreen: WORKING** ✓
**MainMenuScreen: WORKING** ✓
- CREATE ROOM → `Push("CreateRoom")`
- JOIN ROOM → `Push("RoomList")`
- PROFILE → `Push("Profile")`
- DISCONNECT → logout + `GoTo("Login")`

**CreateRoomScreen: WORKING** ✓
- Room Name, Player Slots, Timer Preset, Game Mode — all working
- Private Room / AI Fill-In toggles — working
- CREATE ROOM → sets `LobbyState.PendingRoomId`, `GoTo("PreGameLobby")`
- BACK → `Pop()`

**RoomListScreen: WORKING** ✓
- Fetches room list from VPS on Show(), shows LOADING... while waiting
- Room rows: name, player count/slots, game mode, JOIN button
- JOIN → sets `LobbyState.PendingRoomId`, `GoTo("PreGameLobby")`
- Join by invite code + REFRESH + BACK

**PreGameLobbyScreen: WORKING** ✓
- Reads `LobbyState.PendingRoomId` on Show()
- Polls `/api/rooms/{id}` every 3s via `InvokeRepeating`
- Shows room name, invite code, slot count, player list
- Host sees START GAME button; non-host sees WAITING FOR HOST...
- Player list: host shown as `> NAME [HOST]`, others as `OPERATIVE-XXXX`
- START GAME: reconnects SignalR if dropped, then calls `GameClient.Instance.CreateGameAsync`
- Navigation to HUD happens in `OnStateSync` handler (gameId only known after StateSync)
- LEAVE → `GoTo("MainMenu")`

**HUDScreen: WORKING** ✓
- Top bar: world tracks (MKT/CIV/STB/SAN/GEO) with color coding, phase label, countdown timer, round counter
- Left sidebar: company name, capital/reputation/share price/peace credits/latent risk, player list
- Centre: phase status label (globe placeholder)
- Timer: runs in `Update()` using `DateTimeOffset.UtcNow` vs server `PhaseStartMessage.EndsAt` (Unix ms)
- Track colors: green=safe, yellow=watch, red=danger (stability inverted — low is bad)
- Subscribes to `OnStateSync`, `OnPhaseStart`, `OnConsequences`, `OnWorldUpdate`
- LEAVE → `GoTo("MainMenu")`
- Registers as `"HUD"`

**ProfileScreen: WORKING** ✓
- Home Nation modal with search (NationsList.All, ISO alpha-3)
- Brokerage Name text field
- SAVE PROFILE posts to `POST /api/auth/profile` — confirmed working end-to-end
- On success: green "[ PROFILE SAVED ]" modal with OK button to dismiss
- BACK → `Pop()`

**VPS status: LIVE** ✓
- Server at `https://armsfair.laynekudo.com` (Hostinger VPS, Ubuntu 24.04, Docker Compose)
- PostgreSQL in Docker — accounts + company names persist across sessions
- Nginx reverse proxy handles SSL + SignalR WebSocket upgrade
- `AddCompanyName` migration applied — CompanyName column exists
- Do NOT run a local PostgreSQL — always point at the VPS DB

---

## AuthApiClient Gotchas

- **`PostAsync`/`GetAsync` must be instance methods** — need `_baseUrl` prepended or requests fail silently
- **Server response shape:** `{"token":"...","profile":{"id","username","homeNationIso","companyName"}}` — `AuthResponse` must have a nested `AuthProfile` class
- **`ProfileResponse.homeNationIso`** — field name is `homeNationIso` (NOT `homeNation`) to match server response
- **Error label visibility:** inline `style="display:none"` in UXML overrides USS class removal — always use `style.display = DisplayStyle.Flex/None` directly
- **PATCH method broken** — `UnityWebRequest` does not reliably send Authorization headers with PATCH. Use POST for all update endpoints.

## LobbyApiClient Gotchas

- **`GameMode` enum must be sent as integer** — Realistic=1, EqualWorld=2, BlankSlate=3, HotWorld=4, Custom=5
- **Two response shapes** — `GET /api/rooms` → `RoomSummary` (has `playerCount`). `POST/GET /api/rooms/{id}` → `RoomInfo` (has `playerIds string[]`)
- **`JsonUtility` cannot deserialize `List<string>`** — use `string[]`
- **Bare JSON array** — `GET /api/rooms` returns bare `[...]`. Wrap: `JsonUtility.FromJson<RoomSummaryList>("{\"items\":" + raw + "}")`
- **Bearer token required** — all lobby endpoints need `Authorization: Bearer <token>`

## Nations List

- `NationsList.cs` — shared static class at `ArmsFair/Assets/Scripts/UI/NationsList.cs`
- ISO 3166-1 alpha-3 format: `"USA — United States"`, `"GBR — United Kingdom"`, etc.
- ~155 countries organised by region
- ISO code extracted from entry: `entry[..3]` gives the 3-letter code to send to the server

---

## Known Issues / Gotchas

### CSS Custom Properties Don't Inherit in Unity UI Toolkit
`var(--color-text)` etc. do NOT reliably propagate. Always use hardcoded RGB.

### Button Label Color in Unity UI Toolkit
Unity wraps Button text in an internal Label. Set color directly on the Button element.

### UIManager Execution Order
- UIManager: DefaultExecutionOrder(-100)
- Screen MonoBehaviours: default order 0 — register in Awake
- NetworkManagerBootstrap: DefaultExecutionOrder(100) — calls GoTo after all screens registered

---

## PreGameLobbyScreen Gotchas

- **`LobbyState.PendingRoomId`** — set before `GoTo("PreGameLobby")`, read in `Show()`. UIManager has no param system; this static is the workaround.
- **Polling not SignalR** — LobbyService has no SignalR events. Use `InvokeRepeating`/`CancelInvoke` for lobby refresh.
- **`RoomInfo.gameMode` is a string** — use `Enum.TryParse<GameMode>(room.gameMode, out var gm)` when building `LobbySettingsMessage`.
- **Navigation to HUD is deferred** — `CreateGameAsync` triggers `StateSync` on the server. Listen for `OnStateSync` and call `GoTo("HUD")` there, not on button click.
- **`hostUsername` is on `RoomInfo`** — the only player name available is the host's. Other players render as `OPERATIVE-{id[..4]}`.

---

## Server Deployment Gotchas

- **Redis hostname in Docker** — inside Docker Compose, the Redis service is reachable at `redis:6379` (service name), NOT `localhost:6379`. `localhost` inside the server container refers to the container itself.
- **`ConnectionMultiplexer.Connect` is lazy** — the singleton is resolved on first hub connection, not at startup. If it throws, `OnConnectedAsync` fails and SignalR closes the connection before any hub method runs. Set `AbortOnConnectFail=false` to prevent this.
- **`CreateGame` starts the ticker** — `ticker.StartGame(gameId)` is called inside `CreateGameInternalAsync`. Phases begin automatically when the host clicks START GAME; there is no separate "start phases" step.

## HUDScreen Gotchas

- **`PhaseStartMessage.EndsAt` is Unix milliseconds** — compute `EndsAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. Never use `DateTime.Now`.
- **`GameState.Round == 0` on first `StateSync`** — pre-game limbo before first `PhaseStart`. Show "SETUP" not "ROUND 0".
- **`CreateGame` broadcasts to SignalR group** — all players in the group get `StateSync` and navigate to HUD. Non-host players must have called `JoinGame` to be in the group.
- **`JoinGame` appends player to `state.Players`** — fixed in server. Previously only the host appeared in the player list.
- **SignalR drops after idle in lobby** — `OnStartGame` now reconnects using stored token if `!GameClient.Instance.IsConnected` before invoking `CreateGame`.
- **`SeedService` Redis/ACLED failures** — `SeedRealisticAsync` now catches all exceptions and falls back to `SeedEqualWorld` (static data). `CreateGame` hub method also wrapped in try/catch — failures send `Error` message to client instead of closing the connection.
- **Delta messages are not full state** — `PhaseStart`, `Consequences`, `WorldUpdate` are deltas. Cache last `StateSync` as `_lastState` and apply deltas to it. HUDScreen does this.

---

## Pending Work (priority order)

1. **Phase 14: Procurement Phase UI** — weapon list, confirm orders, capital deduction
2. **Phase 15: Sales Phase UI** — country selection, weapon/sale type, submit sealed order
3. **Phase 16: Reveal Overlay** — show all players' actions after reveal
4. **Phase 17: Consequences Overlay** — profit/rep/share price updates per player

## VPS Status

**Live and working end-to-end as of 2026-05-06:**
- Auth (register/login/profile) ✓
- Lobby (create/list/join rooms) ✓
- Game creation → HUD navigation → phases auto-advance ✓
- Redis connected at `redis:6379` with `AbortOnConnectFail=false` ✓
- SeedService falls back to EqualWorld if Redis/ACLED unavailable ✓
