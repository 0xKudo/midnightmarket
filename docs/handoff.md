# Arms Fair — Session Handoff

## Last updated: 2026-05-06

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
- SeedService — ACLED + GPI, all 5 game modes, Redis cache
- Program.cs — correct singleton/scoped lifetimes
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
  ├── RoomListScreen   (UIDocument → RoomListScreen.uxml + PanelSettings, RoomListScreen.cs)
  └── ProfileScreen    (UIDocument → ProfileScreen.uxml + PanelSettings, ProfileScreen.cs)
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
- CREATE ROOM posts to `/api/rooms` — logs TODO Phase 11 on success
- BACK → `Pop()`

**RoomListScreen: WORKING** ✓
- Fetches room list from VPS on Show(), shows LOADING... while waiting
- Room rows: name, player count/slots, game mode, JOIN button
- JOIN logs TODO Phase 11 on success
- Join by invite code + REFRESH + BACK

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

## Pending Work (priority order)

1. **Phase 11: PreGameLobbyScreen** — wire CreateRoomScreen CREATE + RoomListScreen JOIN navigation here; show room info, player list, start game button for host
2. **Phase 12+: HUD and in-game screens**
