using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmsFair.Map
{
    // Attach to the same GameObject as MapLoader.
    // Detects click/tap on country meshes via Physics2D and fires OnCountrySelected.
    [RequireComponent(typeof(MapLoader))]
    public class CountrySelector : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera;
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f);

        public event Action<string> OnCountrySelected;

        private string _selectedIso;
        private Color _previousColor;
        private MeshRenderer _selectedRenderer;

        private void Awake()
        {
            if (mapCamera == null)
                mapCamera = Camera.main;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var touch = Touchscreen.current;

            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool tapped  = touch != null && touch.primaryTouch.press.wasPressedThisFrame;

            if (!clicked && !tapped) return;

            Vector2 screenPos = clicked
                ? mouse.position.ReadValue()
                : touch.primaryTouch.position.ReadValue();

            var worldPos = mapCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            var hit = Physics2D.OverlapPoint(worldPos);
            if (hit == null) return;

            var iso = hit.gameObject.name.Replace("Country_", "");
            SelectCountry(iso, hit.gameObject);
        }

        private void SelectCountry(string iso, GameObject go)
        {
            if (_selectedRenderer != null)
                _selectedRenderer.material.SetColor("_BaseColor", _previousColor);

            _selectedIso = iso;
            _selectedRenderer = go.GetComponent<MeshRenderer>();

            if (_selectedRenderer != null)
            {
                _previousColor = _selectedRenderer.material.GetColor("_BaseColor");
                _selectedRenderer.material.SetColor("_BaseColor", highlightColor);
            }

            Debug.Log($"[CountrySelector] Selected: {iso}");
            OnCountrySelected?.Invoke(iso);
        }

        public string SelectedIso => _selectedIso;

        public void ClearSelection()
        {
            if (_selectedRenderer != null)
                _selectedRenderer.material.SetColor("_BaseColor", _previousColor);
            _selectedRenderer = null;
            _selectedIso = null;
        }
    }
}
