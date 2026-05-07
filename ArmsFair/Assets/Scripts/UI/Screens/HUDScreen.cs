using System;
using ArmsFair.Auth;
using ArmsFair.Network;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class HUDScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;

        // Top bar
        private Label _marketHeatLabel;
        private Label _civilianCostLabel;
        private Label _stabilityLabel;
        private Label _sanctionsRiskLabel;
        private Label _geoTensionLabel;
        private Label _phaseLabel;
        private Label _timerLabel;
        private Label _roundLabel;

        // Dashboard
        private Label      _companyLabel;
        private Label      _capitalLabel;
        private Label      _reputationLabel;
        private Label      _sharePriceLabel;
        private Label      _peaceCreditsLabel;
        private Label      _latentRiskLabel;
        private ScrollView _playerList;

        // Centre
        private Label _phaseStatusLabel;
        private Label _statusLabel;

        // Timer state
        private long _phaseEndsAt;
        private bool _timerRunning;

        // Cached state
        private GameState _lastState;

        private void Awake()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            docRoot.style.position = Position.Absolute;
            docRoot.style.left     = 0;
            docRoot.style.top      = 0;
            docRoot.style.right    = 0;
            docRoot.style.bottom   = 0;
            docRoot.style.width    = new StyleLength(Length.Percent(100));
            docRoot.style.height   = new StyleLength(Length.Percent(100));

            _root = docRoot.Q("HUDScreen");
            if (_root == null) { Debug.LogError("[HUDScreen] Root element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _marketHeatLabel    = _root.Q<Label>("MarketHeatLabel");
            _civilianCostLabel  = _root.Q<Label>("CivilianCostLabel");
            _stabilityLabel     = _root.Q<Label>("StabilityLabel");
            _sanctionsRiskLabel = _root.Q<Label>("SanctionsRiskLabel");
            _geoTensionLabel    = _root.Q<Label>("GeoTensionLabel");
            _phaseLabel         = _root.Q<Label>("PhaseLabel");
            _timerLabel         = _root.Q<Label>("TimerLabel");
            _roundLabel         = _root.Q<Label>("RoundLabel");
            _companyLabel       = _root.Q<Label>("CompanyLabel");
            _capitalLabel       = _root.Q<Label>("CapitalLabel");
            _reputationLabel    = _root.Q<Label>("ReputationLabel");
            _sharePriceLabel    = _root.Q<Label>("SharePriceLabel");
            _peaceCreditsLabel  = _root.Q<Label>("PeaceCreditsLabel");
            _latentRiskLabel    = _root.Q<Label>("LatentRiskLabel");
            _playerList         = _root.Q<ScrollView>("PlayerList");
            _phaseStatusLabel   = _root.Q<Label>("PhaseStatusLabel");
            _statusLabel        = _root.Q<Label>("StatusLabel");

            _root.Q<Button>("LeaveGameBtn").clicked += OnLeaveGame;
            TerminalUI.StyleDangerButton(_root.Q<Button>("LeaveGameBtn"));

            UIManager.Instance.Register("HUD", this);
        }

        private void Start()
        {
            if (GameClient.Instance == null) return;
            GameClient.Instance.OnStateSync.AddListener(OnStateSync);
            GameClient.Instance.OnPhaseStart.AddListener(OnPhaseStart);
            GameClient.Instance.OnConsequences.AddListener(OnConsequences);
            GameClient.Instance.OnWorldUpdate.AddListener(OnWorldUpdate);
        }

        private void OnDestroy()
        {
            if (GameClient.Instance == null) return;
            GameClient.Instance.OnStateSync.RemoveListener(OnStateSync);
            GameClient.Instance.OnPhaseStart.RemoveListener(OnPhaseStart);
            GameClient.Instance.OnConsequences.RemoveListener(OnConsequences);
            GameClient.Instance.OnWorldUpdate.RemoveListener(OnWorldUpdate);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
            if (_lastState != null) BindState(_lastState);
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _timerRunning = false;
        }

        private void Update()
        {
            if (!_timerRunning || _timerLabel == null) return;
            var remaining = _phaseEndsAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (remaining <= 0)
            {
                _timerLabel.text = "00:00";
                _timerRunning    = false;
                return;
            }
            var secs = (int)(remaining / 1000);
            _timerLabel.text = $"{secs / 60:D2}:{secs % 60:D2}";
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnStateSync(StateSync msg)
        {
            _lastState = msg.FullState;
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            BindState(msg.FullState);
        }

        private void OnPhaseStart(PhaseStartMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            _phaseLabel.text     = msg.Phase.ToString().ToUpper();
            _roundLabel.text     = msg.Round > 0 ? $"ROUND {msg.Round}" : "SETUP";
            _phaseEndsAt         = msg.EndsAt;
            _timerRunning        = true;
            _phaseStatusLabel.text = $"PHASE: {msg.Phase.ToString().ToUpper()}";
            _statusLabel.text    = $"PHASE STARTED — ROUND {msg.Round}";
        }

        private void OnConsequences(ConsequencesMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            if (_lastState == null) return;

            var localId = AccountManager.Instance.LocalPlayer?.Id;
            foreach (var p in msg.ProfitUpdates)
            {
                if (p.PlayerId != localId) continue;
                _capitalLabel.text = $"${p.NewCapital / 1_000_000}M";
            }
            foreach (var r in msg.ReputationUpdates)
            {
                if (r.PlayerId != localId) continue;
                _reputationLabel.text = r.NewReputation.ToString();
            }
            foreach (var s in msg.SharePriceUpdates)
            {
                if (s.PlayerId != localId) continue;
                _sharePriceLabel.text = $"${s.NewPrice}";
            }
        }

        private void OnWorldUpdate(WorldUpdateMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            BindTracks(msg.NewTracks);
        }

        // ── Binding ──────────────────────────────────────────────────────────

        private void BindState(GameState state)
        {
            BindTracks(state.Tracks);

            _roundLabel.text = state.Round > 0 ? $"ROUND {state.Round}" : "SETUP";
            _phaseLabel.text = state.Round > 0 ? state.Phase.ToString().ToUpper() : "AWAITING START";

            // Local player stats
            var localId = AccountManager.Instance.LocalPlayer?.Id;
            var me = state.Players.Find(p => p.Id == localId);
            _companyLabel.text      = AccountManager.Instance.LocalPlayer?.CompanyName ?? AccountManager.Instance.LocalPlayer?.Username ?? "—";
            _capitalLabel.text      = me != null ? $"${me.Capital / 1_000_000}M"    : "$--M";
            _reputationLabel.text   = me != null ? me.Reputation.ToString()          : "--";
            _sharePriceLabel.text   = me != null ? $"${me.SharePrice}"               : "$--";
            _peaceCreditsLabel.text = me != null ? me.PeaceCredits.ToString()        : "--";
            _latentRiskLabel.text   = me != null ? me.LatentRisk.ToString()          : "--";

            // Player list
            _playerList.Clear();
            foreach (var player in state.Players)
            {
                bool isMe   = player.Id == localId;
                var  label  = new Label(isMe ? $"> {player.Username.ToUpper()}  [YOU]"
                                             : $"  {player.Username.ToUpper()}");
                label.style.color      = new StyleColor(isMe
                    ? new Color(138f/255f, 184f/255f, 112f/255f)
                    : new Color(0.831f, 0.812f, 0.722f));
                label.style.fontSize   = 10;
                label.style.paddingTop    = 3;
                label.style.paddingBottom = 3;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                _playerList.Add(label);
            }
        }

        private void BindTracks(WorldTracks t)
        {
            _marketHeatLabel.text    = $"MKT: {t.MarketHeat}";
            _civilianCostLabel.text  = $"CIV: {t.CivilianCost}";
            _stabilityLabel.text     = $"STB: {t.Stability}";
            _sanctionsRiskLabel.text = $"SAN: {t.SanctionsRisk}";
            _geoTensionLabel.text    = $"GEO: {t.GeoTension}";

            SetTrackColor(_marketHeatLabel,    t.MarketHeat,    inverted: false);
            SetTrackColor(_civilianCostLabel,  t.CivilianCost,  inverted: false);
            SetTrackColor(_stabilityLabel,     t.Stability,     inverted: true);
            SetTrackColor(_sanctionsRiskLabel, t.SanctionsRisk, inverted: false);
            SetTrackColor(_geoTensionLabel,    t.GeoTension,    inverted: false);
        }

        // inverted=true means high value is GOOD (stability)
        private static void SetTrackColor(Label label, int value, bool inverted)
        {
            bool danger  = inverted ? value < 30  : value > 70;
            bool warning = inverted ? value < 50  : value > 50;
            label.style.color = new StyleColor(
                danger  ? new Color(192f/255f, 100f/255f, 100f/255f) :
                warning ? new Color(212f/255f, 180f/255f, 100f/255f) :
                          new Color(138f/255f, 184f/255f, 112f/255f));
        }

        private void OnLeaveGame()
        {
            _timerRunning = false;
            _lastState    = null;
            UIManager.Instance.GoTo("MainMenu");
        }
    }
}
