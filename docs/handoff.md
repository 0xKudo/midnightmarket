# Arms Fair — Session Handoff

## Last updated: 2026-05-06

---

## Current State

### Server (ArmsFair.Server) — builds clean, 0 errors

**Done:**
- GameStateService singleton — owns all in-memory game state (extracted from GameHub static fields)
- PhaseOrchestrator — single authority for phase transitions, uses IServiceScopeFactory for scoped DB access
- TickerService — calls PhaseOrchestrator.AdvanceForGameAsync on expiry, does NOT send PhaseStart directly
- GameHub — all state via GameStateService, no static fields
- AuthService — JWT + BCrypt, register/login/me endpoints
- LobbyService — create/join/list rooms
- SeedService — ACLED + GPI, all 5 game modes, Redis cache
- Program.cs — correct singleton/scoped lifetimes

**Known gaps (server):**
- EF Core migrations never run — DB schema not created
- StatsService — lifetime stat updates at game end not wired
- ChatRepository — chat not persisted to DB
- Treaty system stubbed (0 values in PhaseOrchestrator)
- No `JsonStringEnumConverter` registered — enum fields in request bodies must be sent as integers, not strings

---

### Unity Client — Bootstrap scene, auth layer, UIManager wired

**Done:**
- AuthApiClient.cs — UnityWebRequest REST wrapper (login, register, getMe)
- AccountManager.cs — singleton, PlayerPrefs token persistence, OnLoggedIn/OnLoggedOut events
- GameClient.cs — SignalR singleton, all server→client events wired
- UnityMainThreadDispatcher.cs — marshals SignalR callbacks to main thread
- UIManager.cs — singleton navigation (GoTo/Push/Pop), DefaultExecutionOrder(-100)
- IScreen.cs — interface all screens implement
- NetworkManagerBootstrap.cs — calls TryAutoLoginAsync, routes to Login or MainMenu, DefaultExecutionOrder(100)
- LobbyApiClient.cs — UnityWebRequest REST wrapper (create/list/get/join rooms), instance methods, Bearer token from AccountManager.Instance.Token
- LoginScreen.uxml + LoginScreen.cs — terminal-styled login screen, registers as "Login"
- RegisterScreen.uxml + RegisterScreen.cs — registers as "Register"
- MainMenuScreen.uxml + MainMenuScreen.cs — registers as "MainMenu", uses Push("CreateRoom") and Push("RoomList")
- CreateRoomScreen.uxml + CreateRoomScreen.cs — registers as "CreateRoom"
- RoomListScreen.uxml + RoomListScreen.cs — registers as "RoomList"

**Bootstrap scene hierarchy:**
```
NetworkManager
  ├── AccountManager
  ├── UIManager
  ├── GameClient
  ├── UnityMainThreadDispatcher
  ├── NetworkManagerBootstrap
  ├── ViewToggleManager
  ├── LoginScreen      (UIDocument → LoginScreen.uxml + PanelSettings, LoginScreen.cs)
  ├── RegisterScreen   (UIDocument → RegisterScreen.uxml + PanelSettings, RegisterScreen.cs)
  ├── MainMenuScreen   (UIDocument → MainMenuScreen.uxml + PanelSettings, MainMenuScreen.cs)
  ├── CreateRoomScreen (UIDocument → CreateRoomScreen.uxml + PanelSettings, CreateRoomScreen.cs)
  └── RoomListScreen   (UIDocument → RoomListScreen.uxml + PanelSettings, RoomListScreen.cs)
```

**CRITICAL — one UIDocument per screen:**
Each screen is its own child GameObject with its own UIDocument. Do NOT share a UIDocument between screens. Always use `GetComponent<UIDocument>()` — never `FindFirstObjectByType<UIDocument>()`.

**Adding a new screen (follow every time):**
1. Create `UXML/XxxScreen.uxml` — include `<ui:Style>` for variables.uss + terminal.uss + scrollbar.uss, hardcoded RGB inline styles, root element `display:none; position:absolute; left:0; top:0; width:100%; height:100%`
2. Create `Screens/XxxScreen.cs` — copy LoginScreen.cs pattern (docRoot fill to 100%, StyleButton, StyleLabels, Register)
3. Add child GameObject `XxxScreen` under NetworkManager via Unity MCP
4. Add UIDocument component → assign XxxScreen.uxml (`m_PanelSettings` property name) + Assets/PanelSettings.asset
5. Add XxxScreen MonoBehaviour to same GameObject
6. Save scene, check console before entering play mode

