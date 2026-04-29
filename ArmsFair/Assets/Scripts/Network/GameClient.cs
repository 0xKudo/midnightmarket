using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;
using UnityEngine.Events;

namespace ArmsFair.Network
{
    /// <summary>
    /// Singleton that owns the SignalR connection to the server.
    /// Attach to a persistent GameObject in the bootstrap scene.
    /// </summary>
    public class GameClient : MonoBehaviour
    {
        public static GameClient Instance { get; private set; }

        [Header("Server")]
        [SerializeField] private string serverUrl = "http://localhost:5000/gamehub";

        public string ServerUrl
        {
            get => serverUrl;
            set => serverUrl = value.TrimEnd('/') + "/gamehub";
        }

        // ── Connection state ─────────────────────────────────────────────────
        public bool IsConnected => _hub?.State == HubConnectionState.Connected;
        public string GameId    { get; private set; }
        public string PlayerId  { get; private set; }

        private HubConnection _hub;
        private CancellationTokenSource _cts;

        // ── Events (subscribe from UI / game scripts) ────────────────────────
        public UnityEvent<PhaseStartMessage>      OnPhaseStart      = new();
        public UnityEvent<RevealMessage>          OnReveal          = new();
        public UnityEvent<ConsequencesMessage>    OnConsequences    = new();
        public UnityEvent<WorldUpdateMessage>     OnWorldUpdate     = new();
        public UnityEvent<GameEndingMessage>      OnGameEnding      = new();
        public UnityEvent<StateSync>              OnStateSync       = new();
        public UnityEvent<ChatMessage>            OnChatMessage     = new();
        public UnityEvent<ErrorMessage>           OnError           = new();

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _ = _hub?.DisposeAsync();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Call once after login to establish the hub connection.</summary>
        public async Task ConnectAsync(string jwtToken)
        {
            if (_hub != null) await _hub.DisposeAsync();
            _cts = new CancellationTokenSource();

            _hub = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}?access_token={jwtToken}")
                .WithAutomaticReconnect()
                .Build();

            RegisterHandlers();

            try
            {
                await _hub.StartAsync(_cts.Token);
                Debug.Log($"[GameClient] Connected to {serverUrl}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameClient] Connection failed: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            if (_hub != null) await _hub.StopAsync();
        }

        // ── Hub method invocations ───────────────────────────────────────────

        public Task CreateGameAsync(LobbySettingsMessage settings) =>
            InvokeAsync("CreateGame", settings);

        public Task JoinGameAsync(string gameId) =>
            InvokeAsync("JoinGame", gameId);

        public Task SubmitActionAsync(SubmitActionMessage action) =>
            InvokeAsync("SubmitAction", GameId, action);

        public Task SendChatAsync(string text, string recipientId = null, bool isPrivate = false) =>
            InvokeAsync("SendChat", GameId, new ChatMessage(
                PlayerId, text, recipientId, isPrivate, IsSystem: false));

        public Task VoteCeaseFireAsync() =>
            InvokeAsync("VoteCeaseFire", GameId);

        public Task FundCoupAsync(string targetIso) =>
            InvokeAsync("FundCoup", GameId, new FundCoupMessage(targetIso));

        public Task ProposeTreatyAsync(List<string> participantIds, string terms, int durationRounds) =>
            InvokeAsync("ProposeTreaty", GameId, new ProposeTreatyMessage(participantIds, terms, durationRounds));

        public Task WhistleAsync(int level, string targetPlayerId) =>
            InvokeAsync("Whistle", GameId, new WhistleMessage(level, targetPlayerId));

        public Task InvestInPeacekeepingAsync(string targetIso) =>
            InvokeAsync("InvestInPeacekeeping", GameId, new PeacekeepingMessage(targetIso));

        // ── Server → Client handlers ─────────────────────────────────────────

        private void RegisterHandlers()
        {
            _hub.On<PhaseStartMessage>("PhaseStart", msg =>
                RunOnMainThread(() => OnPhaseStart.Invoke(msg)));

            _hub.On<RevealMessage>("Reveal", msg =>
                RunOnMainThread(() => OnReveal.Invoke(msg)));

            _hub.On<ConsequencesMessage>("Consequences", msg =>
                RunOnMainThread(() => OnConsequences.Invoke(msg)));

            _hub.On<WorldUpdateMessage>("WorldUpdate", msg =>
                RunOnMainThread(() => OnWorldUpdate.Invoke(msg)));

            _hub.On<GameEndingMessage>("GameEnding", msg =>
                RunOnMainThread(() => OnGameEnding.Invoke(msg)));

            _hub.On<StateSync>("StateSync", msg =>
            {
                GameId = msg.FullState.GameId;
                RunOnMainThread(() => OnStateSync.Invoke(msg));
            });

            _hub.On<ChatMessage>("ChatMessage", msg =>
                RunOnMainThread(() => OnChatMessage.Invoke(msg)));

            _hub.On<ErrorMessage>("Error", msg =>
            {
                Debug.LogWarning($"[GameClient] Server error {msg.Code}: {msg.Message}");
                RunOnMainThread(() => OnError.Invoke(msg));
            });

            _hub.Reconnected += connectionId =>
            {
                Debug.Log($"[GameClient] Reconnected: {connectionId}");
                return Task.CompletedTask;
            };

            _hub.Closed += ex =>
            {
                Debug.LogWarning($"[GameClient] Connection closed: {ex?.Message}");
                return Task.CompletedTask;
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task InvokeAsync(string method, params object[] args)
        {
            if (!IsConnected) { Debug.LogWarning($"[GameClient] Not connected — cannot invoke {method}"); return; }
            try   { await _hub.SendCoreAsync(method, args, _cts.Token); }
            catch (Exception ex) { Debug.LogError($"[GameClient] {method} failed: {ex.Message}"); }
        }

        // SignalR callbacks arrive on a thread pool thread — marshal to Unity main thread
        private void RunOnMainThread(Action action) =>
            UnityMainThreadDispatcher.Enqueue(action);
    }
}
