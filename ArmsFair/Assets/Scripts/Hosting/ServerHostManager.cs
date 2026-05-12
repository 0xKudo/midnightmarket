using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ArmsFair.Hosting
{
    [Serializable] internal class RelayCodeResponse { public string code; }

    public class ServerHostManager : MonoBehaviour
    {
        public static ServerHostManager Instance { get; private set; }

        private Process _serverProcess;
        private int     _serverPort;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnApplicationQuit() => StopServer();

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Starts the bundled server on a free port, waits indefinitely for it to be ready,
        /// then registers with the relay and returns the invite code.
        /// Pass a CancellationToken to allow the player to cancel. Pass onStatus to receive
        /// human-readable progress updates for the splash screen.
        /// </summary>
        public async Task<string> StartAndGetInviteCodeAsync(
            System.Threading.CancellationToken ct = default,
            Action<string> onStatus = null)
        {
            _serverPort = FindFreePort();

            var serverExe = GetServerExePath();
            if (!File.Exists(serverExe))
                throw new Exception($"Server executable not found at: {serverExe}");

            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = serverExe,
                    Arguments              = $"--urls http://0.0.0.0:{_serverPort}",
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                }
            };
            _serverProcess.OutputDataReceived += (_, e) => { if (e.Data != null) UnityEngine.Debug.Log($"[Server] {e.Data}"); };
            _serverProcess.ErrorDataReceived  += (_, e) => { if (e.Data != null) UnityEngine.Debug.LogError($"[Server] {e.Data}"); };
            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            UnityEngine.Debug.Log($"[ServerHostManager] Server started on port {_serverPort} (pid {_serverProcess.Id})");

            onStatus?.Invoke("STARTING LOCAL SERVER...");
            await WaitForServerReadyAsync(_serverPort, ct, onStatus);

            Network.NetworkConfig.ServerBaseUrl = $"http://localhost:{_serverPort}";
            Network.NetworkConfig.IsHost        = true;

            onStatus?.Invoke("CONNECTING TO RELAY...");
            var code = await WaitForRelayCodeAsync(_serverPort, ct, onStatus);
            Network.NetworkConfig.RelayCode = code;

            UnityEngine.Debug.Log($"[ServerHostManager] Relay code: {code}");
            return code;
        }

        public void StopServer()
        {
            if (_serverProcess == null || _serverProcess.HasExited) return;
            try   { _serverProcess.Kill(); }
            catch { /* process may have already exited */ }
            _serverProcess = null;
        }

        public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetServerExePath()
        {
#if UNITY_EDITOR
            // During editor play, look for a local dev build next to the project
            var devPath = Path.Combine(
                Application.dataPath, "..", "..", "ArmsFair.Server",
                "bin", "Release", "net8.0", "win-x64", "publish", "ArmsFair.Server.exe");
            if (File.Exists(devPath)) return Path.GetFullPath(devPath);
#endif
            return Path.Combine(
                Application.streamingAssetsPath, "Server~", "ArmsFair.Server.exe");
        }

        private static async Task<string> WaitForRelayCodeAsync(
            int port, System.Threading.CancellationToken ct, Action<string> onStatus)
        {
            var url     = $"http://localhost:{port}/relay-code";
            var started = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var req = UnityWebRequest.Get(url);
                    req.timeout = 2;
                    await req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var r = JsonUtility.FromJson<RelayCodeResponse>(req.downloadHandler.text);
                        if (!string.IsNullOrEmpty(r?.code)) return r.code;
                    }
                }
                catch { /* not ready yet */ }

                var elapsed = (int)(DateTime.UtcNow - started).TotalSeconds;
                onStatus?.Invoke($"CONNECTING TO RELAY... {elapsed}s");
                await Task.Delay(500, ct);
            }

            ct.ThrowIfCancellationRequested();
            return null;
        }

        private static async Task WaitForServerReadyAsync(
            int port, System.Threading.CancellationToken ct, Action<string> onStatus)
        {
            var url     = $"http://localhost:{port}/health";
            var started = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var req = UnityWebRequest.Get(url);
                    req.timeout = 2;
                    await req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) return;
                }
                catch { /* not ready yet */ }

                var elapsed = (int)(DateTime.UtcNow - started).TotalSeconds;
                onStatus?.Invoke($"STARTING LOCAL SERVER... {elapsed}s");
                await Task.Delay(500, ct);
            }

            ct.ThrowIfCancellationRequested();
        }

        private static int FindFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