**USS / Styling:**
- variables.uss — CSS custom properties (NOTE: Unity UI Toolkit does NOT reliably inherit CSS vars — reference only)
- terminal.uss — all hardcoded RGB values, no var() references. Top rule sets font via `* { -unity-font: resource("Fonts & Materials/SourceCodePro-Medium"); }`
- scrollbar.uss — hardcoded RGB scrollbar selectors (var() references were broken and have been replaced). Arrow buttons hidden via `display:none`. Import this in any UXML that uses ScrollView.
- ArmsFair.tss — imports all three USS files, assigned to PanelSettings.themeUss

**DO NOT use DropdownField for styled menus.** Unity's DropdownField popup renders in a separate overlay panel outside the USS scope — it cannot be styled to match the terminal aesthetic. Instead use:
- A `Button` showing the current selection
- A modal overlay `VisualElement` (position:absolute; left:0; top:0; right:0; bottom:0) containing a `ScrollView` with choice buttons
- For long lists (nations): show a search `TextField` above the ScrollView, filter with `FindAll` on keystroke
- For short lists (slots, timer, game mode): hide the search field

**ScrollView height in modals — critical:**
- Parent panel must have explicit pixel `height` + `overflow:hidden`
- ScrollView must have explicit pixel `height` set BOTH in UXML and in C# when opening (Unity sometimes ignores UXML-only)
- `flex-grow:1` on ScrollView inside a fixed-height panel is unreliable — always set explicit height

**Absolute overlay positioning:**
- Use `left:0; top:0; right:0; bottom:0` for overlays — NOT `width:100%; height:100%`. In Unity UI Toolkit, percentage dimensions on absolute elements do not reliably fill the parent.

**Font setup (critical):**
- UI Toolkit requires font set via USS `* { -unity-font: resource("Fonts & Materials/SourceCodePro-Medium"); }` in terminal.uss
- The TTF lives at `Assets/TextMesh Pro/Resources/Fonts & Materials/SourceCodePro-Medium.ttf`
- rootVisualElement height fix: set `docRoot.style.height = Length.Percent(100)` in code

**Unity MCP — UIDocument property names:**
- `panelSettings` serialized property is named `m_PanelSettings` — use that when calling `manage_components set_property`
- Never use `manage_components get_components` on UIDocument or Transform — causes StackOverflow crash

---

## Screen Status

**LoginScreen: WORKING** ✓
**RegisterScreen: WORKING** ✓
**MainMenuScreen: WORKING** ✓
- CREATE ROOM uses `Push("CreateRoom")`
- JOIN ROOM uses `Push("RoomList")`
- PROFILE logs TODO warning (Phase 10)

**CreateRoomScreen: WORKING** ✓
- Room Name (text field)
- Player Slots, Timer Preset, Game Mode — styled modal selection (no search)
- Private Room / AI Fill-In — toggle buttons (YES/NO)
- CREATE ROOM posts to `/api/rooms` — logs TODO Phase 11 on success
- BACK uses `Pop()`
- Home Nation removed from this screen — moves to ProfileScreen (Phase 10)

**RoomListScreen: WORKING** ✓
- Fetches room list from VPS on Show() — shows "LOADING..." while waiting
- Displays each room as a row: name, player count/slots, game mode, JOIN button
- JOIN logs TODO Phase 11 on success
- Join by invite code field + button
- REFRESH re-triggers Show()
- BACK uses `Pop()`

**VPS status: LIVE** ✓
- Server running at `https://armsfair.laynekudo.com` (Hostinger VPS, Ubuntu 24.04, Docker Compose)
- PostgreSQL running in Docker container on VPS — accounts persist across sessions
- Nginx reverse proxy handles SSL + SignalR WebSocket upgrade
- Register and login confirmed working end-to-end from Unity client
- Room creation confirmed working (logs roomId to console)
- Do NOT run a local PostgreSQL for development — always point at the VPS DB for testing

---

