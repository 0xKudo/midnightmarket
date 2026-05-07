# Arms Fair — Full Rebuild Plan
_Written 2026-05-02. Execute every step in order. Never skip a step. When adding navigation screens, add one at a time and confirm it compiles and displays before continuing._

**COMPILE RULE: After every file written or modified, run a build check before moving to the next step. For server files: `dotnet build ArmsFair.Server`. For Unity files: use Unity MCP `GetConsoleLogs` to check for compiler errors. Do not proceed if there are errors.**

## Infrastructure (updated 2026-05-06)

- **Server:** `https://armsfair.laynekudo.com` — Hostinger VPS, Ubuntu 24.04, Docker Compose
- **Database:** PostgreSQL running in Docker container on the VPS — this is the only database. Do not run a local PostgreSQL instance for development; always point at the VPS.
- **Nginx** reverse proxy handles SSL termination and SignalR WebSocket upgrade (`Upgrade` + `Connection` headers, `proxy_read_timeout 86400s`)
- **Unity client server URL:** `https://armsfair.laynekudo.com` hardcoded in AccountManager (SerializeField `serverUrl`) and LobbyApiClient

---

## Critical Unity UI Toolkit Gotchas (learned 2026-05-03 / updated 2026-05-06)

- **CSS custom properties (`var()`) do NOT reliably inherit** in Unity UI Toolkit. Never use `var(--color-text)` etc. in `terminal.uss`. Always use hardcoded `rgb()` values.
- **Button internal Label**: Unity wraps Button text in an internal `Label`. The `color` property on `.term-btn` may not cascade into it. Target it explicitly: `.term-btn > .unity-label { color: rgb(...); }` or set color directly on `.unity-button .unity-label`.
- **TSS theme**: assigned to PanelSettings via `themeUss` serialized property (not a public API). TSS `@import` paths must be relative to the TSS file.
- **USS injection fallback**: Each screen's MonoBehaviour injects `variablesUss` and `terminalUss` directly onto `doc.rootVisualElement.styleSheets` at runtime as a fallback.
- **UIDocument root fill**: Must set `position: absolute; left:0; top:0; right:0; bottom:0` on `rootVisualElement` in code, or via `TemplateContainer` rule in USS.
- **Execution order**: UIManager(-100) → Screens(0, Awake registers) → NetworkManagerBootstrap(+100, Start calls GoTo).
- **AuthApiClient `PostAsync`/`GetAsync` must be instance methods** — they need `_baseUrl` to build the full URL. If `static`, requests go to a bare path with no host and fail silently with no console error.
- **Server auth response is nested** — shape is `{"token":"...","profile":{"id":"...","username":"...","homeNationIso":"..."}}`. `AuthResponse` must have a nested `AuthProfile` class. `JsonUtility.FromJson` does not flatten nested objects into flat fields.
- **Error label visibility** — inline `style="display:none"` in UXML takes precedence over USS class changes. Always show/hide error labels with `label.style.display = DisplayStyle.Flex/None`, never with `AddToClassList`/`RemoveFromClassList("hidden")`.
- **One UIDocument per screen — NO SHARING**: Each screen MonoBehaviour must live on its own child GameObject with its own UIDocument component pointing to its own UXML. If two screens share a UIDocument (or one uses `FindFirstObjectByType<UIDocument>()`), the wrong UXML will be loaded when a second screen is added. `GetComponent<UIDocument>()` must find the document directly on the same GameObject.
- **Adding a new screen checklist** (every screen, no exceptions):
  1. Create `Assets/Scripts/UI/UXML/XxxScreen.uxml` (with `<ui:Style>` tags for variables.uss + terminal.uss, inline hardcoded RGB styles)
  2. Create `Assets/Scripts/UI/Screens/XxxScreen.cs` (copy LoginScreen.cs pattern: docRoot fill, StyleButton, StyleLabels, Register with UIManager)
  3. Add empty child GameObject under `NetworkManager` named `XxxScreen`
  4. Add `UIDocument` component to it — assign `XxxScreen.uxml` + `Assets/PanelSettings.asset`
  5. Add `ArmsFair.UI.XxxScreen` MonoBehaviour to the same GameObject
  6. Save scene, check console for errors before entering play mode

---

## Source Documents
This plan synthesises all of the following:
- `docs/arms_fair_game_spec.md` (v0.3)
- `docs/arms_fair_technical_architecture.md` (v0.3)
- `docs/arms_fair_balance.md` (v0.4)
- `docs/2026-04-30-solo-playability-design.md`
- `docs/plans/2026-04-30-solo-playability.md`
- `.claude/memory/project_menu_uss_design.md`
- `.claude/memory/project_auth_server.md`

---

## Phase 1 — Server Foundation

### 1.1 GameStateService
**File:** `ArmsFair.Server/Services/GameStateService.cs`
- `ConcurrentDictionary<string, GameState> _games`
- `ConcurrentDictionary<string, List<PlayerAction>> _pending`
- `ConcurrentDictionary<string, HashSet<string>> _ceaseFireVoters`
- Methods: `TryGet`, `Set`, `Remove`, `SetPendingAction`, `GetAndClearPendingForGame`, `GetOrAddVoters`, `RemoveVoters`

### 1.2 PhaseOrchestrator
**File:** `ArmsFair.Server/Services/PhaseOrchestrator.cs`
- Injected: `IHubContext<GameHub>`, `IServiceScopeFactory`, `GameStateService`, `ILogger`
- `AdvanceForGameAsync(string gameId)` — pulls from GameStateService, calls AdvanceAsync, writes back
- DB blocks use `await using (var scope = scopeFactory.CreateAsyncScope())`
- All phase logic (WorldUpdate, Reveal, Consequences, Ending) lives here

### 1.3 TickerService
**File:** `ArmsFair.Server/Services/TickerService.cs`
- Hosted service; only tracks `_phaseEnds: ConcurrentDictionary<string, long>`
- On expiry calls `phaseOrchestrator.AdvanceForGameAsync(gameId)`
- Does NOT send PhaseStart itself

### 1.4 GameHub
**File:** `ArmsFair.Server/Hubs/GameHub.cs`
- No static fields — all state via `gameStateService.*`
- `CreateGame` → seeds state via SeedService, calls AdvanceForGameAsync for first phase
- `SubmitAction` → calls `gameStateService.SetPendingAction`
- `VoteCeaseFire` → adds to voters set

### 1.5 Program.cs registrations
```csharp
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddSingleton<PhaseOrchestrator>();
builder.Services.AddSingleton<LobbyService>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddSingleton<TickerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TickerService>());
builder.Services.AddScoped<AuthService>();
```

---

## Phase 2 — Unity Auth Layer

### 2.1 AuthApiClient
`ArmsFair/Assets/Scripts/Auth/AuthApiClient.cs`
- `AuthResult` — plain class (not record), properties: Token, PlayerId, Username, HomeNationIso
- `[Serializable] internal class AuthResponse` — camelCase fields for JsonUtility
- `[Serializable] internal class ProfileResponse` — camelCase fields
- `LoginAsync`, `RegisterAsync`, `GetMeAsync` — all use `UnityWebRequest` + `JsonUtility.FromJson<T>`
- URL passed in full (no base concat in private helpers — pass full URL to UnityWebRequest)

### 2.2 AccountManager
`ArmsFair/Assets/Scripts/Auth/AccountManager.cs`
- Singleton MonoBehaviour, `DontDestroyOnLoad`
- `[SerializeField] string serverUrl = "http://localhost:5002"` (set in Inspector to prevent stale scene value)
- `TryAutoLoginAsync` — reads `PlayerPrefs.GetString("auth_token")`, calls GetMeAsync, then `GameClient.Instance.ConnectAsync`
- `LoginAsync` / `RegisterAsync` — call API, set LocalPlayer, save token, ConnectAsync, invoke OnLoggedIn
- `LogOutAsync` — clears state, DeleteKey, DisconnectAsync, invoke OnLoggedOut

### 2.3 NetworkManagerBootstrap
`ArmsFair/Assets/Scripts/Network/NetworkManagerBootstrap.cs`
- `private async void Start()` calls `AccountManager.Instance.TryAutoLoginAsync()`
- If false → UIManager navigate to Login screen
- If true → UIManager navigate to MainMenu screen
- (UIManager stubs added in Phase 3)

---

## Phase 3 — USS Design System

### 3.1 variables.uss
**STOP — ask user to drop their saved `variables.uss` into `ArmsFair/Assets/Scripts/UI/USS/` before continuing.**
_The user has a pre-approved version of this file outside the project. Do not generate or overwrite it. Wait for them to place it, then verify it loaded in Unity._
_Token values below are for reference only._
```css
:root {
  --color-bg:            #0a0e0a;
  --color-surface:       #111a11;
  --color-border:        #2a3a2a;
  --color-text-primary:  #d4cfb8;
  --color-text-dim:      #7a8a7a;
  --color-accent:        #4a7a4a;
  --color-accent-hover:  #5a9a5a;
  --color-danger:        #8a2a2a;
  --color-warning:       #8a6a2a;
  --font-size-xs:        10px;
  --font-size-sm:        12px;
  --font-size-md:        14px;
  --font-size-lg:        18px;
  --font-size-xl:        24px;
  --font-size-2xl:       32px;
  --spacing-xs:          4px;
  --spacing-sm:          8px;
  --spacing-md:          16px;
  --spacing-lg:          24px;
  --spacing-xl:          40px;
  --btn-transition:      0.15s;
}
.mobile-layout {
  --font-size-md: 16px;
  --spacing-md:   20px;
  --spacing-lg:   32px;
}
```

