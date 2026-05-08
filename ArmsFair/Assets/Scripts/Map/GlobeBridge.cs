using System.Collections;
using System.Collections.Generic;
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

        private WorldMapGlobe _map;

        // ISO code → WPM country name
        private readonly Dictionary<string, string> _isoToWpm = new();
        // WPM country name → ISO code
        private readonly Dictionary<string, string> _wpmToIso = new();

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

            // Give WPM one frame to finish its own Awake/Start before we configure it
            StartCoroutine(InitWPM());
        }

        private IEnumerator InitWPM()
        {
            yield return null; // wait one frame

            _map = WorldMapGlobe.instance;
            if (_map == null)
            {
                Debug.LogError("[GlobeBridge] WorldMapGlobe.instance still null after one frame");
                yield break;
            }

            _map.enableCountryHighlight  = true;
            _map.fillColor               = new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.35f);
            _map.frontiersColor          = new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.6f);
            _map.showFrontiers           = true;
            _map.showCountryNames        = false;
            _map.earthAtmosphereVisible  = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_map == null) return;

            // Detect a non-drag left-click on a country
            if (_map.input.GetMouseButtonUp(0) && !_map.hasDragged && _map.countryLastClicked >= 0)
            {
                var wpmName = _map.countries[_map.countryLastClicked].name;
                var iso = _wpmToIso.TryGetValue(wpmName, out var found) ? found : wpmName;
                OnCountryClicked?.Invoke(iso, (Vector2)_map.input.mousePosition);
            }
        }

        // Called from HUDScreen on each StateSync to build ISO ↔ WPM name lookups.
        public void RegisterCountries(IEnumerable<CountryState> countries)
        {
            _isoToWpm.Clear();
            _wpmToIso.Clear();
            if (_map?.countries == null) return;

            foreach (var gs in countries)
            {
                for (int i = 0; i < _map.countries.Length; i++)
                {
                    var wpmName = _map.countries[i].name;
                    if (string.Equals(wpmName, gs.Name, System.StringComparison.OrdinalIgnoreCase) ||
                        wpmName.IndexOf(gs.Name, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        gs.Name.IndexOf(wpmName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _isoToWpm[gs.Iso] = wpmName;
                        _wpmToIso[wpmName] = gs.Iso;
                        break;
                    }
                }
            }
        }

        public void SetCountryStage(string iso, CountryStage stage)
        {
            if (_map == null) return;
            if (!_isoToWpm.TryGetValue(iso, out var wpmName)) return;

            var color = stage switch
            {
                CountryStage.Dormant            => new Color(0.20f, 0.35f, 0.20f, 0.25f),
                CountryStage.Simmering          => new Color(0.55f, 0.45f, 0.10f, 0.35f),
                CountryStage.Active             => new Color(0.75f, 0.30f, 0.10f, 0.45f),
                CountryStage.HotWar             => new Color(0.85f, 0.10f, 0.10f, 0.55f),
                CountryStage.HumanitarianCrisis => new Color(0.60f, 0.10f, 0.50f, 0.55f),
                CountryStage.FailedState        => new Color(0.35f, 0.00f, 0.00f, 0.70f),
                _                               => Color.clear
            };

            if (color.a > 0f)
                _map.ToggleCountrySurface(wpmName, true, color);
        }

        public void PlayArcs(List<ArcAnimation> arcs, IReadOnlyList<PlayerProfile> players) { }

        public void HighlightCountry(string iso)
        {
            if (_map == null || !_isoToWpm.TryGetValue(iso, out var wpmName)) return;
            _map.ToggleCountrySurface(wpmName, true, new Color(138f / 255f, 184f / 255f, 112f / 255f, 0.5f));
        }

        public void ClearHighlights() => _map?.HideCountrySurfaces();
    }
}
