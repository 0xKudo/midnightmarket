using ArmsFair.Auth;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class MainMenuScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Label         _welcomeLabel;

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

            _welcomeLabel = _root.Q<Label>("WelcomeLabel");

            TerminalUI.StyleButton(_root.Q<Button>("CreateRoomBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("JoinRoomBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("ProfileBtn"));
            TerminalUI.StyleDangerButton(_root.Q<Button>("LogoutBtn"));
            TerminalUI.StyleLabels(_root);

            _root.Q<Button>("CreateRoomBtn").clicked += OnCreateRoom;
            _root.Q<Button>("JoinRoomBtn").clicked   += OnJoinRoom;
            _root.Q<Button>("ProfileBtn").clicked    += OnProfile;
            _root.Q<Button>("LogoutBtn").clicked     += OnLogout;

            UIManager.Instance.Register("MainMenu", this);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
            if (_welcomeLabel != null)
            {
                var name = AccountManager.Instance.LocalPlayer?.Username?.ToUpper() ?? "UNKNOWN";
                _welcomeLabel.text = $"OPERATIVE: {name}";
                _welcomeLabel.style.color = new StyleColor(TerminalUI.TextMuted);
            }
        }

        public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }

        private void OnCreateRoom()
        {
            // TODO Phase 8: UIManager.Instance.GoTo("CreateRoom");
            Debug.LogWarning("[MainMenuScreen] CreateRoom screen not yet built (Phase 8)");
        }

        private void OnJoinRoom()
        {
            // TODO Phase 9: UIManager.Instance.GoTo("RoomList");
            Debug.LogWarning("[MainMenuScreen] RoomList screen not yet built (Phase 9)");
        }

        private void OnProfile()
        {
            // TODO Phase 10: UIManager.Instance.Push("Profile");
            Debug.LogWarning("[MainMenuScreen] Profile screen not yet built (Phase 10)");
        }

        private async void OnLogout()
        {
            await AccountManager.Instance.LogOutAsync();
            UIManager.Instance.GoTo("Login");
        }
    }
}