### 3.2 terminal.uss
**STOP — ask user to drop their saved `terminal.uss` into `ArmsFair/Assets/Scripts/UI/USS/` before continuing.**
_Same rule as variables.uss. Wait for placement, then verify in Unity._
_Styles below are for reference only._
```css
.screen {
  position: absolute;
  left: 0; top: 0; right: 0; bottom: 0;
  background-color: var(--color-bg);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.panel {
  background-color: var(--color-surface);
  border-color: var(--color-border);
  border-width: 1px;
  padding: var(--spacing-lg);
}

.panel--wide { width: 640px; }
.panel--narrow { width: 400px; }

.screen__title {
  color: var(--color-text-primary);
  font-size: var(--font-size-2xl);
  -unity-font-style: bold;
  margin-bottom: var(--spacing-lg);
  letter-spacing: 4px;
  -unity-text-align: upper-center;
}

.screen__subtitle {
  color: var(--color-text-dim);
  font-size: var(--font-size-sm);
  margin-bottom: var(--spacing-xl);
  letter-spacing: 2px;
  -unity-text-align: upper-center;
}

.field__label {
  color: var(--color-text-dim);
  font-size: var(--font-size-xs);
  letter-spacing: 2px;
  margin-bottom: var(--spacing-xs);
}

.field__input {
  background-color: transparent;
  border-color: var(--color-border);
  border-width: 1px;
  color: var(--color-text-primary);
  font-size: var(--font-size-md);
  padding: var(--spacing-sm) var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.field__input:focus {
  border-color: var(--color-accent);
}

.btn {
  background-color: transparent;
  border-color: var(--color-accent);
  border-width: 1px;
  color: var(--color-accent);
  font-size: var(--font-size-sm);
  letter-spacing: 2px;
  padding: var(--spacing-sm) var(--spacing-lg);
  transition-duration: var(--btn-transition);
  cursor: pointer;
}

.btn:hover {
  background-color: var(--color-accent);
  color: var(--color-bg);
}

.btn--danger {
  border-color: var(--color-danger);
  color: var(--color-danger);
}

.btn--danger:hover {
  background-color: var(--color-danger);
  color: var(--color-bg);
}

.btn--full { width: 100%; }

.error-label {
  color: var(--color-danger);
  font-size: var(--font-size-xs);
  letter-spacing: 1px;
  display: none;
}

.error-label--visible { display: flex; }

.divider {
  height: 1px;
  background-color: var(--color-border);
  margin-top: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}
```

---

## Phase 4 — UIManager + IScreen

### 4.1 IScreen interface
**Create:** `ArmsFair/Assets/Scripts/UI/IScreen.cs`
```csharp
namespace ArmsFair.UI
{
    public interface IScreen
    {
        void Show();
        void Hide();
    }
}
```

### 4.2 UIManager
**Create:** `ArmsFair/Assets/Scripts/UI/UIManager.cs`
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ArmsFair.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        // All registered screens set via Inspector or RegisterScreen calls
        private readonly Dictionary<string, IScreen> _screens = new();
        private readonly Stack<string> _history = new();
        private string _current;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Register(string name, IScreen screen) => _screens[name] = screen;

        public void GoTo(string name)
        {
            if (_current != null && _screens.TryGetValue(_current, out var old))
                old.Hide();
            _history.Clear();
            _current = name;
            _screens[name].Show();
        }

        public void Push(string name)
        {
            if (_current != null && _screens.TryGetValue(_current, out var old))
            {
                old.Hide();
                _history.Push(_current);
            }
            _current = name;
            _screens[name].Show();
        }

        public void Pop()
        {
            if (_history.Count == 0) return;
            if (_current != null && _screens.TryGetValue(_current, out var old))
                old.Hide();
            _current = _history.Pop();
            _screens[_current].Show();
        }
    }
}
```

### 4.3 Update NetworkManagerBootstrap
Update `ArmsFair/Assets/Scripts/Network/NetworkManagerBootstrap.cs` to call UIManager:
```csharp
private async void Start()
{
    bool autoLoggedIn = await AccountManager.Instance.TryAutoLoginAsync();
    if (!autoLoggedIn)
        UIManager.Instance.GoTo("Login");
    else
        UIManager.Instance.GoTo("MainMenu");
}
```

### 4.4 Scene setup
In `Bootstrap.unity`, the `NetworkManager` GameObject should have:
- `AccountManager` component
- `UIManager` component
- `GameClient` component
- `NetworkManagerBootstrap` component
- One `UIDocument` component (single document for all UI)

**⚠ CONFIRM:** Bootstrap scene compiles with no errors before Phase 5.

---

## Phase 5 — Screen 1: Login Screen

### 5.1 LoginScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/LoginScreen.uxml`
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
  <ui:VisualElement name="LoginScreen" class="screen" style="display:none;">
    <ui:VisualElement class="panel panel--narrow">
      <ui:Label text="THE ARMS FAIR" class="screen__title" />
      <ui:Label text="SECURE TERMINAL ACCESS" class="screen__subtitle" />
      <ui:Label text="IDENTIFIER" class="field__label" />
      <ui:TextField name="UsernameField" class="field__input" />
      <ui:Label text="PASSWORD" class="field__label" />
      <ui:TextField name="PasswordField" password="true" class="field__input" />
      <ui:Label name="ErrorLabel" text="" class="error-label" />
      <ui:Button name="LoginBtn" text="AUTHENTICATE" class="btn btn--full" />
      <ui:VisualElement class="divider" />
      <ui:Button name="RegisterBtn" text="CREATE ACCOUNT" class="btn btn--full" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 5.2 LoginScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/LoginScreen.cs`
```csharp
using ArmsFair.Auth;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class LoginScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private TextField     _usernameField;
        private TextField     _passwordField;
        private Label         _errorLabel;

        private void Awake()
        {
            var doc  = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            _root    = doc.rootVisualElement.Q("LoginScreen");
            _usernameField = _root.Q<TextField>("UsernameField");
            _passwordField = _root.Q<TextField>("PasswordField");
            _errorLabel    = _root.Q<Label>("ErrorLabel");

            _root.Q<Button>("LoginBtn").clicked    += OnLogin;
            _root.Q<Button>("RegisterBtn").clicked += () => UIManager.Instance.GoTo("Register");

            UIManager.Instance.Register("Login", this);
        }

        public void Show() => _root.style.display = DisplayStyle.Flex;
        public void Hide() => _root.style.display = DisplayStyle.None;

        private async void OnLogin()
        {
            _errorLabel.RemoveFromClassList("error-label--visible");
            try
            {
                await AccountManager.Instance.LoginAsync(_usernameField.value, _passwordField.value);
                UIManager.Instance.GoTo("MainMenu");
            }
            catch (System.Exception ex)
            {
                _errorLabel.text = ex.Message.Contains("401") ? "INVALID CREDENTIALS" : "CONNECTION ERROR";
                _errorLabel.AddToClassList("error-label--visible");
            }
        }
    }
}
```

### 5.3 Register LoginScreen GameObject in Bootstrap
- Add empty child GameObject to `NetworkManager` called `LoginScreen`
- Add `LoginScreen` MonoBehaviour
- _(UIDocument shared from parent — LoginScreen queries by name)_

**⚠ CONFIRM:** Login screen shows, accepts input, shows error on bad creds, navigates on success.

---

## Phase 6 — Screen 2: Register Screen

> **Gotchas (learned Phase 5):**
> - CSS class names must match `terminal.uss` — use `screen-root`, `term-panel`, `term-panel--narrow`, `term-title`, `term-subtitle`, `term-field-label`, `term-input`, `term-btn`, `term-divider`, `term-error hidden`. NOT the old `.screen` / `.panel` / `.field__label` names.
> - USS `<ui:Style>` tags must be included in the UXML header (same as LoginScreen).
> - The screen's MonoBehaviour **must** set `docRoot.style.height = Length.Percent(100)` (and width) in Awake, or the TemplateContainer renders at h=0.
> - Call `StyleButton()` and `StyleLabels()` in Awake — USS color cascade on Button internal labels is unreliable; C# forced colors are the fallback.
> - `AccountManager.Instance.RegisterAsync(username, email, password)` — three params.
> - `GoTo("MainMenu")` will throw `KeyNotFoundException` because MainMenu isn't registered until Phase 7. **Temporarily** redirect to `GoTo("Login")` on success — change it to `GoTo("MainMenu")` after Phase 7 is done.
> - New child GameObject needs its own `UIDocument` component with RegisterScreen.uxml assigned and the same PanelSettings (ConstantPixelSize 1.5×) set.

### 6.1 RegisterScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/RegisterScreen.uxml`
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
  <ui:Style src="../USS/variables.uss" />
  <ui:Style src="../USS/terminal.uss" />
  <ui:VisualElement name="RegisterScreen" class="screen-root" style="display:none; position:absolute; left:0; top:0; width:100%; height:100%; flex-direction:column; align-items:center; justify-content:center; background-color:rgb(13,13,13);">
    <ui:VisualElement class="term-panel term-panel--narrow" style="background-color:rgb(17,17,8); border-color:rgb(58,58,42); border-width:1px; padding:20px; flex-direction:column; width:320px;">
      <ui:Label text="THE ARMS FAIR" class="term-title" style="font-size:15px; color:rgb(212,207,184); -unity-font-style:bold; margin-bottom:4px;" />
      <ui:Label text="NEW OPERATIVE REGISTRATION" class="term-subtitle" style="font-size:9px; color:rgb(138,134,112); margin-bottom:16px;" />
      <ui:VisualElement class="term-divider" style="height:1px; background-color:rgb(58,58,42); margin-bottom:12px;" />
      <ui:Label text="USERNAME" class="term-field-label" style="font-size:9px; color:rgb(138,134,112); margin-bottom:4px;" />
      <ui:TextField name="UsernameField" class="term-input" style="margin-bottom:12px; color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(74,74,48); border-width:1px;" />
      <ui:Label text="EMAIL" class="term-field-label" style="font-size:9px; color:rgb(138,134,112); margin-bottom:4px;" />
      <ui:TextField name="EmailField" class="term-input" style="margin-bottom:12px; color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(74,74,48); border-width:1px;" />
      <ui:Label text="PASSWORD" class="term-field-label" style="font-size:9px; color:rgb(138,134,112); margin-bottom:4px;" />
      <ui:TextField name="PasswordField" password="true" class="term-input" style="margin-bottom:12px; color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(74,74,48); border-width:1px;" />
      <ui:Label name="ErrorLabel" text="" class="term-error hidden" style="color:rgb(192,144,144); font-size:10px; display:none; margin-bottom:8px;" />
      <ui:Button name="RegisterBtn" text="REGISTER" class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center; -unity-font-style:normal;" />
      <ui:VisualElement class="term-divider" style="height:1px; background-color:rgb(58,58,42); margin-bottom:12px;" />
      <ui:Button name="BackBtn" text="BACK TO LOGIN" class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center; -unity-font-style:normal;" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 6.2 RegisterScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/RegisterScreen.cs`
