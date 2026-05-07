using System;
using ArmsFair.Auth;
using ArmsFair.Network;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models.Messages;
using ArmsFair.Shared.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class PreGameLobbyScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Label         _roomNameLabel;
        private Label         _inviteCodeLabel;
        private Label         _slotLabel;
        private ScrollView    _playerList;
        private Label         _errorLabel;
        private Button        _startGameBtn;
        private Label         _waitingLabel;

        private LobbyApiClient _lobby;
        private string         _roomId;
        private bool           _isHost;
        private RoomInfo       _currentRoom;

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

            _root = docRoot.Q("PreGameLobbyScreen");
            if (_root == null) { Debug.LogError("[PreGameLobbyScreen] Root element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _roomNameLabel   = _root.Q<Label>("RoomNameLabel");
            _inviteCodeLabel = _root.Q<Label>("InviteCodeLabel");
            _slotLabel       = _root.Q<Label>("SlotLabel");
            _playerList      = _root.Q<ScrollView>("PlayerList");
            _errorLabel      = _root.Q<Label>("ErrorLabel");
            _startGameBtn    = _root.Q<Button>("StartGameBtn");
            _waitingLabel    = _root.Q<Label>("WaitingLabel");

            _startGameBtn.clicked += OnStartGame;
            _root.Q<Button>("LeaveBtn").clicked += OnLeave;

            TerminalUI.StyleButton(_startGameBtn);
            TerminalUI.StyleDangerButton(_root.Q<Button>("LeaveBtn"));
            TerminalUI.StyleLabels(_root);

            _lobby = new LobbyApiClient("https://armsfair.laynekudo.com");

            UIManager.Instance.Register("PreGameLobby", this);
        }

        private void Start()
        {
            if (GameClient.Instance != null)
                GameClient.Instance.OnStateSync.AddListener(OnStateSync);
        }

        private void OnDestroy()
        {
            if (GameClient.Instance != null)
                GameClient.Instance.OnStateSync.RemoveListener(OnStateSync);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display      = DisplayStyle.Flex;
            _errorLabel.style.display = DisplayStyle.None;

            _roomId = LobbyState.PendingRoomId;
            if (string.IsNullOrEmpty(_roomId))
            {
                _errorLabel.text          = "NO ROOM ID — RETURN TO LOBBY";
                _errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            RefreshAsync();
            InvokeRepeating(nameof(PollRoom), 3f, 3f);
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            CancelInvoke(nameof(PollRoom));
        }

        private void PollRoom() => RefreshAsync();

        private async void RefreshAsync()
        {
            try
            {
                var room = await _lobby.GetRoomAsync(_roomId);
                _currentRoom = room;
                BindRoom(room);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PreGameLobbyScreen] RefreshAsync failed: {ex.Message}");
            }
        }

        private void BindRoom(RoomInfo room)
        {
            _roomNameLabel.text   = room.roomName.ToUpper();
            _inviteCodeLabel.text = room.inviteCode;
            _slotLabel.text       = $"{room.playerIds?.Length ?? 1} / {room.playerSlots}";

            _isHost = room.hostPlayerId == AccountManager.Instance.LocalPlayer?.Id;
            _startGameBtn.style.display = _isHost ? DisplayStyle.Flex : DisplayStyle.None;
            _waitingLabel.style.display = _isHost ? DisplayStyle.None : DisplayStyle.Flex;

            // Populate player list
            _playerList.Clear();
            if (room.playerIds != null)
            {
                foreach (var id in room.playerIds)
                {
                    bool isThisHost = id == room.hostPlayerId;
                    var label = new Label(isThisHost
                        ? $"> {room.hostUsername.ToUpper()}  [HOST]"
                        : $"  OPERATIVE-{id[..Math.Min(4, id.Length)].ToUpper()}");
                    label.style.color      = new StyleColor(isThisHost
                        ? new Color(138f/255f, 184f/255f, 112f/255f)
                        : new Color(0.831f, 0.812f, 0.722f));
                    label.style.fontSize   = 11;
                    label.style.paddingTop    = 5;
                    label.style.paddingBottom = 5;
                    label.style.paddingLeft   = 8;
                    label.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                    label.style.borderBottomWidth = 1;
                    label.style.whiteSpace = WhiteSpace.NoWrap;
                    _playerList.Add(label);
                }
            }
        }

        private async void OnStartGame()
        {
            if (_currentRoom == null) return;
            _errorLabel.style.display = DisplayStyle.None;

            try
            {
                var gameMode = Enum.TryParse<GameMode>(_currentRoom.gameMode, out var gm)
                    ? gm : GameMode.Realistic;

                var settings = new LobbySettingsMessage(
                    PlayerSlots  : _currentRoom.playerSlots,
                    TimerPreset  : _currentRoom.timerPreset,
                    VoiceEnabled : false,
                    AiFillIn     : _currentRoom.aiFillIn,
                    IsPrivate    : _currentRoom.isPrivate,
                    GameMode     : gameMode);

                await GameClient.Instance.CreateGameAsync(settings);
                // Navigation happens in OnStateSync once server responds
            }
            catch (Exception ex)
            {
                _errorLabel.text          = $"START FAILED: {ex.Message}";
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }

        private void OnStateSync(StateSync msg)
        {
            if (_root == null || _root.style.display == DisplayStyle.None) return;
            CancelInvoke(nameof(PollRoom));
            UIManager.Instance.GoTo("HUD");
        }

        private void OnLeave()
        {
            CancelInvoke(nameof(PollRoom));
            UIManager.Instance.Pop();
        }
    }
}
