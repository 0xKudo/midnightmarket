using ArmsFair.Auth;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class RegisterScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private TextField     _usernameField;
        private TextField     _emailField;
        private TextField     _passwordField;
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

            _root = docRoot.Q("RegisterScreen");
            if (_root == null) { Debug.LogError("[RegisterScreen] RegisterScreen element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _usernameField = _root.Q<TextField>("UsernameField");
            _emailField    = _root.Q<TextField>("EmailField");
            _passwordField = _root.Q<TextField>("PasswordField");
            _errorLabel    = _root.Q<Label>("ErrorLabel");

            TerminalUI.StyleButton(_root.Q<Button>("RegisterBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("BackBtn"));
            TerminalUI.StyleLabels(_root);

            _root.Q<Button>("RegisterBtn").clicked += OnRegister;
            _root.Q<Button>("BackBtn").clicked     += () => UIManager.Instance.GoTo("Login");

            UIManager.Instance.Register("Register", this);
        }

        public void Show() { if (_root != null) _root.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }

        private async void OnRegister()
        {
            _errorLabel.AddToClassList("hidden");
            var username = _usernameField.value.Trim();
            var email    = _emailField.value.Trim();
            var password = _passwordField.value;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                _errorLabel.text = "ALL FIELDS REQUIRED";
                _errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            _errorLabel.style.display = DisplayStyle.None;
            try
            {
                await AccountManager.Instance.RegisterAsync(username, email, password);
                UIManager.Instance.GoTo("MainMenu");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RegisterScreen] Register failed: {ex.Message}");
                _errorLabel.text = ex.Message.Contains("409") ? "USERNAME OR EMAIL TAKEN"
                                 : ex.Message.Contains("400") ? "INVALID INPUT"
                                 : "CONNECTION ERROR";
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }
    }
}