- Identical structure to LoginScreen.cs — copy and adapt
- In Awake: set `docRoot.style.width/height = Length.Percent(100)` and `_root.style.width/height = Length.Percent(100)`
- Call `StyleButton()` on both buttons and `StyleLabels()` in Awake
- `AccountManager.Instance.RegisterAsync(username, email, password)` — three params
- On success: `GoTo("Login")` for now — **change to `GoTo("MainMenu")` after Phase 7**
- `BackBtn.clicked` → `GoTo("Login")`
- Registers as `"Register"`

### 6.3 Register RegisterScreen GameObject in Bootstrap
- Add empty child GameObject to `NetworkManager` called `RegisterScreen`
- Add `UIDocument` component — assign `RegisterScreen.uxml` as Source Asset
- Set PanelSettings to the same asset used by LoginScreen (ConstantPixelSize 1.5×, ArmsFair.tss)
- Add `RegisterScreen` MonoBehaviour to the same GameObject

**⚠ CONFIRM:** Register screen shows (same terminal styling as Login), creates account, returns to Login screen. No compile errors.

---

## Phase 7 — Screen 3: Main Menu Screen

> **Gotchas (learned Phase 5–6):**
> - Use correct CSS class names: `screen-root`, `term-panel`, `term-panel--narrow`, `term-title`, `term-subtitle`, `term-btn`, `term-btn--danger`. NOT old `.screen`/`.panel`/`.btn--danger` names.
> - Include `<ui:Style>` tags for variables.uss + terminal.uss in UXML.
> - All colors must be hardcoded inline RGB — no var() references.
> - Awake must set `docRoot.style.width/height = Length.Percent(100)` and `_root.style.width/height = Length.Percent(100)`.
> - Call `StyleButton()` on all buttons and `StyleLabels()` in Awake.
> - `LogoutBtn` is a danger button — use `StyleDangerButton()` (separate helper with `rgb(192,144,144)` text/border).
> - `GoTo("CreateRoom")`, `GoTo("RoomList")`, `Push("Profile")` will throw until Phases 8–10 are done. Log a warning and return for now; add `// TODO Phase N` comments.
> - `WelcomeLabel` must be updated in `Show()` not `Awake()` — player may not be set yet at Awake time. Null-guard: `AccountManager.Instance.LocalPlayer?.Username ?? "UNKNOWN"`.
> - After Phase 7 is confirmed working, update RegisterScreen.cs: change `GoTo("Login")` → `GoTo("MainMenu")` on register success.

### 7.1 MainMenuScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/MainMenuScreen.uxml`
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
  <ui:Style src="../USS/variables.uss" />
  <ui:Style src="../USS/terminal.uss" />
  <ui:VisualElement name="MainMenuScreen" class="screen-root" style="display:none; position:absolute; left:0; top:0; width:100%; height:100%; flex-direction:column; align-items:center; justify-content:center; background-color:rgb(13,13,13);">
    <ui:VisualElement class="term-panel term-panel--narrow" style="background-color:rgb(17,17,8); border-color:rgb(58,58,42); border-width:1px; padding:20px; flex-direction:column; width:320px;">
      <ui:Label text="THE ARMS FAIR" class="term-title" style="font-size:15px; color:rgb(212,207,184); -unity-font-style:bold; margin-bottom:4px;" />
      <ui:Label name="WelcomeLabel" text="OPERATIVE: —" class="term-subtitle" style="font-size:9px; color:rgb(138,134,112); margin-bottom:16px;" />
      <ui:VisualElement class="term-divider" style="height:1px; background-color:rgb(58,58,42); margin-bottom:12px;" />
      <ui:Button name="CreateRoomBtn" text="CREATE ROOM" class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center;" />
      <ui:Button name="JoinRoomBtn"   text="JOIN ROOM"   class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center;" />
      <ui:Button name="ProfileBtn"    text="PROFILE"     class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center;" />
      <ui:VisualElement class="term-divider" style="height:1px; background-color:rgb(58,58,42); margin-bottom:12px;" />
      <ui:Button name="LogoutBtn"     text="DISCONNECT"  class="term-btn term-btn--danger" style="color:rgb(192,144,144); -unity-text-color:rgb(192,144,144); background-color:rgb(15,15,8); border-color:rgb(90,42,42); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center;" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 7.2 MainMenuScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/MainMenuScreen.cs`
- Copy LoginScreen.cs pattern: docRoot fill, StyleButton, StyleLabels, Register
- Add `StyleDangerButton()` helper — same as `StyleButton()` but uses `rgb(192,144,144)` for text/border color
- In `Show()`: set `WelcomeLabel.text = $"OPERATIVE: {AccountManager.Instance.LocalPlayer?.Username?.ToUpper() ?? "UNKNOWN"}"`
- `CreateRoomBtn.clicked` → `// TODO Phase 8: GoTo("CreateRoom")` — log warning for now
- `JoinRoomBtn.clicked` → `// TODO Phase 9: GoTo("RoomList")` — log warning for now
- `ProfileBtn.clicked` → `// TODO Phase 10: Push("Profile")` — log warning for now
- `LogoutBtn.clicked` → `await AccountManager.Instance.LogOutAsync()` → `GoTo("Login")`
- Registers as `"MainMenu"`

### 7.3 Register MainMenuScreen GameObject in Bootstrap
- Add empty child GameObject to `NetworkManager` called `MainMenuScreen`
- Add `UIDocument` component — assign `MainMenuScreen.uxml` + `Assets/PanelSettings.asset`
- Add `MainMenuScreen` MonoBehaviour to the same GameObject
- Save scene, check console for errors

### 7.4 Update RegisterScreen after confirming MainMenu works
In `RegisterScreen.cs` `OnRegister()`: change `GoTo("Login")` → `GoTo("MainMenu")`

**⚠ CONFIRM:** MainMenu shows with player username after login/register, logout returns to Login, unbuilt buttons log a warning instead of throwing.

---

## Phase 8 — Screen 4: Create Room Screen

### Phase 8 Gotchas (pre-flight 2026-05-06)

