using System;
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

        // Procurement panel
        private VisualElement                    _procurementPanel;
        private ScrollView                       _weaponList;
        private Label                            _procTotalLabel;
        private Label                            _procErrorLabel;
        private Button                           _confirmProcBtn;
        private Dictionary<WeaponCategory, int>  _quantities  = new();
        private Dictionary<WeaponCategory, int>  _inventory   = new();
        private int                              _procCapitalM;

        // Persistent inventory bar
        private VisualElement _inventoryBar;
        private VisualElement _inventoryItems;

        // Reveal overlay
        private VisualElement _revealOverlay;

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

        // Ready button
        private Button _readyBtn;

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

            _readyBtn = _root.Q<Button>("ReadyBtn");
            if (_readyBtn != null) _readyBtn.clicked += OnReadyClicked;

            _inventoryBar   = _root.Q("InventoryBar");
            _inventoryItems = _root.Q("InventoryItems");

            _procurementPanel = _root.Q("ProcurementPanel");
            _weaponList       = _root.Q<ScrollView>("WeaponList");
            _procTotalLabel   = _root.Q<Label>("ProcTotalLabel");
            _procErrorLabel   = _root.Q<Label>("ProcErrorLabel");
            _confirmProcBtn   = _root.Q<Button>("ConfirmProcBtn");
            if (_confirmProcBtn != null) _confirmProcBtn.clicked += OnConfirmProcurement;

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
            if (_weaponPickerBtn  != null) _weaponPickerBtn.clicked  += OpenWeaponPicker;
            if (_countryPickerBtn != null) _countryPickerBtn.clicked += OpenCountryPicker;
            if (_submitSaleBtn    != null) _submitSaleBtn.clicked    += OnSubmitSale;
            var passSaleBtn = _root.Q<Button>("PassSaleBtn");
            if (passSaleBtn != null) passSaleBtn.clicked += OnPassSale;

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
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
            _inventory.Clear();
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

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnStateSync(StateSync msg)
        {
            _lastState = msg.FullState;
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
            _statusLabel.text      = $"PHASE STARTED — ROUND {round}";
            ShowPanel(msg.Phase);

            if (_readyBtn != null)
            {
                _readyBtn.text    = "READY";
                _readyBtn.SetEnabled(true);
            }
        }

        private void OnPlayerReady(string playerId)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            var localId = AccountManager.Instance.LocalPlayer?.Id;
            if (playerId != localId)
                _statusLabel.text = $"PLAYER {playerId[..Math.Min(8, playerId.Length)]} READY";
        }

        private async void OnReadyClicked()
        {
            if (_readyBtn != null)
            {
                _readyBtn.SetEnabled(false);
                _readyBtn.text = "READY ✓";
            }
            await GameClient.Instance.MarkReadyAsync();
        }

        private void OnConsequences(ConsequencesMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            if (_lastState == null) return;

            // Sum all profit entries per player — multi-weapon orders produce one entry per weapon
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
        }

        private void OnWorldUpdate(WorldUpdateMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            BindTracks(msg.NewTracks);
        }

        private void OnReveal(RevealMessage msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;

            if (_revealOverlay != null) { _root.Remove(_revealOverlay); _revealOverlay = null; }

            _revealOverlay = MakeModalOverlay();

            var panel = MakeModalPanel(460);
            panel.Add(MakeModalTitle("ROUND REVEAL"));

            if (msg.Actions == null || msg.Actions.Count == 0)
            {
                var empty = new Label("No orders were submitted this round.");
                empty.style.color        = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                empty.style.fontSize     = 13;
                empty.style.marginBottom = 14;
                panel.Add(empty);
            }
            else
            {
                var localId = AccountManager.Instance.LocalPlayer?.Id;

                foreach (var action in msg.Actions)
                {
                    var company = string.IsNullOrEmpty(action.CompanyName) ? "UNKNOWN" : action.CompanyName.ToUpper();
                    var isMe    = action.PlayerId == localId;

                    var row = new VisualElement();
                    row.style.flexDirection     = FlexDirection.Row;
                    row.style.alignItems        = Align.Center;
                    row.style.paddingTop        = 7;
                    row.style.paddingBottom     = 7;
                    row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                    row.style.borderBottomWidth = 1;

                    var companyLabel = new Label(isMe ? $"> {company}" : $"  {company}");
                    companyLabel.style.color    = new StyleColor(isMe
                        ? new Color(138f/255f, 184f/255f, 112f/255f)
                        : new Color(212f/255f, 207f/255f, 184f/255f));
                    companyLabel.style.fontSize = 13;
                    companyLabel.style.width    = 160;
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
                            ? WeaponCatalog.Items.FirstOrDefault(i => i.Category == action.WeaponCategory.Value)?.DisplayName ?? action.WeaponCategory.Value.ToString()
                            : "?";
                        var countryStr = action.TargetIso != null
                            ? (_lastState?.Countries.FirstOrDefault(c => c.Iso == action.TargetIso)?.Name ?? action.TargetIso)
                            : "?";
                        detail.text = $"{saleTypeStr}  {weaponStr}  →  {countryStr}";
                    }
                    detail.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                    detail.style.fontSize = 13;
                    detail.style.flexGrow = 1;

                    row.Add(companyLabel);
                    row.Add(detail);
                    panel.Add(row);
                }
            }

            var divider = new VisualElement();
            divider.style.height          = 1;
            divider.style.backgroundColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            divider.style.marginTop       = 12;
            divider.style.marginBottom    = 12;
            panel.Add(divider);

            var closeBtn = new Button { text = "CLOSE" };
            closeBtn.style.paddingTop      = closeBtn.style.paddingBottom = 8;
            closeBtn.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            closeBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
            closeBtn.style.borderTopColor  = closeBtn.style.borderBottomColor =
            closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            closeBtn.style.borderTopWidth  = closeBtn.style.borderBottomWidth =
            closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth  = 1;
            closeBtn.clicked += () =>
            {
                if (_revealOverlay != null) { _root.Remove(_revealOverlay); _revealOverlay = null; }
            };
            panel.Add(closeBtn);

            _revealOverlay.Add(panel);
            _root.Add(_revealOverlay);

            if (_statusLabel != null) _statusLabel.text = "REVEAL: ALL ORDERS DISCLOSED";
        }

        // ── Panel switching ───────────────────────────────────────────────────

        private void ShowPanel(GamePhase phase)
        {
            if (_revealOverlay != null) { _root.Remove(_revealOverlay); _revealOverlay = null; }

            var isProcurement = phase == GamePhase.Procurement;
            var isSales       = phase == GamePhase.Sales;

            if (_procurementPanel != null)
                _procurementPanel.style.display = isProcurement ? DisplayStyle.Flex : DisplayStyle.None;
            if (_salesPanel != null)
                _salesPanel.style.display = isSales ? DisplayStyle.Flex : DisplayStyle.None;
            if (_phaseStatusLabel != null)
                _phaseStatusLabel.style.display = (isProcurement || isSales) ? DisplayStyle.None : DisplayStyle.Flex;

            if (isProcurement)
            {
                var localId = AccountManager.Instance.LocalPlayer?.Id;
                _procCapitalM = _lastState?.Players.FirstOrDefault(p => p.Id == localId)?.Capital ?? 0;
                BuildProcurementPanel();
            }

            if (isSales) BuildSalesPanel();
        }

        private void BuildProcurementPanel()
        {
            if (_weaponList == null) return;
            _weaponList.Clear();
            _quantities.Clear();
            UpdateProcTotal();

            if (_procErrorLabel != null) _procErrorLabel.style.display = DisplayStyle.None;
            if (_confirmProcBtn  != null) _confirmProcBtn.SetEnabled(true);

            foreach (var entry in WeaponCatalog.Items)
            {
                var cat = entry.Category;
                _quantities[cat] = 0;

                var row = new VisualElement();
                row.style.flexDirection     = FlexDirection.Row;
                row.style.justifyContent    = Justify.SpaceBetween;
                row.style.alignItems        = Align.Center;
                row.style.paddingTop        = 7;
                row.style.paddingBottom     = 7;
                row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderBottomWidth = 1;

                var nameLabel = new Label(entry.DisplayName);
                nameLabel.style.color    = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                nameLabel.style.fontSize = 14;
                nameLabel.style.flexGrow = 1;

                var costLabel = new Label($"${entry.BaseCostMillions}M ea");
                costLabel.style.color          = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                costLabel.style.fontSize       = 12;
                costLabel.style.width          = 60;
                costLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                costLabel.style.marginRight    = 10;

                var qtyLabel = new Label("0");
                qtyLabel.style.color          = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                qtyLabel.style.fontSize       = 14;
                qtyLabel.style.width          = 24;
                qtyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                var minusBtn = MakeQtyButton("-");
                var plusBtn  = MakeQtyButton("+");

                minusBtn.clicked += () =>
                {
                    if (_quantities[cat] <= 0) return;
                    _quantities[cat]--;
                    qtyLabel.text = _quantities[cat].ToString();
                    UpdateProcTotal();
                };

                plusBtn.clicked += () =>
                {
                    _quantities[cat]++;
                    qtyLabel.text = _quantities[cat].ToString();
                    UpdateProcTotal();
                };

                row.Add(nameLabel);
                row.Add(costLabel);
                row.Add(minusBtn);
                row.Add(qtyLabel);
                row.Add(plusBtn);
                _weaponList.Add(row);
            }

        }

        private void RefreshInventoryBar()
        {
            if (_inventoryBar == null || _inventoryItems == null) return;
            _inventoryItems.Clear();

            var hasItems = _inventory.Any(kv => kv.Value > 0);
            _inventoryBar.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasItems) return;

            foreach (var kv in _inventory)
            {
                if (kv.Value == 0) continue;
                var entry = WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key);
                if (entry == null) continue;

                var chip = new Label($"{entry.DisplayName} x{kv.Value}");
                chip.style.color            = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
                chip.style.fontSize         = 12;
                chip.style.backgroundColor  = new StyleColor(new Color(20f/255f, 30f/255f, 12f/255f));
                chip.style.borderTopColor   = chip.style.borderBottomColor =
                chip.style.borderLeftColor  = chip.style.borderRightColor  =
                    new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                chip.style.borderTopWidth   = chip.style.borderBottomWidth =
                chip.style.borderLeftWidth  = chip.style.borderRightWidth  = 1;
                chip.style.paddingTop       = chip.style.paddingBottom = 2;
                chip.style.paddingLeft      = chip.style.paddingRight  = 8;
                chip.style.marginRight      = 8;
                chip.style.whiteSpace       = WhiteSpace.NoWrap;
                _inventoryItems.Add(chip);
            }
        }

        private static Button MakeQtyButton(string label)
        {
            var btn = new Button { text = label };
            btn.style.width           = 26;
            btn.style.height          = 26;
            btn.style.fontSize        = 14;
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
            title.style.fontSize       = 16;
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
                nameLabel.style.fontSize = 13;

                var costLabel = new Label($"${lineCost}M");
                costLabel.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
                costLabel.style.fontSize = 13;

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
            totalLabel.style.fontSize       = 14;
            totalLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var totalAmt = new Label($"${totalM}M");
            totalAmt.style.color          = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            totalAmt.style.fontSize       = 14;
            totalAmt.style.unityFontStyleAndWeight = FontStyle.Bold;

            var afterLabel = new Label($"Capital after: ${capitalM - totalM}M");
            afterLabel.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            afterLabel.style.fontSize = 12;
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

            cancelBtn.clicked  += () => _root.Remove(overlay);
            confirmBtn.clicked += () =>
            {
                _root.Remove(overlay);
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

        // ── Sales panel ──────────────────────────────────────────────────────

        private void BuildSalesPanel()
        {
            _selectedSaleType   = SaleType.Open;
            _selectedCountryIso = null;
            _isDualSupply       = false;
            _isProxyRouted      = false;
            _salesOrder.Clear();
            _saleTypeBtns.Clear();

            if (_saleTypeRow != null)
            {
                _saleTypeRow.Clear();
                var types = new[] { SaleType.Open, SaleType.Covert, SaleType.AidCover, SaleType.PeaceBroker };
                var labels = new[] { "OPEN", "COVERT", "AID COVER", "PEACE BROKER" };
                for (int i = 0; i < types.Length; i++)
                {
                    var t   = types[i];
                    var btn = new Button { text = labels[i] };
                    btn.style.fontSize      = 12;
                    btn.style.paddingTop    = 5; btn.style.paddingBottom = 5;
                    btn.style.paddingLeft   = 8; btn.style.paddingRight  = 8;
                    btn.style.marginRight   = 6;
                    StyleSaleTypeBtn(btn, selected: t == SaleType.Open);
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

        private void OpenWeaponPicker()
        {
            var available = _inventory.Where(kv => kv.Value > 0).ToList();
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
            var panel   = MakeModalPanel(320);
            panel.Add(MakeModalTitle("Select Weapon + Quantity"));

            var pending = new Dictionary<WeaponCategory, int>();
            var qtyLabels = new Dictionary<WeaponCategory, Label>();

            foreach (var kv in available)
            {
                var entry = WeaponCatalog.Items.FirstOrDefault(i => i.Category == kv.Key);
                if (entry == null) continue;
                var cat    = kv.Key;
                var maxQty = kv.Value;
                pending[cat] = _salesOrder.TryGetValue(cat, out var prev) ? Math.Min(prev, maxQty) : 0;

                var row = new VisualElement();
                row.style.flexDirection     = FlexDirection.Row;
                row.style.alignItems        = Align.Center;
                row.style.justifyContent    = Justify.SpaceBetween;
                row.style.paddingTop        = 5;
                row.style.paddingBottom     = 5;
                row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderBottomWidth = 1;
                row.style.marginBottom      = 4;

                var nameLabel = new Label($"{entry.DisplayName}  (max {maxQty})");
                nameLabel.style.color    = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                nameLabel.style.fontSize = 13;
                nameLabel.style.flexGrow = 1;

                var qtyLabel = new Label(pending[cat].ToString());
                qtyLabel.style.color          = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
                qtyLabel.style.fontSize       = 14;
                qtyLabel.style.width          = 24;
                qtyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                qtyLabels[cat]                = qtyLabel;

                var minusBtn = MakeQtyButton("-");
                var plusBtn  = MakeQtyButton("+");

                minusBtn.clicked += () =>
                {
                    if (pending[cat] <= 0) return;
                    pending[cat]--;
                    qtyLabel.text = pending[cat].ToString();
                };
                plusBtn.clicked += () =>
                {
                    if (pending[cat] >= maxQty) return;
                    pending[cat]++;
                    qtyLabel.text = pending[cat].ToString();
                };

                row.Add(nameLabel);
                row.Add(minusBtn);
                row.Add(qtyLabel);
                row.Add(plusBtn);
                panel.Add(row);
            }

            var divider = new VisualElement();
            divider.style.height          = 1;
            divider.style.backgroundColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            divider.style.marginTop       = 10;
            divider.style.marginBottom    = 10;
            panel.Add(divider);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var cancelBtn = MakeModalCancelBtn(() => _root.Remove(overlay));
            cancelBtn.style.flexGrow   = 1;
            cancelBtn.style.marginRight = 8;

            var selectBtn = new Button { text = "SELECT" };
            selectBtn.style.flexGrow        = 1;
            selectBtn.style.paddingTop      = selectBtn.style.paddingBottom = 8;
            selectBtn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
            selectBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 25f/255f, 8f/255f));
            selectBtn.style.borderTopColor  = selectBtn.style.borderBottomColor =
            selectBtn.style.borderLeftColor = selectBtn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
            selectBtn.style.borderTopWidth  = selectBtn.style.borderBottomWidth =
            selectBtn.style.borderLeftWidth = selectBtn.style.borderRightWidth  = 1;

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
                        return kv.Value > 1 ? $"{e?.DisplayName} x{kv.Value}" : e?.DisplayName ?? kv.Key.ToString();
                    });
                    _weaponPickerBtn.text = string.Join(", ", parts);
                }
                if (_saleErrorLabel != null) _saleErrorLabel.style.display = DisplayStyle.None;
                _root.Remove(overlay);
                UpdateSaleEstimate();
            };

            btnRow.Add(cancelBtn);
            btnRow.Add(selectBtn);
            panel.Add(btnRow);
            overlay.Add(panel);
            _root.Add(overlay);
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

            var title    = MakeModalTitle("Select Target Country");
            panel.Add(title);

            var search   = new TextField();
            search.style.marginBottom    = 8;
            search.style.backgroundColor = new StyleColor(new Color(25f/255f, 25f/255f, 15f/255f));
            search.style.borderTopColor  = search.style.borderBottomColor =
            search.style.borderLeftColor = search.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            search.style.borderTopWidth  = search.style.borderBottomWidth =
            search.style.borderLeftWidth = search.style.borderRightWidth  = 1;
            search.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            search.style.fontSize        = 13;
            panel.Add(search);

            var listRow = new VisualElement();
            listRow.style.flexShrink = 0;
            var scroll = new ScrollView();
            scroll.style.height   = 220;
            scroll.style.flexGrow = 0;
            listRow.Add(scroll);
            panel.Add(listRow);

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
                        _root.Remove(overlay);
                        UpdateSaleEstimate();
                    };
                    scroll.Add(btn);
                }
            }

            Populate("");
            search.RegisterValueChangedCallback(evt => Populate(evt.newValue));

            var cancelRow = new VisualElement();
            cancelRow.style.marginTop  = 8;
            cancelRow.style.flexShrink = 0;
            var cancel = MakeModalCancelBtn(() => _root.Remove(overlay));
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
                total += (int)(entry.BaseProfitMillions * stageMul * typeMul * dualMul) * kv.Value;
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

        private void ShowSaleConfirmModal()
        {
            var overlay = MakeModalOverlay();
            var panel   = MakeModalPanel(380);

            panel.Add(MakeModalTitle("Confirm Sales Order"));

            AddModalRow(panel, "SALE TYPE", _selectedSaleType.ToString().ToUpper());
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
                est.style.fontSize       = 12;
                est.style.marginBottom   = 14;
                panel.Add(est);
            }

            var note = new Label("This order is sealed. Other players will not see it until Reveal.");
            note.style.color        = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            note.style.fontSize     = 11;
            note.style.marginBottom = 16;
            panel.Add(note);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var cancelBtn  = MakeModalCancelBtn(() => _root.Remove(overlay));
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

            cancelBtn.clicked  += () => _root.Remove(overlay);
            confirmBtn.clicked += () => { _root.Remove(overlay); SubmitSale(); };

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

        // ── Modal helpers ─────────────────────────────────────────────────────

        private VisualElement MakeModalOverlay()
        {
            var overlay = new VisualElement();
            overlay.style.position        = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
            overlay.style.alignItems      = Align.Center;
            overlay.style.justifyContent  = Justify.Center;
            return overlay;
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
            t.style.fontSize                    = 15;
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

        private static void StyleModalRowBtn(Button btn)
        {
            btn.style.fontSize        = 13;
            btn.style.color           = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            btn.style.backgroundColor = new StyleColor(new Color(25f/255f, 25f/255f, 15f/255f));
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  =
                new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
            btn.style.borderTopWidth  = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
            btn.style.marginBottom    = 4;
            btn.style.paddingTop      = btn.style.paddingBottom = 6;
            btn.style.unityTextAlign  = TextAnchor.MiddleLeft;
        }

        private static void AddModalRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom   = 6;
            var l = new Label(label);
            l.style.color    = new StyleColor(new Color(138f/255f, 134f/255f, 112f/255f));
            l.style.fontSize = 13;
            var v = new Label(value);
            v.style.color    = new StyleColor(new Color(212f/255f, 207f/255f, 184f/255f));
            v.style.fontSize = 13;
            row.Add(l); row.Add(v);
            parent.Add(row);
        }

        private static Button MakeSaleToggleBtn(string label)
        {
            var btn = new Button { text = label };
            btn.style.fontSize      = 12;
            btn.style.paddingTop    = 5; btn.style.paddingBottom = 5;
            btn.style.paddingLeft   = 8; btn.style.paddingRight  = 8;
            btn.style.marginRight   = 6;
            StyleSaleToggleBtn(btn, false);
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

        // ── Binding ──────────────────────────────────────────────────────────

        private void BindState(GameState state)
        {
            BindTracks(state.Tracks);

            _roundLabel.text = $"ROUND {Math.Max(1, state.Round)}";
            _phaseLabel.text = state.Phase.ToString().ToUpper();

            // Local player stats
            var localId = AccountManager.Instance.LocalPlayer?.Id;
            var me = state.Players.Find(p => p.Id == localId);
            _companyLabel.text      = AccountManager.Instance.LocalPlayer?.CompanyName ?? AccountManager.Instance.LocalPlayer?.Username ?? "—";
            _capitalLabel.text      = me != null ? $"${me.Capital}M"                : "$--M";
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
                label.style.fontSize   = 14;
                label.style.paddingTop    = 3;
                label.style.paddingBottom = 3;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                _playerList.Add(label);
            }
        }

        private void BindTracks(WorldTracks t)
        {
            _marketHeatLabel.text    = $"MARKET HEAT: {t.MarketHeat}";
            _civilianCostLabel.text  = $"CIVILIAN COST: {t.CivilianCost}";
            _stabilityLabel.text     = $"STABILITY: {t.Stability}";
            _sanctionsRiskLabel.text = $"SANCTIONS: {t.SanctionsRisk}";
            _geoTensionLabel.text    = $"GEO TENSION: {t.GeoTension}";

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