## AuthApiClient Gotchas (learned 2026-05-06)

- **`PostAsync`/`GetAsync` must be instance methods** — they need `_baseUrl` prepended or requests go to a bare path with no host and fail silently
- **Server response shape:** `{"token":"...","profile":{"id":"...","username":"...","homeNationIso":"..."}}` — `AuthResponse` must have a nested `AuthProfile` class. `JsonUtility` does not flatten nested JSON.
- **Error label visibility:** inline `style="display:none"` in UXML overrides USS class removal. Always use `_errorLabel.style.display = DisplayStyle.Flex/None` directly.

## LobbyApiClient Gotchas (learned 2026-05-06)

- **`GameMode` enum must be sent as integer** — server has no `JsonStringEnumConverter`. Sending `"gameMode":"Realistic"` causes a 400. Send the integer value: Realistic=1, EqualWorld=2, BlankSlate=3, HotWorld=4, Custom=5.
- **Two different response shapes** — `GET /api/rooms` returns `RoomSummary` (has `playerCount` int, no `playerIds`). `POST /api/rooms` and `GET /api/rooms/{id}` return `RoomRecord` (has `playerIds string[]`). Use separate C# classes.
- **`JsonUtility` cannot deserialize `List<string>`** — use `string[]` for `playerIds` in `RoomInfo`.
- **Bare JSON array** — `GET /api/rooms` returns a bare `[...]` array. Wrap it: `JsonUtility.FromJson<RoomSummaryList>("{\"items\":" + raw + "}")`.
- **Bearer token required** — all lobby endpoints require `Authorization: Bearer <token>`. Use `AccountManager.Instance.Token`.

## Nations List

- Uses ISO 3166-1 alpha-3 codes (3-letter): `"USA — United States"`, `"GBR — United Kingdom"`, etc.
- ~155 countries organised by region in CreateRoomScreen.cs
- Home Nation selection belongs in ProfileScreen (Phase 10), not CreateRoomScreen

---

## 🔴 BLOCKING — VPS Redeploy Required

**ProfileScreen SAVE PROFILE returns "SAVE FAILED — CONNECTION ERROR"**

The VPS is running stale code. The following changes exist in git (`main` branch, commit `cf9b522`) but have NOT been deployed to the VPS yet:

1. **`ArmsFair.Server/Program.cs`** — `PATCH /api/auth/profile` changed to `POST /api/auth/profile` (UnityWebRequest does not reliably send Authorization headers with PATCH)
2. **`ArmsFair.Server/Data/Entities/PlayerEntity.cs`** — `CompanyName` field added
3. **`ArmsFair.Server/Program.cs`** — `companyName` included in register/login/me responses; PATCH→POST profile endpoint added; `UpdateProfileRequest` record added

**Steps to fix:**
```bash
# On the VPS — pull latest and redeploy
git pull origin main

# Run the EF migration (adds CompanyName column)
dotnet ef migrations add AddCompanyName --project ArmsFair.Server
dotnet ef database update --project ArmsFair.Server

# Restart the server (Docker or systemd — confirm method with user)
```

After redeploy, ProfileScreen SAVE PROFILE should work end-to-end.

---

## Known Issues / Gotchas

### CSS Custom Properties Don't Inherit in Unity UI Toolkit
`var(--color-text)` etc. do NOT reliably propagate. Always use hardcoded RGB. scrollbar.uss was broken by var() references and has been fixed.

### Button Label Color in Unity UI Toolkit
Unity wraps Button text in an internal Label. Target `.unity-button > .unity-label` or set color directly on the Button.

### UIManager Execution Order
- UIManager: DefaultExecutionOrder(-100)
- Screen MonoBehaviours: default order 0 — register in Awake
- NetworkManagerBootstrap: DefaultExecutionOrder(100) — calls GoTo after all screens registered

### EF Core Migrations Never Run
Before deploying: `dotnet ef migrations add InitialSchema --project ArmsFair.Server && dotnet ef database update`

---

## Pending Work (priority order)

1. **Phase 10: ProfileScreen** — includes Home Nation + Company Name selection
2. **Phase 11: PreGameLobbyScreen** — wire CreateRoomScreen + RoomListScreen JOIN navigation here
3. **Phase 12+: HUD and game screens**
