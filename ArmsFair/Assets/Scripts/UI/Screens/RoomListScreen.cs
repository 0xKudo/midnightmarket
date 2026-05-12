using System;
using ArmsFair.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class RoomListScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private TextField     _inviteCodeField;
        private Button        _joinByCodeBtn;
        private Label         _statusLabel;
        private ScrollView    _roomList;
        private Label         _errorLabel;

        private LobbyApiClient Lobby => new LobbyApiClient(Network.NetworkConfig.ServerBaseUrl);

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

            _root = docRoot.Q("RoomListScreen");
            if (_root == null) { Debug.LogError("[RoomListScreen] RoomListScreen element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _inviteCodeField = _root.Q<TextField>("InviteCodeField");
            _joinByCodeBtn   = _root.Q<Button>("JoinByCodeBtn");
            _statusLabel     = _root.Q<Label>("StatusLabel");
            _roomList        = _root.Q<ScrollView>("RoomList");
            _errorLabel      = _root.Q<Label>("ErrorLabel");

            _joinByCodeBtn.clicked += () => OnJoin(_inviteCodeField.value.Trim());
            _root.Q<Button>("RefreshBtn").clicked += Show;
            _root.Q<Button>("BackBtn").clicked    += () => UIManager.Instance.Pop();

            TerminalUI.StyleButton(_root.Q<Button>("JoinByCodeBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("RefreshBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("BackBtn"));
            TerminalUI.StyleLabels(_root);


            UIManager.Instance.Register("RoomList", this);
        }

        public async void Show()
        {
            if (_root == null) return;
            _root.style.display      = DisplayStyle.Flex;
            _errorLabel.style.display = DisplayStyle.None;
            _statusLabel.text         = "LOADING...";
            _statusLabel.style.display = DisplayStyle.Flex;
            _roomList.Clear();

            try
            {
                var rooms = await Lobby.ListRoomsAsync();
                PopulateRoomList(rooms);
            }
            catch (Exception ex)
            {
                _statusLabel.text = "FAILED TO LOAD ROOMS";
                Debug.LogError($"[RoomListScreen] ListRoomsAsync failed: {ex.Message}");
            }
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        private void PopulateRoomList(RoomSummary[] rooms)
        {
            _roomList.Clear();

            if (rooms == null || rooms.Length == 0)
            {
                _statusLabel.text          = "NO OPEN ROOMS";
                _statusLabel.style.display = DisplayStyle.Flex;
                return;
            }

            _statusLabel.style.display = DisplayStyle.None;

            foreach (var room in rooms)
            {
                var r   = room;
                var row = new VisualElement();
                row.style.flexDirection  = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems     = Align.Center;
                row.style.borderTopColor    = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderBottomColor = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderLeftColor   = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderRightColor  = new StyleColor(new Color(58f/255f, 58f/255f, 42f/255f));
                row.style.borderTopWidth    = 1;
                row.style.borderBottomWidth = 1;
                row.style.borderLeftWidth   = 1;
                row.style.borderRightWidth  = 1;
                row.style.paddingTop        = 8;
                row.style.paddingBottom     = 8;
                row.style.paddingLeft       = 10;
                row.style.paddingRight      = 10;
                row.style.marginBottom      = 4;

                var infoLabel = new Label($"{r.roomName}   [{r.playerCount}/{r.playerSlots}]   {r.gameMode}");
                infoLabel.style.color     = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
                infoLabel.style.fontSize  = 11;
                infoLabel.style.flexGrow  = 1;
                infoLabel.style.whiteSpace = WhiteSpace.NoWrap;

                var joinBtn = new Button(() => OnJoin(r.roomId));
                joinBtn.text                   = "JOIN";
                joinBtn.style.color            = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
                joinBtn.style.backgroundColor  = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
                joinBtn.style.borderTopColor   = new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                joinBtn.style.borderBottomColor = new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                joinBtn.style.borderLeftColor  = new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                joinBtn.style.borderRightColor = new StyleColor(new Color(58f/255f, 90f/255f, 42f/255f));
                joinBtn.style.borderTopWidth   = 1;
                joinBtn.style.borderBottomWidth = 1;
                joinBtn.style.borderLeftWidth  = 1;
                joinBtn.style.borderRightWidth = 1;
                joinBtn.style.paddingTop       = 5;
                joinBtn.style.paddingBottom    = 5;
                joinBtn.style.paddingLeft      = 12;
                joinBtn.style.paddingRight     = 12;
                joinBtn.style.fontSize         = 11;

                if (r.isStarted)
                {
                    joinBtn.SetEnabled(false);
                    joinBtn.style.opacity = 0.4f;
                    joinBtn.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        _statusLabel.text          = "GAME ALREADY IN PROGRESS";
                        _statusLabel.style.display = DisplayStyle.Flex;
                    });
                    joinBtn.RegisterCallback<PointerLeaveEvent>(_ =>
                        _statusLabel.style.display = DisplayStyle.None);
                }
                else
                {
                    joinBtn.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        joinBtn.style.backgroundColor = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
                        joinBtn.style.color           = new StyleColor(new Color(0.051f, 0.051f, 0.031f));
                    });
                    joinBtn.RegisterCallback<PointerLeaveEvent>(_ =>
                    {
                        joinBtn.style.backgroundColor = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
                        joinBtn.style.color           = new StyleColor(new Color(138f/255f, 184f/255f, 112f/255f));
                    });
                }

                row.Add(infoLabel);
                row.Add(joinBtn);
                _roomList.Add(row);
            }
        }

        private async void OnJoin(string roomIdOrCode)
        {
            if (string.IsNullOrWhiteSpace(roomIdOrCode))
            {
                _errorLabel.text          = "ENTER AN INVITE CODE";
                _errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            _errorLabel.style.display = DisplayStyle.None;

            try
            {
                var room = await Lobby.JoinRoomAsync(roomIdOrCode);
                LobbyState.PendingRoomId = room.roomId;
                UIManager.Instance.GoTo("PreGameLobby");
            }
            catch (Exception ex)
            {
                _errorLabel.text = ex.Message.Contains("401") ? "SESSION EXPIRED — PLEASE LOG IN AGAIN"
                                 : ex.Message.Contains("404") ? "ROOM NOT FOUND"
                                 : ex.Message.Contains("400") ? "ROOM FULL OR ALREADY STARTED"
                                 : "CONNECTION ERROR";
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }
    }
}
