// World Political Map - Globe Edition for Unity - Main Script
// Copyright 2015 - 2024 by Ramiro Oliva
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WPM

//#define PAINT_MODE
//#define TRACE_CTL

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Poly2Tri;
using UnityEngine;

namespace WPM {

    [ExecuteAlways]
    public partial class WorldMapGlobe : MonoBehaviour {

        public const float MAP_PRECISION = 5000000f;
        const float EARTH_RADIUS_KM = 6371f;
        public const string WPM_OVERLAY_NAME = "WPMOverlay";
        const string SPHERE_OVERLAY_LAYER_NAME = "SphereOverlayLayer";

        enum OVERLAP_CLASS {
            OUTSIDE = -1,
            PARTLY_OVERLAP = 0,
            INSIDE = 1
        }

        [NonSerialized]
        public bool
            isDirty; // internal variable used to confirm changes in custom inspector - don't change its value


        #region Internal variables

        // resources
        Material coloredMat;
        Material outlineMat, cursorMat, gridMat;
        Material markerMat;

        // gameObjects
        GameObject _surfacesLayer;
        GameObject surfacesLayer { get { if (_surfacesLayer == null) CreateSurfacesLayer(); return _surfacesLayer; } }
        GameObject cursorLayer, latitudeLayer, longitudeLayer;
        GameObject markersLayer, overlayMarkersLayer;
        Camera mapperCam;

        // caché and gameObject lifetime control
        Dictionary<int, GameObject> surfaces;

        Dictionary<Color, Material> coloredMatCache;
        Dictionary<Color, Material> markerMatCache;

        // FlyTo functionality
        Quaternion flyToStartQuaternion, flyToEndQuaternion;
        bool flyToActive;
        float flyToStartTime, flyToDuration;

        // UI interaction variables
        Vector3 mouseDragStart, dragDirection;
        bool mouseStartedDragging;
        int dragDamping;
        float wheelAccel;
        Vector3 lastGlobeScaleCheck;

        // Overlay (Labels, tickers, ...)
        public const int overlayWidth = 200;     // don't change these values or 
        public const int overlayHeight = 100;    // overlay wont' work
        RenderTexture overlayRT;
        GameObject overlayLayer, sphereOverlayLayer;
        Font labelsFont;
        Material labelsShadowMaterial;

        #endregion

        #region System initialization

        public void Init() {
            // Load materials
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": init");
#endif
            // Labels materials
            labelsFont = GameObject.Instantiate(Resources.Load<Font>("Font/Lato"));
            labelsFont.hideFlags = HideFlags.DontSave;
            Material fontMaterial = Instantiate(labelsFont.material);
            fontMaterial.hideFlags = HideFlags.DontSave;
            labelsFont.material = fontMaterial;
            labelsShadowMaterial = GameObject.Instantiate(fontMaterial);
            labelsShadowMaterial.hideFlags = HideFlags.DontSave;
            labelsShadowMaterial.renderQueue--;

            // Map materials
            frontiersMat = Instantiate(Resources.Load<Material>("Materials/Frontiers"));
            frontiersMat.hideFlags = HideFlags.DontSave;
            hudMatCountry = Instantiate(Resources.Load<Material>("Materials/HudCountry"));
            hudMatCountry.hideFlags = HideFlags.DontSave;
            citySpot = Resources.Load<GameObject>("Prefabs/CitySpot");
            citiesMat = Instantiate(Resources.Load<Material>("Materials/Cities"));
            citiesMat.hideFlags = HideFlags.DontSave;
            outlineMat = Instantiate(Resources.Load<Material>("Materials/Outline"));
            outlineMat.hideFlags = HideFlags.DontSave;
            outlineMat.renderQueue++;
            coloredMat = Instantiate(Resources.Load<Material>("Materials/ColorizedRegion"));
            coloredMat.hideFlags = HideFlags.DontSave;
            cursorMat = Instantiate(Resources.Load<Material>("Materials/Cursor"));
            cursorMat.hideFlags = HideFlags.DontSave;
            gridMat = Instantiate(Resources.Load<Material>("Materials/Grid"));
            gridMat.hideFlags = HideFlags.DontSave;
            markerMat = Instantiate(Resources.Load<Material>("Materials/Marker"));
            markerMat.hideFlags = HideFlags.DontSave;

            coloredMatCache = new Dictionary<Color, Material>();
            markerMatCache = new Dictionary<Color, Material>();

            // Destroy obsolete labels layer -> now replaced with overlay feature
            GameObject o = GameObject.Find("WPMLabels");
            if (o != null)
                DestroyImmediate(o);
            Transform t = transform.Find("LabelsLayer");
            if (t != null)
                DestroyImmediate(t.gameObject);
            // End destroy obsolete.

            ReloadData();

            lastGlobeScaleCheck = transform.localScale;

        }

        /// <summary>
        /// Reloads the data of frontiers and cities from datafiles and redraws the map.
        /// </summary>
        public void ReloadData() {
            // read baked data
            ReadCountriesPackedString();
            ReadCitiesPackedString();

            // Redraw frontiers and cities -- destroy layers if they already exists
            Redraw();
        }

        void GetLatLonFromPackedString(string s, out float lat, out float lon) {
            int j = s.IndexOf(",");
            string slat = s.Substring(0, j);
            string slon = s.Substring(j + 1);
            lat = float.Parse(slat, CultureInfo.InvariantCulture) / MAP_PRECISION;
            lon = float.Parse(slon, CultureInfo.InvariantCulture) / MAP_PRECISION;
        }

        #endregion

        #region Gameloop events


        public void OnEnable() {
#if UNITY_EDITOR
			UnityEditor.PrefabInstanceStatus prefabInstanceStatus = UnityEditor.PrefabUtility.GetPrefabInstanceStatus(gameObject);
			if (prefabInstanceStatus != UnityEditor.PrefabInstanceStatus.NotAPrefab) {
				UnityEditor.EditorApplication.delayCall += () => {
					UnityEditor.PrefabUtility.UnpackPrefabInstance(gameObject, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
					OnEnableDelayed();
				};
				return;
			}
#endif
            OnEnableDelayed();
        }

        void OnEnableDelayed() {

#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": enable wpm");
#endif
            if (input == null) {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                input = new NewInputSystem();
#else
                input = new DefaultInputSystem();
#endif
            }
            input.Init();

            if (countries == null) {
                Init();
            }

            // Check material
            Renderer renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null) {
                RestyleEarth();
            }

            if (hudMatCountry != null && hudMatCountry.color != _fillColor) {
                hudMatCountry.color = _fillColor;
            }
            if (frontiersMat != null && frontiersMat.color != _frontiersColor) {
                frontiersMat.color = _frontiersColor;
            }
            if (citiesLayer != null && citiesMat.color != _citiesColor) {
                citiesMat.color = _citiesColor;
            }
            if (outlineMat.color != _outlineColor) {
                outlineMat.color = _outlineColor;
            }
            if (cursorMat.color != _cursorColor) {
                cursorMat.color = _cursorColor;
            }
            if (gridMat.color != _gridColor) {
                gridMat.color = _gridColor;
            }
        }

