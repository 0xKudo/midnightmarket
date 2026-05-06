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
- LoginScreen.uxml + LoginScreen.cs — terminal-styled login screen, registers as "Login"

**Bootstrap scene hierarchy:**
```
NetworkManager
  ├── AccountManager
  ├── UIManager
  ├── GameClient
  ├── UnityMainThreadDispatcher
  ├── NetworkManagerBootstrap
  ├── ViewToggleManager
  ├── LoginScreen    (UIDocument → LoginScreen.uxml + PanelSettings, LoginScreen.cs)
  └── RegisterScreen (UIDocument → RegisterScreen.uxml + PanelSettings, RegisterScreen.cs)
```

**CRITICAL — one UIDocument per screen:**
Each screen is its own child GameObject with its own UIDocument. Do NOT share a UIDocument between screens — if two screens use `FindFirstObjectByType<UIDocument>()` and share one document, whichever screen runs Awake second will query the wrong UXML and fail to find its root element. Always use `GetComponent<UIDocument>()` (which works because each screen has its own).

**Adding a new screen (follow every time):**
1. Create `UXML/XxxScreen.uxml` — include `<ui:Style>` for variables.uss + terminal.uss, hardcoded RGB inline styles
2. Create `Screens/XxxScreen.cs` — copy LoginScreen.cs pattern (docRoot fill to 100%, StyleButton, StyleLabels, Register)
3. Add child GameObject `XxxScreen` under NetworkManager
4. Add UIDocument component → assign XxxScreen.uxml + Assets/PanelSettings.asset
5. Add XxxScreen MonoBehaviour to same GameObject
6. Save scene, check console before entering play mode

**USS / Styling:**
- variables.uss — CSS custom properties (NOTE: Unity UI Toolkit does NOT reliably inherit CSS vars — use hardcoded RGB in terminal.uss instead)
- terminal.uss — all hardcoded RGB values, no var() references. Top rule sets font via `* { -unity-font: resource("Fonts & Materials/SourceCodePro-Medium"); }`
- scrollbar.uss — Unity-compatible scrollbar selectors (no webkit)
- ArmsFair.tss — imports all three USS files, assigned to PanelSettings.themeUss

**Font setup (critical):**
- UI Toolkit requires font set via USS `* { -unity-font: resource("Fonts & Materials/SourceCodePro-Medium"); }` in terminal.uss
- The TTF lives at `Assets/TextMesh Pro/Resources/Fonts & Materials/SourceCodePro-Medium.ttf` — `resource()` resolves from any Resources folder
- PanelSettings → Panel Text Settings: assigned `ArmsFairTextSettings.asset` (Assets/Settings/)
- TMP_FontAsset cannot be dragged into PanelTextSettings — they are different types. Font must be set via USS resource() reference to the .ttf directly
- rootVisualElement height fix: LoginScreen.cs sets `docRoot.style.height = Length.Percent(100)` — without this the TemplateContainer has h=0

**LoginScreen status: WORKING** ✓
- Centered panel, correct terminal styling, text visible, inputs functional

**RegisterScreen status: WORKING** ✓
- Same terminal styling as LoginScreen
- Username, Email, Password fields functional
- On success navigates back to Login (temporary — change to MainMenu after Phase 7)
- Back button returns to Login

**NOT yet done (Unity):**
- MainMenuScreen (Phase 7) — after which RegisterScreen's GoTo("Login") becomes GoTo("MainMenu")
- CreateRoomScreen + LobbyApiClient (Phase 8)
- RoomListScreen (Phase 9)
- ProfileScreen (Phase 10)
- PreGameLobbyScreen (Phase 11)
- HUD + all game screens (Phases 12–24)

---

## Known Issues / Gotchas

### CSS Custom Properties Don't Inherit in Unity UI Toolkit
`var(--color-text)` etc. defined on `VisualElement {}` in variables.uss do NOT reliably propagate to child elements. Always use hardcoded RGB values in terminal.uss. Keep variables.uss as a reference document only.

### Button Label Color in Unity UI Toolkit
Unity wraps Button text in an internal Label. To style it, target `.unity-button > .unity-label` or set color on the Button element directly. The `.term-btn` color property may not cascade into the internal label.

### UIManager Execution Order
- UIManager: DefaultExecutionOrder(-100) — initialises first
- Screen MonoBehaviours: default order 0 — register in Awake
- NetworkManagerBootstrap: DefaultExecutionOrder(100) — calls GoTo after all screens registered

### WPM Scroll Wheel Patch
`WPMInternal.cs` line ~553: scroll wheel read must be unconditional.

### EF Core Migrations Never Run
Before deploying: `dotnet ef migrations add InitialSchema --project ArmsFair.Server && dotnet ef database update`

---

## Pending Work (priority order)

1. **Phase 6: RegisterScreen**
3. **Phase 7: MainMenuScreen**
4. **Phase 8: CreateRoomScreen + LobbyApiClient**
5. **Phase 9: RoomListScreen**
6. **Phase 10: ProfileScreen**
7. **Phase 11: PreGameLobbyScreen**
8. **Phase 12+: HUD and game screens**
