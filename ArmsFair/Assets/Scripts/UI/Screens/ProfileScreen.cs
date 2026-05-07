using System;
using System.Collections.Generic;
using ArmsFair.Auth;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class ProfileScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Label         _usernameLabel;
        private Button        _nationBtn;
        private TextField     _companyNameField;
        private Label         _errorLabel;
        private VisualElement _successModal;

        private VisualElement _choiceModal;
        private TextField     _choiceSearch;
        private ScrollView    _choiceList;

        private string _selectedNation;

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

            _root = docRoot.Q("ProfileScreen");
            if (_root == null) { Debug.LogError("[ProfileScreen] ProfileScreen element not found"); return; }

            _root.style.width  = new StyleLength(Length.Percent(100));
            _root.style.height = new StyleLength(Length.Percent(100));

            _usernameLabel    = _root.Q<Label>("UsernameLabel");
            _nationBtn        = _root.Q<Button>("NationBtn");
            _companyNameField = _root.Q<TextField>("CompanyNameField");
            _errorLabel       = _root.Q<Label>("ErrorLabel");
            _successModal     = _root.Q<VisualElement>("SuccessModal");
            _choiceModal      = _root.Q<VisualElement>("ChoiceModal");
            _choiceSearch     = _root.Q<TextField>("ChoiceSearch");
            _choiceList       = _root.Q<ScrollView>("ChoiceList");

            _nationBtn.clicked += OpenNationModal;
            _root.Q<Button>("SuccessOkBtn").clicked += () => _successModal.style.display = DisplayStyle.None;
            _choiceSearch.RegisterValueChangedCallback(evt => FilterChoices(evt.newValue));

            _root.Q<Button>("SaveBtn").clicked += OnSave;
            _root.Q<Button>("BackBtn").clicked += () => UIManager.Instance.Pop();

            TerminalUI.StyleButton(_nationBtn);
            TerminalUI.StyleButton(_root.Q<Button>("SaveBtn"));
            TerminalUI.StyleButton(_root.Q<Button>("BackBtn"));
            TerminalUI.StyleLabels(_root);

            UIManager.Instance.Register("Profile", this);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display          = DisplayStyle.Flex;
            _errorLabel.style.display    = DisplayStyle.None;
            _successModal.style.display  = DisplayStyle.None;
            _choiceModal.style.display   = DisplayStyle.None;

            var p = AccountManager.Instance.LocalPlayer;
            if (p == null) return;

            _usernameLabel.text = $"OPERATIVE: {p.Username?.ToUpper() ?? "UNKNOWN"}";

            _selectedNation = FindNationEntry(p.HomeNation) ?? NationsList.All[0];
            _nationBtn.text = _selectedNation;

            _companyNameField.SetValueWithoutNotify(p.CompanyName ?? "");
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        private string FindNationEntry(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            return NationsList.All.Find(n => n.StartsWith(iso, StringComparison.OrdinalIgnoreCase));
        }

        private void OpenNationModal()
        {
            _choiceSearch.SetValueWithoutNotify("");
            _choiceSearch.style.color             = new StyleColor(new Color(0.831f, 0.812f, 0.722f));
            _choiceSearch.style.backgroundColor   = new StyleColor(new Color(15f/255f, 15f/255f, 8f/255f));
            _choiceSearch.style.borderTopColor    = new StyleColor(new Color(74f/255f, 74f/255f, 48f/255f));
            _choiceSearch.style.borderBottomColor = _choiceSearch.style.borderTopColor;
            _choiceSearch.style.borderLeftColor   = _choiceSearch.style.borderTopColor;
            _choiceSearch.style.borderRightColor  = _choiceSearch.style.borderTopColor;

            PopulateChoiceList(NationsList.All);
            _choiceList.style.height   = new StyleLength(340f);
            _choiceModal.style.display = DisplayStyle.Flex;
            _choiceSearch.Focus();
        }

        private void FilterChoices(string query)
        {
            var filtered = string.IsNullOrEmpty(query)
                ? NationsList.All
                : NationsList.All.FindAll(n => n.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            PopulateChoiceList(filtered);
        }

        private void PopulateChoiceList(List<string> choices)
        {
            _choiceList.Clear();
            foreach (var choice in choices)
            {
                var c           = choice;
                bool isSelected = c == _selectedNation;
                var textCol     = isSelected ? new Color(0.416f, 0.588f, 0.314f) : new Color(0.831f, 0.812f, 0.722f);
                var bdrCol      = isSelected ? new Color(0.314f, 0.467f, 0.196f) : new Color(74f/255f, 74f/255f, 48f/255f);

                var btn = new Button(() =>
                {
                    _selectedNation         = c;
                    _nationBtn.text         = c;
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
                btn.style.paddingTop        = 7;
                btn.style.paddingBottom     = 7;
                btn.style.paddingLeft       = 10;
                btn.style.paddingRight      = 10;
                btn.style.marginBottom      = 4;
                btn.style.fontSize          = 12;
                btn.style.unityTextAlign    = TextAnchor.MiddleLeft;
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

        private async void OnSave()
        {
            _errorLabel.style.display   = DisplayStyle.None;
            _successLabel.style.display = DisplayStyle.None;

            var companyName = _companyNameField.value.Trim();
            if (string.IsNullOrWhiteSpace(companyName))
            {
                _errorLabel.text          = "BROKERAGE NAME REQUIRED";
                _errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            // Extract ISO code from "USA — United States" → "USA"
            var iso = _selectedNation.Length >= 3 ? _selectedNation[..3] : _selectedNation;

            try
            {
                await AccountManager.Instance.SaveProfileAsync(iso, companyName);
                _successModal.style.display = DisplayStyle.Flex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileScreen] SaveProfile failed: {ex.Message}");
                _errorLabel.text = ex.Message.Contains("401") ? "SESSION EXPIRED — PLEASE LOG IN AGAIN"
                                 : ex.Message.Contains("404") ? "ENDPOINT NOT FOUND (404)"
                                 : ex.Message.Contains("500") ? "SERVER ERROR (500)"
                                 : $"ERROR: {ex.Message}";
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }
    }
}
