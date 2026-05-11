using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ArmsFair.Server.Services
{
    public class RelayTunnelService : BackgroundService
    {
        private readonly ILogger<RelayTunnelService> _log;
        private readonly IHttpClientFactory          _http;
        private readonly IServer                     _server;

        public string RelayCode { get; private set; } = "";

        private const string TunnelUrl = "wss://armsfair.laynekudo.com/arms-tunnel";

        public RelayTunnelService(
            ILogger<RelayTunnelService> log,
            IHttpClientFactory http,
            IServer server)
        {
            _log    = log;
            _http   = http;
            _server = server;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            // Wait for Kestrel to bind and expose its addresses
            string localBase;
            while (true)
            {
                if (ct.IsCancellationRequested) return;
                var addr = _server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
                if (addr != null)
                {
                    localBase = addr.Replace("0.0.0.0", "localhost")
                                   .Replace("[::]", "localhost")
                                   .TrimEnd('/');
                    break;
                }
                await Task.Delay(100, ct);
            }

            _log.LogInformation("[RelayTunnel] Local server at {Base}", localBase);

            while (!ct.IsCancellationRequested)
            {
                try   { await RunTunnelAsync(localBase, ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.LogWarning("[RelayTunnel] Disconnected: {Msg}. Reconnecting in 5s…", ex.Message);
                    await Task.Delay(5000, ct);
                }
            }
        }

        // ── Main tunnel loop ─────────────────────────────────────────────────────

        private async Task RunTunnelAsync(string localBase, CancellationToken ct)
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(TunnelUrl), ct);
            _log.LogInformation("[RelayTunnel] Connected to relay");

            var writeLock   = new SemaphoreSlim(1, 1);
            var localWsMap  = new ConcurrentDictionary<string, ClientWebSocket>();
            var recvBuffer  = new byte[64 * 1024];

            async Task SendAsync(string json)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await writeLock.WaitAsync(ct);
                try   { await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
                finally { writeLock.Release(); }
            }

            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(recvBuffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(recvBuffer, 0, result.Count);
                var doc  = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "registered":
                        RelayCode = doc.RootElement.GetProperty("code").GetString()!;
                        _log.LogInformation("[RelayTunnel] Relay code: {Code}", RelayCode);
                        break;

                    case "http_req":
                        _ = HandleHttpAsync(doc.RootElement, localBase, SendAsync, ct);
                        break;

                    case "ws_open":
                        _ = HandleWsOpenAsync(doc.RootElement, localBase, localWsMap, SendAsync, ct);
                        break;

                    case "ws_frame":
                    {
                        var cid = doc.RootElement.GetProperty("cid").GetString()!;
                        if (localWsMap.TryGetValue(cid, out var localWs) &&
                            localWs.State == WebSocketState.Open)
                        {
                            var data    = Convert.FromBase64String(doc.RootElement.GetProperty("data").GetString()!);
                            var isBin   = doc.RootElement.GetProperty("bin").GetBoolean();
                            var msgType = isBin ? WebSocketMessageType.Binary : WebSocketMessageType.Text;
                            await localWs.SendAsync(data, msgType, true, ct);
                        }
                        break;
                    }

                    case "ws_client_close":
                    {
                        var cid = doc.RootElement.GetProperty("cid").GetString()!;
                        if (localWsMap.TryRemove(cid, out var localWs))
                        {
                            try { await localWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                            catch { /* ignore */ }
                            localWs.Dispose();
                        }
                        break;
                    }
                }
            }

            foreach (var (_, localWs) in localWsMap)
            {
                try { await localWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
                localWs.Dispose();
            }
        }

        // ── HTTP forwarding ──────────────────────────────────────────────────────

        private async Task HandleHttpAsync(
            JsonElement msg, string localBase,
            Func<string, Task> send, CancellationToken ct)
        {
            var id = msg.GetProperty("id").GetString()!;
            try
            {
                var method  = msg.GetProperty("method").GetString()!;
                var path    = msg.GetProperty("path").GetString()!;
                using var client  = _http.CreateClient();
                var request = new HttpRequestMessage(new HttpMethod(method), localBase + path);

                if (msg.TryGetProperty("headers", out var hdrs))
                    foreach (var h in hdrs.EnumerateObject())
                    {
                        var k = h.Name.ToLower();
                        if (k is "host" or "connection" or "transfer-encoding" or "content-length") continue;
                        try { request.Headers.TryAddWithoutValidation(h.Name, h.Value.GetString()); } catch { }
                    }

                if (msg.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind != JsonValueKind.Null)
                {
                    var bodyBytes = Convert.FromBase64String(bodyEl.GetString()!);
                    var content   = new ByteArrayContent(bodyBytes);
                    if (request.Headers.TryGetValues("Content-Type", out var ct2))
                        content.Headers.TryAddWithoutValidation("Content-Type", string.Join(", ", ct2));
                    request.Content = content;
                    request.Headers.Remove("Content-Type");
                }

                using var resp = await client.SendAsync(request, ct);
                var body = await resp.Content.ReadAsByteArrayAsync(ct);

                var headers = new List<string[]>();
                foreach (var h in resp.Headers)
                    foreach (var v in h.Value) headers.Add([h.Key, v]);
                foreach (var h in resp.Content.Headers)
                    foreach (var v in h.Value) headers.Add([h.Key, v]);

                await send(JsonSerializer.Serialize(new
                {
                    type = "http_res", id,
                    status  = (int)resp.StatusCode,
                    headers,
                    body = Convert.ToBase64String(body)
                }));
            }
            catch (Exception ex)
            {
                _log.LogWarning("[RelayTunnel] HTTP forward error: {Msg}", ex.Message);
                await send(JsonSerializer.Serialize(new
                {
                    type = "http_res", id, status = 500,
                    headers = Array.Empty<string[]>(),
                    body    = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"error\":\"tunnel error\"}"))
                }));
            }
        }

        // ── WebSocket forwarding ─────────────────────────────────────────────────

        private async Task HandleWsOpenAsync(
            JsonElement msg, string localBase,
            ConcurrentDictionary<string, ClientWebSocket> localWsMap,
            Func<string, Task> send, CancellationToken ct)
        {
            var cid  = msg.GetProperty("cid").GetString()!;
            var path = msg.GetProperty("path").GetString()!;

            var localWs = new ClientWebSocket();

            if (msg.TryGetProperty("headers", out var hdrs))
                foreach (var h in hdrs.EnumerateObject())
                    if (h.Name.ToLower() == "sec-websocket-protocol")
                        localWs.Options.AddSubProtocol(h.Value.GetString()!);

            var wsBase = localBase.Replace("http://", "ws://").Replace("https://", "wss://");
            await localWs.ConnectAsync(new Uri(wsBase + path), ct);
            localWsMap[cid] = localWs;

            _ = Task.Run(async () =>
            {
                var buf = new byte[64 * 1024];
                try
                {
                    while (localWs.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    {
                        var result = await localWs.ReceiveAsync(buf, ct);
                        if (result.MessageType == WebSocketMessageType.Close) break;

                        await send(JsonSerializer.Serialize(new
                        {
                            type = "ws_frame", cid,
                            data = Convert.ToBase64String(buf, 0, result.Count),
                            bin  = result.MessageType == WebSocketMessageType.Binary
                        }));
                    }
                }
                catch { /* tunnel or local WS closed */ }
                finally
                {
                    localWsMap.TryRemove(cid, out _);
                    try { await send(JsonSerializer.Serialize(new { type = "ws_close", cid })); } catch { }
                    try { localWs.Dispose(); } catch { }
                }
            }, ct);
        }
    }
}
