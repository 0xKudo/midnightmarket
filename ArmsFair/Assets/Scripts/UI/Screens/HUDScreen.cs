using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArmsFair.Auth;
using ArmsFair.Network;
using ArmsFair.Shared;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class HUDScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private VisualElement _docRoot;

        // Top bar
        private Label _marketHeatLabel;
        private Label _civilianCostLabel;
        private Label _instabilityLabel;
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

        // Procurement panel
        private VisualElement                    _procurementPanel;
        private ScrollView                       _weaponList;
        private Label                            _procTotalLabel;
        private Label                            _procErrorLabel;
        private Button                           _confirmProcBtn;
        private Dictionary<WeaponCategory, int>  _quantities  = new();
        private Dictionary<WeaponCategory, int>  _inventory   = new();
        private int                              _procCapitalM;
        private WeaponTab                        _procActiveTab  = WeaponTab.Light;
        private WeaponTab                        _salesActiveTab = WeaponTab.Light;
        private VisualElement                    _procTabBar;

        // Persistent inventory bar
        private VisualElement _inventoryBar;
        private VisualElement _inventoryItems;

        // Country info card (globe click popup)
        private VisualElement _countryInfoCard;
        private Label         _cardCountryName;
        private Label         _cardStageLabel;
        private Label         _cardTensionLabel;
        private string        _cardIso;

        // Globe camera viewport sync
        private VisualElement _worldMapArea;
        private Camera        _globeCamera;

        // Negotiation panel
        private VisualElement _negotiationPanel;
        private VisualElement _negoIntelTab;
        private VisualElement _negoPeaceTab;
        private VisualElement _negoTreatyTab;
        private ScrollView    _negoTracksList;
        private ScrollView    _negoRevealList;
        private Label         _ceaseFireVotersLabel;
        private Button        _voteCeaseFireBtn;
        private bool          _hasVotedCeaseFire;
        private int           _ceaseFireVoterCount;

        // Reveal panel
        private VisualElement _revealPanel;
        private ScrollView    _revealList;
        private RevealMessage _lastReveal;

        // Consequences panel
        private VisualElement _consequencesPanel;
        private ScrollView    _profitList;
        private ScrollView    _blowbackList;
        private ScrollView    _repList;
        private ScrollView    _sharePriceList;

        // WorldUpdate panel (built in code)
        private VisualElement _worldUpdatePanel;
        private ScrollView    _worldUpdateDeltaList;

        // Open modal overlays -- cleared on every phase transition
        private readonly List<VisualElement> _openModals = new();

        // Sales panel
        private VisualElement                _salesPanel;
        private VisualElement                _saleTypeRow;
        private VisualElement                _weaponRow;
        private VisualElement                _countryRow;
        private VisualElement                _modifierRow;
        private Button                       _weaponPickerBtn;
        private Button                       _countryPickerBtn;
        private Label                        _saleEstimateLabel;
        private Label                        _peaceBrokerNote;
        private Label                        _saleErrorLabel;
        private Button                       _submitSaleBtn;
        private Button                       _dualSupplyBtn;
        private Button                       _proxyBtn;
        private SaleType                     _selectedSaleType   = SaleType.Open;
        private string                       _selectedCountryIso = null;
        private bool                         _isDualSupply       = false;
        private bool                         _isProxyRouted      = false;
        private Dictionary<WeaponCategory, int> _salesOrder      = new();
        private Dictionary<SaleType, Button> _saleTypeBtns       = new();

        // Ready buttons (one per phase panel)
        private List<Button> _readyBtns = new();

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

            _docRoot = docRoot;
            Debug.Log($"[HUDScreen] docRoot childCount={docRoot.childCount}, first={docRoot.Children().FirstOrDefault()?.name}");
            _root = docRoot.Q("HUDScreen");
            if (_root == null) { Debug.LogError("[HUDScreen] Root element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _marketHeatLabel    = _root.Q<Label>("MarketHeatLabel");
            _civilianCostLabel  = _root.Q<Label>("CivilianCostLabel");
            _instabilityLabel     = _root.Q<Label>("InstabilityLabel");
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
            if (_playerList != null)
                _playerList.contentContainer.style.flexDirection = FlexDirection.Row;
            _phaseStatusLabel   = _root.Q<Label>("PhaseStatusLabel");
            _statusLabel        = _root.Q<Label>("StatusLabel");

            var leaveBtn = _root.Q<Button>("LeaveGameBtn");
            leaveBtn.clicked += OnLeaveGame;
            TerminalUI.StyleDangerButton(leaveBtn);
            leaveBtn.style.paddingTop    = 4;
            leaveBtn.style.paddingBottom = 4;
            leaveBtn.style.paddingLeft   = 12;
            leaveBtn.style.paddingRight  = 12;
            leaveBtn.style.fontSize      = 15;

            _root.Query<Button>("ReadyBtn").ForEach(btn =>
            {
                btn.clicked += OnReadyClicked;
                TerminalUI.AddHover(btn);
                _readyBtns.Add(btn);
            });

            _inventoryBar   = _root.Q("InventoryBar");
            _inventoryItems = _root.Q("InventoryItems");

            _procurementPanel = _root.Q("ProcurementPanel");
            _weaponList       = _root.Q<ScrollView>("WeaponList");
            _procTotalLabel   = _root.Q<Label>("ProcTotalLabel");
            _procErrorLabel   = _root.Q<Label>("ProcErrorLabel");
            _confirmProcBtn   = _root.Q<Button>("ConfirmProcBtn");
            if (_confirmProcBtn != null) { _confirmProcBtn.clicked += OnConfirmProcurement; TerminalUI.AddHover(_confirmProcBtn); }
            var skipProcBtn = _root.Q<Button>("SkipProcBtn");
            if (skipProcBtn != null) { skipProcBtn.clicked += OnReadyClicked; TerminalUI.AddHover(skipProcBtn); }

            _salesPanel        = _root.Q("SalesPanel");
            _saleTypeRow       = _root.Q("SaleTypeRow");
            _weaponRow         = _root.Q("WeaponRow");
            _countryRow        = _root.Q("CountryRow");
            _modifierRow       = _root.Q("ModifierRow");
            _weaponPickerBtn   = _root.Q<Button>("WeaponPickerBtn");
            _countryPickerBtn  = _root.Q<Button>("CountryPickerBtn");
            _saleEstimateLabel = _root.Q<Label>("SaleEstimateLabel");
            _peaceBrokerNote   = _root.Q<Label>("PeaceBrokerNote");
            _saleErrorLabel    = _root.Q<Label>("SaleErrorLabel");
            _submitSaleBtn     = _root.Q<Button>("SubmitSaleBtn");
            if (_weaponPickerBtn  != null) { _weaponPickerBtn.clicked  += OpenWeaponPicker;  TerminalUI.AddHover(_weaponPickerBtn); }
            if (_countryPickerBtn != null) { _countryPickerBtn.clicked += OpenCountryPicker; TerminalUI.AddHover(_countryPickerBtn); }
            if (_submitSaleBtn    != null) { _submitSaleBtn.clicked    += OnSubmitSale;      TerminalUI.AddHover(_submitSaleBtn); }
            var passSaleBtn = _root.Q<Button>("PassSaleBtn");
            if (passSaleBtn != null) { passSaleBtn.clicked += OnPassSale; TerminalUI.AddHover(passSaleBtn); }

            _negotiationPanel     = _root.Q("NegotiationPanel");
            _negoIntelTab         = _root.Q("NegoIntelTab");
            _negoPeaceTab         = _root.Q("NegoPeaceTab");
            _negoTreatyTab        = _root.Q("NegoTreatyTab");
            _negoTracksList       = _root.Q<ScrollView>("NegoTracksList");
            _negoRevealList       = _root.Q<ScrollView>("NegoRevealList");
            _ceaseFireVotersLabel = _root.Q<Label>("CeaseFireVotersLabel");
            _voteCeaseFireBtn     = _root.Q<Button>("VoteCeaseFireBtn");
            if (_voteCeaseFireBtn != null) { _voteCeaseFireBtn.clicked += OnVoteCeaseFire; TerminalUI.AddHover(_voteCeaseFireBtn); }

            var negoIntelBtn  = _root.Q<Button>("NegoIntelBtn");
            var negoPeaceBtn  = _root.Q<Button>("NegoPeaceBtn");
            var negoTreatyBtn = _root.Q<Button>("NegoTreatyBtn");
            if (negoIntelBtn  != null) { negoIntelBtn.clicked  += () => SwitchNegoTab(0); TerminalUI.AddHover(negoIntelBtn); }
            if (negoPeaceBtn  != null) { negoPeaceBtn.clicked  += () => SwitchNegoTab(1); TerminalUI.AddHover(negoPeaceBtn); }
            if (negoTreatyBtn != null) { negoTreatyBtn.clicked += () => SwitchNegoTab(2); TerminalUI.AddHover(negoTreatyBtn); }
            var skipNegoBtn = _root.Q<Button>("SkipNegoBtn");
            if (skipNegoBtn != null) { skipNegoBtn.clicked += OnReadyClicked; TerminalUI.AddHover(skipNegoBtn); }

            _revealPanel = _root.Q("RevealPanel");
            _revealList  = _root.Q<ScrollView>("RevealList");

            _consequencesPanel = _root.Q("ConsequencesPanel");
            _profitList        = _root.Q<ScrollView>("ProfitList");
            _blowbackList      = _root.Q<ScrollView>("BlowbackList");
            _sharePriceList    = _root.Q<ScrollView>("SharePriceList");
            _repList           = _root.Q<ScrollView>("RepList");

            BuildWorldUpdatePanel();

            _countryInfoCard = _root.Q("CountryInfoCard");
            if (_countryInfoCard != null) _countryInfoCard.pickingMode = PickingMode.Position;
            _cardCountryName = _root.Q<Label>("CardCountryName");
            if (_cardCountryName != null) _cardCountryName.style.whiteSpace = WhiteSpace.Normal;
            _cardStageLabel  = _root.Q<Label>("CardStageLabel");
            _cardTensionLabel= _root.Q<Label>("CardTensionLabel");
            var cardCloseBtn = _root.Q<Button>("CardCloseBtn");
            if (cardCloseBtn != null) { cardCloseBtn.clicked += HideCountryInfoCard; TerminalUI.AddHover(cardCloseBtn); }
            var cardSellBtn = _root.Q<Button>("CardSellBtn");
            if (cardSellBtn != null) { cardSellBtn.clicked += OnCardSellClicked; TerminalUI.AddHover(cardSellBtn); }

            _worldMapArea = _root.Q("WorldMapArea");
            _worldMapArea?.RegisterCallback<GeometryChangedEvent>(_ => UpdateGlobeViewport());

            // Dismiss CountryInfoCard when clicking anywhere outside the globe area.
            // WorldMapArea has picking-mode:Ignore so its clicks don't reach UI Toolkit;
            // this handler only fires for left-panel / footer clicks.
            _root.RegisterCallback<PointerDownEvent>(_ => HideCountryInfoCard());

            UIManager.Instance.Register("HUD", this);
        }

        private void Start()
        {
            if (GameClient.Instance == null) return;
            GameClient.Instance.OnStateSync.AddListener(OnStateSync);
            GameClient.Instance.OnPhaseStart.AddListener(OnPhaseStart);
            GameClient.Instance.OnConsequences.AddListener(OnConsequences);
            GameClient.Instance.OnWorldUpdate.AddListener(OnWorldUpdate);
            GameClient.Instance.OnReveal.AddListener(OnReveal);
            GameClient.Instance.OnPlayerReady.AddListener(OnPlayerReady);
            GameClient.Instance.OnCeaseFireVote.AddListener(OnCeaseFireVoteReceived);
            GameClient.Instance.OnGameEnding.AddListener(OnGameEnding);

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private Coroutine _globeReadyCoroutine;

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != "MapGlobe") return;
            if (_globeCamera == null) _globeCamera = Camera.main;
            UpdateGlobeViewport();
            if (_globeReadyCoroutine != null) StopCoroutine(_globeReadyCoroutine);
            _globeReadyCoroutine = StartCoroutine(WaitForGlobeReady());
        }

        private IEnumerator WaitForGlobeReady()
        {
            var globe = ArmsFair.Map.GlobeBridge.Instance;
            if (globe == null || !globe.IsReady)
            {
                // Wait until GlobeBridge exists and fires OnReady
                while (ArmsFair.Map.GlobeBridge.Instance == null)
                    yield return null;
                globe = ArmsFair.Map.GlobeBridge.Instance;
                if (!globe.IsReady)
                {
                    bool ready = false;
                    globe.OnReady += () => ready = true;
                    yield return new WaitUntil(() => ready);
                }
            }
            // Unsubscribe first to prevent double-subscription on scene reload
            globe.OnCountryClicked -= OnGlobeCountryClicked;
            globe.OnCountryClicked += OnGlobeCountryClicked;
            // Paint stage colors with the cached state — StateSync likely arrived before GlobeBridge existed
            if (_lastState?.Countries != null)
            {
                globe.RegisterCountries(_lastState.Countries);
                globe.ApplyAllStages(_lastState.Countries);
            }
            _globeReadyCoroutine = null;
        }

        private void UpdateGlobeViewport()
        {
            if (_globeCamera == null || _worldMapArea == null || _root?.panel == null) return;
            var rootBound = _root.panel.visualTree.worldBound;
            if (rootBound.width <= 0f) return;
            var wb = _worldMapArea.worldBound;
            if (wb.width <= 0f) return;
            float nx = wb.x / rootBound.width;
            float nw = wb.width / rootBound.width;
            // Unity camera rect is bottom-left origin; UI Toolkit is top-left — convert Y
            float ny = (rootBound.height - (wb.y + wb.height)) / rootBound.height;
            float nh = wb.height / rootBound.height;
            _globeCamera.rect = new Rect(nx, ny, nw, nh);
        }

        private void OnDestroy()
        {
            if (GameClient.Instance == null) return;
            GameClient.Instance.OnStateSync.RemoveListener(OnStateSync);
            GameClient.Instance.OnPhaseStart.RemoveListener(OnPhaseStart);
            GameClient.Instance.OnConsequences.RemoveListener(OnConsequences);
            GameClient.Instance.OnWorldUpdate.RemoveListener(OnWorldUpdate);
            GameClient.Instance.OnReveal.RemoveListener(OnReveal);
            GameClient.Instance.OnPlayerReady.RemoveListener(OnPlayerReady);
            GameClient.Instance.OnCeaseFireVote.RemoveListener(OnCeaseFireVoteReceived);
            GameClient.Instance.OnGameEnding.RemoveListener(OnGameEnding);

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_globeReadyCoroutine != null) StopCoroutine(_globeReadyCoroutine);
            if (ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.OnCountryClicked -= OnGlobeCountryClicked;
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
            ArmsFair.Map.ViewToggleManager.Instance?.EnsureGlobeVisible();
            UpdateGlobeViewport();
            RefreshInventoryBar();
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

        // â"€â"€ Event handlers â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private void OnStateSync(StateSync msg)
        {
            _lastState = msg.FullState;
            ArmsFair.Map.GlobeBridge.Instance?.RegisterCountries(msg.FullState.Countries);
            ArmsFair.Map.GlobeBridge.Instance?.ApplyAllStages(msg.FullState.Countries);
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            BindState(msg.FullState);

            if (_procurementPanel != null && _procurementPanel.style.display == DisplayStyle.Flex)
            {
                var localId = AccountManager.Instance.LocalPlayer?.Id;
                _procCapitalM = msg.FullState.Players.FirstOrDefault(p => p.Id == localId)?.Capital ?? 0;
                UpdateProcTotal();
            }
        }

        private void OnPhaseStart(PhaseStartMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            var round              = Math.Max(1, msg.Round);
            _phaseLabel.text       = msg.Phase.ToString().ToUpper();
            _roundLabel.text       = $"ROUND {round}";
            _phaseEndsAt           = msg.EndsAt;
            _timerRunning          = true;
            _phaseStatusLabel.text = $"PHASE: {msg.Phase.ToString().ToUpper()}";
            _statusLabel.text      = $"PHASE STARTED -- ROUND {round}";

            // Keep _lastState phase in sync so phase guards (e.g. procurement) work correctly.
            // StateSync is not sent on every phase transition, so we update it here.
            if (_lastState != null)
                _lastState = _lastState with { Phase = msg.Phase, Round = round };

            ShowPanel(msg.Phase);

            foreach (var btn in _readyBtns) { btn.text = "READY"; btn.SetEnabled(true); }
        }

        private void OnGameEnding(GameEndingMessage msg)
        {
            _timerRunning = false;

            // Disable globe interaction permanently for the rest of this session
            if (ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.BlockInput = true;

            var overlay = new VisualElement();
            overlay.style.position        = Position.Absolute;
            overlay.style.top             = 0;
            overlay.style.left            = 0;
            overlay.style.right           = 0;
            overlay.style.bottom          = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.88f));
            overlay.style.alignItems      = Align.Center;
            overlay.style.justifyContent  = Justify.Center;

            var worldDestroyed = new Label("WORLD DESTROYED!");
            worldDestroyed.style.fontSize     = 20;
            worldDestroyed.style.color        = new StyleColor(new Color(0.75f, 0.2f, 0.2f));
            worldDestroyed.style.marginBottom = 8;
            worldDestroyed.style.unityFontStyleAndWeight = FontStyle.Bold;
            overlay.Add(worldDestroyed);

            var title = new Label(EndingTitle(msg.EndingType));
            title.style.fontSize     = 28;
            title.style.color        = new StyleColor(new Color(0.9f, 0.8f, 0.2f));
            title.style.marginBottom = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            overlay.Add(title);

            if (msg.FinalScores != null)
            {
                foreach (var s in msg.FinalScores.OrderByDescending(s => s.Composite))
                {
                    var name = s.CompanyName;
                    // Try to enrich with username from last known state
                    var profile = _lastState?.Players.FirstOrDefault(p => p.Id == s.PlayerId);
                    if (profile != null && !string.IsNullOrEmpty(profile.CompanyName))
                        name = $"{profile.CompanyName} ({profile.Username})";
                    else if (profile != null)
                        name = profile.Username;

                    var row = new Label($"{name}   ${s.Profit}M   REP {s.Reputation}   SCORE {s.Composite}");
                    row.style.fontSize     = 14;
                    row.style.color        = new StyleColor(Color.white);
                    row.style.marginBottom = 4;
                    overlay.Add(row);
                }
            }

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop     = 24;
            btnRow.style.justifyContent = Justify.Center;

            var menuBtn = new Button(() => UIManager.Instance.GoTo("MainMenu")) { text = "MAIN MENU" };
            menuBtn.style.paddingLeft    = menuBtn.style.paddingRight  = 32;
            menuBtn.style.paddingTop     = menuBtn.style.paddingBottom = 14;
            menuBtn.style.fontSize       = 16;
            menuBtn.style.marginRight    = 12;
            TerminalUI.StyleButton(menuBtn);

            var lobbyBtn = new Button(() =>
            {
                UIManager.Instance.GoTo("MainMenu");
                UIManager.Instance.Push("RoomList");
            }) { text = "LOBBY" };
            lobbyBtn.style.paddingLeft    = lobbyBtn.style.paddingRight  = 32;
            lobbyBtn.style.paddingTop     = lobbyBtn.style.paddingBottom = 14;
            lobbyBtn.style.fontSize       = 16;
            TerminalUI.StyleButton(lobbyBtn);

            btnRow.Add(menuBtn);
            btnRow.Add(lobbyBtn);
            overlay.Add(btnRow);

            _root.Add(overlay);
        }

        private static string EndingTitle(string type) => type switch
        {
            "great_power_confrontation" => "GREAT POWER CONFRONTATION",
            "total_war"                 => "TOTAL WAR",
            "global_sanctions"          => "GLOBAL SANCTIONS",
            "market_saturation"         => "MARKET SATURATION",
            "negotiated_peace"          => "NEGOTIATED PEACE",
            _                           => "GAME OVER"
        };

        private void OnPlayerReady(string playerId)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            var localId = AccountManager.Instance.LocalPlayer?.Id;
            if (playerId != localId)
                _statusLabel.text = $"PLAYER {playerId[..Math.Min(8, playerId.Length)]} READY";
        }

        private async void OnReadyClicked()
        {
            foreach (var btn in _readyBtns) { btn.SetEnabled(false); btn.text = "READY"; }
            await GameClient.Instance.MarkReadyAsync();
        }

        private void OnConsequences(ConsequencesMessage msg)
        {
            if (_root == null) return;
            if (_lastState == null) return;

            // Capture old share prices before state is overwritten
            var oldSharePrices = _lastState.Players.ToDictionary(p => p.Id, p => p.SharePrice);

            // Sum all profit entries per player -- multi-weapon orders produce one entry per weapon
            var updatedPlayers = _lastState.Players.Select(p =>
            {
                var totalEarned = msg.ProfitUpdates.Where(u => u.PlayerId == p.Id).Sum(u => u.ProfitEarned);
                var rep         = msg.ReputationUpdates.FirstOrDefault(u => u.PlayerId == p.Id);
                var share       = msg.SharePriceUpdates.FirstOrDefault(u => u.PlayerId == p.Id);
                return p with
                {
                    Capital    = p.Capital + totalEarned,
                    Reputation = rep   != null ? rep.NewReputation : p.Reputation,
                    SharePrice = share != null ? share.NewPrice    : p.SharePrice,
                };
            }).ToList();
            _lastState = _lastState with { Players = updatedPlayers };
            RefreshPlayerFooter(_lastState.Players);

            var localId = AccountManager.Instance.LocalPlayer?.Id;
            var me = _lastState.Players.FirstOrDefault(p => p.Id == localId);
            if (me != null && msg.ProfitUpdates.Any(u => u.PlayerId == localId))
                _capitalLabel.text = $"${me.Capital}M";

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

            // Populate consequences UXML panel
            if (_profitList != null)
            {
                _profitList.Clear();
                if (msg.ProfitUpdates.Count == 0)
                {
                    _profitList.Add(MakeOverlayRowLabel("NONE"));
                }
                else
                {
                    foreach (var u in msg.ProfitUpdates)
                    {
                        var name = _lastState.Players.FirstOrDefault(p => p.Id == u.PlayerId)?.CompanyName
                                ?? _lastState.Players.FirstOrDefault(p => p.Id == u.PlayerId)?.Username
                                ?? u.PlayerId;
                        _profitList.Add(MakeOverlayRowLabel($"{name.ToUpper()}  +${u.ProfitEarned}M  ->  ${u.NewCapital}M"));
                    }
                }
            }

            if (_blowbackList != null)
            {
                _blowbackList.Clear();
                if (msg.BlowbackEvents.Count == 0)
                {
                    _blowbackList.Add(MakeOverlayRowLabel("NONE"));
                }
                else
                {
                    foreach (var b in msg.BlowbackEvents)
                    {
                        var name = _lastState.Players.FirstOrDefault(p => p.Id == b.PlayerId)?.CompanyName
                                ?? _lastState.Players.FirstOrDefault(p => p.Id == b.PlayerId)?.Username
                                ?? b.PlayerId;
                        _blowbackList.Add(MakeOverlayRowLabel($"{name.ToUpper()}  {WeaponDisplay(b.Weapon).ToUpper()}  ->  {b.CountryIso}  TRACED"));
                    }
                }
            }

            if (_repList != null)
            {
                _repList.Clear();
                if (msg.ReputationUpdates.Count == 0)
                {
                    _repList.Add(MakeOverlayRowLabel("NONE"));
                }
                else
                {
                    foreach (var r in msg.ReputationUpdates)
                    {
                        var name = _lastState.Players.FirstOrDefault(p => p.Id == r.PlayerId)?.CompanyName
                                ?? _lastState.Players.FirstOrDefault(p => p.Id == r.PlayerId)?.Username
                                ?? r.PlayerId;
                        var sign       = r.Delta >= 0 ? "+" : "";
                        var reasonText = r.Reason switch
                        {
                            "covert_traced" => "covert sale traced",
                            _               => r.Reason
                        };
                        _repList.Add(MakeOverlayRowLabel($"{name.ToUpper()}  {sign}{r.Delta}  ->  {r.NewReputation}  ({reasonText})"));
                    }
                }
            }

            if (_sharePriceList != null)
            {
                _sharePriceList.Clear();
                if (msg.SharePriceUpdates.Count == 0)
                {
                    _sharePriceList.Add(MakeOverlayRowLabel("NONE"));
                }
                else
                {
                    foreach (var s in msg.SharePriceUpdates)
                    {
                        var name     = _lastState.Players.FirstOrDefault(p => p.Id == s.PlayerId)?.CompanyName
                                    ?? _lastState.Players.FirstOrDefault(p => p.Id == s.PlayerId)?.Username
                                    ?? s.PlayerId;
                        var oldPrice = oldSharePrices.TryGetValue(s.PlayerId, out var op) ? op : s.NewPrice;
                        var delta    = s.NewPrice - oldPrice;
                        var sign     = delta >= 0 ? "+" : "";
                        _sharePriceList.Add(MakeOverlayRowLabel($"{name.ToUpper()}  ${oldPrice}  ->  ${s.NewPrice}  ({sign}${delta})"));
                    }
                }
            }
        }

        private void OnWorldUpdate(WorldUpdateMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            BindTracks(msg.NewTracks);

            if (ArmsFair.Map.GlobeBridge.Instance != null && msg.CountryChanges != null)
                foreach (var cc in msg.CountryChanges)
                    ArmsFair.Map.GlobeBridge.Instance.SetCountryStage(cc.Iso, (CountryStage)cc.NewStage);

            PopulateWorldUpdateDeltas(msg);
        }

        private void PopulateWorldUpdateDeltas(WorldUpdateMessage msg)
        {
            if (_worldUpdateDeltaList == null) return;
            _worldUpdateDeltaList.Clear();

            var tracks = msg.NewTracks;
            var deltas = msg.TrackDeltas;

            void AddDeltaRow(string name, int delta, int newVal)
            {
                if (delta == 0) return;
                var sign = delta > 0 ? "+" : "";
                _worldUpdateDeltaList.Add(MakeOverlayRowLabel($"{name}  {sign}{delta}  →  {newVal}"));
            }

            AddDeltaRow("MARKET HEAT",    deltas.MarketHeat,    tracks.MarketHeat);
            AddDeltaRow("CIVILIAN COST",  deltas.CivilianCost,  tracks.CivilianCost);
            AddDeltaRow("INSTABILITY",    deltas.Instability,   tracks.Instability);
            AddDeltaRow("SANCTIONS RISK", deltas.SanctionsRisk, tracks.SanctionsRisk);
            AddDeltaRow("GEO TENSION",    deltas.GeoTension,    tracks.GeoTension);

            if (msg.SpreadEvents != null)
                foreach (var s in msg.SpreadEvents)
                    _worldUpdateDeltaList.Add(MakeOverlayRowLabel(
                        $"CONFLICT SPREADS: {s.FromIso} → {s.ToIso} (STAGE {s.NewStage})"));

            if (msg.Events != null)
                foreach (var e in msg.Events)
                    _worldUpdateDeltaList.Add(MakeOverlayRowLabel(e.Description?.ToUpper() ?? "EVENT"));

            if (_worldUpdateDeltaList.childCount == 0)
                _worldUpdateDeltaList.Add(MakeOverlayRowLabel("NO SIGNIFICANT CHANGES"));
        }

        private void OnGlobeCountryClicked(string iso, Vector2 screenPos)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            if (_countryInfoCard == null) return;
            if (_openModals.Count > 0) return;

            var country = _lastState?.Countries.FirstOrDefault(c => c.Iso == iso)
                       ?? _lastState?.Countries.FirstOrDefault(c =>
                              string.Equals(c.Name, iso, System.StringComparison.OrdinalIgnoreCase));
            if (country == null) return;

            _cardIso = iso;
            if (_cardCountryName  != null) _cardCountryName.text  = country.Name?.ToUpper() ?? iso;
            if (_cardStageLabel   != null) _cardStageLabel.text   = $"STAGE: {StageDisplay(country.Stage).ToUpper()}";
            if (_cardTensionLabel != null) _cardTensionLabel.text = $"TENSION: {country.Tension}";

            // Show SELL TO only during sales phase
            var sellBtn = _root.Q<Button>("CardSellBtn");
            if (sellBtn != null)
                sellBtn.style.display = _lastState?.Phase == GamePhase.Sales ? DisplayStyle.Flex : DisplayStyle.None;

            // Convert Unity screen pos (y=0 at bottom) to WorldMapArea-local UI Toolkit coords.
            // PanelSettings may use a scale factor, so normalise via panel/screen ratio.
            var panelBound = _root.panel.visualTree.worldBound;
            float scaleX   = panelBound.width  / Screen.width;
            float scaleY   = panelBound.height / Screen.height;
            var wb         = _worldMapArea?.worldBound ?? Rect.zero;
            float uitX     = screenPos.x * scaleX;
            float uitY     = (Screen.height - screenPos.y) * scaleY;
            float localX   = uitX - wb.x + 10f;
            float localY   = uitY - wb.y + 10f;
            const float cardW = 210f;
            const float cardH = 130f;
            localX = Mathf.Clamp(localX, 0f, Mathf.Max(0f, wb.width  - cardW));
            localY = Mathf.Clamp(localY, 0f, Mathf.Max(0f, wb.height - cardH));
            _countryInfoCard.style.left    = localX;
            _countryInfoCard.style.top     = localY;
            _countryInfoCard.style.display = DisplayStyle.Flex;
            if (ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.BlockInput = true;
        }

        private void HideCountryInfoCard()
        {
            if (_countryInfoCard != null) _countryInfoCard.style.display = DisplayStyle.None;
            if (ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.BlockInput = false;
        }

        private void OnCardSellClicked()
        {
            if (_cardIso == null) return;
            var country = _lastState?.Countries.FirstOrDefault(c => c.Iso == _cardIso);
            if (country == null) return;

            // Pre-fill the country in the sales form
            _selectedCountryIso = _cardIso;
            if (_countryPickerBtn != null) _countryPickerBtn.text = country.Name ?? _cardIso;

            // Hide the info card and make sure sales panel is visible
            HideCountryInfoCard();
            UpdateSaleEstimate();
        }

        private void OnReveal(RevealMessage msg)
        {
            _lastReveal = msg;

            if (ArmsFair.Map.GlobeBridge.Instance != null && msg.Animations != null && msg.Animations.Count > 0)
                ArmsFair.Map.GlobeBridge.Instance.PlayArcs(msg.Animations, _lastState?.Players);

            if (_revealList == null) return;

            _revealList.Clear();

            if (msg.Actions == null || msg.Actions.Count == 0)
            {
                var empty = new Label("No orders were submitted this round.");
                empty.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                empty.style.fontSize = 16;
                _revealList.Add(empty);
            }
            else
            {
                var localId = AccountManager.Instance.LocalPlayer?.Id;
                foreach (var action in msg.Actions)
                {
                    var player  = _lastState?.Players.FirstOrDefault(p => p.Id == action.PlayerId);
                    var company = (player?.CompanyName ?? player?.Username ?? action.CompanyName ?? action.PlayerId).ToUpper();
                    var isMe    = action.PlayerId == localId;

                    var row = new VisualElement();
                    row.style.flexDirection     = FlexDirection.Row;
                    row.style.alignItems        = Align.Center;
                    row.style.paddingTop        = 7;
                    row.style.paddingBottom     = 7;
                    row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                    row.style.borderBottomWidth = 1;

                    var companyLabel = new Label(isMe ? $"> {company}" : $"  {company}");
                    companyLabel.style.color     = new StyleColor(isMe ? new Color(138f/255f, 184f/255f, 112f/255f) : new Color(212f/255f, 207f/255f, 184f/255f));
                    companyLabel.style.fontSize = 16;
                    companyLabel.style.width     = 160;
                    companyLabel.style.flexShrink = 0;

                    var saleTypeStr = action.SaleType switch
                    {
                        SaleType.Open        => "OPEN",
                        SaleType.Covert      => "COVERT",
                        SaleType.AidCover    => "AID COVER",
                        SaleType.PeaceBroker => "PEACE BROKER",
                        _                    => action.SaleType.ToString().ToUpper()
                    };

                    var detail = new Label();
                    if (action.SaleType == SaleType.PeaceBroker)
                    {
                        detail.text = "PEACE BROKER";
                    }
                    else
                    {
                        var weaponStr  = action.WeaponCategory.HasValue
                            ? WeaponCatalog.Items.FirstOrDefault(i => i.Category == action.WeaponCategory.Value)?.DisplayName ?? WeaponDisplay(action.WeaponCategory.Value)
                            : "?";
                        var countryStr = action.TargetIso != null
                            ? (_lastState?.Countries.FirstOrDefault(c => c.Iso == action.TargetIso)?.Name ?? action.TargetIso)
                            : "?";
                        detail.text = $"{saleTypeStr}  {weaponStr}  ->  {countryStr}";
                    }
                    detail.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                    detail.style.fontSize = 16;
                    detail.style.flexGrow = 1;

                    row.Add(companyLabel);
                    row.Add(detail);
                    _revealList.Add(row);
                }
            }

            if (_statusLabel != null) _statusLabel.text = "REVEAL: ALL ORDERS DISCLOSED";
        }

        private void OnCeaseFireVoteReceived(string voterId)
        {
            _ceaseFireVoterCount++;
            if (_ceaseFireVotersLabel != null)
                _ceaseFireVotersLabel.text = $"CEASE-FIRE VOTES: {_ceaseFireVoterCount}";
        }

        private async void OnVoteCeaseFire()
        {
            if (_hasVotedCeaseFire) return;
            _hasVotedCeaseFire = true;
            if (_voteCeaseFireBtn != null)
            {
                _voteCeaseFireBtn.SetEnabled(false);
                _voteCeaseFireBtn.text = "VOTED";
            }
            await GameClient.Instance.VoteCeaseFireAsync();
        }

        // â"€â"€ Panel switching â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private void ShowPanel(GamePhase phase)
        {
            CloseAllModals();

            var isProcurement  = phase == GamePhase.Procurement;
            var isSales        = phase == GamePhase.Sales;
            var isNegotiation  = phase == GamePhase.Negotiation;
            var isReveal       = phase == GamePhase.Reveal;
            var isConsequences = phase == GamePhase.Consequences;
            var isWorldUpdate  = phase == GamePhase.WorldUpdate;
            var hasPanel       = isProcurement || isSales || isNegotiation || isReveal || isConsequences || isWorldUpdate;

            if (_procurementPanel  != null) _procurementPanel.style.display  = isProcurement  ? DisplayStyle.Flex : DisplayStyle.None;
            if (_salesPanel        != null) _salesPanel.style.display        = isSales        ? DisplayStyle.Flex : DisplayStyle.None;
            if (_negotiationPanel  != null) _negotiationPanel.style.display  = isNegotiation  ? DisplayStyle.Flex : DisplayStyle.None;
            if (_revealPanel       != null) _revealPanel.style.display       = isReveal       ? DisplayStyle.Flex : DisplayStyle.None;
            if (_consequencesPanel != null) _consequencesPanel.style.display = isConsequences ? DisplayStyle.Flex : DisplayStyle.None;
            if (_worldUpdatePanel  != null)
            {
                _worldUpdatePanel.style.display = isWorldUpdate ? DisplayStyle.Flex : DisplayStyle.None;
                if (isWorldUpdate && _worldUpdateDeltaList != null) _worldUpdateDeltaList.Clear();
            }
            if (_phaseStatusLabel  != null) _phaseStatusLabel.style.display  = hasPanel ? DisplayStyle.None : DisplayStyle.Flex;

            if (!isProcurement && _procTabBar != null)
            {
                _procTabBar.RemoveFromHierarchy();
                _procTabBar = null;
                _procActiveTab = WeaponTab.Light;
            }

            if (isProcurement)
            {
                var localId = AccountManager.Instance.LocalPlayer?.Id;
                _procCapitalM = _lastState?.Players.FirstOrDefault(p => p.Id == localId)?.Capital ?? 0;
                BuildProcurementPanel();
            }

            if (isSales) BuildSalesPanel();

            if (isNegotiation) BuildNegotiationPanel();
        }

        private void BuildProcurementPanel()
        {
            if (_weaponList == null) return;

            // Build tab bar once; rebuild only the weapon rows on tab switch
            if (_procTabBar == null && _weaponList.parent != null)
            {
                _procTabBar = new VisualElement();
                _procTabBar.style.flexDirection = FlexDirection.Row;
                _procTabBar.style.marginBottom  = 6;

                var tabs = new (string label, WeaponTab tab)[]
                {
                    ("LIGHT",     WeaponTab.Light),
                    ("AIRCRAFT",  WeaponTab.Aircraft),
                    ("MISSILES",  WeaponTab.Missiles),
                    ("WMD",       WeaponTab.Wmd),
                };

                foreach (var (label, tab) in tabs)
                {
                    var btn = new Button(() =>
                    {
                        _procActiveTab = tab;
                        StyleProcTabs();
                        RebuildProcWeaponRows();
                    });
                    btn.text = label;
                    btn.name = $"ProcTab_{tab}";
                    btn.style.flexGrow        = 1;
                    btn.style.marginRight     = 3;
                    btn.style.paddingTop      = 5;
                    btn.style.paddingBottom   = 5;
                    btn.style.fontSize        = 15;
                    btn.style.unityTextAlign  = TextAnchor.MiddleCenter;
                    TerminalUI.AddHover(btn);
                    _procTabBar.Add(btn);
                }

                int listIdx = _weaponList.parent.IndexOf(_weaponList);
                _weaponList.parent.Insert(listIdx, _procTabBar);
            }

            StyleProcTabs();

            _quantities.Clear();
            foreach (var e in WeaponCatalog.Items) _quantities[e.Category] = 0;
            UpdateProcTotal();

            if (_procErrorLabel != null) _procErrorLabel.style.display = DisplayStyle.None;
            if (_confirmProcBtn  != null) _confirmProcBtn.SetEnabled(true);

            RebuildProcWeaponRows();
        }

        private void StyleProcTabs()
        {
            if (_procTabBar == null) return;
            var active   = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            var inactive = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
            var activeBg   = new StyleColor(new Color(38f/255f, 58f/255f, 32f/255f));
            var inactiveBg = new StyleColor(new Color(18f/255f, 22f/255f, 14f/255f));
            var activeBorder   = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            var inactiveBorder = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));

            foreach (WeaponTab tab in System.Enum.GetValues(typeof(WeaponTab)))
            {
                var btn = _procTabBar.Q<Button>($"ProcTab_{tab}");
                if (btn == null) continue;
                bool isActive = tab == _procActiveTab;
                btn.style.color              = isActive ? active   : inactive;
                btn.style.backgroundColor    = isActive ? activeBg : inactiveBg;
                btn.style.borderTopColor     = isActive ? activeBorder : inactiveBorder;
                btn.style.borderBottomColor  = isActive ? activeBorder : inactiveBorder;
                btn.style.borderLeftColor    = isActive ? activeBorder : inactiveBorder;
                btn.style.borderRightColor   = isActive ? activeBorder : inactiveBorder;
                btn.style.borderTopWidth     = 1;
                btn.style.borderBottomWidth  = 1;
                btn.style.borderLeftWidth    = 1;
                btn.style.borderRightWidth   = 1;
            }
        }

        private void RebuildProcWeaponRows()
        {
            if (_weaponList == null) return;
            _weaponList.Clear();

            foreach (var entry in WeaponCatalog.Items.Where(e => e.Tab == _procActiveTab))
            {
                var cat = entry.Category;
                if (!_quantities.ContainsKey(cat)) _quantities[cat] = 0;

                var row = new VisualElement();
                row.style.flexDirection     = FlexDirection.Row;
                row.style.justifyContent    = Justify.SpaceBetween;
                row.style.alignItems        = Align.Center;
                row.style.paddingTop        = 8;
                row.style.paddingBottom     = 8;
                row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderBottomWidth = 1;

                var nameLabel = new Label(entry.DisplayName);
                nameLabel.style.color    = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                nameLabel.style.fontSize = 17;
                nameLabel.style.flexGrow = 1;

                var costLabel = new Label($"${entry.BaseCostMillions}M ea");
                costLabel.style.color          = new StyleColor(entry.BaseCostMillions > _procCapitalM
                    ? new Color(0.85f, 0.30f, 0.25f)
                    : new Color(138f/255f, 134f/255f, 112f/255f));
                costLabel.style.fontSize       = 15;
                costLabel.style.whiteSpace     = WhiteSpace.NoWrap;
                costLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                costLabel.style.marginRight    = 10;
                costLabel.style.marginLeft     = 8;

                var qtyField = MakeQtyField(0);
                var maxBtn   = MakeMaxButton();
                var minusBtn = MakeQtyButton("-");
                var plusBtn  = MakeQtyButton("+");

                maxBtn.clicked += () =>
                {
                    var maxAffordable = entry.BaseCostMillions > 0
                        ? Math.Max(0, _procCapitalM / entry.BaseCostMillions) : 0;
                    _quantities[cat] = maxAffordable;
                    qtyField.SetValueWithoutNotify(maxAffordable);
                    UpdateProcTotal();
                };

                minusBtn.clicked += () =>
                {
                    if (_quantities[cat] <= 0) return;
                    _quantities[cat]--;
                    qtyField.SetValueWithoutNotify(_quantities[cat]);
                    UpdateProcTotal();
                };

                plusBtn.clicked += () =>
                {
                    _quantities[cat]++;
                    qtyField.SetValueWithoutNotify(_quantities[cat]);
                    UpdateProcTotal();
                };

                qtyField.RegisterValueChangedCallback(evt =>
                {
                    var clamped = Math.Max(0, evt.newValue);
                    _quantities[cat] = clamped;
                    if (clamped != evt.newValue) qtyField.SetValueWithoutNotify(clamped);
                    UpdateProcTotal();
                });

                row.Add(nameLabel);
                row.Add(costLabel);
                row.Add(maxBtn);
                row.Add(minusBtn);
                row.Add(qtyField);
                row.Add(plusBtn);
                _weaponList.Add(row);
            }
        }

        private void RefreshInventoryBar()
        {
            if (_inventoryBar == null || _inventoryItems == null) return;
            _inventoryItems.Clear();

            _inventoryBar.style.display = DisplayStyle.Flex;

            foreach (var entry in WeaponCatalog.Items)
            {
                _inventory.TryGetValue(entry.Category, out var count);
                var chip = new Label($"{entry.DisplayName} x{count}");
                chip.style.color            = count > 0
                    ? new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f))
                    : new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                chip.style.fontSize = 15;
                chip.style.backgroundColor  = new StyleColor(new Color(20f/255f, 30f/255f, 12f/255f));
                chip.style.borderTopColor   = chip.style.borderBottomColor =
                chip.style.borderLeftColor  = chip.style.borderRightColor  =
                    new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                chip.style.borderTopWidth   = chip.style.borderBottomWidth =
                chip.style.borderLeftWidth  = chip.style.borderRightWidth  = 1;
                chip.style.paddingTop       = chip.style.paddingBottom = 2;
                chip.style.paddingLeft      = chip.style.paddingRight  = 8;
                chip.style.marginRight      = 8;
                chip.style.marginBottom     = 5;
                chip.style.whiteSpace       = WhiteSpace.NoWrap;
                _inventoryItems.Add(chip);
            }
        }

        private static Button MakeQtyButton(string label)
        {
            var btn = new Button { text = label };
            btn.style.width           = 26;
            btn.style.height          = 26;
            btn.style.fontSize = 17;
            btn.style.paddingTop      = 0;
            btn.style.paddingBottom   = 0;
            btn.style.paddingLeft     = 0;
            btn.style.paddingRight    = 0;
            btn.style.unityTextAlign  = TextAnchor.MiddleCenter;
            btn.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            btn.style.backgroundColor = new StyleColor(new Color(25f/255f, 25f/255f, 15f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
            return btn;
        }

        private static Button MakeMaxButton()
        {
            var btn = new Button { text = "MAX" };
            btn.style.width           = 40;
            btn.style.height          = 26;
            btn.style.fontSize        = 14;
            btn.style.paddingTop      = 0;
            btn.style.paddingBottom   = 0;
            btn.style.paddingLeft     = 2;
            btn.style.paddingRight    = 2;
            btn.style.marginRight     = 4;
            btn.style.flexShrink      = 0;
            btn.style.unityTextAlign  = TextAnchor.MiddleCenter;
            btn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            btn.style.backgroundColor = new StyleColor(new Color(15f/255f, 25f/255f, 8f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
            btn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                btn.style.backgroundColor = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
                btn.style.color           = new StyleColor(new Color(0.051f, 0.051f, 0.031f));
            });
            btn.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = new StyleColor(new Color(15f/255f, 25f/255f, 8f/255f));
                btn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            });
            return btn;
        }

        private static IntegerField MakeQtyField(int initial)
        {
            var field = new IntegerField { value = initial };
            field.style.width      = 55;
            field.style.height     = 26;
            field.style.flexShrink = 0;
            field.style.alignSelf  = Align.Center;
            field.style.marginLeft = field.style.marginRight = 2;

            var inner = field.Q<VisualElement>(className: "unity-base-text-field__input");
            if (inner != null)
            {
                inner.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                inner.style.fontSize = 16;
                inner.style.backgroundColor = new StyleColor(new Color(20f/255f, 25f/255f, 10f/255f));
                inner.style.borderTopColor  = inner.style.borderBottomColor =
                inner.style.borderLeftColor = inner.style.borderRightColor  =
                    new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                inner.style.borderTopWidth  = inner.style.borderBottomWidth =
                inner.style.borderLeftWidth = inner.style.borderRightWidth  = 1;
                inner.style.unityTextAlign  = TextAnchor.MiddleCenter;
            }
            return field;
        }

        private void UpdateProcTotal()
        {
            if (_procTotalLabel == null) return;
            var total = _quantities.Sum(kv =>
                (WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key)?.BaseCostMillions ?? 0) * kv.Value);
            _procTotalLabel.text = $"${total}M";
            _procTotalLabel.style.color = new StyleColor(total > _procCapitalM
                ? new Color(192f/255f, 100f/255f, 100f/255f)
                : new Color(212f/255f, 207f/255f, 184f/255f));
        }

        private void OnConfirmProcurement()
        {
            if (_confirmProcBtn == null) return;
            if (_lastState?.Phase != GamePhase.Procurement) return;
            var total = _quantities.Sum(kv =>
                (WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key)?.BaseCostMillions ?? 0) * kv.Value);

            if (total == 0)
            {
                if (_procErrorLabel != null)
                {
                    _procErrorLabel.text = "Select at least one weapon before confirming.";
                    _procErrorLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            if (total > _procCapitalM)
            {
                if (_procErrorLabel != null)
                {
                    _procErrorLabel.text = $"INSUFFICIENT CAPITAL: need ${total}M, have ${_procCapitalM}M";
                    _procErrorLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            if (_procErrorLabel != null) _procErrorLabel.style.display = DisplayStyle.None;
            ShowProcurementConfirmModal(total, _procCapitalM);
        }

        private void ShowProcurementConfirmModal(int totalM, int capitalM)
        {
            var overlay = new VisualElement();
            overlay.style.position        = Position.Absolute;
            overlay.style.left            = 0; overlay.style.top    = 0;
            overlay.style.right           = 0; overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
            overlay.style.alignItems      = Align.Center;
            overlay.style.justifyContent  = Justify.Center;

            var panel = new VisualElement();
            panel.style.width           = 380;
            panel.style.backgroundColor = new StyleColor(new Color(17f/255f, 17f/255f, 8f/255f));
            panel.style.borderTopColor  = panel.style.borderBottomColor =
            panel.style.borderLeftColor = panel.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            panel.style.borderTopWidth  = panel.style.borderBottomWidth =
            panel.style.borderLeftWidth = panel.style.borderRightWidth  = 1;
            panel.style.paddingTop      = panel.style.paddingBottom =
            panel.style.paddingLeft     = panel.style.paddingRight  = 20;

            var title = new Label("Confirm Purchase");
            title.style.fontSize = 19;
            title.style.color          = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom   = 14;
            panel.Add(title);

            foreach (var kv in _quantities)
            {
                if (kv.Value == 0) continue;
                var entry    = WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key);
                if (entry == null) continue;
                var lineCost = entry.BaseCostMillions * kv.Value;

                var row = new VisualElement();
                row.style.flexDirection  = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom   = 6;

                var nameLabel = new Label($"{entry.DisplayName} x{kv.Value}");
                nameLabel.style.color    = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                nameLabel.style.fontSize = 16;

                var costLabel = new Label($"${lineCost}M");
                costLabel.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                costLabel.style.fontSize = 16;

                row.Add(nameLabel);
                row.Add(costLabel);
                panel.Add(row);
            }

            var divider = new VisualElement();
            divider.style.height          = 1;
            divider.style.backgroundColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            divider.style.marginTop       = 10;
            divider.style.marginBottom    = 10;
            panel.Add(divider);

            var totalRow = new VisualElement();
            totalRow.style.flexDirection  = FlexDirection.Row;
            totalRow.style.justifyContent = Justify.SpaceBetween;
            totalRow.style.marginBottom   = 16;

            var totalLabel = new Label("TOTAL");
            totalLabel.style.color          = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            totalLabel.style.fontSize = 17;
            totalLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var totalAmt = new Label($"${totalM}M");
            totalAmt.style.color          = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            totalAmt.style.fontSize = 17;
            totalAmt.style.unityFontStyleAndWeight = FontStyle.Bold;

            var afterLabel = new Label($"Capital after: ${capitalM - totalM}M");
            afterLabel.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            afterLabel.style.fontSize = 15;
            afterLabel.style.marginBottom = 16;

            totalRow.Add(totalLabel);
            totalRow.Add(totalAmt);
            panel.Add(totalRow);
            panel.Add(afterLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.SpaceBetween;

            var cancelBtn  = new Button { text = "CANCEL" };
            cancelBtn.style.flexGrow        = 1;
            cancelBtn.style.marginRight     = 8;
            cancelBtn.style.paddingTop      = cancelBtn.style.paddingBottom = 8;
            cancelBtn.style.color           = new StyleColor(new Color(192f/255f, 144f/255f, 144f/255f));
            cancelBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
            cancelBtn.style.borderTopColor  = cancelBtn.style.borderBottomColor =
            cancelBtn.style.borderLeftColor = cancelBtn.style.borderRightColor  =
                new StyleColor(new Color(90f/255f, 42f/255f, 42f/255f));
            cancelBtn.style.borderTopWidth  = cancelBtn.style.borderBottomWidth =
            cancelBtn.style.borderLeftWidth = cancelBtn.style.borderRightWidth  = 1;

            var confirmBtn = new Button { text = "CONFIRM" };
            confirmBtn.style.flexGrow        = 1;
            confirmBtn.style.paddingTop      = confirmBtn.style.paddingBottom = 8;
            confirmBtn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            confirmBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 25f/255f, 8f/255f));
            confirmBtn.style.borderTopColor  = confirmBtn.style.borderBottomColor =
            confirmBtn.style.borderLeftColor = confirmBtn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            confirmBtn.style.borderTopWidth  = confirmBtn.style.borderBottomWidth =
            confirmBtn.style.borderLeftWidth = confirmBtn.style.borderRightWidth  = 1;

            cancelBtn.clicked  += () => CloseModal(overlay);
            confirmBtn.clicked += () =>
            {
                CloseModal(overlay);
                SubmitProcurement();
            };

            btnRow.Add(cancelBtn);
            btnRow.Add(confirmBtn);
            panel.Add(btnRow);
            overlay.Add(panel);
            _root.Add(overlay);
        }

        private async void SubmitProcurement()
        {
            if (_confirmProcBtn != null) _confirmProcBtn.SetEnabled(false);

            var selected = new List<WeaponCategory>();
            foreach (var kv in _quantities)
                for (int i = 0; i < kv.Value; i++)
                    selected.Add(kv.Key);

            // Accumulate into inventory before resetting quantities
            foreach (var kv in _quantities)
            {
                if (kv.Value == 0) continue;
                _inventory.TryGetValue(kv.Key, out var existing);
                _inventory[kv.Key] = existing + kv.Value;
            }

            await GameClient.Instance.SubmitProcurementAsync(new ProcurementMessage(selected));

            // Reset quantity selectors after submit
            _quantities.Clear();
            RefreshInventoryBar();
            if (_confirmProcBtn != null) _confirmProcBtn.SetEnabled(true);
        }

        // â"€â"€ Sales panel â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private void BuildSalesPanel()
        {
            _selectedSaleType   = SaleType.Open;
            _selectedCountryIso = null;
            _isDualSupply       = false;
            _isProxyRouted      = false;
            _salesOrder.Clear();
            _saleTypeBtns.Clear();
            _salesActiveTab = WeaponTab.Light;
            ClearWmdRestriction();

            if (_saleTypeRow != null)
            {
                _saleTypeRow.Clear();
                var types = new[] { SaleType.Open, SaleType.Covert, SaleType.AidCover, SaleType.PeaceBroker };
                var labels = new[] { "OPEN", "COVERT", "AID COVER", "PEACE BROKER" };
                for (int i = 0; i < types.Length; i++)
                {
                    var t   = types[i];
                    var btn = new Button { text = labels[i] };
                    btn.style.fontSize       = 15;
                    btn.style.paddingTop     = 5; btn.style.paddingBottom = 5;
                    btn.style.paddingLeft    = 8; btn.style.paddingRight  = 8;
                    btn.style.marginRight    = 6;
                    btn.style.unityTextAlign = TextAnchor.MiddleCenter;
                    StyleSaleTypeBtn(btn, selected: t == SaleType.Open);
                    btn.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        if (!btn.enabledSelf) return;
                        btn.style.backgroundColor = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                        btn.style.color           = new StyleColor(new Color(0.051f, 0.051f, 0.031f));
                    });
                    btn.RegisterCallback<PointerLeaveEvent>(_ =>
                        StyleSaleTypeBtn(btn, btn == _saleTypeBtns.GetValueOrDefault(_selectedSaleType)));
                    btn.clicked += () => OnSaleTypeChanged(t);
                    _saleTypeBtns[t] = btn;
                    _saleTypeRow.Add(btn);
                }
            }

            if (_modifierRow != null)
            {
                _modifierRow.Clear();
                _dualSupplyBtn = MakeSaleToggleBtn("DUAL SUPPLY");
                _proxyBtn      = MakeSaleToggleBtn("GRAY CHANNEL");
                _dualSupplyBtn.clicked += () =>
                {
                    _isDualSupply = !_isDualSupply;
                    StyleSaleToggleBtn(_dualSupplyBtn, _isDualSupply);
                };
                _proxyBtn.clicked += () =>
                {
                    _isProxyRouted = !_isProxyRouted;
                    StyleSaleToggleBtn(_proxyBtn, _isProxyRouted);
                };
                _modifierRow.Add(_dualSupplyBtn);
                _modifierRow.Add(_proxyBtn);
            }

            if (_weaponPickerBtn  != null) _weaponPickerBtn.text  = "Select Weapon";
            if (_countryPickerBtn != null) _countryPickerBtn.text = "Select Country";
            if (_saleEstimateLabel != null) _saleEstimateLabel.text = "";
            if (_saleErrorLabel   != null) _saleErrorLabel.style.display = DisplayStyle.None;
            if (_submitSaleBtn    != null) _submitSaleBtn.SetEnabled(true);

            OnSaleTypeChanged(SaleType.Open);
        }

        private void OnSaleTypeChanged(SaleType type)
        {
            _selectedSaleType = type;
            foreach (var kv in _saleTypeBtns)
                StyleSaleTypeBtn(kv.Value, kv.Key == type);

            var isPeace = type == SaleType.PeaceBroker;
            if (_weaponRow      != null) _weaponRow.style.display      = isPeace ? DisplayStyle.None : DisplayStyle.Flex;
            if (_countryRow     != null) _countryRow.style.display     = isPeace ? DisplayStyle.None : DisplayStyle.Flex;
            if (_modifierRow    != null) _modifierRow.style.display    = isPeace ? DisplayStyle.None : DisplayStyle.Flex;
            if (_peaceBrokerNote != null) _peaceBrokerNote.style.display = isPeace ? DisplayStyle.Flex : DisplayStyle.None;

            if (isPeace)
            {
                _isDualSupply  = false;
                _isProxyRouted = false;
                if (_dualSupplyBtn != null) StyleSaleToggleBtn(_dualSupplyBtn, false);
                if (_proxyBtn      != null) StyleSaleToggleBtn(_proxyBtn,      false);
            }

            UpdateSaleEstimate();
        }

        private void BuildNegotiationPanel()
        {
            _ceaseFireVoterCount = 0;
            _hasVotedCeaseFire   = false;

            if (_ceaseFireVotersLabel != null) _ceaseFireVotersLabel.text = "CEASE-FIRE VOTES: 0";
            if (_voteCeaseFireBtn     != null) { _voteCeaseFireBtn.SetEnabled(true); _voteCeaseFireBtn.text = "VOTE CEASE-FIRE"; }

            SwitchNegoTab(0);

            if (_negoTracksList != null && _lastState != null)
            {
                _negoTracksList.Clear();
                var t = _lastState.Tracks;
                _negoTracksList.Add(MakeOverlayRowLabel($"MARKET HEAT: {t.MarketHeat}   CIVILIAN COST: {t.CivilianCost}   INSTABILITY: {t.Instability}"));
                _negoTracksList.Add(MakeOverlayRowLabel($"SANCTIONS: {t.SanctionsRisk}   GEO TENSION: {t.GeoTension}"));
            }

            if (_negoRevealList != null)
            {
                _negoRevealList.Clear();
                if (_lastReveal == null || _lastReveal.Actions == null || _lastReveal.Actions.Count == 0)
                {
                    _negoRevealList.Add(MakeOverlayRowLabel("NO DATA FROM LAST ROUND"));
                }
                else
                {
                    foreach (var action in _lastReveal.Actions)
                    {
                        var player  = _lastState?.Players.FirstOrDefault(p => p.Id == action.PlayerId);
                        var company = (player?.CompanyName ?? player?.Username ?? action.PlayerId).ToUpper();
                        var detail  = action.SaleType == SaleType.PeaceBroker
                            ? "PEACE BROKER"
                            : $"{action.SaleType.ToString().ToUpper()}  {(action.WeaponCategory.HasValue ? WeaponDisplay(action.WeaponCategory.Value) : "?")}  ->  {action.TargetIso ?? "?"}";
                        _negoRevealList.Add(MakeOverlayRowLabel($"{company}  {detail}"));
                    }
                }
            }
        }

        private void SwitchNegoTab(int tab)
        {
            if (_negoIntelTab  != null) _negoIntelTab.style.display  = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_negoPeaceTab  != null) _negoPeaceTab.style.display  = tab == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_negoTreatyTab != null) _negoTreatyTab.style.display = tab == 2 ? DisplayStyle.Flex : DisplayStyle.None;

            var activeColor   = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            var inactiveColor = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            var activeBg      = new StyleColor(new Color(15f/255f, 25f/255f,  8f/255f));
            var inactiveBg    = new StyleColor(new Color(15f/255f, 15f/255f,  8f/255f));
            var activeBorder  = new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            var inactiveBorder= new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));

            void StyleTab(Button btn, bool active)
            {
                if (btn == null) return;
                btn.style.color           = active ? activeColor   : inactiveColor;
                btn.style.backgroundColor = active ? activeBg      : inactiveBg;
                btn.style.borderTopColor = btn.style.borderRightColor = btn.style.borderBottomColor = btn.style.borderLeftColor
                    = active ? activeBorder : inactiveBorder;
            }

            StyleTab(_root?.Q<Button>("NegoIntelBtn"),  tab == 0);
            StyleTab(_root?.Q<Button>("NegoPeaceBtn"),  tab == 1);
            StyleTab(_root?.Q<Button>("NegoTreatyBtn"), tab == 2);
        }

        private void OpenWeaponPicker()
        {
            var available = _inventory.Where(kv => kv.Value > 0)
                .Select(kv => (Cat: kv.Key, Max: kv.Value, Entry: WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key)))
                .Where(t => t.Entry != null)
                .ToList();

            if (available.Count == 0)
            {
                if (_saleErrorLabel != null)
                {
                    _saleErrorLabel.text = "No inventory. Purchase weapons during Procurement phase.";
                    _saleErrorLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            var overlay = MakeModalOverlay();
            var panel   = MakeModalPanel(420);
            panel.Add(MakeModalTitle("Select Weapon + Quantity"));

            // Persist quantities across tab switches
            var pending = new Dictionary<WeaponCategory, int>();
            foreach (var t in available)
                pending[t.Cat] = _salesOrder.TryGetValue(t.Cat, out var prev) ? Math.Min(prev, t.Max) : 0;

            var availableTabs = available.Select(t => t.Entry.Tab).Distinct().ToHashSet();

            // Ensure _salesActiveTab has inventory; fall back to first available
            if (!availableTabs.Contains(_salesActiveTab))
                _salesActiveTab = availableTabs.FirstOrDefault();

            // Tab bar
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.marginBottom  = 6;

            // Weapon rows area
            var weaponRows = new ScrollView();
            weaponRows.style.maxHeight = 280;

            void StylePickerTabs(WeaponTab active)
            {
                foreach (WeaponTab tab in System.Enum.GetValues(typeof(WeaponTab)))
                {
                    var b = tabBar.Q<Button>($"SalesTab_{tab}");
                    if (b == null) continue;
                    bool isActive = tab == active;
                    bool hasInv   = availableTabs.Contains(tab);
                    b.style.color             = isActive ? new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f))
                                                         : new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                    b.style.backgroundColor   = isActive ? new StyleColor(new Color(38f/255f, 58f/255f, 32f/255f))
                                                         : new StyleColor(new Color(18f/255f, 22f/255f, 14f/255f));
                    b.style.borderTopColor    = b.style.borderBottomColor =
                    b.style.borderLeftColor   = b.style.borderRightColor  =
                        isActive ? new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f))
                                 : new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                    b.style.borderTopWidth    = b.style.borderBottomWidth =
                    b.style.borderLeftWidth   = b.style.borderRightWidth  = 1;
                    b.style.opacity           = hasInv ? 1f : 0.4f;
                    b.SetEnabled(hasInv);
                }
            }

            void BuildPickerRows(WeaponTab tab)
            {
                _salesActiveTab = tab;
                StylePickerTabs(tab);
                weaponRows.Clear();

                var tabItems = available.Where(t => t.Entry.Tab == tab).ToList();
                if (tabItems.Count == 0)
                {
                    var empty = new Label("No inventory in this category.");
                    empty.style.color          = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                    empty.style.paddingTop     = 12;
                    empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                    weaponRows.Add(empty);
                    return;
                }

                foreach (var (cat, maxQty, entry) in tabItems)
                {
                    var row = new VisualElement();
                    row.style.flexDirection     = FlexDirection.Row;
                    row.style.alignItems        = Align.Center;
                    row.style.justifyContent    = Justify.SpaceBetween;
                    row.style.paddingTop        = 5;
                    row.style.paddingBottom     = 5;
                    row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                    row.style.borderBottomWidth = 1;
                    row.style.marginBottom      = 4;

                    var nameCol = new VisualElement();
                    nameCol.style.flexDirection = FlexDirection.Column;
                    nameCol.style.flexGrow      = 1;

                    var nameLabel = new Label(entry.DisplayName);
                    nameLabel.style.color      = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                    nameLabel.style.fontSize   = 16;
                    nameLabel.style.whiteSpace = WhiteSpace.NoWrap;

                    var subLabel = new Label($"x{maxQty}");
                    subLabel.style.color      = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                    subLabel.style.fontSize   = 13;
                    subLabel.style.whiteSpace = WhiteSpace.NoWrap;

                    nameCol.Add(nameLabel);
                    nameCol.Add(subLabel);

                    var qtyField = MakeQtyField(pending[cat]);
                    var maxBtn   = MakeMaxButton();
                    var minusBtn = MakeQtyButton("-");
                    var plusBtn  = MakeQtyButton("+");

                    maxBtn.clicked += () => { pending[cat] = maxQty; qtyField.SetValueWithoutNotify(maxQty); };
                    minusBtn.clicked += () =>
                    {
                        if (pending[cat] <= 0) return;
                        pending[cat]--;
                        qtyField.SetValueWithoutNotify(pending[cat]);
                    };
                    plusBtn.clicked += () =>
                    {
                        if (pending[cat] >= maxQty) return;
                        pending[cat]++;
                        qtyField.SetValueWithoutNotify(pending[cat]);
                    };
                    qtyField.RegisterValueChangedCallback(evt =>
                    {
                        var clamped = Math.Clamp(evt.newValue, 0, maxQty);
                        pending[cat] = clamped;
                        if (clamped != evt.newValue) qtyField.SetValueWithoutNotify(clamped);
                    });

                    row.Add(nameCol);
                    row.Add(maxBtn);
                    row.Add(minusBtn);
                    row.Add(qtyField);
                    row.Add(plusBtn);
                    weaponRows.Add(row);
                }
            }

            // Build tab buttons
            var tabLabels = new Dictionary<WeaponTab, string>
            {
                { WeaponTab.Light,    "LIGHT"    },
                { WeaponTab.Aircraft, "AIRCRAFT" },
                { WeaponTab.Missiles, "MISSILES" },
                { WeaponTab.Wmd,      "WMD"      },
            };
            foreach (var (tab, label) in tabLabels)
            {
                var capturedTab = tab;
                var btn = new Button(() => BuildPickerRows(capturedTab));
                btn.text          = label;
                btn.name          = $"SalesTab_{tab}";
                btn.style.flexGrow       = 1;
                btn.style.marginRight    = 3;
                btn.style.paddingTop     = 5;
                btn.style.paddingBottom  = 5;
                btn.style.fontSize       = 13;
                btn.style.unityTextAlign = TextAnchor.MiddleCenter;
                TerminalUI.AddHover(btn);
                tabBar.Add(btn);
            }
            panel.Add(tabBar);
            panel.Add(weaponRows);

            BuildPickerRows(_salesActiveTab);

            var divider = new VisualElement();
            divider.style.height          = 1;
            divider.style.backgroundColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            divider.style.marginTop       = 10;
            divider.style.marginBottom    = 10;
            panel.Add(divider);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var cancelBtn = MakeModalCancelBtn(() => CloseModal(overlay));
            cancelBtn.style.flexGrow        = 1;
            cancelBtn.style.marginRight     = 8;
            cancelBtn.style.unityTextAlign  = TextAnchor.MiddleCenter;

            var selectBtn = new Button { text = "SELECT" };
            selectBtn.style.flexGrow        = 1;
            selectBtn.style.paddingTop      = selectBtn.style.paddingBottom = 8;
            selectBtn.style.unityTextAlign  = TextAnchor.MiddleCenter;
            selectBtn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            selectBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 25f/255f, 8f/255f));
            selectBtn.style.borderTopColor  = selectBtn.style.borderBottomColor =
            selectBtn.style.borderLeftColor = selectBtn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            selectBtn.style.borderTopWidth  = selectBtn.style.borderBottomWidth =
            selectBtn.style.borderLeftWidth = selectBtn.style.borderRightWidth  = 1;
            TerminalUI.AddHover(selectBtn);

            selectBtn.clicked += () =>
            {
                var chosen = pending.Where(kv => kv.Value > 0).ToList();
                if (chosen.Count == 0)
                {
                    if (_saleErrorLabel != null)
                    {
                        _saleErrorLabel.text = "Set a quantity before selecting.";
                        _saleErrorLabel.style.display = DisplayStyle.Flex;
                    }
                    return;
                }
                _salesOrder.Clear();
                foreach (var kv in chosen) _salesOrder[kv.Key] = kv.Value;

                if (_weaponPickerBtn != null)
                {
                    var parts = chosen.Select(kv =>
                    {
                        var e = WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key);
                        return kv.Value > 1 ? $"{e?.DisplayName} x{kv.Value}" : e?.DisplayName ?? WeaponDisplay(kv.Key);
                    });
                    _weaponPickerBtn.text = string.Join(", ", parts);
                }
                if (_saleErrorLabel != null) _saleErrorLabel.style.display = DisplayStyle.None;
                CloseModal(overlay);

                bool hasWmd = _salesOrder.Keys.Any(c => WeaponCatalog.Items.FirstOrDefault(i => i.Category == c)?.IsWmd == true);
                if (hasWmd) ApplyWmdRestriction();
                else        ClearWmdRestriction();

                UpdateSaleEstimate();
            };

            btnRow.Add(cancelBtn);
            btnRow.Add(selectBtn);
            panel.Add(btnRow);
            overlay.Add(panel);
            _root.Add(overlay);
        }

        private void ApplyWmdRestriction()
        {
            // Force Covert — WMD cannot be sold openly or as aid cover
            if (_selectedSaleType != SaleType.Covert)
            {
                _selectedSaleType = SaleType.Covert;
                foreach (var kv in _saleTypeBtns)
                    StyleSaleTypeBtn(kv.Value, kv.Key == SaleType.Covert);
            }
            foreach (var kv in _saleTypeBtns)
                kv.Value.SetEnabled(kv.Key == SaleType.Covert);

            // Show or create warning label above the sale-type row
            if (_salesPanel == null) return;
            var warn = _salesPanel.Q<Label>("WmdWarningLabel");
            if (warn == null)
            {
                warn = new Label("WARNING: WMD SELECTED — COVERT ONLY — EXTREME BLOWBACK RISK");
                warn.name = "WmdWarningLabel";
                warn.style.color          = new StyleColor(new Color(0.85f, 0.25f, 0.20f));
                warn.style.fontSize       = 12;
                warn.style.unityTextAlign = TextAnchor.MiddleCenter;
                warn.style.marginBottom   = 5;
                warn.style.whiteSpace     = WhiteSpace.Normal;
                if (_saleTypeRow != null)
                {
                    int idx = _saleTypeRow.parent.IndexOf(_saleTypeRow);
                    _saleTypeRow.parent.Insert(idx, warn);
                }
                else
                {
                    _salesPanel.Add(warn);
                }
            }
            warn.style.display = DisplayStyle.Flex;
        }

        private void ClearWmdRestriction()
        {
            foreach (var kv in _saleTypeBtns)
                kv.Value.SetEnabled(true);
            var warn = _salesPanel?.Q<Label>("WmdWarningLabel");
            if (warn != null) warn.style.display = DisplayStyle.None;
        }

        private void OpenCountryPicker()
        {
            var countries = _lastState?.Countries
                .Where(c => c.Stage > CountryStage.Dormant)
                .OrderByDescending(c => c.Stage)
                .ThenByDescending(c => c.Tension)
                .ToList();

            if (countries == null || countries.Count == 0)
            {
                if (_saleErrorLabel != null)
                {
                    _saleErrorLabel.text = "No active markets. Wait for world tension to rise.";
                    _saleErrorLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            var overlay  = MakeModalOverlay();
            var panel    = MakeModalPanel(320);
            panel.style.overflow = Overflow.Hidden;

            var title    = MakeModalTitle("Select Target Country");
            panel.Add(title);

            var search   = new TextField();
            search.style.marginBottom    = 8;
            search.style.flexShrink      = 0;
            search.style.backgroundColor = new StyleColor(new Color(25f/255f, 25f/255f, 15f/255f));
            search.style.borderTopColor  = search.style.borderBottomColor =
            search.style.borderLeftColor = search.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            search.style.borderTopWidth  = search.style.borderBottomWidth =
            search.style.borderLeftWidth = search.style.borderRightWidth  = 1;
            search.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            search.style.fontSize = 16;
            panel.Add(search);

            var scroll = new ScrollView();
            scroll.style.height     = 240;
            scroll.style.flexShrink = 0;
            scroll.style.overflow   = Overflow.Hidden;
            // Force the scroll viewport to also clip
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            if (viewport != null) viewport.style.overflow = Overflow.Hidden;
            panel.Add(scroll);

            void Populate(string filter)
            {
                scroll.Clear();
                foreach (var c in countries)
                {
                    if (!string.IsNullOrEmpty(filter) &&
                        !c.Name.ToUpper().Contains(filter.ToUpper()) &&
                        !c.Iso.ToUpper().Contains(filter.ToUpper())) continue;

                    var iso  = c.Iso;
                    var name = c.Name;
                    var btn  = new Button { text = $"{name}  [{c.Stage}]" };
                    StyleModalRowBtn(btn);
                    btn.clicked += () =>
                    {
                        _selectedCountryIso = iso;
                        if (_countryPickerBtn != null) _countryPickerBtn.text = name;
                        if (_saleErrorLabel   != null) _saleErrorLabel.style.display = DisplayStyle.None;
                        CloseModal(overlay);
                        UpdateSaleEstimate();

                        // Fly globe to country and show info card
                        var globe = ArmsFair.Map.GlobeBridge.Instance;
                        if (globe != null)
                        {
                            globe.FlyToCountry(iso);
                            // Use centre of WorldMapArea as card position
                            var wb = _worldMapArea?.worldBound ?? Rect.zero;
                            var panelBound = _root.panel.visualTree.worldBound;
                            float sx = (wb.x + wb.width  * 0.5f) / panelBound.width  * Screen.width;
                            float sy = (1f - (wb.y + wb.height * 0.5f) / panelBound.height) * Screen.height;
                            OnGlobeCountryClicked(iso, new Vector2(sx, sy));
                        }
                    };
                    scroll.Add(btn);
                }
            }

            Populate("");
            search.RegisterValueChangedCallback(evt => Populate(evt.newValue));

            var cancelRow = new VisualElement();
            cancelRow.style.marginTop  = 8;
            cancelRow.style.flexShrink = 0;
            var cancel = MakeModalCancelBtn(() => CloseModal(overlay));
            cancelRow.Add(cancel);
            panel.Add(cancelRow);
            overlay.Add(panel);
            _root.Add(overlay);
        }

        private void UpdateSaleEstimate()
        {
            if (_saleEstimateLabel == null) return;
            if (_selectedSaleType == SaleType.PeaceBroker)
            {
                _saleEstimateLabel.text = "Costs $2M. Earns 1 peace credit.";
                return;
            }
            if (_salesOrder.Count == 0 || _selectedCountryIso == null)
            {
                _saleEstimateLabel.text = "";
                return;
            }
            var country = _lastState?.Countries.FirstOrDefault(c => c.Iso == _selectedCountryIso);
            if (country == null) { _saleEstimateLabel.text = ""; return; }

            var stage    = (int)country.Stage;
            var stageMul = Balance.StageMultiplier[stage];
            var typeMul  = _selectedSaleType switch
            {
                SaleType.Covert   => Balance.CovertProfitPremium,
                SaleType.AidCover => Balance.AidCoverProfitPenalty,
                _                 => 1.0f
            };
            var dualMul  = _isDualSupply ? Balance.DualSupplyProfitMul : 1.0f;

            var total = 0;
            foreach (var kv in _salesOrder)
            {
                var entry = WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key);
                if (entry == null) continue;
                total += (int)MathF.Round(entry.BaseProfitMillions * stageMul * typeMul * dualMul * kv.Value, MidpointRounding.AwayFromZero);
            }
            _saleEstimateLabel.text = $"Est. profit: ${total}M  (Stage {stage} market)";
        }

        private void OnSubmitSale()
        {
            if (_saleErrorLabel != null) _saleErrorLabel.style.display = DisplayStyle.None;

            if (GameClient.Instance?.GameId == null)
            {
                ShowSaleError("Not connected to a game.");
                return;
            }

            if (_selectedSaleType != SaleType.PeaceBroker)
            {
                if (_salesOrder.Count == 0 || !_salesOrder.Any(kv => kv.Value > 0))
                { ShowSaleError("Select at least one weapon."); return; }
                if (_selectedCountryIso == null) { ShowSaleError("Select a target country."); return; }

                var targetCountry = _lastState?.Countries.FirstOrDefault(c => c.Iso == _selectedCountryIso);
                if (targetCountry?.Stage == CountryStage.FailedState)
                {
                    ShowFailedStateBlockModal(targetCountry.Name);
                    return;
                }
            }

            ShowSaleConfirmModal();
        }

        private void OnPassSale()
        {
            if (_submitSaleBtn != null) _submitSaleBtn.SetEnabled(false);
        }

        private void ShowSaleError(string msg)
        {
            if (_saleErrorLabel == null) return;
            _saleErrorLabel.text = msg;
            _saleErrorLabel.style.display = DisplayStyle.Flex;
        }

        private void ShowFailedStateBlockModal(string countryName)
        {
            var overlay = MakeModalOverlay();
            var panel   = MakeModalPanel(360);

            panel.Add(MakeModalTitle("SALE BLOCKED"));

            var body = new Label(
                $"{countryName} is a Failed State. No functioning government or market exists — weapons sales here generate no profit and cannot be processed.");
            body.style.color        = new StyleColor(new Color(0.85f, 0.85f, 0.85f));
            body.style.fontSize     = 14;
            body.style.whiteSpace   = WhiteSpace.Normal;
            body.style.marginBottom = 18;
            panel.Add(body);

            var okBtn = MakeModalCancelBtn(() => CloseModal(overlay));
            okBtn.text = "UNDERSTOOD";
            okBtn.style.width = Length.Percent(100);
            panel.Add(okBtn);

            overlay.Add(panel);
            _root.Add(overlay);
        }

        private void ShowSaleConfirmModal()
        {
            var overlay = MakeModalOverlay();
            var panel   = MakeModalPanel(380);

            panel.Add(MakeModalTitle("Confirm Sales Order"));

            AddModalRow(panel, "SALE TYPE", SaleTypeDisplay(_selectedSaleType).ToUpper());
            foreach (var kv in _salesOrder.Where(kv => kv.Value > 0))
            {
                var e = WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key);
                AddModalRow(panel, "WEAPON", kv.Value > 1 ? $"{e?.DisplayName}  x{kv.Value}" : e?.DisplayName ?? "");
            }
            if (_selectedCountryIso != null)
                AddModalRow(panel, "TARGET",
                    _lastState?.Countries.FirstOrDefault(c => c.Iso == _selectedCountryIso)?.Name ?? _selectedCountryIso);
            if (_isDualSupply)  AddModalRow(panel, "MODIFIER", "DUAL SUPPLY");
            if (_isProxyRouted) AddModalRow(panel, "MODIFIER", "GRAY CHANNEL");

            var divider = new VisualElement();
            divider.style.height          = 1;
            divider.style.backgroundColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            divider.style.marginTop       = 10;
            divider.style.marginBottom    = 10;
            panel.Add(divider);

            if (_saleEstimateLabel?.text.Length > 0)
            {
                var est = new Label(_saleEstimateLabel.text);
                est.style.color          = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                est.style.fontSize = 15;
                est.style.marginBottom   = 14;
                panel.Add(est);
            }

            var noteText = _selectedSaleType == SaleType.Open
                ? "This order is public. Other players will see it on the ticker immediately."
                : "This order is sealed. Other players will not see it until Reveal.";
            var note = new Label(noteText);
            note.style.color        = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            note.style.fontSize = 14;
            note.style.marginBottom = 16;
            panel.Add(note);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var cancelBtn  = MakeModalCancelBtn(() => CloseModal(overlay));
            cancelBtn.style.flexGrow   = 1;
            cancelBtn.style.marginRight = 8;

            var confirmBtn = new Button { text = "CONFIRM" };
            confirmBtn.style.flexGrow        = 1;
            confirmBtn.style.paddingTop      = confirmBtn.style.paddingBottom = 8;
            confirmBtn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            confirmBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 25f/255f, 8f/255f));
            confirmBtn.style.borderTopColor  = confirmBtn.style.borderBottomColor =
            confirmBtn.style.borderLeftColor = confirmBtn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            confirmBtn.style.borderTopWidth  = confirmBtn.style.borderBottomWidth =
            confirmBtn.style.borderLeftWidth = confirmBtn.style.borderRightWidth  = 1;

            cancelBtn.clicked  += () => CloseModal(overlay);
            confirmBtn.clicked += () => { CloseModal(overlay); SubmitSale(); };

            btnRow.Add(cancelBtn);
            btnRow.Add(confirmBtn);
            panel.Add(btnRow);
            overlay.Add(panel);
            _root.Add(overlay);
        }

        private async void SubmitSale()
        {
            if (_submitSaleBtn != null) _submitSaleBtn.SetEnabled(false);
            var lines = _salesOrder
                .Where(kv => kv.Value > 0)
                .Select(kv => new OrderLine(kv.Key, kv.Value))
                .ToList();

            foreach (var line in lines)
            {
                if (_inventory.TryGetValue(line.Category, out var cur))
                    _inventory[line.Category] = Math.Max(0, cur - line.Quantity);
            }
            RefreshInventoryBar();

            await GameClient.Instance.SubmitOrderAsync(new SubmitOrderMessage(
                SaleType      : _selectedSaleType,
                TargetCountry : _selectedCountryIso,
                Weapons       : lines,
                IsDualSupply  : _isDualSupply,
                IsProxyRouted : _isProxyRouted));
        }

        // â"€â"€ Modal helpers â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private VisualElement MakeModalOverlay()
        {
            var overlay = new VisualElement();
            overlay.style.position        = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
            overlay.style.alignItems      = Align.Center;
            overlay.style.justifyContent  = Justify.Center;
            _openModals.Add(overlay);
            if (ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.BlockInput = true;
            return overlay;
        }

        // Like MakeModalOverlay but NOT tracked in _openModals and attached to _docRoot (guaranteed full-screen size).
        // Caller is responsible for cleanup via its own _xxxOverlay field.
        private VisualElement MakePersistentOverlay()
        {
            var overlay = new VisualElement();
            overlay.style.position        = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
            overlay.style.alignItems      = Align.Center;
            overlay.style.justifyContent  = Justify.Center;
            return overlay;
        }

        private void AddPersistentOverlay(VisualElement overlay) => _docRoot.Add(overlay);

        private void RemovePersistentOverlay(VisualElement overlay)
        {
            if (_docRoot.Contains(overlay)) _docRoot.Remove(overlay);
        }

        private void CloseModal(VisualElement overlay)
        {
            _openModals.Remove(overlay);
            if (_root.Contains(overlay)) _root.Remove(overlay);
            if (_openModals.Count == 0 && ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.BlockInput = false;
        }

        private void CloseAllModals()
        {
            foreach (var m in _openModals.ToList())
                if (_root.Contains(m)) _root.Remove(m);
            _openModals.Clear();
            if (ArmsFair.Map.GlobeBridge.Instance != null)
                ArmsFair.Map.GlobeBridge.Instance.BlockInput = false;
        }

        private VisualElement MakeModalPanel(int width)
        {
            var panel = new VisualElement();
            panel.style.width           = width;
            panel.style.backgroundColor = new StyleColor(new Color(17f/255f, 17f/255f, 8f/255f));
            panel.style.borderTopColor  = panel.style.borderBottomColor =
            panel.style.borderLeftColor = panel.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            panel.style.borderTopWidth  = panel.style.borderBottomWidth =
            panel.style.borderLeftWidth = panel.style.borderRightWidth  = 1;
            panel.style.paddingTop      = panel.style.paddingBottom =
            panel.style.paddingLeft     = panel.style.paddingRight  = 20;
            return panel;
        }

        private Label MakeModalTitle(string text)
        {
            var t = new Label(text);
            t.style.fontSize = 18;
            t.style.color                       = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            t.style.unityFontStyleAndWeight     = FontStyle.Bold;
            t.style.marginBottom                = 14;
            return t;
        }

        private Button MakeModalCancelBtn(Action onCancel)
        {
            var btn = new Button { text = "CANCEL" };
            btn.style.paddingTop      = btn.style.paddingBottom = 8;
            btn.style.color           = new StyleColor(new Color(192f/255f, 144f/255f, 144f/255f));
            btn.style.backgroundColor = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  =
                new StyleColor(new Color(90f/255f, 42f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
            btn.clicked += onCancel;
            return btn;
        }

        private Label MakeOverlaySectionLabel(string text)
        {
            var l = new Label(text);
            l.style.color        = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            l.style.fontSize = 14;
            l.style.marginTop    = 10;
            l.style.marginBottom = 4;
            return l;
        }

        private Label MakeOverlayRowLabel(string text)
        {
            var l = new Label(text);
            l.style.color        = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            l.style.fontSize = 17;
            l.style.marginBottom = 2;
            return l;
        }

        private static void StyleModalRowBtn(Button btn)
        {
            btn.style.fontSize = 16;
            btn.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            btn.style.backgroundColor = new StyleColor(new Color(25f/255f, 25f/255f, 15f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
            btn.style.marginBottom    = 4;
            btn.style.paddingTop      = btn.style.paddingBottom = 8;
            btn.style.paddingLeft     = btn.style.paddingRight  = 8;
            btn.style.flexShrink      = 0;
            btn.style.height          = StyleKeyword.Auto;
            btn.style.unityTextAlign  = TextAnchor.UpperLeft;
            TerminalUI.AddHover(btn);
        }

        private static void AddModalRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom   = 6;
            var l = new Label(label);
            l.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            l.style.fontSize = 16;
            var v = new Label(value);
            v.style.color    = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            v.style.fontSize = 16;
            row.Add(l); row.Add(v);
            parent.Add(row);
        }

        private static Button MakeSaleToggleBtn(string label)
        {
            var btn = new Button { text = label };
            btn.style.fontSize = 15;
            btn.style.paddingTop    = 5; btn.style.paddingBottom = 5;
            btn.style.paddingLeft   = 8; btn.style.paddingRight  = 8;
            btn.style.marginRight   = 6;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            StyleSaleToggleBtn(btn, false);

            bool isActive = false;
            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                btn.style.backgroundColor = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                btn.style.color           = new StyleColor(new Color(13f/255f, 13f/255f, 13f/255f));
            });
            btn.RegisterCallback<MouseLeaveEvent>(_ => StyleSaleToggleBtn(btn, isActive));
            btn.RegisterCallback<ClickEvent>(_ => isActive = !isActive);

            return btn;
        }

        private static void StyleSaleToggleBtn(Button btn, bool active)
        {
            btn.style.color           = new StyleColor(active
                ? new Color(138f/255f, 184f/255f, 112f/255f)
                : new Color(138f/255f, 134f/255f, 112f/255f));
            btn.style.backgroundColor = new StyleColor(active
                ? new Color(20f/255f, 35f/255f, 15f/255f)
                : new Color(15f/255f, 15f/255f, 8f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  = new StyleColor(active
                ? new Color(58f/255f, 90f/255f, 42f/255f)
                : new Color(58f/255f, 58f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
        }

        private static void StyleSaleTypeBtn(Button btn, bool selected)
        {
            btn.style.color           = new StyleColor(selected
                ? new Color(138f/255f, 184f/255f, 112f/255f)
                : new Color(138f/255f, 134f/255f, 112f/255f));
            btn.style.backgroundColor = new StyleColor(selected
                ? new Color(20f/255f, 35f/255f, 15f/255f)
                : new Color(15f/255f, 15f/255f, 8f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  = new StyleColor(selected
                ? new Color(58f/255f, 90f/255f, 42f/255f)
                : new Color(58f/255f, 58f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
        }

        // â"€â"€ Binding â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private void BindState(GameState state)
        {
            BindTracks(state.Tracks);

            _roundLabel.text = $"ROUND {Math.Max(1, state.Round)}";
            _phaseLabel.text = state.Phase.ToString().ToUpper();

            // Local player stats
            var localId = AccountManager.Instance.LocalPlayer?.Id;
            var me = state.Players.Find(p => p.Id == localId);
            _companyLabel.text      = AccountManager.Instance.LocalPlayer?.CompanyName ?? AccountManager.Instance.LocalPlayer?.Username ?? "--";
            _capitalLabel.text      = me != null ? $"${me.Capital}M"                : "$--M";
            _reputationLabel.text   = me != null ? me.Reputation.ToString()          : "--";
            _sharePriceLabel.text   = me != null ? $"${me.SharePrice}"               : "$--";
            _peaceCreditsLabel.text = me != null ? me.PeaceCredits.ToString()        : "--";
            _latentRiskLabel.text   = me != null ? me.LatentRisk.ToString()          : "--";

            RefreshPlayerFooter(state.Players);
        }

        private void RefreshPlayerFooter(List<PlayerProfile> players)
        {
            if (_playerList == null) return;
            var localId = AccountManager.Instance.LocalPlayer?.Id;

            // Players footer -- horizontal cards showing name + capital on one line
            _playerList.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                var player   = players[i];
                bool isMe    = player.Id == localId;
                var  name    = (player.CompanyName ?? player.Username ?? "?").ToUpper();
                var  capital = $"${player.Capital}M";

                var arcColor = ArmsFair.Map.GlobeBridge.PlayerArcColors[
                    i % ArmsFair.Map.GlobeBridge.PlayerArcColors.Length];

                var card = new VisualElement();
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems    = Align.Center;
                card.style.borderTopWidth    = card.style.borderRightWidth =
                    card.style.borderBottomWidth = card.style.borderLeftWidth = isMe ? 2 : 1;
                card.style.borderTopColor    = card.style.borderRightColor =
                    card.style.borderBottomColor = card.style.borderLeftColor =
                    new StyleColor(arcColor);
                card.style.backgroundColor = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
                card.style.paddingTop    = card.style.paddingBottom = 4;
                card.style.paddingLeft   = card.style.paddingRight  = 10;
                card.style.marginRight   = 8;

                var nameText  = isMe ? $"> {name}" : name;
                var nameLabel = new Label(nameText);
                nameLabel.style.color      = new StyleColor(isMe
                    ? new Color(138f/255f, 184f/255f, 112f/255f)
                    : new Color(0.831f, 0.812f, 0.722f));
                nameLabel.style.fontSize   = 15;
                nameLabel.style.whiteSpace = WhiteSpace.NoWrap;

                var capLabel = new Label(capital);
                capLabel.style.color       = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                capLabel.style.fontSize    = 14;
                capLabel.style.whiteSpace  = WhiteSpace.NoWrap;
                capLabel.style.marginLeft  = 8;

                card.Add(nameLabel);
                card.Add(capLabel);
                _playerList.Add(card);
            }
        }

        private void BindTracks(WorldTracks t)
        {
            _marketHeatLabel.text    = $"MARKET HEAT: {t.MarketHeat}";
            _civilianCostLabel.text  = $"CIVILIAN COST: {t.CivilianCost}";
            _instabilityLabel.text     = $"INSTABILITY: {t.Instability}";
            _sanctionsRiskLabel.text = $"SANCTIONS: {t.SanctionsRisk}";
            _geoTensionLabel.text    = $"GEO TENSION: {t.GeoTension}";

            SetTrackColor(_marketHeatLabel,    t.MarketHeat,    inverted: false);
            SetTrackColor(_civilianCostLabel,  t.CivilianCost,  inverted: false);
            SetTrackColor(_instabilityLabel,     t.Instability,   inverted: false);
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

        private void BuildWorldUpdatePanel()
        {
            if (_root == null) return;

            _worldUpdatePanel = new VisualElement();
            _worldUpdatePanel.style.flexDirection  = FlexDirection.Column;
            _worldUpdatePanel.style.paddingTop     = 16;
            _worldUpdatePanel.style.paddingBottom  = 16;
            _worldUpdatePanel.style.paddingLeft    = 16;
            _worldUpdatePanel.style.paddingRight   = 16;
            _worldUpdatePanel.style.display        = DisplayStyle.None;

            var title = new Label("WORLD UPDATE");
            title.style.fontSize   = 15;
            title.style.color      = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
            title.style.marginBottom = 10;
            _worldUpdatePanel.Add(title);

            _worldUpdateDeltaList = new ScrollView();
            _worldUpdateDeltaList.style.flexGrow   = 1;
            _worldUpdateDeltaList.style.maxHeight  = 280;
            _worldUpdatePanel.Add(_worldUpdateDeltaList);

            var leftColumn = _root.Q("LeftColumn") ?? _root;
            leftColumn.Add(_worldUpdatePanel);
        }

        private static string WeaponDisplay(WeaponCategory cat) => cat switch
        {
            WeaponCategory.SmallArms        => "Small Arms",
            WeaponCategory.CombatHelicopters => "Combat Helicopters",
            WeaponCategory.FighterJets      => "Fighter Jets",
            WeaponCategory.AirDefense       => "Air Defense",
            WeaponCategory.CruiseMissiles   => "Cruise Missiles",
            WeaponCategory.IcbmComponents   => "ICBM Components",
            WeaponCategory.NuclearWarhead   => "Nuclear Warhead",
            WeaponCategory.FissileMaterials => "Fissile Materials",
            _                               => cat.ToString()
        };

        private static string StageDisplay(CountryStage stage) => stage switch
        {
            CountryStage.HotWar             => "Hot War",
            CountryStage.HumanitarianCrisis => "Humanitarian Crisis",
            CountryStage.FailedState        => "Failed State",
            _                               => stage.ToString()
        };

        private static string SaleTypeDisplay(SaleType t) => t switch
        {
            SaleType.Open        => "Open",
            SaleType.Covert      => "Covert",
            SaleType.AidCover    => "Aid Cover",
            SaleType.PeaceBroker => "Peace Broker",
            _                    => t.ToString()
        };
    }
}

