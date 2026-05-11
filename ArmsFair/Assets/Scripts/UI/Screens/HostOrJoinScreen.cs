using System;
using ArmsFair.Hosting;
using ArmsFair.Network;  // NetworkConfig
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class HostOrJoinScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Button        _hostBtn;
        private Button        _joinBtn;
        private Label         _hostStatusLabel;
        private Label         _inviteCodeDisplay;
        private TextField     _inviteCodeField;
        private Label         _errorLabel;

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

            _root = docRoot.Q("HostOrJoinScreen");
            if (_root == null) { Debug.LogError("[HostOrJoinScreen] Root element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _hostBtn           = _root.Q<Button>("HostBtn");
            _joinBtn           = _root.Q<Button>("JoinBtn");
            _hostStatusLabel   = _root.Q<Label>("HostStatusLabel");
            _inviteCodeDisplay = _root.Q<Label>("InviteCodeDisplay");
            _inviteCodeField   = _root.Q<TextField>("InviteCodeField");
            _errorLabel        = _root.Q<Label>("ErrorLabel");

            TerminalUI.StyleButton(_hostBtn);
            TerminalUI.StyleButton(_joinBtn);
            TerminalUI.StyleLabels(_root);

            _hostBtn.clicked += OnHost;
            _joinBtn.clicked += OnJoin;

            UIManager.Instance.Register("HostOrJoin", this);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display            = DisplayStyle.Flex;
            _errorLabel.style.display      = DisplayStyle.None;
            _hostStatusLabel.style.display = DisplayStyle.None;
            _inviteCodeDisplay.style.display = DisplayStyle.None;
            _inviteCodeField.SetValueWithoutNotify("");

            // Reset state from any previous session
            NetworkConfig.RelayCode = "";
            NetworkConfig.IsHost    = false;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        // ── Host ─────────────────────────────────────────────────────────────

        private async void OnHost()
        {
            _hostBtn.SetEnabled(false);
            _joinBtn.SetEnabled(false);
            _errorLabel.style.display        = DisplayStyle.None;
            _hostStatusLabel.text            = "STARTING SERVER...";
            _hostStatusLabel.style.display   = DisplayStyle.Flex;
            _inviteCodeDisplay.style.display = DisplayStyle.None;

            try
            {
                var code = await ServerHostManager.Instance.StartAndGetInviteCodeAsync();

                _hostStatusLabel.text          = "SERVER READY — SHARE THIS CODE:";
                _inviteCodeDisplay.text        = code;
                _inviteCodeDisplay.style.display = DisplayStyle.Flex;

                // Give the host a moment to see the code, then proceed to login
                await System.Threading.Tasks.Task.Delay(1500);
                UIManager.Instance.GoTo("Login");
            }
            catch (Exception ex)
            {
                _hostStatusLabel.style.display = DisplayStyle.None;
                _errorLabel.text               = $"FAILED TO START SERVER: {ex.Message}";
                _errorLabel.style.display      = DisplayStyle.Flex;
                _hostBtn.SetEnabled(true);
                _joinBtn.SetEnabled(true);
                Debug.LogError($"[HostOrJoinScreen] Host failed: {ex}");
            }
        }

        // ── Join ─────────────────────────────────────────────────────────────

        private async void OnJoin()
        {
            var code = _inviteCodeField.value.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                _errorLabel.text          = "ENTER AN INVITE CODE";
                _errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            _hostBtn.SetEnabled(false);
            _joinBtn.SetEnabled(false);
            _errorLabel.style.display      = DisplayStyle.None;
            _hostStatusLabel.text          = "CONNECTING...";
            _hostStatusLabel.style.display = DisplayStyle.Flex;

            // Route all traffic through VPS relay — no IP lookup, no port forwarding needed
            var relayBase = $"https://armsfair.laynekudo.com/relay/{code}";

            try
            {
                // Quick health check to verify the host is connected to the relay
                var req = UnityEngine.Networking.UnityWebRequest.Get($"{relayBase}/health");
                req.timeout = 8;
                await req.SendWebRequest();

                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    throw new Exception(req.responseCode == 503
                        ? "INVITE CODE NOT FOUND OR HOST OFFLINE"
                        : $"CONNECTION ERROR: {req.error}");
                }

                NetworkConfig.ServerBaseUrl = relayBase;
                NetworkConfig.IsHost        = false;

                Debug.Log($"[HostOrJoinScreen] Joining via relay: {relayBase}");
                UIManager.Instance.GoTo("Login");
            }
            catch (Exception ex)
            {
                _hostStatusLabel.style.display = DisplayStyle.None;
                _errorLabel.text               = ex.Message;
                _errorLabel.style.display      = DisplayStyle.Flex;
                _hostBtn.SetEnabled(true);
                _joinBtn.SetEnabled(true);
            }
        }
    }
}