        void OnDestroy() {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": destroy wpm");
#endif
            DestroyOverlay();
            DestroySurfacesLayer();
        }


#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
        void OnMouseEnter() {
            mouseIsOver = true;
        }

        void OnMouseExit() {
            mouseIsOver = false;
            HideCountryRegionHighlight();
        }
#endif

#if ENABLE_INPUT_SYSTEM
        static readonly RaycastHit[] pointer_hits = new RaycastHit[20];
        
        void CheckMouseIsOver() {
            if (Camera.main == null) return;
            
            bool isOver = false;
            Ray ray = Camera.main.ScreenPointToRay(input.mousePosition);
            int hitCount = Physics.RaycastNonAlloc(ray, pointer_hits, Camera.main.farClipPlane);
            for (int k = 0; k < hitCount; k++) {
                if (pointer_hits[k].collider != null && pointer_hits[k].collider.gameObject == gameObject) {
                    isOver = true;
                    break;
                }
            }

            if (isOver != mouseIsOver) {
                mouseIsOver = isOver;
                if (!isOver) {
                    HideCountryRegionHighlight();
                }
            }
        }
#endif

        void Reset() {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": reset");
#endif
            Redraw();
        }

        void LateUpdate() {

            if (!Application.isPlaying) {
                // when saving the scene from Editor, the material of the sphere label layer is cleared - here's a fix to recreate it
                if (_showCountryNames && sphereOverlayLayer != null && sphereOverlayLayer.GetComponent<Renderer>() == null) {
                    CreateOverlay();
                }
            }

            // Check scale and adapts atmosphere
            if (transform.localScale != lastGlobeScaleCheck) {
                lastGlobeScaleCheck = transform.localScale;
                transform.Find("WorldMapGlobeAtmosphere").GetComponent<Light>().range = Mathf.Min(lastGlobeScaleCheck.x, lastGlobeScaleCheck.y, lastGlobeScaleCheck.z) * 1.2f;
                Redraw();
            }

            if (!Application.isPlaying) return;

#if ENABLE_INPUT_SYSTEM
            CheckMouseIsOver();
#endif

            // Verify if mouse enter a country boundary - we only check if mouse is inside the sphere of world
            if (mouseIsOver) { // dragDamping == 0 && wheelAccel == 0) {
                CheckMousePos();
            }

            // Check if navigateTo... has been called and in this case rotate the globe until the country is centered
            if (flyToActive) {
                RotateGlobeToDestination();
            } else {
                // subtle/slow continuous rotation
                if (autoRotationSpeed != 0) {
                    transform.Rotate(Vector3.up, -autoRotationSpeed);
                }
            }

            // Handle user input
            CheckUserInteractionNormalMode();
        }


        #endregion

        #region Drawing stuff

        /// <summary>
        /// Used internally and by other components to redraw the layers in specific moments. You shouldn't call this method directly.
        /// </summary>
        public void Redraw() {
            if (!gameObject.activeInHierarchy)
                return;

            if (countries == null) Init();

#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": Redraw");
#endif

            DestroyMapLabels();

            // Initialize surface cache
            if (surfaces != null) {
                List<GameObject> cached = new List<GameObject>(surfaces.Values);
                for (int k = 0; k < cached.Count; k++)
                    if (cached[k] != null)
                        DestroyImmediate(cached[k]);
            }
            surfaces = new Dictionary<int, GameObject>();
            _surfacesCount = 0;
            DestroySurfacesLayer();

            RestyleEarth(); // Apply texture to Earth

            DrawFrontiers();    // Redraw frontiers -- the next method is also called from width property when this is changed

            DrawCities();       // Redraw cities layer

            DrawCursor();       // Draw cursor lines

            DrawGrid();     // Draw longitude & latitude lines

            DrawAtmosphere();

            if (_showCountryNames) {
                DrawMapLabels();
            }
        }

        void CreateSurfacesLayer() {
            Transform t = transform.Find("Surfaces");
            if (t != null) {
                DestroyImmediate(t.gameObject);
                for (int k = 0; k < countries.Length; k++)
                    for (int r = 0; r < countries[k].regions.Count; r++)
                        countries[k].regions[r].customMaterial = null;
            }
            _surfacesLayer = new GameObject("Surfaces");
            _surfacesLayer.transform.SetParent(transform, false);
            _surfacesLayer.transform.localScale = MiscVector.Vector3one;
        }

        void DestroySurfacesLayer() {
            if (_surfacesLayer != null) GameObject.DestroyImmediate(_surfacesLayer);
        }

        void RestyleEarth() {
            if (gameObject == null)
                return;

            string materialName;
            switch (_earthStyle) {
                case EARTH_STYLE.SolidColor:
                    materialName = "EarthSolidColor";
                    break;
                default:
                    materialName = "Earth";
                    break;
            }
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.name.Equals(materialName)) {
                Material earthMaterial = Instantiate(Resources.Load<Material>("Materials/" + materialName));
                earthMaterial.hideFlags = HideFlags.DontSave;
                if (_earthStyle == EARTH_STYLE.SolidColor) {
                    earthMaterial.color = _earthColor;
                }
                earthMaterial.name = materialName;
                renderer.material = earthMaterial;
            }

            //			Drawing.ReverseSphereNormals (gameObject, false);
            if (lastGlobeScaleCheck.x < 0) {
                transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
                lastGlobeScaleCheck = transform.localScale;
            }
        }

        void DrawAtmosphere() {
            transform.Find("WorldMapGlobeAtmosphere").gameObject.SetActive(_showWorld);
        }

        Material GetColoredTexturedMaterial(Color color) {
            if (coloredMatCache.ContainsKey(color)) {
                return coloredMatCache[color];
            } else {
                Material customMat;
                customMat = Instantiate(coloredMat);
                customMat.name = coloredMat.name;
                coloredMatCache[color] = customMat;
                customMat.color = color;
                customMat.hideFlags = HideFlags.DontSave;
                return customMat;
            }
        }


        Material GetColoredMarkerMaterial(Color color) {
            if (markerMatCache.ContainsKey(color)) {
                return markerMatCache[color];
            } else {
                Material customMat;
                customMat = Instantiate(markerMat);
                customMat.name = markerMat.name;
                markerMatCache[color] = customMat;
                customMat.color = color;
                customMat.hideFlags = HideFlags.DontSave;
                return customMat;
            }
        }

        void ApplyMaterialToSurface(GameObject obj, Material sharedMaterial) {
            if (obj != null) {
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                    r.sharedMaterial = sharedMaterial;
            }
        }


        #endregion

        #region Internal functions

        /// <summary>
        /// Used internally to rotate the globe during FlyTo operations. Use FlyTo method.
        /// </summary>
        void RotateGlobeToDestination() {
            float delta;
            Quaternion rotation;
            if (flyToDuration == 0) {
                delta = flyToDuration;
                rotation = flyToEndQuaternion;
            } else {
                delta = (Time.time - flyToStartTime);
                float t = delta / flyToDuration;
                rotation = Quaternion.Lerp(flyToStartQuaternion, flyToEndQuaternion, Mathf.SmoothStep(0, 1, t));
            }
            transform.rotation = rotation;
            if (delta >= flyToDuration)
                flyToActive = false;
        }


        void CheckUserInteractionNormalMode() {
            // if mouse/finger is over map, implement drag and rotation of the world
            if (mouseIsOver) {
                // Use left mouse button and drag to rotate the world
                if (_allowUserRotation) {
                    if (input.GetMouseButtonDown(0)) {
                        mouseDragStart = input.mousePosition;
                        mouseStartedDragging = true;
                        _hasDragged = false;
                    } else if (mouseStartedDragging && input.GetMouseButton(0) && (!input.touchSupported || input.touchCount == 1)) {
                        float distFactor = Vector3.Distance(Camera.main.transform.position, transform.position) / transform.localScale.z;
                        dragDirection = (mouseDragStart - input.mousePosition) * 0.01f * distFactor * _mouseDragSensitivity;
                        if (Mathf.Abs(dragDirection.x) > _mouseDragThreshold || Mathf.Abs(dragDirection.y) > _mouseDragThreshold) {
                            transform.Rotate(Vector3.up, dragDirection.x, Space.World);
                            Vector3 axisY = Vector3.Cross(transform.position - Camera.main.transform.position, Vector3.up);
                            transform.Rotate(axisY, dragDirection.y, Space.World);
                            dragDamping = 1;
                            flyToActive = false;
                            _hasDragged = true;
                        }
                    } else {
                        mouseStartedDragging = false;
                    }

                    // Use right mouse button and drag to spin the world around z-axis
                    if (input.GetMouseButton(1) && !input.touchSupported && !flyToActive) {
                        if (_countryHighlightedIndex >= 0 && _countryHighlightedIndex != _countryLastClicked && input.GetMouseButtonDown(1) && _centerOnRightClick) {
                            FlyToCountry(_countryHighlightedIndex, 0.8f);
                        } else if (!_centerOnRightClick) {
                            Vector3 axis = (transform.position - Camera.main.transform.position).normalized;
                            transform.Rotate(axis, 2, Space.World);
                        }
                    }

                    // Remember the last country clicked
                    if (input.GetMouseButtonDown(0) || input.GetMouseButtonDown(1))
                        _countryLastClicked = _countryHighlightedIndex;

                }
            }

            // Check special keys
            if (_allowUserKeys && _allowUserRotation) {
                bool pressed = false;
                dragDirection = MiscVector.Vector3zero;
                if (input.GetKey(KeyCode.W)) {
                    dragDirection += Vector3.down;
                    pressed = true;
                }
                if (input.GetKey(KeyCode.S)) {
                    dragDirection += Vector3.up;
                    pressed = true;
                }
                if (input.GetKey(KeyCode.A)) {
                    dragDirection += Vector3.right;
                    pressed = true;
                }
                if (input.GetKey(KeyCode.D)) {
                    dragDirection += Vector3.left;
                    pressed = true;
                }
                if (pressed) {
                    dragDirection *= Vector3.Distance(Camera.main.transform.position, transform.position) * _mouseDragSensitivity;
                    transform.Rotate(Vector3.up, dragDirection.x, Space.World);
                    Vector3 axisY = Vector3.Cross(transform.position - Camera.main.transform.position, Vector3.up);
                    transform.Rotate(axisY, dragDirection.y, Space.World);
                    dragDamping = 1;
                }
            }

            if (dragDamping > 0) {
                if (++dragDamping < 20) {
                    transform.Rotate(Vector3.up, dragDirection.x / dragDamping, Space.World);
                    Vector3 axisY = Vector3.Cross(transform.position - Camera.main.transform.position, Vector3.up);
                    transform.Rotate(axisY, dragDirection.y / dragDamping, Space.World);
                } else {
                    dragDamping = 0;
                }
            }

            // Use mouse wheel to zoom in and out
            if (allowUserZoom) {
                if (mouseIsOver || wheelAccel != 0) {
                    float wheel = input.GetAxis("Mouse ScrollWheel");
                    wheelAccel += wheel;
                }

                // Support for pinch on mobile
                if (input.touchSupported && input.touchCount == 2) {
                    // Store both touches.
                    Touch touchZero = input.GetTouch(0);
                    Touch touchOne = input.GetTouch(1);

                    // Find the position in the previous frame of each touch.
                    Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                    Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                    // Find the magnitude of the vector (the distance) between the touches in each frame.
                    float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                    float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                    // Find the difference in the distances between each frame.
                    float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                    // Pass the delta to the wheel accel
                    wheelAccel += deltaMagnitudeDiff;
                }
            }

            if (wheelAccel != 0) {
                wheelAccel = Mathf.Clamp(wheelAccel, -0.1f, 0.1f);
                if (wheelAccel >= 0.01f || wheelAccel <= -0.01f) {
                    Vector3 oldCamPos = Camera.main.transform.position;
                    Vector3 camPos = Camera.main.transform.position - (transform.position - Camera.main.transform.position) * wheelAccel * _mouseWheelSensitivity;
                    Camera.main.transform.position = camPos;
                    Camera.main.transform.LookAt(transform.position);
                    float radius = (transform.localScale.z * 0.5f + Camera.main.nearClipPlane + 0.01f);
                    float radiusSqr = radius * radius;
                    if ((Camera.main.transform.position - transform.position).sqrMagnitude < radiusSqr) {
                        Camera.main.transform.position = oldCamPos;
                    }
                    wheelAccel /= 1.15f;
                } else {
                    wheelAccel = 0;
                }
            }

        }

        void UpdateSurfaceCount() {
            if (_surfacesLayer != null)
                _surfacesCount = (_surfacesLayer.GetComponentsInChildren<Transform>().Length - 1) / 2;
            else
                _surfacesCount = 0;
        }


        #endregion

        #region Highlighting

        public int layerMask {
            get {
                return 1 << gameObject.layer;
            }
        }

        void CheckMousePos() {

            Vector3 mousePos = input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            RaycastHit[] hits;
            hits = Physics.RaycastAll(Camera.main.transform.position, ray.direction, 5, layerMask);
            if (hits.Length > 0) {
                for (int k = 0; k < hits.Length; k++) {
                    if (hits[k].collider.gameObject == gameObject) {
                        // Cursor follow
                        if (_enableCountryHighlight || (_cursorFollowMouse && _showCursor)) { // need the cursor location for highlighting test
                            cursorLocation = transform.InverseTransformPoint(hits[k].point);
                        }
                        // Country highlight?
                        if (!_enableCountryHighlight)
                            return;
                        // verify if hitPos is inside any country polygon
                        int c, cr;
                        if (GetCountryUnderMouse(cursorLocation, out c, out cr)) {
                            if (c != _countryHighlightedIndex || (c == _countryHighlightedIndex && cr != _countryRegionHighlightedIndex)) {
                                HideCountryRegionHighlight();
                                _countryHighlighted = countries[c];
                                _countryHighlightedIndex = c;
                                _countryRegionHighlighted = countries[c].regions[cr];
                                _countryRegionHighlightedIndex = cr;
                            }
                            HighlightCountryRegion(c, cr, false, _showOutline);
                            return;
                        }
                    }
                }
                HideCountryRegionHighlight();
            }
        }

        #endregion

        #region Geometric functions

        float SignedAngleBetween(Vector3 a, Vector3 b, Vector3 n) {
            // angle in [0,180]
            float angle = Vector3.Angle(a, b);
            float sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(a, b)));

            // angle in [-179,180]
            float signed_angle = angle * sign;

            return signed_angle;
        }

        Quaternion GetQuaternion(Vector3 point) {
            Quaternion oldRotation = transform.localRotation;
            Quaternion q;
            // center destination
            Vector3 v1 = point.normalized;
            Vector3 v2 = Camera.main.transform.position - transform.position;
            float angle = Vector3.Angle(v1, v2);
            Vector3 axis = Vector3.Cross(v1, v2);
            transform.localRotation = Quaternion.AngleAxis(angle, axis);
            // straighten view
            Vector3 v3 = Vector3.ProjectOnPlane(transform.up, v2);
            float angle2 = SignedAngleBetween(Camera.main.transform.up, v3, v2);
            transform.Rotate(v2, -angle2, Space.World);
            q = transform.localRotation;
            transform.localRotation = oldRotation;
            return q;
        }

        bool ContainsPoint2D(PolygonPoint[] polyPoints, double x, double y) {
            int j = polyPoints.Length - 1;
            bool inside = false;
            for (int i = 0; i < polyPoints.Length; j = i++) {
                if (((polyPoints[i].Y <= y && y < polyPoints[j].Y) || (polyPoints[j].Y <= y && y < polyPoints[i].Y)) &&
                    (x < (polyPoints[j].X - polyPoints[i].X) * (y - polyPoints[i].Y) / (polyPoints[j].Y - polyPoints[i].Y) + polyPoints[i].X))
                    inside = !inside;
            }
            return inside;
        }

        Vector2 GetBillboardPointFromLatLon(float lat, float lon) {
            Vector2 p;
            float mapWidth = 200.0f;
            float mapHeight = 100.0f;
            p.x = (lon + 180) * (mapWidth / 360) - mapWidth * 0.5f;
            p.y = lat * (mapHeight / 180);
            return p;
        }

        Rect GetRect2DFromMinMaxLatLon(Vector2 minMaxLat, Vector2 minMaxLon) {
            Vector2 min = GetBillboardPointFromLatLon(minMaxLat.x, minMaxLon.x);
            Vector2 max = GetBillboardPointFromLatLon(minMaxLat.y, minMaxLon.y);
            return new Rect(min.x, min.y, Math.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }

        public Vector3 GetSpherePointFromLatLon2(PolygonPoint point) {
            double phi = point.X * 0.0174532924; //Mathf.Deg2Rad;
            double theta = (point.Y + 90.0) * 0.0174532924; //Mathf.Deg2Rad;
            double x = Math.Cos(phi) * Math.Cos(theta) * 0.5;
            double y = Math.Sin(phi) * 0.5;
            double z = Math.Cos(phi) * Math.Sin(theta) * 0.5;
            return new Vector3((float)x, (float)y, (float)z);
        }

        public Vector3 GetSpherePointFromLatLon2(double lat, double lon) {
            double phi = lat * 0.0174532924; //Mathf.Deg2Rad;
            double theta = (lon + 90.0) * 0.0174532924; //Mathf.Deg2Rad;
            double x = Math.Cos(phi) * Math.Cos(theta) * 0.5;
            double y = Math.Sin(phi) * 0.5;
            double z = Math.Cos(phi) * Math.Sin(theta) * 0.5;
            return new Vector3((float)x, (float)y, (float)z);
        }

        public PolygonPoint GetLatLonFromSpherePoint(Vector3 p) {
            double phi = Mathf.Asin(p.y * 2.0f);
            double theta = Mathf.Atan2(p.x, p.z);
            return new PolygonPoint(phi * Mathf.Rad2Deg, -theta * Mathf.Rad2Deg);
        }

        Vector3 ConvertToTextureCoordinates(Vector3 p, int width, int height) {
            float phi = Mathf.Asin(p.y * 2.0f);
            float theta = Mathf.Atan2(p.x, p.z);
            float lonDec = -theta * Mathf.Rad2Deg;
            float latDec = phi * Mathf.Rad2Deg;
            p.x = (lonDec + 180) * width / 360.0f;
            p.y = latDec * (height / 180.0f) + height / 2.0f;
            return p;
        }


        #endregion

        #region World Gizmos

        void DrawCursor() {
            // Compute cursor dash lines
            float r = 0.501f;
            Vector3 north = new Vector3(0, r, 0);
            Vector3 south = new Vector3(0, -r, 0);
            Vector3 west = new Vector3(-r, 0, 0);
            Vector3 east = new Vector3(r, 0, 0);
            Vector3 equatorFront = new Vector3(0, 0, r);
            Vector3 equatorPost = new Vector3(0, 0, -r);

            Vector3[] points = new Vector3[800];
            int[] indices = new int[800];

            // Generate circumference V
            for (int k = 0; k < 800; k++) {
                indices[k] = k;
            }
            for (int k = 0; k < 100; k++) {
                points[k] = Vector3.Lerp(north, equatorFront, k / 100.0f).normalized * r;
            }
            for (int k = 0; k < 100; k++) {
                points[100 + k] = Vector3.Lerp(equatorFront, south, k / 100.0f).normalized * r;
            }
            for (int k = 0; k < 100; k++) {
                points[200 + k] = Vector3.Lerp(south, equatorPost, k / 100.0f).normalized * r;
            }
            for (int k = 0; k < 100; k++) {
                points[300 + k] = Vector3.Lerp(equatorPost, north, k / 100.0f).normalized * r;
            }
            // Generate circumference H
            for (int k = 0; k < 100; k++) {
                points[400 + k] = Vector3.Lerp(west, equatorFront, k / 100.0f).normalized * r;
            }
            for (int k = 0; k < 100; k++) {
                points[500 + k] = Vector3.Lerp(equatorFront, east, k / 100.0f).normalized * r;
            }
            for (int k = 0; k < 100; k++) {
                points[600 + k] = Vector3.Lerp(east, equatorPost, k / 100.0f).normalized * r;
            }
            for (int k = 0; k < 100; k++) {
                points[700 + k] = Vector3.Lerp(equatorPost, west, k / 100.0f).normalized * r;
            }

            Transform t = transform.Find("Cursor");
            if (t != null)
                DestroyImmediate(t.gameObject);
            cursorLayer = new GameObject("Cursor");
            cursorLayer.layer = gameObject.layer;
            cursorLayer.transform.SetParent(transform, false);
            cursorLayer.transform.localPosition = MiscVector.Vector3zero;
            cursorLayer.transform.localRotation = Quaternion.Euler(MiscVector.Vector3zero);
            cursorLayer.SetActive(_showCursor);

            Mesh mesh = new Mesh();
            mesh.vertices = points;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontSave;

            MeshFilter mf = cursorLayer.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = cursorLayer.AddComponent<MeshRenderer>();
            mr.receiveShadows = false;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sharedMaterial = cursorMat;

        }

        void DrawGrid() {
            DrawLatitudeLines();
            DrawLongitudeLines();
        }

        void DrawLatitudeLines() {
            // Generate latitude lines
            List<Vector3> points = new List<Vector3>();
            List<int> indices = new List<int>();
            float r = 0.501f;
            int idx = 0;
            float m = 5.0f;

            for (float a = 0; a < 90; a += _latitudeStepping) {
                for (int h = 1; h >= -1; h--) {
                    if (h == 0)
                        continue;

                    float angle = a * Mathf.Deg2Rad;
                    float y = h * Mathf.Sin(angle) * r;
                    float r2 = Mathf.Cos(angle) * r;

                    int step = Mathf.Min(1 + Mathf.FloorToInt(m * r / r2), 24);
                    if ((100 / step) % 2 != 0)
                        step++;

                    for (int k = 0; k < 360 + step; k += step) {
                        float ax = k * Mathf.Deg2Rad;
                        float x = Mathf.Cos(ax) * r2;
                        float z = Mathf.Sin(ax) * r2;
                        points.Add(new Vector3(x, y, z));
                        if (k > 0) {
                            indices.Add(idx);
                            indices.Add(++idx);
                        }
                    }
                    idx++;
                    if (a == 0)
                        break;
                }
            }

            Transform t = transform.Find("LatitudeLines");
            if (t != null)
                DestroyImmediate(t.gameObject);
            latitudeLayer = new GameObject("LatitudeLines");
            latitudeLayer.layer = gameObject.layer;
            latitudeLayer.transform.SetParent(transform, false);
            latitudeLayer.transform.localPosition = MiscVector.Vector3zero;
            latitudeLayer.transform.localRotation = Quaternion.Euler(MiscVector.Vector3zero);
            latitudeLayer.SetActive(_showLatitudeLines);

            Mesh mesh = new Mesh();
            mesh.vertices = points.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontSave;

            MeshFilter mf = latitudeLayer.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = latitudeLayer.AddComponent<MeshRenderer>();
            mr.receiveShadows = false;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            //			mr.useLightProbes = false;
            mr.sharedMaterial = gridMat;

        }

        void DrawLongitudeLines() {
            // Generate longitude lines
            List<Vector3> points = new List<Vector3>();
            List<int> indices = new List<int>();
            float r = 0.501f;
            int idx = 0;
            int step = 5;

            for (float a = 0; a < 180; a += 180 / _longitudeStepping) {
                float angle = a * Mathf.Deg2Rad;

                for (int k = 0; k < 360 + step; k += step) {
                    float ax = k * Mathf.Deg2Rad;
                    float x = Mathf.Cos(ax) * r * Mathf.Sin(angle); //Mathf.Cos (ax) * Mathf.Sin (angle) * r;
                    float y = Mathf.Sin(ax) * r;
                    float z = Mathf.Cos(ax) * r * Mathf.Cos(angle);
                    points.Add(new Vector3(x, y, z));
                    if (k > 0) {
                        indices.Add(idx);
                        indices.Add(++idx);
                    }
                }
                idx++;
            }

            Transform t = transform.Find("LongitudeLines");
            if (t != null)
                DestroyImmediate(t.gameObject);
            longitudeLayer = new GameObject("LongitudeLines");
            longitudeLayer.layer = gameObject.layer;
            longitudeLayer.transform.SetParent(transform, false);
            longitudeLayer.transform.localPosition = MiscVector.Vector3zero;
            longitudeLayer.transform.localRotation = Quaternion.Euler(MiscVector.Vector3zero);
            longitudeLayer.SetActive(_showLongitudeLines);

            Mesh mesh = new Mesh();
            mesh.vertices = points.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontSave;

            MeshFilter mf = longitudeLayer.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = longitudeLayer.AddComponent<MeshRenderer>();
            mr.receiveShadows = false;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            //			mr.useLightProbes = false;
            mr.sharedMaterial = gridMat;

        }

        #endregion

        #region Map Labels

        /// <summary>
        /// Forces redraw of all labels.
        /// </summary>
        public void RedrawMapLabels() {
            DestroyMapLabels();
            DrawMapLabels();
        }

        /// <summary>
        /// Draws the map labels. Note that it will update cached textmesh objects if labels are already drawn.
        /// </summary>
        void DrawMapLabels() {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": Draw map labels");
#endif

            //			DateTime start = DateTime.Now;
            GameObject textRoot = null;

            // Set colors
            labelsFont.material.color = _countryLabelsColor;
            labelsShadowMaterial.color = _countryLabelsShadowColor;

            // Create texts
            GameObject overlay = GetOverlayLayer(true);
            Transform t = overlay.transform.Find("TextRoot");
            if (t == null) {
                textRoot = new GameObject("TextRoot");
                textRoot.layer = overlay.layer;
            } else {
                textRoot = t.gameObject;
                textRoot.transform.SetParent(null);
            }
            textRoot.transform.localPosition = new Vector3(0, 0, -0.001f);
            textRoot.transform.rotation = Quaternion.Euler(MiscVector.Vector3zero);       // needs rotation to be 0,0,0 for getting correct bounds sizes - it's fixed at the end of the method
            textRoot.transform.localScale = MiscVector.Vector3one;

            List<MeshRect> meshRects = new List<MeshRect>();
            for (int countryIndex = 0; countryIndex < countries.Length; countryIndex++) {
                Country country = countries[countryIndex];
                Vector2 center = Drawing.SphereToBillboardCoordinates(country.center);
                Region region = country.regions[country.mainRegionIndex];

                // Special countries adjustements
                switch (countryIndex) {
                    case 135: // Russia
                        center.y++;
                        center.x -= 8;
                        break;
                    case 6: // Antartica
                        center.y += 9f;
                        break;
                    case 65: // Greenland
                        center.y -= 2f;
                        break;
                    case 22: // Brazil
                        center.y += 4f;
                        center.x += 1.0f;
                        break;
                    case 73: // India
                        center.x -= 2f;
                        break;
                    case 168: // USA
                        center.x -= 1f;
                        break;
                    case 27: // Canada
                        center.x -= 3f;
                        break;
                    case 30: // China
                        center.x -= 1f;
                        center.y -= 2f;
                        break;
                }

                // Adjusts country name length
                string countryName = country.customLabel != null ? country.customLabel : country.name.ToUpper();
                bool introducedCarriageReturn = false;
                if (countryName.Length > 15) {
                    int spaceIndex = countryName.IndexOf(' ', countryName.Length / 2);
                    if (spaceIndex >= 0) {
                        countryName = countryName.Substring(0, spaceIndex) + "\n" + countryName.Substring(spaceIndex + 1);
                        introducedCarriageReturn = true;
                    }
                }

                // add caption
                GameObject textObj;
                if (country.labelGameObject == null) {
                    Color labelColor = country.labelColorOverride ? country.labelColor : _countryLabelsColor;
                    Font customFont = country.labelFontOverride ?? labelsFont;
                    Material customLabelShadowMaterial = country.labelFontShadowMaterial ?? labelsShadowMaterial;
                    textObj = Drawing.CreateText(countryName, null, center, customFont, labelColor, _showLabelsShadow, customLabelShadowMaterial, _countryLabelsShadowColor);
                    country.labelGameObject = textObj;
                    Bounds bounds = textObj.GetComponent<Renderer>().bounds;
                    country.labelMeshWidth = bounds.size.x;
                    country.labelMeshHeight = bounds.size.y;
                    country.labelMeshCenter = center;
                    textObj.transform.SetParent(textRoot.transform, false);
                    textObj.transform.localPosition = center;
                    textObj.layer = textRoot.gameObject.layer;
                    if (_showLabelsShadow) {
                        Transform shadowObj = textObj.transform.Find("shadow");
                        if (shadowObj != null) {
                            shadowObj.gameObject.layer = textObj.layer;
                        }
                    }
                } else {
                    textObj = country.labelGameObject;
                    textObj.transform.localPosition = center;
                }

                float meshWidth = country.labelMeshWidth;
                float meshHeight = country.labelMeshHeight;

                // adjusts caption
                Rect rect = region.rect2D;
                float absoluteHeight;
                if (rect.height > rect.width * 1.45f) {
                    float angle;
                    if (rect.height > rect.width * 1.5f) {
                        angle = 90;
                    } else {
                        angle = Mathf.Atan2(rect.height, rect.width) * Mathf.Rad2Deg;
                    }
                    textObj.transform.localRotation = Quaternion.Euler(0, 0, angle);
                    absoluteHeight = Mathf.Min(rect.width * _countryLabelsSize, rect.height);
                } else {
                    absoluteHeight = Mathf.Min(rect.height * _countryLabelsSize, rect.width);
                }

                // adjusts scale to fit width in rect
                float adjustedMeshHeight = introducedCarriageReturn ? meshHeight * 0.5f : meshHeight;
                float scale = absoluteHeight / adjustedMeshHeight;
                float desiredWidth = meshWidth * scale;
                if (desiredWidth > rect.width) {
                    scale = rect.width / meshWidth;
                }
                if (adjustedMeshHeight * scale < _countryLabelsAbsoluteMinimumSize) {
                    scale = _countryLabelsAbsoluteMinimumSize / adjustedMeshHeight;
                }

                // stretchs out the caption
                float displayedMeshWidth = meshWidth * scale;
                float displayedMeshHeight = meshHeight * scale;
                string wideName;
                int times = Mathf.FloorToInt(rect.width * 0.45f / (meshWidth * scale));
                if (times > 10)
                    times = 10;
                if (times > 0) {
                    StringBuilder sb = new StringBuilder();
                    string spaces = new string(' ', times * 2);
                    for (int c = 0; c < countryName.Length; c++) {
                        sb.Append(countryName[c]);
                        if (c < countryName.Length - 1) {
                            sb.Append(spaces);
                        }
                    }
                    wideName = sb.ToString();
                } else {
                    wideName = countryName;
                }

                TextMesh tm = textObj.GetComponent<TextMesh>();
                if (tm.text.Length != wideName.Length) {
                    tm.text = wideName;
                    displayedMeshWidth = textObj.GetComponent<Renderer>().bounds.size.x * scale;
                    displayedMeshHeight = textObj.GetComponent<Renderer>().bounds.size.y * scale;
                    if (_showLabelsShadow) {
                        textObj.transform.Find("shadow").GetComponent<TextMesh>().text = wideName;
                    }
                }

                // apply scale
                textObj.transform.localScale = new Vector3(scale, scale, 1);

                // Save mesh rect for overlapping checking
                MeshRect mr = new MeshRect(countryIndex, new Rect(center.x - displayedMeshWidth * 0.5f, center.y - displayedMeshHeight * 0.5f, displayedMeshWidth, displayedMeshHeight));
                meshRects.Add(mr);
            }

            // Simple-fast overlapping checking
            int cont = 0;
            bool needsResort = true;

            while (needsResort && ++cont < 10) {
                meshRects.Sort(overlapComparer);

                for (int c = 1; c < meshRects.Count; c++) {
                    Rect thisMeshRect = meshRects[c].rect;
                    for (int prevc = c - 1; prevc >= 0; prevc--) {
                        Rect otherMeshRect = meshRects[prevc].rect;
                        if (thisMeshRect.Overlaps(otherMeshRect)) {
                            needsResort = true;
                            int thisCountryIndex = meshRects[c].countryIndex;
                            Country country = countries[thisCountryIndex];
                            GameObject thisLabel = country.labelGameObject;

                            // displaces this label
                            float offsety = (thisMeshRect.yMax - otherMeshRect.yMin);
                            offsety = Mathf.Min(country.regions[country.mainRegionIndex].rect2D.height * 0.35f, offsety);
                            thisLabel.transform.localPosition = new Vector3(country.labelMeshCenter.x, country.labelMeshCenter.y - offsety, thisLabel.transform.localPosition.z);
                            thisMeshRect = new Rect(thisLabel.transform.localPosition.x - thisMeshRect.width * 0.5f,
                                            thisLabel.transform.localPosition.y - thisMeshRect.height * 0.5f,
                                            thisMeshRect.width, thisMeshRect.height);
                            meshRects[c].rect = thisMeshRect;
                        }
                    }
                }
            }

            textRoot.transform.SetParent(overlay.transform, false);
            textRoot.transform.localPosition = MiscVector.Vector3zero;
            textRoot.transform.localRotation = Quaternion.Euler(MiscVector.Vector3zero);

            //			Debug.Log ("DRAW LABELS: " + (DateTime.Now - start).TotalMilliseconds);
        }

        int overlapComparer(MeshRect r1, MeshRect r2) {
            return (r2.rect.center.y).CompareTo(r1.rect.center.y);
        }

        class MeshRect {
            public int countryIndex;
            public Rect rect;

            public MeshRect(int countryIndex, Rect rect) {
                this.countryIndex = countryIndex;
                this.rect = rect;
            }
        }

        void DestroyMapLabels() {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": destroy labels");
