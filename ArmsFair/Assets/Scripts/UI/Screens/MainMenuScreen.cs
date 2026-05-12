using System;
using ArmsFair.Auth;
using ArmsFair.Hosting;
using ArmsFair.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class MainMenuScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Label         _welcomeLabel;
        private Label         _hostStatusLabel;
        private Label         _errorLabel;
        private Button        _createRoomBtn;
        private Button        _joinRoomBtn;
        private Button        _profileBtn;

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

            _root = docRoot.Q("MainMenuScreen");
            if (_root == null) { Debug.LogError("[MainMenuScreen] MainMenuScreen element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _welcomeLabel    = _root.Q<Label>("WelcomeLabel");
            _hostStatusLabel = _root.Q<Label>("HostStatusLabel");
            _errorLabel      = _root.Q<Label>("ErrorLabel");
            _createRoomBtn   = _root.Q<Button>("CreateRoomBtn");
            _joinRoomBtn     = _root.Q<Button>("JoinRoomBtn");
            _profileBtn      = _root.Q<Button>("ProfileBtn");

            TerminalUI.StyleButton(_createRoomBtn);
            TerminalUI.StyleButton(_joinRoomBtn);
            TerminalUI.StyleButton(_profileBtn);
            TerminalUI.StyleDangerButton(_root.Q<Button>("LogoutBtn"));
            TerminalUI.StyleLabels(_root);

            _createRoomBtn.clicked             += OnCreateRoom;
            _joinRoomBtn.clicked               += OnJoinRoom;
            _profileBtn.clicked                += OnProfile;
            _root.Q<Button>("LogoutBtn").clicked += OnLogout;

            UIManager.Instance.Register("MainMenu", this);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;

            _hostStatusLabel.style.display = DisplayStyle.None;
            _errorLabel.style.display      = DisplayStyle.None;
            SetButtonsEnabled(true);

            if (_welcomeLabel != null)
            {
                var name = AccountManager.Instance.LocalPlayer?.Username?.ToUpper() ?? "UNKNOWN";
                _welcomeLabel.text = $"OPERATIVE: {name}";
                _welcomeLabel.style.color = new StyleColor(TerminalUI.TextMuted);
            }
        }

        public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }

        private async void OnCreateRoom()
        {
            SetButtonsEnabled(false);
            _errorLabel.style.display      = DisplayStyle.None;
            _hostStatusLabel.text          = "STARTING SERVER...";
            _hostStatusLabel.style.display = DisplayStyle.Flex;

            try
            {
                var code = await ServerHostManager.Instance.StartAndGetInviteCodeAsync();

                _hostStatusLabel.text = $"SERVER READY  [{code}]";
                Debug.Log($"[MainMenuScreen] Relay code: {code}");

                await System.Threading.Tasks.Task.Delay(1500);
                UIManager.Instance.Push("CreateRoom");
            }
            catch (Exception ex)
            {
                _hostStatusLabel.style.display = DisplayStyle.None;
                _errorLabel.text               = $"FAILED TO START SERVER: {ex.Message}";
                _errorLabel.style.display      = DisplayStyle.Flex;
                SetButtonsEnabled(true);
                Debug.LogError($"[MainMenuScreen] Host failed: {ex}");
            }
        }

        private void OnJoinRoom()
        {
            UIManager.Instance.Push("RoomList");
        }

        private void OnProfile()
        {
            UIManager.Instance.Push("Profile");
        }

        private async void OnLogout()
        {
            await AccountManager.Instance.LogOutAsync();
            UIManager.Instance.GoTo("Login");
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _createRoomBtn.SetEnabled(enabled);
            _joinRoomBtn.SetEnabled(enabled);
            _profileBtn.SetEnabled(enabled);
        }
    }
}
