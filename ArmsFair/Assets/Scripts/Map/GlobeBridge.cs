using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WPM;
using UnityEngine;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;

namespace ArmsFair.Map
{
    // Lives on the Globe GameObject in MapGlobe scene.
    // Bridges the WPM WorldMapGlobe asset to ArmsFair game logic.
    public class GlobeBridge : MonoBehaviour
    {
        public static GlobeBridge Instance { get; private set; }

        public event System.Action<string, Vector2> OnCountryClicked;
        public event System.Action OnReady;
        public bool IsReady { get; private set; }
        public bool BlockInput { get; set; }

        private WorldMapGlobe _map;
        private IEnumerable<CountryState> _pendingCountries; // cached so InitWPM can replay after WPM is ready
        private int _lastFiredClickIndex = -1;
        private int _lastHoveredIndex   = -1;
        private Coroutine _arcCoroutine;

        // ISO code → WPM country name
        private readonly Dictionary<string, string> _isoToWpm = new();
        // WPM country name → ISO code
        private readonly Dictionary<string, string> _wpmToIso = new();
        // stage colors applied via SetCountryStage, so ClearHighlights can restore them
        private readonly Dictionary<string, Color> _stageColors = new();

        private static readonly Color _defaultHoverColor = new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.55f);

        private static readonly Color[] _playerArcColors =
        {
            new Color(0.25f, 0.75f, 1.00f, 0.90f), // cyan
            new Color(1.00f, 0.40f, 0.25f, 0.90f), // orange-red
            new Color(0.90f, 0.85f, 0.20f, 0.90f), // yellow
            new Color(0.75f, 0.30f, 0.90f, 0.90f), // purple
            new Color(0.25f, 0.90f, 0.55f, 0.90f), // green
            new Color(0.95f, 0.35f, 0.70f, 0.90f), // pink
        };

        // WPM uses different names than the server for these countries
        private static readonly Dictionary<string, string> _wpmNameToIso =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Democratic Republic of the Congo", "COD" },
            { "Republic of Congo",                "COG" },
            { "Ivory Coast",                      "CIV" },
            { "Swaziland",                        "SWZ" },
            { "East Timor",                       "TLS" },
            { "Czech Republic",                   "CZE" },
            { "Guinea Bissau",                    "GNB" },
            { "North Korea",                       "PRK" },
            { "South Korea",                      "KOR" },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Disable old custom globe components; WPM handles rendering
            var gr = GetComponent<GlobeRenderer>();
            if (gr != null) gr.enabled = false;
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var sc = GetComponent<SphereCollider>();
            if (sc != null) sc.enabled = false;

