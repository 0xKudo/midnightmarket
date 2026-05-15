using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ArmsFair.Update
{
    public enum UpdateState { Idle, Checking, UpToDate, UpdateAvailable, Downloading, Launching }

    public struct ReleaseInfo
    {
        public string Version;
        public string DownloadUrl;
        public string ReleaseName;
        public string ReleaseNotes;
        public string ReleasePageUrl;
        public bool   HasDirectDownload;
    }

    public class UpdateChecker : MonoBehaviour
    {
        public static UpdateChecker Instance { get; private set; }

        public UpdateState  State            { get; private set; } = UpdateState.Idle;
        public ReleaseInfo? LatestRelease    { get; private set; }
        public float        DownloadProgress { get; private set; }

        public static event Action<ReleaseInfo> OnUpdateAvailable;
        public static event Action<float>       OnDownloadProgress;
        public static event Action<string>      OnDownloadComplete;
        public static event Action<string>      OnCheckFailed;

        private const string ApiUrl  = "https://api.github.com/repos/0xkudo/midnightmarket/releases/latest";
        private const int    Timeout = 8;

        private UnityWebRequest _downloadRequest;
        private string          _downloadPath;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            State = UpdateState.Idle;
        }

        private void Start()
        {
            StartCoroutine(CheckForUpdateCoroutine());
        }

        private IEnumerator CheckForUpdateCoroutine()
        {
            State = UpdateState.Checking;

            using var req = UnityWebRequest.Get(ApiUrl);
            req.SetRequestHeader("User-Agent", $"ArmsFair/{Application.version}");
            req.timeout = Timeout;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                State = UpdateState.UpToDate;
                OnCheckFailed?.Invoke(req.error);
                yield break;
            }

            ReleaseInfo info;
            try
            {
                info = ParseRelease(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                State = UpdateState.UpToDate;
                OnCheckFailed?.Invoke($"Parse error: {ex.Message}");
                yield break;
            }

            bool isNewer;
            try
            {
                isNewer = new System.Version(info.Version) > new System.Version(Application.version);
            }
            catch
            {
                State = UpdateState.UpToDate;
                yield break;
            }

            if (!isNewer)
            {
                State = UpdateState.UpToDate;
                yield break;
            }

            LatestRelease = info;
            State         = UpdateState.UpdateAvailable;
            OnUpdateAvailable?.Invoke(info);
        }

        private static ReleaseInfo ParseRelease(string json)
        {
            var root    = JObject.Parse(json);
            var tagName = root["tag_name"]?.Value<string>() ?? "";
            var version = tagName.TrimStart('v');
            var name    = root["name"]?.Value<string>() ?? tagName;
            var body    = root["body"]?.Value<string>() ?? "";
            var pageUrl = root["html_url"]?.Value<string>() ?? "";
            var notes   = body.Length > 200 ? body[..200] + "..." : body;

            string downloadUrl = "";
            bool   hasDirect   = false;

            var assets = root["assets"] as JArray;
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    var assetName = asset["name"]?.Value<string>() ?? "";
                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset["browser_download_url"]?.Value<string>() ?? "";
                        hasDirect   = !string.IsNullOrEmpty(downloadUrl);
                        break;
                    }
                }
            }

            return new ReleaseInfo
            {
                Version           = version,
                DownloadUrl       = downloadUrl,
                ReleaseName       = name,
                ReleaseNotes      = notes,
                ReleasePageUrl    = pageUrl,
                HasDirectDownload = hasDirect,
            };
        }

        public void StartDownload()
        {
            if (State != UpdateState.UpdateAvailable || LatestRelease == null) return;
            StartCoroutine(DownloadCoroutine(LatestRelease.Value));
        }

        private IEnumerator DownloadCoroutine(ReleaseInfo info)
        {
            State         = UpdateState.Downloading;
            _downloadPath = Path.Combine(Application.temporaryCachePath, "ArmsFair_Update.exe");

            _downloadRequest = UnityWebRequest.Get(info.DownloadUrl);
            _downloadRequest.downloadHandler = new DownloadHandlerFile(_downloadPath);
            _downloadRequest.SendWebRequest();

            while (!_downloadRequest.isDone)
            {
                DownloadProgress = _downloadRequest.downloadProgress;
                OnDownloadProgress?.Invoke(DownloadProgress);
                yield return null;
            }

            if (_downloadRequest.result != UnityWebRequest.Result.Success)
            {
                var err = _downloadRequest.error;
                _downloadRequest.Dispose();
                _downloadRequest = null;
                State            = UpdateState.UpdateAvailable;
                OnCheckFailed?.Invoke($"Download failed: {err}");
                yield break;
            }

            _downloadRequest.Dispose();
            _downloadRequest = null;
            State            = UpdateState.Launching;
            OnDownloadComplete?.Invoke(_downloadPath);
        }

        public void LaunchInstaller()
        {
            if (State != UpdateState.Launching || string.IsNullOrEmpty(_downloadPath)) return;
            System.Diagnostics.Process.Start(_downloadPath);
            Application.Quit();
        }

        public void CancelDownload()
        {
            if (State != UpdateState.Downloading) return;
            _downloadRequest?.Abort();
            _downloadRequest?.Dispose();
            _downloadRequest = null;

            if (File.Exists(_downloadPath))
                File.Delete(_downloadPath);

            State = UpdateState.UpdateAvailable;
        }
    }
}
