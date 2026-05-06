---
name: add-screen
description: >
  Use this skill whenever adding a new UI screen to the Arms Fair Unity project.
  Trigger on any request like "add a X screen", "create the Y screen", "build the Z page/menu",
  or when a rebuild plan phase involves creating a new UXML + MonoBehaviour screen pair.
  This skill encodes all the hard-won gotchas from Phase 5-6 development — use it every time,
  even if the task seems simple, to avoid the one-UIDocument-per-screen and docRoot-height bugs.
---

# Add Screen — Arms Fair UI Toolkit

Covers the full process: UXML → C# MonoBehaviour → Unity scene wiring via MCP.

## Design system reference

CSS classes (from `terminal.uss` — use these, not the old `.screen`/`.panel` names):

| Purpose | Class |
|---|---|
| Full-screen root | `screen-root` |
| Panel container | `term-panel` |
| Panel widths | `term-panel--narrow` (320px) · `term-panel--medium` (420px) · `term-panel--wide` (520px) |
| Title label | `term-title` |
| Subtitle label | `term-subtitle` |
| Field label (above input) | `term-field-label` |
| Text input | `term-input` |
| Button | `term-btn` |
| Danger button | `term-btn--danger` |
| Back/secondary button | `term-btn--back` |
| Horizontal rule | `term-divider` |
| Error message | `term-error hidden` |
| Hidden utility | `hidden` |

