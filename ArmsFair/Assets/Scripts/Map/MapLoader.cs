using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace ArmsFair.Map
{
    // Loads countries.json from StreamingAssets and generates one mesh per polygon per country.
    // Call LoadMap() after the scene is ready. Subscribe to OnMapLoaded for completion.
    public class MapLoader : MonoBehaviour
    {
        [SerializeField] private Material countryMaterial;
        [SerializeField] private float mapWidth  = 1920f;
        [SerializeField] private float mapHeight =  960f;

        public event Action OnMapLoaded;

        private readonly Dictionary<string, List<GameObject>> _countryObjects = new();
        public IReadOnlyDictionary<string, List<GameObject>> CountryObjects => _countryObjects;

        private void Start() => LoadMap();

        public void LoadMap() => StartCoroutine(LoadMapCoroutine());

        private IEnumerator LoadMapCoroutine()
        {
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "GeoData/countries.json");

            using var request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MapLoader] Failed to load countries.json: {request.error}");
                yield break;
            }

            var countries = JSON.Parse(request.downloadHandler.text).AsArray;
            if (countries == null)
            {
                Debug.LogError("[MapLoader] countries.json root is not a JSON array.");
                yield break;
            }

            foreach (JSONNode country in countries)
            {
                var iso      = country["iso"].Value;
                var geometry = country["geometry"];
                var type     = geometry["type"].Value;
                var coords   = geometry["coordinates"];

                _countryObjects[iso] = new List<GameObject>();

                if (type == "Polygon")
                {
                    var ring = ParseRing(coords[0]);
                    if (ring.Length >= 3)
                    {
                        var go = CreateCountryMesh(iso, ring);
                        if (go != null) _countryObjects[iso].Add(go);
                    }
                }
                else if (type == "MultiPolygon")
                {
                    foreach (JSONNode polygon in coords.AsArray)
                    {
                        var ring = ParseRing(polygon[0]);
                        if (ring.Length < 3) continue;
                        var go = CreateCountryMesh(iso, ring);
                        if (go != null) _countryObjects[iso].Add(go);
                    }
                }
            }

            Debug.Log($"[MapLoader] Loaded {_countryObjects.Count} countries.");
            OnMapLoaded?.Invoke();
        }

        private Vector2[] ParseRing(JSONNode ring)
        {
            var arr = ring.AsArray;
            var pts = new Vector2[arr.Count];
            for (int i = 0; i < arr.Count; i++)
                pts[i] = ProjectMercator(arr[i][0].AsFloat, arr[i][1].AsFloat);
            return pts;
        }

        private bool _loggedFirst;

        private GameObject CreateCountryMesh(string iso, Vector2[] vertices2D)
        {
            // Collider for click/tap detection — must be added before triangulation
            // Unity limits PolygonCollider2D to 256 points; clamp if needed
            var colPts = vertices2D.Length <= 256
                ? vertices2D
                : ResampleRing(vertices2D, 256);

            var triangles = new Triangulator(vertices2D).Triangulate();
            if (triangles.Length < 3) return null;

            var verts3D = new Vector3[vertices2D.Length];
            for (int i = 0; i < vertices2D.Length; i++)
                verts3D[i] = new Vector3(vertices2D[i].x, vertices2D[i].y, 0f);

            var mesh = new Mesh { name = iso };
            mesh.vertices  = verts3D;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (!_loggedFirst)
            {
                _loggedFirst = true;
                Debug.Log($"[MapLoader] First mesh {iso}: verts={verts3D.Length} tris={triangles.Length/3} bounds={mesh.bounds}");
            }

            var mat = countryMaterial != null
                ? new Material(countryMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", new Color(0.20f, 0.45f, 0.20f));

            var go = new GameObject($"Country_{iso}");
            go.transform.SetParent(transform, false);
            var col = go.AddComponent<PolygonCollider2D>();
            col.SetPath(0, colPts);
            go.AddComponent<MeshFilter>().mesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            return go;
        }

        private Vector2 ProjectMercator(float lng, float lat)
        {
            lat = Mathf.Clamp(lat, -89.9f, 89.9f);
            float x      = (lng + 180f) / 360f * mapWidth;
            float latRad = lat * Mathf.Deg2Rad;
            float mercN  = Mathf.Log(Mathf.Tan(Mathf.PI / 4f + latRad / 2f));
            // Y=0 at bottom (south), Y=mapHeight at top (north)
            float y      = (mercN + Mathf.PI) / (2f * Mathf.PI) * mapHeight;
            return new Vector2(x, y);
        }

        private static Vector2[] ResampleRing(Vector2[] pts, int maxCount)
        {
            var result = new Vector2[maxCount];
            for (int i = 0; i < maxCount; i++)
                result[i] = pts[i * pts.Length / maxCount];
            return result;
        }

        public void SetCountryColor(string iso, Color color)
        {
            if (!_countryObjects.TryGetValue(iso, out var objects)) return;
            foreach (var go in objects)
            {
                if (go == null) continue;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = color;
            }
        }
    }
}
