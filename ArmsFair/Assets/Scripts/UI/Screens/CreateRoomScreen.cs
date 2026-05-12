using System;
using System.Collections.Generic;
using ArmsFair.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class CreateRoomScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private TextField     _roomNameField;
        private Button        _slotsBtn;
        private Button        _timerBtn;
        private Button        _gameModeBtn;
        private Button        _privateBtn;
        private Button        _aiFillBtn;
        private Label         _errorLabel;

        private VisualElement _choiceModal;
        private Label         _choiceTitle;
        private TextField     _choiceSearch;
        private ScrollView    _choiceList;

        private List<string>   _currentChoices;
        private Action<string> _currentOnSelect;
        private string         _currentSelected;

        private string _selectedSlots    = "4";
        private string _selectedTimer    = "90s";
        private string _selectedGameMode = "Realistic";
        private bool   _isPrivate;
        private bool   _isAiFill;

        private LobbyApiClient Lobby => new LobbyApiClient(Network.NetworkConfig.ServerBaseUrl);

        private static readonly List<string> Slots = new()
            { "2", "3", "4", "5", "6" };

        private static readonly List<string> Timers = new()
            { "60s", "90s", "120s", "180s" };

        private static readonly List<string> GameModes = new()
            { "Realistic", "EqualWorld", "BlankSlate", "HotWorld", "Custom" };

        private static readonly Dictionary<string, int> GameModeValues = new()
        {
            { "Realistic",  1 },
            { "EqualWorld", 2 },
            { "BlankSlate", 3 },
            { "HotWorld",   4 },
            { "Custom",     5 },
        };

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

            _root = docRoot.Q("CreateRoomScreen");
            if (_root == null) { Debug.LogError("[CreateRoomScreen] CreateRoomScreen element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _roomNameField = _root.Q<TextField>("RoomNameField");
            _slotsBtn      = _root.Q<Button>("SlotsBtn");
            _timerBtn      = _root.Q<Button>("TimerBtn");
            _gameModeBtn   = _root.Q<Button>("GameModeBtn");
            _privateBtn    = _root.Q<Button>("PrivateBtn");
            _aiFillBtn     = _root.Q<Button>("AiFillBtn");
            _errorLabel    = _root.Q<Label>("ErrorLabel");
            _choiceModal   = _root.Q<VisualElement>("ChoiceModal");
            _choiceTitle   = _root.Q<Label>("ChoiceTitle");
            _choiceSearch  = _root.Q<TextField>("ChoiceSearch");
            _choiceList    = _root.Q<ScrollView>("ChoiceList");

            _choiceSearch.RegisterValueChangedCallback(evt => FilterChoices(evt.newValue));

            _slotsBtn.clicked    += () => OpenModal("PLAYER SLOTS", Slots,     _selectedSlots,    false, v => { _selectedSlots    = v; _slotsBtn.text    = v + " PLAYERS"; });
            _timerBtn.clicked    += () => OpenModal("TIMER PRESET",  Timers,    _selectedTimer,    false, v => { _selectedTimer    = v; _timerBtn.text    = v; });
            _gameModeBtn.clicked += () => OpenModal("GAME MODE",     GameModes, _selectedGameMode, false, v => { _selectedGameMode = v; _gameModeBtn.text = v; });

            _privateBtn.clicked += () => SetToggle(ref _isPrivate, _privateBtn);
            _aiFillBtn.clicked  += () => SetToggle(ref _isAiFill,  _aiFillBtn);
            SetToggleVisual(_privateBtn, _isPrivate);
            SetToggleVisual(_aiFillBtn,  _isAiFill);

            TerminalUI.StyleButton(_root.Q<Button>("CreateBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("BackBtn"));
            TerminalUI.StyleLabels(_root);

            _root.Q<Button>("CreateBtn").clicked += OnCreate;
            _root.Q<Button>("BackBtn").clicked   += () => UIManager.Instance.Pop();


            UIManager.Instance.Register("CreateRoom", this);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display        = DisplayStyle.Flex;
            _errorLabel.style.display  = DisplayStyle.None;
            _choiceModal.style.display = DisplayStyle.None;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        private void OpenModal(string title, List<string> choices, string current, bool showSearch, Action<string> onSelect)
        {
            _choiceTitle.text  = title;
            _currentChoices    = choices;
            _currentOnSelect   = onSelect;
            _currentSelected   = current;

            _choiceSearch.SetValueWithoutNotify("");
            _choiceSearch.style.display = showSearch ? DisplayStyle.Flex : DisplayStyle.None;

            if (showSearch)
            {
                _choiceSearch.style.color             = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                _choiceSearch.style.backgroundColor   = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
                _choiceSearch.style.borderTopColor    = new StyleColor(new Color(74f/255f, 74f/255f, 48f/255f));
                _choiceSearch.style.borderBottomColor = _choiceSearch.style.borderTopColor;
                _choiceSearch.style.borderLeftColor   = _choiceSearch.style.borderTopColor;
                _choiceSearch.style.borderRightColor  = _choiceSearch.style.borderTopColor;
            }

            PopulateChoiceList(choices);
            _choiceList.style.height   = new StyleLength(showSearch ? 340f : 390f);
            _choiceModal.style.display = DisplayStyle.Flex;

            if (showSearch) _choiceSearch.Focus();
        }

        private void FilterChoices(string query)
        {
            if (_currentChoices == null) return;
            var filtered = string.IsNullOrEmpty(query)
                ? _currentChoices
                : _currentChoices.FindAll(c => c.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            PopulateChoiceList(filtered);
        }

        private void PopulateChoiceList(List<string> choices)
        {
            _choiceList.Clear();
            foreach (var choice in choices)
            {
                var c           = choice;
                bool isSelected = c == _currentSelected;
                var textCol     = isSelected ? new Color(0.416f, 0.588f, 0.314f) : new Color(0.831f, 0.812f, 0.722f);
                var bdrCol      = isSelected ? new Color(0.314f, 0.467f, 0.196f) : new Color(74f/255f, 74f/255f, 48f/255f);

                var btn = new Button(() =>
                {
                    _currentOnSelect(c);
                    _choiceModal.style.display = DisplayStyle.None;
                });
                btn.text                    = c;
                btn.style.color             = new StyleColor(textCol);
                btn.style.backgroundColor   = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
                btn.style.borderTopColor    = new StyleColor(bdrCol);
                btn.style.borderBottomColor = new StyleColor(bdrCol);
                btn.style.borderLeftColor   = new StyleColor(bdrCol);
                btn.style.borderRightColor  = new StyleColor(bdrCol);
                btn.style.borderTopWidth    = 1;
                btn.style.borderBottomWidth = 1;
                btn.style.borderLeftWidth   = 1;
                btn.style.borderRightWidth  = 1;
                btn.style.paddingTop        = 10;
                btn.style.paddingBottom     = 10;
                btn.style.paddingLeft       = 12;
                btn.style.paddingRight      = 12;
                btn.style.marginBottom      = 4;
                btn.style.fontSize          = 15;
                btn.style.unityTextAlign    = TextAnchor.MiddleCenter;
                btn.style.whiteSpace        = WhiteSpace.NoWrap;

                btn.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    btn.style.backgroundColor = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                    btn.style.color           = new StyleColor(new Color(0.051f, 0.051f, 0.031f));
                });
                btn.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    btn.style.backgroundColor = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
                    btn.style.color           = new StyleColor(textCol);
                });

                _choiceList.Add(btn);
            }
        }

        private void SetToggle(ref bool state, Button btn)
        {
            state = !state;
            SetToggleVisual(btn, state);
        }

        private static void SetToggleVisual(Button btn, bool on)
        {
            btn.text = on ? "YES" : "NO";
            var col  = on ? new Color(0.416f, 0.588f, 0.314f) : new Color(0.541f, 0.525f, 0.439f);
            var bdr  = on ? new Color(0.314f, 0.467f, 0.196f) : new Color(0.227f, 0.227f, 0.165f);
            btn.style.color             = new StyleColor(col);
            btn.style.borderTopColor    = new StyleColor(bdr);
            btn.style.borderBottomColor = new StyleColor(bdr);
            btn.style.borderLeftColor   = new StyleColor(bdr);
            btn.style.borderRightColor  = new StyleColor(bdr);
        }

        private async void OnCreate()
        {
            _errorLabel.style.display = DisplayStyle.None;

            if (string.IsNullOrWhiteSpace(_roomNameField.value))
            {
                _errorLabel.text          = "ROOM NAME REQUIRED";
                _errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            if (!int.TryParse(_selectedSlots, out int slots)) slots = 4;
            if (!GameModeValues.TryGetValue(_selectedGameMode, out int gameModeInt)) gameModeInt = 1;

            var payload = new CreateRoomPayload
            {
                roomName     = _roomNameField.value.Trim(),
                playerSlots  = slots,
                timerPreset  = _selectedTimer,
                voiceEnabled = false,
                aiFillIn     = _isAiFill,
                isPrivate    = _isPrivate,
                gameMode     = gameModeInt,
            };

            try
            {
                var room = await Lobby.CreateRoomAsync(payload);
                LobbyState.PendingRoomId = room.roomId;
                UIManager.Instance.GoTo("PreGameLobby");
            }
            catch (Exception ex)
            {
                _errorLabel.text = ex.Message.Contains("401") ? "SESSION EXPIRED — PLEASE LOG IN AGAIN"
                                 : ex.Message.Contains("400") ? "INVALID ROOM SETTINGS"
                                 : "CONNECTION ERROR";
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }
    }
}
