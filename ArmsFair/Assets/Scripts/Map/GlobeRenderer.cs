using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace ArmsFair.Map
{
    // Attach to the UV-sphere GameObject in MapGlobe scene.
    // Call UpdateCountryTensions() each round from GameClient callbacks.
    public class GlobeRenderer : MonoBehaviour
    {
        [SerializeField] private Renderer globeRenderer;
        [SerializeField] private float globeRadius = 5f;

        public event System.Action<string> OnCountrySelected;

        private MaterialPropertyBlock _props;
        private readonly float[] _tensions = new float[250];
        private Dictionary<string, int> _isoToIndex;
        private Dictionary<string, Vector2> _centroids;
        private string _selectedIso;

        private void Start()
        {
            _props = new MaterialPropertyBlock();
            StartCoroutine(LoadCountryData());
        }

        private IEnumerator LoadCountryData()
        {
            var path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "GeoData/countries.json");

            using var req = UnityWebRequest.Get(path);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GlobeRenderer] Failed to load countries.json: {req.error}");
                yield break;
            }

            _isoToIndex = new Dictionary<string, int>();
            _centroids   = new Dictionary<string, Vector2>();

            var arr = JSON.Parse(req.downloadHandler.text).AsArray;
            for (int i = 0; i < arr.Count; i++)
            {
                var node = arr[i];
                var iso  = node["iso"].Value;
                _isoToIndex[iso] = i;

                var c = node["centroid"];
                if (c != null && c.Count >= 2)
                    _centroids[iso] = new Vector2(c[1].AsFloat, c[0].AsFloat); // lat, lng
            }

            Debug.Log($"[GlobeRenderer] Loaded {_isoToIndex.Count} countries.");
        }

        public void UpdateCountryTensions(Dictionary<string, float> tensionByIso)
        {
            if (_isoToIndex == null) return;

            foreach (var kvp in tensionByIso)
            {
                if (_isoToIndex.TryGetValue(kvp.Key, out int idx) && idx < 250)
                    _tensions[idx] = kvp.Value;
            }

            globeRenderer.GetPropertyBlock(_props);
            _props.SetFloatArray("_CountryTensions", _tensions);
            globeRenderer.SetPropertyBlock(_props);
        }

        private void Update()
        {
            HandleClick();
        }

        private void HandleClick()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;

            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool tapped  = touch != null && touch.primaryTouch.press.wasPressedThisFrame;
            if (!clicked && !tapped) return;

            Vector2 screenPos = clicked
                ? mouse.position.ReadValue()
                : touch.primaryTouch.position.ReadValue();

            var ray = Camera.main.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit)) return;
            if (hit.collider.gameObject != gameObject) return;

            var local = transform.InverseTransformPoint(hit.point).normalized;
            float lat = Mathf.Asin(local.y) * Mathf.Rad2Deg;
            float lng = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

            var iso = LatLngToIso(lat, lng);
            if (iso != null && iso != _selectedIso)
            {
                _selectedIso = iso;
                Debug.Log($"[GlobeRenderer] Selected: {iso}");
                OnCountrySelected?.Invoke(iso);
            }
        }

        private string LatLngToIso(float lat, float lng)
        {
            if (_centroids == null) return null;

            string nearest = null;
            float  minDist = float.MaxValue;

            foreach (var kvp in _centroids)
            {
                float dlat = kvp.Value.x - lat;
                float dlng = kvp.Value.y - lng;
                float dist = dlat * dlat + dlng * dlng;
                if (dist < minDist) { minDist = dist; nearest = kvp.Key; }
            }

            return nearest;
        }

        public string SelectedIso => _selectedIso;
    }
}