- **`JsonUtility` cannot deserialize `List<string>`** — declare `PlayerIds` as `string[]` in `RoomInfo`. `List<string>` silently deserializes as null.
- **Server returns PascalCase record fields serialized as camelCase** — `System.Text.Json` defaults to camelCase for record types. Unity `[Serializable]` field names must be camelCase: `roomId`, `hostPlayerId`, `playerIds`, etc.
- **Two different server response shapes**: `GET /api/rooms` returns `RoomSummary` (has `playerCount` int, no `playerIds`). `GET /api/rooms/{id}` and `POST /api/rooms` return `RoomRecord` (has `playerIds string[]`). Use two separate C# classes: `RoomSummary` and `RoomInfo`.
- **NationDropdown choices must be set in C# Awake(), not UXML** — the ISO list is too long for UXML. Leave UXML `NationDropdown` with no choices attribute. Populate via `dropdown.choices = new List<string>{...}` in Awake.
- **Pre-filling NationDropdown** — `dropdown.value = "XX — Country"` only works if the exact string is already in `.choices`. Set `.choices` first, then `.value`.
- **`CreateBtn` navigates to `"PreGameLobby"` — screen not registered until Phase 11** — for now, log `Debug.LogWarning("TODO Phase 11: GoTo PreGameLobby")` and do NOT call `UIManager.Instance.GoTo("PreGameLobby")`. Add actual navigation in Phase 11.
- **`BackBtn → Pop()`** — requires `MainMenuScreen` to navigate here via `Push("CreateRoom")` not `GoTo`. Update `MainMenuScreen.cs` `CreateRoomBtn.clicked` to call `UIManager.Instance.Push("CreateRoom")`.
- **`voiceEnabled` not in UXML** — `CreateRoomRequest` on server has a `VoiceEnabled` bool. It has no default in the record but C# bool defaults to `false` when omitted from JSON. Send `voiceEnabled: false` in the payload to be explicit.
- **Token in Authorization header** — all lobby endpoints require `Authorization: Bearer <token>`. Use `AccountManager.Instance.Token` (public property, confirmed). Set on the `UnityWebRequest` via `request.SetRequestHeader("Authorization", $"Bearer {token}")`.
- **`LobbyApiClient` must use instance methods** — same bug as `AuthApiClient`. `PostAsync`/`GetAsync` must be instance methods so `_baseUrl` is accessible. Never make them static.
- **`GameMode` enum must be sent as integer** — server has no `JsonStringEnumConverter`. Sending `"gameMode":"Realistic"` causes 400. Send integer: Realistic=1, EqualWorld=2, BlankSlate=3, HotWorld=4, Custom=5. In `CreateRoomPayload`, declare `gameMode` as `int` and map with a `Dictionary<string,int>`.
- **Do NOT use `DropdownField` for styled selections** — Unity's DropdownField popup renders in a separate overlay outside USS scope and cannot be styled to match the terminal aesthetic. Use a `Button` showing the current value that opens a custom modal overlay instead.
- **Modal overlay positioning** — use `left:0; top:0; right:0; bottom:0` on absolute-positioned overlays, NOT `width:100%; height:100%`. Percentage dimensions on absolute elements do not fill the parent reliably in Unity UI Toolkit.
- **ScrollView height in modals** — parent panel needs explicit pixel `height` + `overflow:hidden`. Set ScrollView height in BOTH UXML and C# (call `_choiceList.style.height = new StyleLength(Xf)` when opening the modal). `flex-grow:1` alone is unreliable.
- **Search bar** — only show for long lists (nations). Short lists (slots 2-6, timers, game modes) do not need search. Control via `showSearch` bool parameter on `OpenModal()`.
- **Home Nation removed from CreateRoomScreen** — belongs in ProfileScreen (Phase 10) with company name. Do not add it back here.
- **Nations use ISO 3166-1 alpha-3 codes** — format `"USA — United States"` (3-letter code, not 2-letter). ~155 countries in CreateRoomScreen.cs, organised by region.
- **scrollbar.uss must use hardcoded RGB** — the original file used `var()` references which don't resolve. All scrollbar USS rules must use `rgb()` values. Arrow buttons hidden with `display:none`.

**⚠ PHASE 8 STATUS: COMPLETE** ✓
- LobbyApiClient.cs — create/list/get/join rooms, Bearer auth, RoomInfo + RoomSummary response types
- CreateRoomScreen.uxml + CreateRoomScreen.cs — modal-based selections, toggle buttons, posts to `/api/rooms`
- CreateBtn logs `TODO Phase 11: GoTo PreGameLobby with roomId=...` on success
- MainMenuScreen updated to use `Push("CreateRoom")`

### 8.1 CreateRoomScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/CreateRoomScreen.uxml`
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="CreateRoomScreen" class="screen" style="display:none;">
    <ui:VisualElement class="panel panel--wide">
      <ui:Label text="CREATE ROOM" class="screen__title" />
      <ui:Label text="ROOM NAME" class="field__label" />
      <ui:TextField name="RoomNameField" class="field__input" />
      <ui:Label text="NATION" class="field__label" />
      <ui:DropdownField name="NationDropdown" class="field__input" />
      <ui:Label text="COMPANY NAME" class="field__label" />
      <ui:DropdownField name="CompanyDropdown" class="field__input" />
      <ui:Label text="PLAYER SLOTS (2–6)" class="field__label" />
      <ui:SliderInt name="SlotsSlider" low-value="2" high-value="6" value="4" class="field__input" />
      <ui:Label text="TIMER PRESET" class="field__label" />
      <ui:DropdownField name="TimerDropdown" class="field__input" />
      <ui:Label text="GAME MODE" class="field__label" />
      <ui:DropdownField name="GameModeDropdown" class="field__input" />
      <ui:Toggle name="PrivateToggle" label="PRIVATE ROOM" class="field__input" />
      <ui:Toggle name="AiFillToggle"  label="AI FILL-IN"   class="field__input" />
      <ui:Label name="ErrorLabel" text="" class="error-label" style="display:none;" />
      <ui:Button name="CreateBtn" text="CREATE" class="btn btn--full" />
      <ui:Button name="BackBtn"   text="BACK"   class="btn btn--full" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

### 8.2 LobbyApiClient
**Create:** `ArmsFair/Assets/Scripts/Network/LobbyApiClient.cs`
- All calls use `UnityWebRequest` (not HttpClient) — instance methods only, never static
- Constructor takes `string baseUrl`
- `CreateRoomAsync(CreateRoomPayload payload) → RoomInfo` — POST `/api/rooms`, returns `RoomInfo` (full RoomRecord shape)
- `ListRoomsAsync() → RoomSummary[]` — GET `/api/rooms`, returns `RoomSummary[]` (different shape — has `playerCount` not `playerIds`)
- `GetRoomAsync(string id) → RoomInfo` — GET `/api/rooms/{id}`
- `JoinRoomAsync(string id) → RoomInfo` — POST `/api/rooms/{id}/join`
- Token read from `AccountManager.Instance.Token` internally — set as `Authorization: Bearer <token>` header on every request
- `[Serializable] class RoomInfo` fields (camelCase, matching server RoomRecord): `roomId`, `roomName`, `hostPlayerId`, `hostUsername`, `timerPreset`, `inviteCode`, `gameMode`, `playerSlots`, `isPrivate`, `aiFillIn`, `isStarted`, `playerIds` as `string[]`
- `[Serializable] class RoomSummary` fields (camelCase, matching server RoomSummary): `roomId`, `roomName`, `hostUsername`, `gameMode`, `timerPreset`, `playerSlots`, `playerCount`, `isPrivate`, `isStarted`, `inviteCode`
- `[Serializable] class CreateRoomPayload` fields: `roomName`, `playerSlots`, `timerPreset`, `voiceEnabled` (always false), `aiFillIn`, `isPrivate`, `gameMode` (string)
- For array responses (`ListRoomsAsync`): wrap in `{"items":[...]}` on the server side OR use a `RoomSummaryList` wrapper class with a `items` field — `JsonUtility` cannot deserialize a bare JSON array. **Use the wrapper approach client-side.**

### 8.3 CreateRoomScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/CreateRoomScreen.cs`
- In Awake: populate all dropdowns via `.choices` lists:
  - `NationDropdown.choices` = hardcoded list of `"ISO — Full Name"` strings (20–30 key nations)
  - `CompanyDropdown.choices` = 10 preset arms company names
  - `TimerDropdown.choices` = `new List<string>{"60s","90s","120s","180s"}` (set `.value = "90s"` as default)
  - `GameModeDropdown.choices` = `new List<string>{"Realistic","EqualWorld","BlankSlate","HotWorld"}`
- In `Show()`: pre-fill nation from `AccountManager.Instance.LocalPlayer?.HomeNation` — find matching entry in NationDropdown.choices by prefix, fall back to choices[0]
- `CreateBtn.clicked` → validate RoomNameField not empty → build `CreateRoomPayload` → `await LobbyApiClient.CreateRoomAsync(payload)` → on success log `Debug.LogWarning("TODO Phase 11: GoTo PreGameLobby with roomId")` — do NOT navigate yet
- `BackBtn.clicked` → `UIManager.Instance.Pop()`
- ErrorLabel: use `style.display = DisplayStyle.Flex/None` — never AddToClassList
- `timerPreset`: read `TimerDropdown.value` directly (already a string like `"90s"`)
- `gameMode`: read `GameModeDropdown.value` directly (string passed to server)
- Registers as `"CreateRoom"`
- Also update `MainMenuScreen.cs` `CreateRoomBtn.clicked` to use `Push("CreateRoom")` not `GoTo`

