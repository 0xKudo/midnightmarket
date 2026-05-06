using ArmsFair.Auth;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class LoginScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private TextField     _usernameField;
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

            _root = docRoot.Q("LoginScreen");
            if (_root == null) { Debug.LogError("[LoginScreen] LoginScreen element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _usernameField = _root.Q<TextField>("UsernameField");
            _passwordField = _root.Q<TextField>("PasswordField");
            _errorLabel    = _root.Q<Label>("ErrorLabel");

            TerminalUI.StyleButton(_root.Q<Button>("LoginBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("RegisterBtn"));
            TerminalUI.StyleLabels(_root);

            _root.Q<Button>("LoginBtn").clicked    += OnLogin;
            _root.Q<Button>("RegisterBtn").clicked += () => UIManager.Instance.GoTo("Register");

            UIManager.Instance.Register("Login", this);
        }

        public void Show() { if (_root != null) _root.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }

        private async void OnLogin()
        {
            _errorLabel.AddToClassList("hidden");
            try
            {
                await AccountManager.Instance.LoginAsync(_usernameField.value, _passwordField.value);
                UIManager.Instance.GoTo("MainMenu");
            }
            catch (System.Exception ex)
            {
                _errorLabel.text = ex.Message.Contains("401") ? "INVALID CREDENTIALS" : "CONNECTION ERROR";
                _errorLabel.RemoveFromClassList("hidden");
            }
        }
    }
}
