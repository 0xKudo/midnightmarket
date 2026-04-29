using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArmsFair.Map
{
    // Persistent singleton. Add to the Bootstrap NetworkManager GameObject.
    // G key or call ToggleView() to swap flat↔globe.
    public class ViewToggleManager : MonoBehaviour
    {
        public static ViewToggleManager Instance { get; private set; }

        [SerializeField] private string flatSceneName  = "MapFlat";
        [SerializeField] private string globeSceneName = "MapGlobe";
        [SerializeField] private KeyCode toggleKey     = KeyCode.G;

        public enum MapView { Flat, Globe }
        public MapView CurrentView { get; private set; } = MapView.Flat;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                ToggleView();
        }

        public void ToggleView()
        {
            if (CurrentView == MapView.Flat)
                StartCoroutine(SwitchToGlobe());
            else
                StartCoroutine(SwitchToFlat());
        }

        private IEnumerator SwitchToGlobe()
        {
            if (!IsSceneLoaded(globeSceneName))
                yield return SceneManager.LoadSceneAsync(globeSceneName, LoadSceneMode.Additive);

            SetCameraActive(flatSceneName,  false);
            SetCameraActive(globeSceneName, true);
            CurrentView = MapView.Globe;
        }

        private IEnumerator SwitchToFlat()
        {
            if (!IsSceneLoaded(flatSceneName))
                yield return SceneManager.LoadSceneAsync(flatSceneName, LoadSceneMode.Additive);

            SetCameraActive(globeSceneName, false);
            SetCameraActive(flatSceneName,  true);
            CurrentView = MapView.Flat;
        }

        private static void SetCameraActive(string sceneName, bool active)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name != sceneName) continue;
                foreach (var go in scene.GetRootGameObjects())
                {
                    var cam = go.GetComponentInChildren<Camera>(true);
                    if (cam != null) cam.enabled = active;
                }
            }
        }

        private static bool IsSceneLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == name) return true;
            return false;
        }
    }
}