**⚠ CONFIRM:** Room created, server returns roomId logged to console, Back returns to MainMenu.

---

## Phase 9 — Screen 5: Room List Screen ✅ COMPLETE (2026-05-06)

### Phase 9 Gotchas (pre-flight 2026-05-06)

- **`RoomSummary` has `playerCount` (int), NOT `playerIds`** — `GET /api/rooms` returns the summary shape. Row label must read `room.playerCount` not `room.playerIds.Length`. Do NOT use `RoomInfo` here.
- **UXML uses old USS class names** — the original spec UXML used `.screen`, `.panel`, `.btn` etc. which are not in the actual USS files. Follow the hardcoded-RGB pattern from CreateRoomScreen/LoginScreen instead. Import variables.uss + terminal.uss + scrollbar.uss in UXML.
- **Room rows built entirely in C#** — same pattern as `PopulateChoiceList` in CreateRoomScreen. Do not try to define row structure in UXML. Clear the ScrollView, loop `RoomSummary[]`, create a `VisualElement` row per room with a Label + JOIN Button, style inline.
- **`Show()` must be `async void`** to `await ListRoomsAsync()`. Guard with a loading/empty state label so the ScrollView isn't blank while fetching.
- **JOIN navigates to `"PreGameLobby"` — not registered until Phase 11** — log `Debug.LogWarning("TODO Phase 11: GoTo PreGameLobby with roomId=...")` and do NOT call GoTo. Same pattern as CreateRoomScreen's OnCreate success path.
- **Join by invite code uses `JoinRoomAsync(code)`** — the server's `/api/rooms/{id}/join` accepts either roomId or inviteCode. Pass the TextField value directly; same TODO log on success.
- **`_lobby` instance must be created in Awake** — same as CreateRoomScreen (`new LobbyApiClient("https://armsfair.laynekudo.com")`). Do not use static methods.
- **ScrollView height** — set explicit pixel height in UXML (`height:400px`) and `overflow:hidden` on the parent panel. Do not rely on `flex-grow:1` alone.
- **Update `MainMenuScreen.cs`** — `JoinRoomBtn.clicked` currently logs a TODO. Change to `UIManager.Instance.Push("RoomList")`.

### 9.1 RoomListScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/RoomListScreen.uxml`
- Import variables.uss, terminal.uss, scrollbar.uss
- Root element: `name="RoomListScreen"` — `display:none; position:absolute; left:0; top:0; width:100%; height:100%` — centered column
- FormPanel: `width:480px` — slightly wider than CreateRoom to fit room rows
- Header: "OPEN ROOMS" title + subtitle "SELECT AN ENGAGEMENT"
- Divider line
- Join-by-code row: `InviteCodeField` TextField + `JoinByCodeBtn` Button — side by side (`flex-direction:row`)
- Divider line
- Status label `name="StatusLabel"` — shows "LOADING..." / "NO OPEN ROOMS" / empty
- `ScrollView name="RoomList"` — `vertical-scroller-visibility:AlwaysVisible; height:360px; overflow:hidden`
- Bottom buttons: `RefreshBtn` + `BackBtn` — full width, stacked

### 9.2 RoomListScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/RoomListScreen.cs`
- Copy Awake pattern from CreateRoomScreen (docRoot fill, Q elements, Register as "RoomList")
- `_lobby = new LobbyApiClient("https://armsfair.laynekudo.com")` in Awake
- `Show()` is `async void`: set `StatusLabel.text = "LOADING..."`, clear RoomList, call `await _lobby.ListRoomsAsync()`, then call `PopulateRoomList(rooms)`
- `PopulateRoomList(RoomSummary[] rooms)`: if empty → `StatusLabel.text = "NO OPEN ROOMS"`. Otherwise hide StatusLabel and build one row per room:
  - Row: `VisualElement` flex-row, space-between, border `rgb(58,58,42)`, padding 8px, margin-bottom 4px
  - Left: `Label` — `"{room.roomName}  [{room.playerCount}/{room.playerSlots}]  {room.gameMode}"` — color `rgb(212,207,184)`
  - Right: `Button` — "JOIN" — green border `rgb(58,90,42)`, color `rgb(138,184,112)` — clicked → `OnJoin(room.roomId)`
- `OnJoin(string roomId)` is `async void`: call `await _lobby.JoinRoomAsync(roomId)` → `Debug.LogWarning($"TODO Phase 11: GoTo PreGameLobby with roomId={roomId}")`; catch → show error
- `JoinByCodeBtn.clicked` → `OnJoin(_inviteCodeField.value.Trim())`
- `RefreshBtn.clicked` → call `Show()` (re-triggers the async fetch)
- `BackBtn.clicked` → `UIManager.Instance.Pop()`
- ErrorLabel: shown on join failure; hidden on Show()

### 9.3 Update MainMenuScreen
In `MainMenuScreen.cs` `OnJoinRoom()`: change `Debug.LogWarning("TODO Phase 9...")` → `UIManager.Instance.Push("RoomList")`

### 9.4 Register RoomListScreen in Bootstrap
- Add child GameObject `RoomListScreen` under NetworkManager
- Add UIDocument component → assign RoomListScreen.uxml + Assets/PanelSettings.asset
- Add RoomListScreen MonoBehaviour
- Save scene, check console before play mode

**⚠ CONFIRM:** Room list populates with rooms from VPS, JOIN logs TODO with roomId, join by code logs TODO, Refresh re-fetches, Back returns to MainMenu.

**STATUS: CONFIRMED WORKING** ✓
- RoomListScreen.uxml + RoomListScreen.cs created
- Bootstrap scene wired via Unity MCP (GameObject + UIDocument + MonoBehaviour)
- Duplicate empty MainMenuScreen GameObject cleaned up
- MainMenuScreen.OnJoinRoom updated to Push("RoomList")
- async Show() fetches from VPS, displays rows with player count from RoomSummary.playerCount

---

## Phase 10 — Screen 6: Profile Screen ✅ COMPLETE (2026-05-06)

### Phase 10 Notes (updated 2026-05-06)
Home Nation selection was originally in CreateRoomScreen but was moved here — it belongs with the company/operative profile setup, not room configuration.

### 10.1 ProfileScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/ProfileScreen.uxml`
- Shows: Username, HomeNation (display), CompanyName (display), Capital, Reputation, SharePrice, PeaceCredits, LatentRisk, Status
- If HomeNation or CompanyName not yet set: show a setup section with modal selectors (same modal pattern as CreateRoomScreen)
- Single BACK button → Pop

### 10.2 ProfileScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/ProfileScreen.cs`
- On Show: read `AccountManager.Instance.LocalPlayer` and bind labels
- Home Nation selector: reuse the same Nations list + modal pattern from CreateRoomScreen (ISO alpha-3 codes, search bar, ~155 countries)
- Company Name: free text field the player types (not a dropdown — player creates their own brokerage name per the game spec)
- `BackBtn.clicked` → Pop
- Registers as `"Profile"`

**⚠ CONFIRM:** Profile screen shows correct data, Home Nation can be selected, Back returns to previous screen.

**STATUS: CONFIRMED WORKING** ✓
- ProfileScreen.uxml + ProfileScreen.cs created
- NationsList.cs extracted as shared static (~155 countries, ISO alpha-3)
- CompanyName added to PlayerEntity + all auth responses on server
- POST /api/auth/profile endpoint added (NOT PATCH — UnityWebRequest drops auth headers on PATCH)
- Server endpoint reads player from ctx.User claims, not manual token re-validation
- AddCompanyName EF migration applied on VPS
- Success feedback is a modal popup with OK button (not inline label)
- Bootstrap scene wired via Unity MCP
- MainMenuScreen PROFILE button wired to Push("Profile")

---

## Phase 11 — Screen 7: Pre-Game Lobby Screen

### 11.1 PreGameLobbyScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/PreGameLobbyScreen.uxml`
- Room info panel: room name, invite code, slot count
- Player list: ScrollView showing each player's name + ready badge
- Host controls (only shown when `AccountManager.Instance.LocalPlayer.Id == room.hostPlayerId`):
  - START GAME button
- READY toggle for non-hosts
- LEAVE button

### 11.2 PreGameLobbyScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/PreGameLobbyScreen.cs`
- On Show: receive roomId, fetch room via `LobbyApiClient.GetRoomAsync`
- Subscribe to SignalR `PlayerJoined`, `PlayerReady`, `GameStarting` events from `GameClient`
- `StartGameBtn.clicked` (host only) → calls `GameClient.Instance.Hub.InvokeAsync("CreateGame", roomId, gameMode)`
- On `GameStarting` event → GoTo HUD
- `LeaveBtn.clicked` → Pop back to previous screen
- Registers as `"PreGameLobby"`

**⚠ CONFIRM:** Lobby shows players, invite code displays, host can start game, transitions to HUD.

---

## Phase 12 — GameClient SignalR Wiring

