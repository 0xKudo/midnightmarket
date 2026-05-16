using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        private IntPtr  _jobHandle = IntPtr.Zero;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Kill server if Unity process exits without OnApplicationQuit (crash, force-close)
            AppDomain.CurrentDomain.ProcessExit += (_, __) => StopServer();
        }

        private void OnApplicationQuit() => StopServer();

        // ── Public API ───────────────────────────────────────────────────────

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

            // Tie server lifetime to this process via a Windows Job Object so it
            // dies even if Unity is force-killed or crashes before OnApplicationQuit fires.
            AssignToJobObject(_serverProcess);

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
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try   { _serverProcess.Kill(); }
                catch { /* process may have already exited */ }
            }
            _serverProcess = null;

            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
        }

        public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

        // ── Windows Job Object — kills child when parent dies ─────────────────

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int infoType,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpInfo, uint cbInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long  PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint  LimitFlags, MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint  ActiveProcessLimit, Affinity, PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        private void AssignToJobObject(Process process)
        {
            try
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero) return;

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                SetInformationJobObject(_jobHandle, JobObjectExtendedLimitInformation,
                    ref info, (uint)Marshal.SizeOf(info));

                AssignProcessToJobObject(_jobHandle, process.Handle);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ServerHostManager] Job object setup failed: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetServerExePath()
        {
#if UNITY_EDITOR
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