#endif
            if (countries != null) {
                for (int k = 0; k < countries.Length; k++) {
                    if (countries[k].labelGameObject != null) {
                        DestroyImmediate(countries[k].labelGameObject);
                        countries[k].labelGameObject = null;
                    }
                }
            }
            // Security check: if there're still gameObjects under TextRoot, also delete it
            if (overlayLayer != null) {
                Transform t = overlayLayer.transform.Find("TextRoot");
                if (t != null && t.childCount > 0) {
                    DestroyImmediate(t.gameObject);
                }
            }
        }

        #endregion

        #region Overlay

        GameObject CreateOverlay() {

            if (!gameObject.activeInHierarchy) return null;

            // Prepare layer
            Transform t = transform.Find(WPM_OVERLAY_NAME);
            if (t == null) {
                overlayLayer = new GameObject(WPM_OVERLAY_NAME);
                overlayLayer.transform.SetParent(transform, false);
                overlayLayer.transform.position = new Vector3(5000, 5000, 0);
                overlayLayer.layer = gameObject.layer;
            } else {
                overlayLayer = t.gameObject;
            }
            overlayLayer.transform.localScale = new Vector3(1.0f / transform.localScale.x, 1.0f / transform.localScale.y, 1.0f / transform.localScale.z);
            t = transform.Find(SPHERE_OVERLAY_LAYER_NAME);
            if (t != null) {
                Renderer r = t.gameObject.GetComponent<Renderer>();
                if (r == null || r.sharedMaterial == null) {
                    DestroyImmediate(t.gameObject);
                    t = null;
                }
            }

            // Sphere labels layer
            Material sphereOverlayMaterial;
            if (t == null) {
                sphereOverlayLayer = Instantiate(Resources.Load<GameObject>("Prefabs/SphereOverlayLayer"));
                sphereOverlayLayer.hideFlags = HideFlags.DontSave;
                sphereOverlayLayer.name = SPHERE_OVERLAY_LAYER_NAME;
                sphereOverlayLayer.transform.SetParent(transform, false);
                sphereOverlayLayer.transform.localPosition = MiscVector.Vector3zero;
                sphereOverlayMaterial = Instantiate(sphereOverlayLayer.GetComponent<Renderer>().sharedMaterial);
                sphereOverlayLayer.GetComponent<Renderer>().sharedMaterial = sphereOverlayMaterial;
            } else {
                sphereOverlayLayer = t.gameObject;
                sphereOverlayLayer.SetActive(true);
                sphereOverlayMaterial = sphereOverlayLayer.GetComponent<Renderer>().sharedMaterial;
            }
            sphereOverlayMaterial.hideFlags = HideFlags.DontSave;

            // Billboard
            t = overlayLayer.transform.Find("Billboard");
            if (t == null) {
                GameObject billboard = Instantiate(Resources.Load<GameObject>("Prefabs/Billboard"));
                billboard.name = "Billboard";
                billboard.transform.SetParent(overlayLayer.transform, false);
                billboard.transform.localPosition = MiscVector.Vector3zero;
                billboard.transform.localScale = new Vector3(overlayWidth, overlayHeight, 1);
                billboard.layer = overlayLayer.layer;
            }

            // Render texture
            int imageWidth, imageHeight;
            switch (_labelsQuality) {
                case LABELS_QUALITY.Medium:
                    imageWidth = 4096;
                    imageHeight = 2048;
                    break;
                case LABELS_QUALITY.High:
                    imageWidth = 8192;
                    imageHeight = 4096;
                    break;
                default:
                    imageWidth = 2048;
                    imageHeight = 1024;
                    break;
            }
            if (overlayRT != null && (overlayRT.width != imageWidth || overlayRT.height != imageHeight)) {
                overlayRT.Release();
                DestroyImmediate(overlayRT);
                overlayRT = null;
            }

            if (overlayRT == null) {
                overlayRT = new RenderTexture(imageWidth, imageHeight, 24);
                overlayRT.hideFlags = HideFlags.DontSave;
                overlayRT.filterMode = FilterMode.Trilinear;
            }

            // Camera
            if (mapperCam == null) {
                Transform transform = overlayLayer.transform.Find("MapperCam");
                if (t != null) {
                    mapperCam = t.GetComponent<Camera>();
                }
                if (mapperCam == null) {
                    GameObject camObj = Instantiate(Resources.Load<GameObject>("Prefabs/MapperCam"));
                    camObj.name = "MapperCam";
                    camObj.transform.SetParent(overlayLayer.transform, false);
                    camObj.layer = overlayLayer.layer;
                    mapperCam = camObj.GetComponent<Camera>();
                }
            }

            mapperCam.aspect = 2;
            mapperCam.targetTexture = overlayRT;
            mapperCam.fieldOfView = 60f;
            mapperCam.targetTexture = overlayRT;
            mapperCam.transform.localPosition = Vector3.back * 86.5f; // (10000.0f - 9999.13331f);
            mapperCam.cullingMask = 1 << mapperCam.gameObject.layer;
            mapperCam.farClipPlane = 90 * transform.lossyScale.z;

            // Assigns render texture to current material and recreates the camera
            sphereOverlayMaterial.mainTexture = overlayRT;

            // Reverse normals if inverted mode is enabled
            AdjustSphereOverlayLayerScale();
            return overlayLayer;
        }


        void AdjustSphereOverlayLayerScale() {
            sphereOverlayLayer.transform.localScale = MiscVector.Vector3one * (1.01f + _labelsElevation * 0.05f);
        }

        void DestroyOverlay() {

            if (sphereOverlayLayer != null) {
                sphereOverlayLayer.SetActive(false);
            }

            if (overlayLayer != null) {
                DestroyImmediate(overlayLayer);
                overlayLayer = null;
            }

            GameObject oldWPMOverlay = GameObject.Find(WPM_OVERLAY_NAME);
            if (oldWPMOverlay != null) {
                DestroyImmediate(oldWPMOverlay);
            }
            if (overlayRT != null) {
                overlayRT.Release();
                DestroyImmediate(overlayRT);
                overlayRT = null;
            }
        }

        #endregion

        #region Markers support

        void CheckMarkersLayer() {
            if (markersLayer == null) { // try to capture an existing marker layer
                Transform t = transform.Find("Markers");
                if (t != null) markersLayer = t.gameObject;
            }
            if (markersLayer == null) { // create it otherwise
                markersLayer = new GameObject("Markers");
                markersLayer.transform.SetParent(transform, false);
                markersLayer.transform.localPosition = MiscVector.Vector3zero;
                markersLayer.layer = gameObject.layer;
            }
        }

        void CheckOverlayMarkersLayer() {
            GameObject overlayLayer = GetOverlayLayer(true);
            if (overlayMarkersLayer == null) { // try to capture an existing marker layer
                Transform t = overlayLayer.transform.Find("OverlayMarkers");
                if (t != null) overlayMarkersLayer = t.gameObject;
            }
            if (overlayMarkersLayer == null) { // create it otherwise
                overlayMarkersLayer = new GameObject("OverlayMarkers");
                overlayMarkersLayer.transform.SetParent(overlayLayer.transform, false);
                overlayMarkersLayer.transform.localPosition = MiscVector.Vector3zero;
                overlayMarkersLayer.layer = overlayLayer.layer;
            }
        }


        /// <summary>
        /// Adds a custom marker (gameobject) to the globe on specified location and with custom scale.
        /// </summary>
        void mAddMarker(GameObject marker, Vector3 sphereLocation, float markerScale) {
            // Try to get the height of the object
            float height = 0;
            if (marker.GetComponent<MeshFilter>() != null)
                height = marker.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
            else if (marker.GetComponent<Collider>() != null)
                height = marker.GetComponent<Collider>().bounds.size.y;

            float h = height * markerScale / sphereLocation.magnitude; // lift the marker so it appears on the surface of the globe

            CheckMarkersLayer();

            marker.transform.SetParent(markersLayer.transform, false);
            marker.transform.localPosition = sphereLocation * (1.0f + h * 0.5f);

            // apply custom scale
            marker.transform.localScale = MiscVector.Vector3one * markerScale;

            // once the marker is on the surface, rotate it so it looks to the surface
            marker.transform.LookAt(transform.position);
            marker.transform.Rotate(new Vector3(90, 0, 0), Space.Self);
        }

        /// <summary>
        /// Adds a polygon over the sphere.
        /// </summary>
        GameObject mAddMarker(MARKER_TYPE type, Vector3 sphereLocation, float kmRadius, float ringWidthStart, float ringWidthEnd, Color color) {
            CheckOverlayMarkersLayer();
            GameObject marker = null;
            Vector2 position = Drawing.SphereToBillboardCoordinates(sphereLocation);
            switch (type) {
                case MARKER_TYPE.CIRCLE:
                    float rw = 2.0f * Mathf.PI * EARTH_RADIUS_KM;
                    float w = kmRadius / rw;
                    w *= 2.0f * overlayWidth;
                    //				float rh = 2.0f * Mathf.PI * EARTH_RADIUS_KM;
                    //				float h = kmRadius / rh;
                    //				h *=  overlayHeight;
                    float h = w;
                    marker = Drawing.DrawCircle("MarkerCircle", position, w, h, 0, Mathf.PI * 2.0f, ringWidthStart, ringWidthEnd, 64, GetColoredMarkerMaterial(color));
                    break;
            }
            if (marker != null) {
                marker.transform.SetParent(overlayMarkersLayer.transform, false);
                marker.transform.localPosition = new Vector3(position.x, position.y, -0.01f);
                marker.layer = overlayMarkersLayer.layer;
            }
            return marker;
        }

        #endregion

    }

}