            // Instantiate WPM globe if not already in the scene
            if (GameObject.Find("WorldMapGlobe") == null)
            {
                var prefab = Resources.Load<GameObject>("Prefabs/WorldMapGlobe");
                if (prefab != null)
                {
                    var go = Instantiate(prefab);
                    go.name = "WorldMapGlobe"; // WPM finds itself by this exact name
                }
                else
                {
                    Debug.LogError("[GlobeBridge] Could not load Prefabs/WorldMapGlobe from Resources");
                }
            }
        }

        private void Start()
        {
            // Disable our custom orbit controller; WPM spins the globe by dragging
            var ctrl = FindObjectOfType<GlobeCameraController>(true);
            if (ctrl != null) ctrl.enabled = false;

            StartCoroutine(InitWPM());
        }

        private IEnumerator InitWPM()
        {
            // Retry up to 3 frames — WPM may need more than one frame to finish its own Awake/Start
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return null;
                _map = WorldMapGlobe.instance;
                if (_map != null) break;
            }

            if (_map == null)
            {
                Debug.LogError("[GlobeBridge] WorldMapGlobe.instance still null after 3 frames");
                yield break;
            }

            _map.enableCountryHighlight  = true;
            _map.fillColor               = new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.35f);
            _map.frontiersColor          = new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.6f);
            _map.showFrontiers           = true;
            _map.showCountryNames        = false;
            _map.earthAtmosphereVisible  = false;
            _map.centerOnRightClick      = true;
            _map.allowUserZoom           = false; // GlobeBridge.Update() owns scroll zoom directly
            _map.autoRotationSpeed       = 0f;
            // FOV must be set BEFORE SetZoomLevel — WPM reads fieldOfView to compute frustumDistance.
            // WPM's frustumDistance formula is calibrated for FOV=60°: only at 60° does zoom=1.0
            // place the camera exactly at the no-clipping tangent distance. Higher FOV → camera
            // too close → globe clips. Lower FOV → some clipping but less than higher values.
            if (Camera.main != null) Camera.main.fieldOfView = 60f;
            _map.SetZoomLevel(1.0f); // positions camera at frustumDistance (= 2R at FOV=60°, where sphere fills vertical FOV)
            // Push camera back for a comfortable initial view — frustumDistance (zoom=1) places
            // the globe edge-to-edge; 2.5R gives ~79% fill with breathing room on all sides.
            // Direct position override bypasses WPM's 0-1 zoom clamp.
            if (Camera.main != null)
            {
                float R = _map.transform.localScale.y * 0.5f;
                Vector3 dir = (Camera.main.transform.position - _map.transform.position).normalized;
                Camera.main.transform.position = _map.transform.position + dir * R * 2.5f;
            }

            // Replay RegisterCountries + ApplyAllStages if they were called before WPM was ready
            if (_pendingCountries != null)
            {
                RegisterCountries(_pendingCountries);
                ApplyAllStages(_pendingCountries);
            }

            IsReady = true;
            OnReady?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_map == null) return;

            // Detect a non-drag left-click on a country — only when mouse is over the globe viewport
            int clicked = _map.countryLastClicked;
            if (!BlockInput && _map.mouseIsOver && _map.input.GetMouseButtonUp(0) && !_map.hasDragged && clicked >= 0 && clicked != _lastFiredClickIndex)
            {
                _lastFiredClickIndex = clicked;
                var wpmName = _map.countries[clicked].name;
                var iso = _wpmToIso.TryGetValue(wpmName, out var found) ? found : wpmName;
                OnCountryClicked?.Invoke(iso, (Vector2)_map.input.mousePosition);
            }
            else if (!_map.mouseIsOver)
            {
                _lastFiredClickIndex = -1; // reset when mouse leaves globe so next entry is fresh
            }

            // Stage-tinted hover: boost alpha of existing stage surface color on hover
            int hovered = _map.countryHighlightedIndex;
            if (hovered != _lastHoveredIndex)
            {
                // Re-apply stage surface color for the country we just stopped hovering —
                // WPM's HideCountryRegionHighlight wipes the ToggleCountrySurface custom material on exit
                if (_lastHoveredIndex >= 0)
                {
                    var prevName = _map.countries[_lastHoveredIndex].name;
                    if (_wpmToIso.TryGetValue(prevName, out var prevIso) &&
                        _stageColors.TryGetValue(prevIso, out var prevColor))
                        _map.ToggleCountrySurface(prevName, true, prevColor);
                }

                _lastHoveredIndex = hovered;
                if (hovered >= 0)
                {
                    var wpmName = _map.countries[hovered].name;
                    if (_wpmToIso.TryGetValue(wpmName, out var hovIso) &&
                        _stageColors.TryGetValue(hovIso, out var baseColor))
                    {
                        var hoverColor = baseColor;
                        hoverColor.a = Mathf.Min(baseColor.a + 0.25f, 1f);
                        _map.fillColor = hoverColor;
                    }
                    else
                    {
                        _map.fillColor = _defaultHoverColor;
                    }
                }
                else
                {
                    _map.fillColor = _defaultHoverColor;
                }
            }

            // Direct proportional scroll zoom — bypasses WPM's wheelAccel inertia which
            // fought our distance clamp and caused stuck/jittery behavior at zoom limits.
            // allowUserZoom=false disables WPM's scroll handler so we have sole control.
            if (Camera.main != null)
            {
                float scroll = _map.input.GetAxis("Mouse ScrollWheel");
                if (!BlockInput && Mathf.Abs(scroll) > 0.001f && _map.mouseIsOver)
                {
                    float R    = _map.transform.localScale.y * 0.5f;
                    float dist = Vector3.Distance(Camera.main.transform.position, _map.transform.position);
                    float newDist = Mathf.Clamp(dist * (1f + scroll * 2.0f), R * 1.2f, R * 5.0f);
                    Vector3 dir = (Camera.main.transform.position - _map.transform.position).normalized;
                    Camera.main.transform.position = _map.transform.position + dir * newDist;
                    Camera.main.transform.LookAt(_map.transform.position);
                }
            }
        }

        // Called from HUDScreen on each StateSync to build ISO ↔ WPM name lookups.
        public void RegisterCountries(IEnumerable<CountryState> countries)
        {
            _pendingCountries = countries; // cache so InitWPM can replay if _map wasn't ready yet
            _isoToWpm.Clear();
            _wpmToIso.Clear();
            if (_map?.countries == null) return;

            // Pass 1: exact match — avoids substring ambiguity (e.g. "Congo" matching the wrong Congo)
            var unmatched = new List<CountryState>();
            foreach (var gs in countries)
            {
                bool matched = false;
                for (int i = 0; i < _map.countries.Length; i++)
                {
                    if (string.Equals(_map.countries[i].name, gs.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _isoToWpm[gs.Iso]          = _map.countries[i].name;
                        _wpmToIso[_map.countries[i].name] = gs.Iso;
                        matched = true;
                        break;
                    }
                }
                if (!matched) unmatched.Add(gs);
            }

            // Pass 2: substring match for countries that didn't exact-match
            foreach (var gs in unmatched)
            {
                for (int i = 0; i < _map.countries.Length; i++)
                {
                    var wpmName = _map.countries[i].name;
                    if (wpmName.IndexOf(gs.Name, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        gs.Name.IndexOf(wpmName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _isoToWpm[gs.Iso] = wpmName;
                        _wpmToIso[wpmName] = gs.Iso;
                        break;
                    }
                }
            }

            // Pass 3: static overrides for countries where WPM and server names diverge entirely
            foreach (var (wpmName, iso) in _wpmNameToIso)
            {
                if (_wpmToIso.ContainsKey(wpmName)) continue; // already matched by passes 1 or 2
                _wpmToIso[wpmName] = iso;
                _isoToWpm[iso]     = wpmName;
            }
        }

        public void SetCountryStage(string iso, CountryStage stage)
        {
            if (_map == null) return;
            if (!_isoToWpm.TryGetValue(iso, out var wpmName)) return;

            if (stage == CountryStage.Dormant)
            {
                _stageColors.Remove(iso);
                _map.ToggleCountrySurface(wpmName, false, Color.clear);
                return;
            }

            var color = stage switch
            {
                CountryStage.Simmering          => new Color(0.55f, 0.45f, 0.10f, 0.35f),
                CountryStage.Active             => new Color(0.75f, 0.30f, 0.10f, 0.45f),
                CountryStage.HotWar             => new Color(0.85f, 0.10f, 0.10f, 0.55f),
                CountryStage.HumanitarianCrisis => new Color(0.60f, 0.10f, 0.50f, 0.55f),
                CountryStage.FailedState        => new Color(0.35f, 0.00f, 0.35f, 0.70f),
                _                               => Color.clear
            };

            if (color.a > 0f)
            {
                _stageColors[iso] = color;
                _map.ToggleCountrySurface(wpmName, true, color);
            }
        }

        public void ApplyAllStages(IEnumerable<CountryState> countries)
        {
            if (_map == null) return;
            _stageColors.Clear();
            _map.HideCountrySurfaces();
            foreach (var c in countries)
                SetCountryStage(c.Iso, c.Stage);
        }

        public void PlayArcs(List<ArcAnimation> arcs, IReadOnlyList<PlayerProfile> players)
        {
            if (_map == null) return;
            if (_arcCoroutine != null) StopCoroutine(_arcCoroutine);
            _map.ClearLineMarkers();
            if (arcs == null || arcs.Count == 0) return;
            _arcCoroutine = StartCoroutine(RunArcs(arcs, players));
        }

        private IEnumerator RunArcs(List<ArcAnimation> arcs, IReadOnlyList<PlayerProfile> players)
        {
            // Build player index map so each player gets a consistent color
            var playerColorIndex = new Dictionary<string, int>();
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                    playerColorIndex[players[i].Id] = i;

            int prevDelay = 0;
            foreach (var arc in arcs.OrderBy(a => a.DelayMs))
            {
                int waitMs = arc.DelayMs - prevDelay;
                if (waitMs > 0) yield return new WaitForSeconds(waitMs / 1000f);
                prevDelay = arc.DelayMs;

                PlayerProfile profile = null;
                if (players != null)
                    foreach (var p in players) { if (p.Id == arc.PlayerId) { profile = p; break; } }
                if (profile == null) continue;

                if (!_isoToWpm.TryGetValue(profile.HomeNation, out var srcWpm)) continue;
                if (!_isoToWpm.TryGetValue(arc.TargetIso,     out var dstWpm)) continue;

                int srcIdx = _map.GetCountryIndex(srcWpm);
                int dstIdx = _map.GetCountryIndex(dstWpm);
                if (srcIdx < 0 || dstIdx < 0) continue;

                var start      = _map.countries[srcIdx].center;
                var end        = _map.countries[dstIdx].center;
                int colorIndex = playerColorIndex.TryGetValue(arc.PlayerId, out var ci) ? ci : 0;
                var color      = _playerArcColors[colorIndex % _playerArcColors.Length];

                _map.AddLine(start, end, color, 0.3f, 1.5f, 0.002f, 4f);
            }
            _arcCoroutine = null;
        }

        public void FlyToCountry(string iso)
        {
            if (_map == null || !_isoToWpm.TryGetValue(iso, out var wpmName)) return;
            _map.FlyToCountry(wpmName);
        }

        public void HighlightCountry(string iso)
        {
            if (_map == null || !_isoToWpm.TryGetValue(iso, out var wpmName)) return;
            _map.ToggleCountrySurface(wpmName, true, new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.5f));
        }

        public void ClearHighlights()
        {
            if (_map == null) return;
            _map.HideCountrySurfaces();
            // Restore stage colors — HideCountrySurfaces wipes everything including SetCountryStage overlays
            foreach (var (iso, color) in _stageColors)
            {
                if (_isoToWpm.TryGetValue(iso, out var wpmName))
                    _map.ToggleCountrySurface(wpmName, true, color);
            }
        }
    }
}