### 12.1 GameClient
**Create/overwrite:** `ArmsFair/Assets/Scripts/Network/GameClient.cs`
- Singleton MonoBehaviour, `DontDestroyOnLoad`
- `ConnectAsync(string token)` — connect to `ws://localhost:5002/gamehub?access_token={token}`
- `DisconnectAsync()` — stop connection
- `Hub` property exposes `HubConnection` for direct invocations
- Events (C# Action<T> or UnityEvent<T>):
  - `OnPhaseStart(PhaseStartMessage)`
  - `OnWorldUpdate(WorldUpdateMessage)`
  - `OnReveal(RevealMessage)`
  - `OnConsequences(ConsequencesMessage)`
  - `OnGameEnding(GameEndingMessage)`
  - `OnStateSync(GameState)`
  - `OnPlayerJoined(string playerId)`
  - `OnPlayerReady(string playerId)`
  - `OnChatMsg(ChatMessage)`
- SignalR DLL: use the compiled DLL in `Assets/Plugins/` — NOT `Microsoft.AspNetCore.SignalR.Client` NuGet (already removed from Unity project)

**⚠ NOTE:** If SignalR DLLs were deleted from `Assets/Plugins/` (seen in git status), re-copy them from the server's NuGet cache or rebuild the shared DLL pipeline. Check `Assets/Plugins/` contents before proceeding.

---

## Phase 13 — HUD (Game Screen)

### 13.1 HUD UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/HUDScreen.uxml`
Structure:
```
<HUDScreen>
  <TopBar>
    <GlobalTracks>   (MarketHeat, CivilianCost, Stability, SanctionsRisk, GeoTension)
    <PhaseTimer>     (phase name + countdown)
    <RoundCounter>   (ROUND N)
  </TopBar>
  <WorldMapArea>     (placeholder; WPM globe goes here)
  <PlayerDashboard>  (Capital, Reputation, SharePrice, PeaceCredits, LatentRisk)
  <ActionPanel>      (phase-specific controls — hidden by default)
  <ChatPanel>        (side panel, collapsible)
  <PhaseOverlays>    (Procurement, Sales, Reveal, Consequences — shown one at a time)
</HUDScreen>
```

### 13.2 HUDScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/HUDScreen.cs`
- On Show: subscribe to all GameClient events
- On Hide: unsubscribe
- `OnPhaseStart` → update phase label + timer + show/hide ActionPanel sub-sections
- `OnStateSync` → refresh player dashboard, track bars
- Registers as `"HUD"`

### 13.3 PhaseTimer
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/PhaseTimer.cs`
- `MonoBehaviour` that ticks countdown from `endsAt` (Unix ms) using `DateTimeOffset.UtcNow`
- Updates a label every second

### 13.4 TrackBar
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/TrackBar.cs`
- Fills a ProgressBar element 0–100
- Color threshold: green <60, yellow 60–80, red >80

### 13.5 PlayerDashboard
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/PlayerDashboard.cs`
- Binds to `AccountManager.Instance.LocalPlayer`
- Updates on `OnConsequences` event

**⚠ CONFIRM:** HUD shows, phase timer ticks, track bars update on WorldUpdate.

---

## Phase 14 — Procurement Phase UI

### 14.1 ProcurementPanel UXML
Inside HUDScreen, show when phase == Procurement:
```xml
<VisualElement name="ProcurementPanel">
  <Label text="PROCUREMENT" class="screen__title" />
  <ScrollView name="WeaponList" />
  <Label name="CapitalLabel" />
  <Button name="ConfirmProcBtn" text="CONFIRM ORDERS" class="btn btn--full" />
</VisualElement>
```
Weapon rows populated from `WeaponCatalog` (hardcoded C# list matching balance sheet):
- MANPADS, Light Arms, Heavy Weapons, Artillery, Armored Vehicles, Air Defense, Combat Aircraft, Naval Systems, Cyber/EW, CBRN
- Each row: name, base cost, BUY toggle

### 14.2 ProcurementPanel MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/ProcurementPanel.cs`
- Builds weapon rows dynamically
- `ConfirmProcBtn.clicked` → call `GameClient.Instance.Hub.InvokeAsync("SubmitProcurement", selectedWeapons)`
- Server deducts capital; client waits for `OnConsequences` or `OnStateSync` to update display
- Balance: base cost $2–18M per weapon category (see balance sheet)

---

## Phase 15 — Sales Phase UI + State Machine

### 15.1 SalesPanel UXML
Inside HUDScreen, show when phase == Sales:
```xml
<VisualElement name="SalesPanel">
  <Label text="SUBMIT SALES ORDER — SEALED" class="screen__title" />
  <Label text="SELECT WEAPON CATEGORY" class="field__label" />
  <DropdownField name="WeaponDropdown" />
  <Label text="SALE TYPE" class="field__label" />
  <RadioButtonGroup name="SaleTypeGroup">
    <RadioButton text="OPEN SALE"       value="Open" />
    <RadioButton text="COVERT SALE"     value="Covert" />
    <RadioButton text="AID COVER"       value="AidCover" />
    <RadioButton text="PEACE BROKER"    value="PeaceBroker" />
  </RadioButtonGroup>
  <Toggle name="DualSupplyToggle" label="DUAL SUPPLY" />
  <Toggle name="ProxyToggle"      label="GRAY CHANNEL (PROXY ROUTED)" />
  <Label text="TARGET COUNTRY (click globe)" class="field__label" />
  <Label name="TargetLabel" text="— NONE SELECTED —" />
  <Label name="EstimateLabel" text="" />
  <Button name="SubmitSaleBtn" text="SUBMIT SEALED ORDER" class="btn btn--full" />
  <Button name="PassBtn"       text="PASS THIS ROUND"     class="btn btn--full" />
</VisualElement>
```

### 15.2 SalesPanel MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/SalesPanel.cs`
- Globe click → sets `TargetLabel.text` via `WPMGlobeBridge` callback
- `WeaponDropdown` + `SaleTypeGroup` drive profit estimate shown in `EstimateLabel`
  - Estimate formula from balance: `ProfitEngine.Calculate(weapon, countryStage, saleType, isDualSupply, marketHeat, relPoints: 0)`
- `SubmitSaleBtn.clicked` → `GameClient.Instance.Hub.InvokeAsync("SubmitAction", playerAction)`
- `PassBtn.clicked` → submit null action (or do nothing)
- PeaceBroker selection → hide country/weapon fields, show cost warning ($2M deducted)

### 15.3 PlayerAction SignalR payload
`ArmsFair.Shared.Models.Messages.ClientMessages.cs`:
```csharp
public class SubmitActionRequest
{
    public string SaleType       { get; set; }
    public string WeaponCategory { get; set; }
    public string TargetCountry  { get; set; }
    public bool   IsDualSupply   { get; set; }
    public bool   IsProxyRouted  { get; set; }
}
```

**⚠ CONFIRM:** Sales panel shows, globe click sets target, submit sends to server.

---

## Phase 16 — Reveal Overlay

### 16.1 RevealOverlay UXML
Inside HUDScreen, shown during Reveal phase:
```xml
<VisualElement name="RevealOverlay" class="screen" style="display:none;">
  <Label text="REVEAL" class="screen__title" />
  <ScrollView name="ActionList" />
  <!-- Arc animations drawn in WorldMapArea via WPMGlobeBridge -->
</VisualElement>
```

### 16.2 RevealOverlay MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/RevealOverlay.cs`
- Subscribes to `GameClient.OnReveal`
- Populates `ActionList` with rows: `"{companyName} → {targetIso} ({saleType} {weaponCategory})"`
- Passes `ArcAnimation` list to `WPMGlobeBridge.PlayArcs(animations)`
- Auto-hides after `Balance.PhaseReveal` ms

---

## Phase 17 — Consequences Overlay

### 17.1 ConsequencesOverlay UXML
Inside HUDScreen, shown during Consequences phase:
```xml
<VisualElement name="ConsequencesOverlay" class="screen" style="display:none;">
  <Label text="CONSEQUENCES" class="screen__title" />
  <ScrollView name="ProfitList" />
  <ScrollView name="BlowbackList" />
  <ScrollView name="RepList" />
</VisualElement>
```

### 17.2 ConsequencesOverlay MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/ConsequencesOverlay.cs`
- Subscribes to `GameClient.OnConsequences`
- Populates ProfitList: `"+${profit}M → total ${newTotal}M"` per player
- Populates BlowbackList: covert traced events per player
- Populates RepList: reputation changes
- Auto-hides after `Balance.PhaseConsequences` ms

**⚠ CONFIRM:** After Sales → Reveal → Consequences cycle, correct data shown.

---

## Phase 18 — Globe Integration

### 18.1 WPMGlobeBridge
**File:** `ArmsFair/Assets/Scripts/Globe/WPMGlobeBridge.cs` (verify exists and exposes required API; write if missing)
- Must expose:
  - `event Action<string> OnCountryClicked` (fires ISO-3166-1 alpha-2)
  - `void SetCountryStage(string iso, CountryStage stage)` — updates country color/texture
  - `void PlayArcs(List<ArcAnimation> arcs)` — draws sales arcs during Reveal
  - `void HighlightCountry(string iso)`
  - `void ClearHighlights()`

### 18.2 CountryInfoCard UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/CountryInfoCard.uxml`
```xml
<VisualElement name="CountryInfoCard" class="panel" style="display:none; position:absolute;">
  <Label name="CountryName" class="screen__title" />
  <Label name="StageLabel" />
  <Label name="TensionLabel" />
  <Button name="CloseBtn" text="CLOSE" class="btn" />
</VisualElement>
```

### 18.3 CountryInfoCard MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/CountryInfoCard.cs`
- Subscribes to `WPMGlobeBridge.OnCountryClicked` when HUD is showing
- Opens at screen position near click: `style.left = x; style.top = y`
- Looks up country data from last `OnStateSync` GameState
- `CloseBtn.clicked` → hide

### 18.4 Wire WorldUpdate → Globe
In `HUDScreen.OnWorldUpdate`:
```csharp
foreach (var change in msg.CountryChanges)
    WPMGlobeBridge.Instance.SetCountryStage(change.Iso, (CountryStage)change.NewStage);
```

**⚠ CONFIRM:** Click on country opens info card. WorldUpdate changes country colors.

---

## Phase 19 — Negotiation Phase UI

### 19.1 NegotiationPanel UXML
Inside HUDScreen, shown during Negotiation phase:
- Intel tab: shows current track values, last round's revealed actions
- Peace Proposal tab: VoteCeaseFire button, current voters count
- Treaty tab (future): placeholder

### 19.2 NegotiationPanel MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/NegotiationPanel.cs`
- Intel: reads last cached `GameState` tracks
- Peace Proposal: `VoteBtn.clicked` → `Hub.InvokeAsync("VoteCeaseFire")`; button disabled after vote
- Shows current cease-fire voter count from last StateSync

---

## Phase 20 — WorldUpdate Overlay

### 20.1 WorldUpdateOverlay
Inside HUDScreen, briefly shown during WorldUpdate phase:
- List of `SpreadEvent` rows: `"{sourceIso} conflict spreads to {targetIso} (Stage {newStage})"`
- List of `CountryChange` rows
- Track delta summary
- Auto-hides after 3 seconds

---

## Phase 21 — Chat System

### 21.1 ChatPanel UXML
Side panel in HUDScreen:
```xml
<VisualElement name="ChatPanel" style="display:none;">
  <DropdownField name="ChannelDropdown" choices="All,Team,Whisper" />
  <ScrollView name="MessageList" />
  <TextField name="ChatInput" />
  <Button name="SendBtn" text="SEND" class="btn" />
  <Button name="ToggleBtn" text="CHAT ▶" class="btn" />
</VisualElement>
```

### 21.2 ChatPanel MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/ChatPanel.cs`
- `ToggleBtn.clicked` → toggle panel visibility
- Subscribes to `GameClient.OnChatMsg`
- `SendBtn.clicked` → `Hub.InvokeAsync("SendChat", channel, message, targetPlayerId)`
- Rate limit: client-side 1 message per 0.5s
- Whisper: only shown to target player (server filters)

### 21.3 Server: ChatHub method
In `GameHub.cs`:
```csharp
public async Task SendChat(string gameId, string channel, string message, string? targetPlayerId)
{
    // Rate limiting: 1 msg per 500ms per player tracked in GameStateService
    // channel: "all" → group send, "team" → filtered, "whisper" → target only
}
```

---

## Phase 22 — Special Actions

### 22.1 SpecialActionsPanel UXML
Available during Negotiation phase (side panel or modal):
- Whistleblowing (3 levels: Tip-Off, Evidence Package, Full Exposure)
- Coup d'état
- Manufactured Demand
- Peacekeeping Investment
- Reconstruction Contract

### 22.2 SpecialActionsPanel MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/HUD/SpecialActionsPanel.cs`
- Each action shows cost + probability of success
- Costs (from balance sheet):
  - Tip-Off: $3M, Evidence Package: $8M, Full Exposure: $15M
  - Coup: $20–40M depending on country stage
  - Manufactured Demand: $10–25M
  - Peacekeeping Investment: $10M
  - Reconstruction Contract: $24M over 3 rounds
- On confirm → `Hub.InvokeAsync("SubmitSpecialAction", actionType, targetCountry, level)`

### 22.3 Server: SpecialAction processing
In `PhaseOrchestrator.RunRevealAsync`:
- Add handling for `SpecialActionType` enum
- Coup: 5 outcomes (success+stable, success+chaos, fail+exposed, fail+escalation, neutral)
  - Random roll against `CoupSuccessChance` (base 40%, modified by country stage)
- Manufactured Demand: bump target country Tension +15, unlock +10% profit to that zone for 2 rounds
- Peacekeeping: -10 GeoTension globally, +1 PeaceCredit
- Reconstruction: +$8M/round × 3, -5 CivilianCost/round

---

## Phase 23 — Track Threshold Events

### 23.1 ThresholdEventBroadcaster
In `PhaseOrchestrator.RunWorldUpdate`, after track mutations:
```csharp
// Check thresholds and emit events
var events = TrackThresholdChecker.Check(previousTracks, newTracks);
foreach (var evt in events)
    await hub.Clients.Group(gameId).SendAsync("TrackEvent", evt);
```

### 23.2 TrackThresholdChecker
**Create:** `ArmsFair.Server/Simulation/TrackThresholdChecker.cs`
Thresholds (from balance sheet):
- MarketHeat ≥ 60: "UN General Assembly Discussion"
- CivilianCost ≥ 75: "International Court Filing"
- Stability ≤ 25: "Global Stability Warning"
- SanctionsRisk ≥ 80: "Emergency Security Council Session"
- GeoTension ≥ 90: "Great Power Confrontation Warning"

### 23.3 Client: TrackEventNotification
In HUDScreen, subscribe to `GameClient.OnTrackEvent`:
- Show toast notification at top of screen for 4 seconds
- Color: yellow for warnings, red for critical

---

## Phase 24 — Ending Conditions

### 24.1 EndingChecker
**Create/verify:** `ArmsFair.Server/Simulation/EndingChecker.cs` — all endings must be implemented:
- **Total War:** GeoTension ≥ 100 OR ≥ 3 countries at Stage 6 (FailedState)
- **Global Sanctions:** SanctionsRisk ≥ 100
- **Market Saturation:** MarketHeat ≥ 100 AND all players at rep < 30
- **Negotiated Peace:** CeaseFire vote passed by all surviving players
- **Great Power Confrontation:** GeoTension ≥ 90 AND any single player controls > 60% MarketHeat contribution
- **World Peace:** Stability ≥ 80 AND GeoTension ≤ 20 AND ≥ 1 player with 5+ PeaceCredits

### 24.2 Ending broadcast
`PhaseOrchestrator.TriggerEndingAsync` broadcasts `GameEnding` with `FinalScore` list.

### 24.3 GameEndingScreen UXML
**Create:** `ArmsFair/Assets/Scripts/UI/UXML/GameEndingScreen.uxml`
- Shows ending type name + flavor text
- Leaderboard: sorted by Composite score (Profit × Reputation / 100)
- Legacy score column: PeaceCredits × 10
- RETURN TO MENU button

### 24.4 GameEndingScreen MonoBehaviour
**Create:** `ArmsFair/Assets/Scripts/UI/Screens/GameEndingScreen.cs`
- Subscribes to `GameClient.OnGameEnding` (registers handler in Awake, not Show)
- On receive: GoTo "GameEnding", populate scores
- `ReturnBtn.clicked` → GoTo MainMenu, disconnect
- Registers as `"GameEnding"`

**⚠ CONFIRM:** Game can complete a full cycle: start → phases → ending → debrief → menu.

---

## Phase 25 — Dev Mode

### 25.1 DevModeToggle
**Create:** `ArmsFair/Assets/Scripts/Debug/DevMode.cs`
- `Ctrl+D` toggles dev overlay
- Dev overlay (shown above HUD):
  - Track value sliders (MarketHeat, CivilianCost, Stability, SanctionsRisk, GeoTension)
  - Country stage dropdown + SET button
  - Capital +/- buttons
  - SKIP PHASE button → `Hub.InvokeAsync("DebugSkipPhase")`
  - TRIGGER ENDING dropdown

### 25.2 Server: DebugSkipPhase
In `GameHub.cs` (dev builds only, guarded by `#if DEBUG`):
```csharp
public async Task DebugSkipPhase(string gameId)
{
    await phaseOrchestrator.AdvanceForGameAsync(gameId);
}
```

**⚠ NOTE:** Remove `#if DEBUG` guard before shipping. Keep it during development.

---

## Phase 26 — Shared DLL Pipeline

### 26.1 ArmsFair.Shared project
`ArmsFair.Shared/` — class library targeting `netstandard2.1`
All models must use:
- Plain classes with `[Serializable]` for Unity JSON compat
- No `record` keyword
- No `init` setters
- No top-level statements

After any change to `ArmsFair.Shared`:
1. `dotnet build ArmsFair.Shared`
2. Copy `ArmsFair.Shared.dll` → `ArmsFair/Assets/Plugins/ArmsFair.Shared.dll`
3. Restart Unity or wait for reimport

### 26.2 SignalR client DLLs
Required DLLs in `ArmsFair/Assets/Plugins/`:
- `Microsoft.AspNetCore.SignalR.Client.dll`
- `Microsoft.AspNetCore.SignalR.Client.Core.dll`
- `Microsoft.AspNetCore.SignalR.Common.dll`
- `Microsoft.AspNetCore.SignalR.Protocols.Json.dll`
- `Microsoft.AspNetCore.Http.Connections.Client.dll`
- `Microsoft.AspNetCore.Http.Connections.Common.dll`
- `Microsoft.AspNetCore.Connections.Abstractions.dll`
- `Microsoft.Bcl.AsyncInterfaces.dll`
- `Microsoft.Bcl.TimeProvider.dll`
- `Microsoft.Extensions.DependencyInjection.dll`
- `Microsoft.Extensions.DependencyInjection.Abstractions.dll`

If missing: copy from `~/.nuget/packages/microsoft.aspnetcore.signalr.client/{version}/lib/netstandard2.1/`

---

## Phase 27 — Balance Constants (verify all are in code)

**File:** `ArmsFair.Shared/Balance.cs`
All constants from `docs/arms_fair_balance.md` must be present:

```csharp
// Starting values
StartingCapital      = 50_000_000
StartingReputation   = 75
StartingSharePrice   = 100

// Phase durations (ms)
PhaseWorldUpdate     = 15_000
PhaseProcurement     = 60_000   // "standard" preset
PhaseNegotiation     = 60_000
PhaseSales           = 60_000
PhaseReveal          = 15_000
PhaseConsequences    = 15_000

// Track starting values (Realistic mode)
RealisticMarketHeat  = 30
RealisticCivilianCost = 20
RealisticStability   = 25
RealisticSanctionsRisk = 10
RealisticGeoTension  = 35

// Stage multipliers [Dormant, Tension, CivilUnrest, HotWar, HumanitarianCrisis, FailedState]
StageMultipliers     = [0f, 0.5f, 1.0f, 1.8f, 2.2f, 0f]

// Profit base by weapon (USD millions)
// MANPADS=4, LightArms=4, HeavyWeapons=8, Artillery=10, ArmoredVehicles=12,
// AirDefense=18, CombatAircraft=34, NavalSystems=28, CyberEW=20, CBRN=25

// Procurement base cost by weapon (approx 50% of profit base)
// MANPADS=2, LightArms=2, HeavyWeapons=4, Artillery=5, ArmoredVehicles=6,
// AirDefense=9, CombatAircraft=18, NavalSystems=14, CyberEW=10, CBRN=12

// Blowback
LatentPerCovert      = 5
LatentPerGrayChannel = 3
CollapseThreshold    = 10
RepGainPeaceBroker   = 5
RepLossOpenSale      = -2
PeaceCostToPlayer    = 2_000_000
PeaceCreditEarned    = 1

// Spread
SpreadHighStabilityThresh = 60

// Ending thresholds
EndingTotalWarGeoTension   = 100
EndingTotalWarFailedStates = 3
EndingSanctionsRisk        = 100
EndingMarketHeat           = 100
EndingCeasefireRepThresh   = 10
EndingWorldPeaceStability  = 80
EndingWorldPeaceGeoTension = 20
EndingWorldPeacePeaceCredits = 5
```

---

## Phase 28 — Game Modes

### 28.1 Seeding by mode
In `SeedService.SeedGameAsync(GameMode mode)`:
- **Realistic:** call ACLED API + GPI data for country stages and tension; if unavailable, fall back to stored snapshot
- **EqualWorld:** all countries at Stage 1 (Tension), all tracks at 20
- **BlankSlate:** all countries at Stage 0 (Dormant), all tracks at minimum (10/5/10/0/10)
- **HotWorld:** all countries at Stage 2–3 (random), tracks at 50/40/30/30/60
- **Custom/Scenario:** load from scenario JSON if provided

### 28.2 SeedService update
**File:** `ArmsFair.Server/Services/SeedService.cs`
- Verify GameMode enum switch handles all 4 modes
- Add ACLED HTTP client call (rate-limited, cached in Redis with 24h TTL)

---

## Phase 29 — Stats Persistence

### 29.1 Player stats updated at game end
In `PhaseOrchestrator.TriggerEndingAsync`, after DB update:
```csharp
// Update per-player lifetime stats
foreach (var score in scores)
{
    var player = await db.Players.FindAsync(score.PlayerId);
    if (player is null) continue;
    player.GamesPlayed++;
    player.TotalProfit  += score.Profit;
    player.BestRep       = Math.Max(player.BestRep, score.Reputation);
    player.PeaceCredits += score.Legacy / 10L;
    if (scores.OrderByDescending(s => s.Composite).First().PlayerId == score.PlayerId)
        player.Wins++;
    await db.SaveChangesAsync();
}
```

### 29.2 Stats shown in ProfileScreen
Add to ProfileScreen: GamesPlayed, Wins, TotalProfit, BestRep, PeaceCredits lifetime.

---

## Phase 30 — Final Polish and Known Gaps

### 30.1 PanelSettings (UI Toolkit scaling)
- In Project Settings → UI Toolkit → PanelSettings: `ConstantPixelSize`, scale = 1.5
- Do NOT use ScaleWithScreenSize (causes layout breakage)

### 30.2 Server URL
- `AccountManager.serverUrl` default = `"http://localhost:5002"`
- Set in Bootstrap scene Inspector (do not hardcode in C#)

### 30.3 Redis optional
- Server should not throw if Redis is unavailable
- Wrap `ConnectionMultiplexer.Connect` in try/catch; if fails, use in-memory fallback for chat rate-limiting

### 30.4 Vivox voice chat
- Not implemented in this plan. Stub `VoiceEnabled` toggle in CreateRoomScreen as visual-only.
- Full Vivox integration is post-MVP.

### 30.5 WebGL compatibility
- `UnityWebRequest` (not HttpClient) — already enforced
- SignalR: use LongPolling fallback if WebSocket unavailable
  - `HubConnectionBuilder.WithUrl(url, opts => opts.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling)`

### 30.6 Mobile input
- CSS `.mobile-layout` class applied by `MobileDetector` MonoBehaviour
- Touch inputs map to mouse equivalents via Unity's Input System

---

## Execution Order Summary

| # | Phase | Key deliverable | Confirm before next |
|---|-------|----------------|---------------------|
| 1 | Server Foundation | GameStateService, PhaseOrchestrator, TickerService | Server starts, phases advance |
| 2 | Unity Auth | AuthApiClient, AccountManager | Login + register work |
| 3 | USS | variables.uss, terminal.uss | USS loads, no compile errors |
| 4 | UIManager + IScreen | Navigation infrastructure | Bootstrap routes to Login/MainMenu |
| 5 | Login Screen | LoginScreen.uxml + .cs | Login works end-to-end |
| 6 | Register Screen | RegisterScreen.uxml + .cs | Register works |
| 7 | Main Menu | MainMenuScreen.uxml + .cs | Menu shows, logout works |
| 8 | Create Room | CreateRoomScreen.uxml, LobbyApiClient | Room created server-side |
| 9 | Room List | RoomListScreen.uxml + .cs | Rooms listed, join works |
| 10 | Profile | ProfileScreen.uxml + .cs | Profile data shows |
| 11 | Pre-Game Lobby | PreGameLobbyScreen.uxml + .cs | Players shown, game starts |
| 12 | GameClient | SignalR events wired | HUD receives PhaseStart |
| 13 | HUD | HUDScreen.uxml + .cs + PhaseTimer | HUD shows, timer ticks |
| 14 | Procurement | ProcurementPanel | Capital deducted |
| 15 | Sales | SalesPanel + state machine | Action submitted, sealed |
| 16 | Reveal | RevealOverlay + arcs | Actions shown on reveal |
| 17 | Consequences | ConsequencesOverlay | Profit/rep updates shown |
| 18 | Globe | WPMGlobeBridge + CountryInfoCard | Country click opens card |
| 19 | Negotiation | NegotiationPanel + ceaseFire vote | Vote sent to server |
| 20 | WorldUpdate | WorldUpdateOverlay | Spread events displayed |
| 21 | Chat | ChatPanel | Messages sent/received |
| 22 | Special Actions | SpecialActionsPanel | Coup/whistle processed |
| 23 | Track Events | ThresholdEventBroadcaster | Toasts appear |
| 24 | Endings | EndingChecker + GameEndingScreen | Full game cycle complete |
| 25 | Dev Mode | DevMode.cs + DebugSkipPhase | Skip phase works |
| 26 | Shared DLL | Build pipeline documented | DLL copies correctly |
| 27 | Balance | All constants verified | Numbers match spec |
| 28 | Game Modes | SeedService per mode | Realistic + BlankSlate seed |
| 29 | Stats | Lifetime stats persisted | Profile shows totals |
| 30 | Polish | Scaling, Redis fallback, WebGL | Full solo test passes |