Hardcoded RGB values (CSS vars don't inherit reliably in Unity UI Toolkit — always use these):

| Token | Value |
|---|---|
| Text primary | `rgb(212,207,184)` |
| Text muted | `rgb(138,134,112)` |
| Background dark | `rgb(13,13,13)` |
| Panel background | `rgb(17,17,8)` |
| Input background | `rgb(15,15,8)` |
| Border dim | `rgb(58,58,42)` |
| Border input | `rgb(74,74,48)` |
| Border button | `rgb(90,90,58)` |
| Error text | `rgb(192,144,144)` |

---

## Step 1 — Create the UXML

**File:** `ArmsFair/Assets/Scripts/UI/UXML/XxxScreen.uxml`

Use this template (replace `XxxScreen` and the `term-subtitle` text):

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
  <ui:Style src="../USS/variables.uss" />
  <ui:Style src="../USS/terminal.uss" />
  <ui:VisualElement name="XxxScreen" class="screen-root" style="display:none; position:absolute; left:0; top:0; width:100%; height:100%; flex-direction:column; align-items:center; justify-content:center; background-color:rgb(13,13,13);">
    <ui:VisualElement class="term-panel term-panel--narrow" style="background-color:rgb(17,17,8); border-color:rgb(58,58,42); border-width:1px; padding:20px; flex-direction:column; width:320px;">

      <ui:Label text="THE ARMS FAIR" class="term-title" style="font-size:15px; color:rgb(212,207,184); -unity-font-style:bold; margin-bottom:4px;" />
      <ui:Label text="SCREEN SUBTITLE HERE" class="term-subtitle" style="font-size:9px; color:rgb(138,134,112); margin-bottom:16px;" />
      <ui:VisualElement class="term-divider" style="height:1px; background-color:rgb(58,58,42); margin-bottom:12px;" />

      <!-- Fields go here — copy from LoginScreen/RegisterScreen as needed -->

      <ui:Label name="ErrorLabel" text="" class="term-error hidden" style="color:rgb(192,144,144); font-size:10px; display:none; margin-bottom:8px;" />

      <!-- Primary action button -->
      <ui:Button name="ConfirmBtn" text="ACTION" class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center;" />

      <ui:VisualElement class="term-divider" style="height:1px; background-color:rgb(58,58,42); margin-bottom:12px;" />

      <!-- Back/secondary button -->
      <ui:Button name="BackBtn" text="BACK" class="term-btn" style="color:rgb(212,207,184); -unity-text-color:rgb(212,207,184); background-color:rgb(15,15,8); border-color:rgb(90,90,58); border-width:1px; padding:8px 12px; margin-bottom:8px; font-size:13px; -unity-text-align:middle-center;" />

    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

Key rules:
- Always include both `<ui:Style>` tags at the top
- Always include `display:none` on the root element (UIManager calls Show/Hide)
- Always inline the hardcoded RGB values — USS classes alone are not enough due to Unity cascade bugs
- The root element `name` must match exactly what the C# does `docRoot.Q("XxxScreen")`

---

## Step 2 — Create the MonoBehaviour

**File:** `ArmsFair/Assets/Scripts/UI/Screens/XxxScreen.cs`

Copy the pattern from `LoginScreen.cs` exactly. The critical parts that must be present:

```csharp
private void Awake()
{
    // CRITICAL: GetComponent finds THIS GameObject's UIDocument — never use FindFirstObjectByType
    // when multiple UIDocuments exist (one per screen). Sharing UIDocuments breaks navigation.
    var doc     = GetComponent<UIDocument>();
    var docRoot = doc.rootVisualElement;

    // CRITICAL: Without these, TemplateContainer renders at h=0 and nothing is visible
    docRoot.style.position = Position.Absolute;
    docRoot.style.left     = 0;
    docRoot.style.top      = 0;
    docRoot.style.right    = 0;
    docRoot.style.bottom   = 0;
    docRoot.style.width    = new StyleLength(Length.Percent(100));
    docRoot.style.height   = new StyleLength(Length.Percent(100));

    _root = docRoot.Q("XxxScreen"); // must match UXML element name
    if (_root == null) { Debug.LogError("[XxxScreen] root element not found"); return; }

    _root.style.width  = new StyleLength(Length.Percent(100));
    _root.style.height = new StyleLength(Length.Percent(100));

    // CRITICAL: USS color cascade into Button's internal Label is unreliable in Unity 6.
    // StyleButton() forces colors via C# API as a fallback.
    TerminalUI.StyleButton(_root.Q<Button>("ConfirmBtn"));
    TerminalUI.StyleButton(_root.Q<Button>("BackBtn"));
    TerminalUI.StyleLabels(_root);

    // Wire events
    _root.Q<Button>("ConfirmBtn").clicked += OnConfirm;
    _root.Q<Button>("BackBtn").clicked    += () => UIManager.Instance.GoTo("PreviousScreen");

    UIManager.Instance.Register("XxxScreen", this);
}
```

Use `TerminalUI.StyleButton()`, `TerminalUI.StyleDangerButton()`, and `TerminalUI.StyleLabels()` from `ArmsFair/Assets/Scripts/UI/TerminalUI.cs` — do NOT copy the helpers inline. `TerminalUI` handles colors, hover enter/leave callbacks, and child label colors in one call. This is the single source of truth for all button styling and hover effects.

```csharp
public void Show() { if (_root != null) _root.style.display = DisplayStyle.Flex; }
public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }
```

---

## Step 3 — Wire in Unity via MCP

Run these MCP operations in order (sequential, not parallel):

### 3a. Create the child GameObject
```
manage_gameobject action=create name="XxxScreen" parent="NetworkManager"
```

### 3b. Attach UIDocument
```
manage_ui action=attach_ui_document
  target="XxxScreen"
  source_asset="Assets/Scripts/UI/UXML/XxxScreen.uxml"
  panel_settings="Assets/PanelSettings.asset"
```

### 3c. Add the MonoBehaviour
```
manage_components action=add
  target="XxxScreen"
  component_type="ArmsFair.UI.XxxScreen"
```

### 3d. Save scene and check console
```
execute_code: EditorSceneManager.SaveOpenScenes()
read_console types=["error"] count=10
```

---

## Critical gotchas (do not skip)

**One UIDocument per screen — no sharing.**
Every screen MonoBehaviour must be on its own child GameObject with its own UIDocument. `GetComponent<UIDocument>()` must find the document directly. If `FindFirstObjectByType<UIDocument>()` is used as a fallback and multiple UIDocuments exist, it returns an unpredictable one and the wrong screen's elements are queried, causing "element not found" errors and failed UIManager registration.

**docRoot height must be set in code.**
`position:absolute; top:0; bottom:0` in USS does not expand the TemplateContainer height. You must set `docRoot.style.height = new StyleLength(Length.Percent(100))` in Awake or the screen renders at h=0.

**Inline RGB styles are not optional.**
Unity UI Toolkit does not reliably propagate CSS custom properties (`var()`) to child elements. All color values must be hardcoded `rgb()` inline on each element. The USS classes provide structure; the inline styles provide color.

**Button text color requires C# forcing.**
Unity wraps Button text in an internal Label. Setting `color` on the Button element via USS does not cascade into that Label. Call `StyleButton()` in Awake to force the color via `btn.style.color` and iterate `btn.Children()`.

**Exit play mode before any scene edits.**
Scene saves and component changes made while in play mode are not persisted.

**GoTo a screen that isn't registered yet throws KeyNotFoundException.**
If the new screen navigates to a screen not yet built (e.g., MainMenu in Phase 6 before Phase 7), temporarily use `GoTo("Login")` and leave a `// TODO Phase N: change to GoTo("RealScreen")` comment.
