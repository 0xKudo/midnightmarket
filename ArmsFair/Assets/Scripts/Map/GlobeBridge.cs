using System.Collections;
using System.Collections.Generic;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using UnityEngine;

namespace ArmsFair.Map
{
    [RequireComponent(typeof(GlobeRenderer))]
    public class GlobeBridge : MonoBehaviour
    {
        public static GlobeBridge Instance { get; private set; }

        public event System.Action<string, Vector2> OnCountryClicked;

        private GlobeRenderer _globe;
        private readonly Dictionary<string, float> _stageMap = new();
        private readonly List<GameObject> _arcObjects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _globe = GetComponent<GlobeRenderer>();
            _globe.OnCountrySelected += OnGlobeClick;
        }

        private void OnDestroy()
        {
            if (_globe != null) _globe.OnCountrySelected -= OnGlobeClick;
            if (Instance == this) Instance = null;
        }

        private void OnGlobeClick(string iso)
        {
            var cam = Camera.main?.GetComponent<GlobeCameraController>();
            var screenPos = cam != null ? cam.ClickScreenPos : Vector2.zero;
            OnCountryClicked?.Invoke(iso, screenPos);
        }

        public void SetCountryStage(string iso, CountryStage stage)
        {
            _stageMap[iso] = stage switch
            {
                CountryStage.Dormant            => 0f,
                CountryStage.Simmering          => 0.2f,
                CountryStage.Active             => 0.4f,
                CountryStage.HotWar             => 0.6f,
                CountryStage.HumanitarianCrisis => 0.8f,
                CountryStage.FailedState        => 1.0f,
                _                               => 0f
            };
            _globe.UpdateCountryTensions(_stageMap);
        }

        public void PlayArcs(List<ArcAnimation> arcs, IReadOnlyList<PlayerProfile> players)
        {
            ClearArcs();
            foreach (var arc in arcs)
                StartCoroutine(DrawArc(arc, players));
        }

        public void HighlightCountry(string iso) { }
        public void ClearHighlights() { }

        private IEnumerator DrawArc(ArcAnimation arc, IReadOnlyList<PlayerProfile> players)
        {
            if (arc.DelayMs > 0)
                yield return new WaitForSeconds(arc.DelayMs / 1000f);

            var player   = players != null ? System.Linq.Enumerable.FirstOrDefault(players, p => p.Id == arc.PlayerId) : null;
            var fromIso  = player?.HomeNation ?? "US";
            var fromPt   = _globe.GetWorldPoint(fromIso);
            var toPt     = _globe.GetWorldPoint(arc.TargetIso);

            if (fromPt == Vector3.zero || toPt == Vector3.zero) yield break;

            var color = arc.SaleType switch
            {
                "Open"        => new Color(138f/255f, 184f/255f, 112f/255f),
                "Covert"      => new Color(192f/255f, 144f/255f, 80f/255f),
                "AidCover"    => new Color(80f/255f,  144f/255f, 192f/255f),
                "PeaceBroker" => new Color(212f/255f, 207f/255f, 184f/255f),
                _             => Color.white
            };

            var go = new GameObject($"Arc_{arc.PlayerId}_{arc.TargetIso}");
            go.transform.SetParent(_globe.transform, worldPositionStays: true);
            _arcObjects.Add(go);

            var lr            = go.AddComponent<LineRenderer>();
            lr.positionCount  = 24;
            lr.startWidth     = 0.04f;
            lr.endWidth       = 0.02f;
            lr.useWorldSpace  = true;
            lr.material       = new Material(Shader.Find("Sprites/Default"));
            lr.startColor     = color;
            lr.endColor       = new Color(color.r, color.g, color.b, 0f);

            var center  = _globe.transform.position;
            float radius = _globe.GlobeRadius;
            var fromDir  = (fromPt - center).normalized;
            var toDir    = (toPt   - center).normalized;
            float height = radius * 0.25f;

            for (int i = 0; i < 24; i++)
            {
                float t = i / 23f;
                var dir = Vector3.Slerp(fromDir, toDir, t);
                float h = height * Mathf.Sin(t * Mathf.PI);
                lr.SetPosition(i, center + dir * (radius + h));
            }

            Destroy(go, 6f);
        }

        private void ClearArcs()
        {
            foreach (var go in _arcObjects)
                if (go != null) Destroy(go);
            _arcObjects.Clear();
        }
    }
}
