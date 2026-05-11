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
        /// Starts the bundled server on a free port, waits for it to be ready,
        /// then registers with the relay and returns the invite code.
        /// </summary>
        public async Task<string> StartAndGetInviteCodeAsync()
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

            await WaitForServerReadyAsync(_serverPort, timeoutMs: 15000);

            Network.NetworkConfig.ServerBaseUrl = $"http://localhost:{_serverPort}";
            Network.NetworkConfig.IsHost        = true;

            // Server's RelayTunnelService registers with VPS in the background; poll until it has a code
            var code = await WaitForRelayCodeAsync(_serverPort, timeoutMs: 15000);
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

        private static async Task<string> WaitForRelayCodeAsync(int port, int timeoutMs)
        {
            var url      = $"http://localhost:{port}/relay-code";
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
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

                await Task.Delay(500);
            }

            throw new Exception("Server did not receive a relay code within the timeout. Check VPS connection.");
        }

        private static async Task WaitForServerReadyAsync(int port, int timeoutMs)
        {
            var url      = $"http://localhost:{port}/health";
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var req = UnityWebRequest.Get(url);
                    req.timeout = 2;
                    await req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) return;
                }
                catch { /* not ready yet */ }

                await Task.Delay(500);
            }

            throw new Exception($"Server did not become ready within {timeoutMs}ms");
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
