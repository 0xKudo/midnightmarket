using ArmsFair.Auth;
using ArmsFair.Update;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class MainMenuScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Label         _welcomeLabel;

        // Update banner elements
        private VisualElement _updateBanner;
        private Label         _updateVersionLabel;
        private Label         _updateNotesLabel;
        private VisualElement _updateProgressTrack;
        private VisualElement _updateProgressFill;
        private Label         _updateStatusLabel;
        private Button        _updateActionBtn;
        private Button        _updateDismissBtn;

        private bool _downloadFailed;

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

            _root.Q<Button>("CreateRoomBtn").clicked += () => UIManager.Instance.Push("CreateRoom");
            _root.Q<Button>("JoinRoomBtn").clicked   += () => UIManager.Instance.Push("RoomList");
            _root.Q<Button>("ProfileBtn").clicked    += () => UIManager.Instance.Push("Profile");
            _root.Q<Button>("LogoutBtn").clicked     += OnLogout;

            // Update banner
            _updateBanner        = _root.Q<VisualElement>("UpdateBanner");
            _updateVersionLabel  = _root.Q<Label>("UpdateVersionLabel");
            _updateNotesLabel    = _root.Q<Label>("UpdateNotesLabel");
            _updateProgressTrack = _root.Q<VisualElement>("UpdateProgressTrack");
            _updateProgressFill  = _root.Q<VisualElement>("UpdateProgressFill");
            _updateStatusLabel   = _root.Q<Label>("UpdateStatusLabel");
            _updateActionBtn     = _root.Q<Button>("UpdateActionBtn");
            _updateDismissBtn    = _root.Q<Button>("UpdateDismissBtn");

            if (_updateActionBtn  != null) _updateActionBtn.clicked  += OnUpdateAction;
            if (_updateDismissBtn != null) _updateDismissBtn.clicked += OnDismiss;

            UIManager.Instance.Register("MainMenu", this);
        }

        private void OnEnable()
        {
            UpdateChecker.OnUpdateAvailable  += HandleUpdateAvailable;
            UpdateChecker.OnDownloadProgress += HandleDownloadProgress;
            UpdateChecker.OnDownloadComplete += HandleDownloadComplete;
            UpdateChecker.OnCheckFailed      += HandleDownloadFailed;

            // If check already completed before this screen enabled
            if (UpdateChecker.Instance != null &&
                UpdateChecker.Instance.State == UpdateState.UpdateAvailable &&
                UpdateChecker.Instance.LatestRelease.HasValue)
            {
                HandleUpdateAvailable(UpdateChecker.Instance.LatestRelease.Value);
            }
        }

        private void OnDisable()
        {
            UpdateChecker.OnUpdateAvailable  -= HandleUpdateAvailable;
            UpdateChecker.OnDownloadProgress -= HandleDownloadProgress;
            UpdateChecker.OnDownloadComplete -= HandleDownloadComplete;
            UpdateChecker.OnCheckFailed      -= HandleDownloadFailed;
        }

        private void HandleUpdateAvailable(ReleaseInfo info)
        {
            if (_updateBanner == null) return;
            _downloadFailed = false;

            if (_updateVersionLabel != null)
                _updateVersionLabel.text = $"UPDATE AVAILABLE — v{info.Version}";
            if (_updateNotesLabel != null)
                _updateNotesLabel.text = info.ReleaseNotes;

            SetBannerToAvailableState(info.HasDirectDownload);
            _updateBanner.style.display = DisplayStyle.Flex;
        }

        private void SetBannerToAvailableState(bool hasDirectDownload)
        {
            if (_updateProgressTrack != null) _updateProgressTrack.style.display = DisplayStyle.None;
            if (_updateStatusLabel   != null) _updateStatusLabel.style.display   = DisplayStyle.None;
            if (_updateActionBtn     != null)
            {
                _updateActionBtn.text          = hasDirectDownload ? "DOWNLOAD" : "OPEN GITHUB";
                _updateActionBtn.style.display = DisplayStyle.Flex;
                _updateActionBtn.SetEnabled(true);
            }
        }

        private void HandleDownloadProgress(float progress)
        {
            if (_updateProgressTrack == null) return;
            _updateProgressTrack.style.display = DisplayStyle.Flex;

            float pct = progress * 100f;
            if (_updateProgressFill != null)
                _updateProgressFill.style.width = new StyleLength(Length.Percent(pct));
            if (_updateStatusLabel != null)
            {
                _updateStatusLabel.text          = $"DOWNLOADING... {Mathf.RoundToInt(pct)}%";
                _updateStatusLabel.style.display = DisplayStyle.Flex;
            }
            if (_updateActionBtn != null)
            {
                _updateActionBtn.text          = "CANCEL";
                _updateActionBtn.style.display = DisplayStyle.Flex;
            }
            if (_updateNotesLabel != null)
                _updateNotesLabel.style.display = DisplayStyle.None;
        }

        private void HandleDownloadComplete(string path)
        {
            if (_updateStatusLabel != null)
            {
                _updateStatusLabel.text          = "LAUNCHING INSTALLER...";
                _updateStatusLabel.style.display = DisplayStyle.Flex;
            }
            if (_updateActionBtn != null)
                _updateActionBtn.style.display = DisplayStyle.None;

            UpdateChecker.Instance?.LaunchInstaller();
        }

        private void HandleDownloadFailed(string error)
        {
            // OnCheckFailed fires for both API errors and download errors.
            // Only treat as a download failure if we were back in UpdateAvailable state.
            if (UpdateChecker.Instance?.State != UpdateState.UpdateAvailable) return;

            _downloadFailed = true;
            if (_updateStatusLabel != null)
            {
                _updateStatusLabel.text          = "DOWNLOAD FAILED";
                _updateStatusLabel.style.display = DisplayStyle.Flex;
            }
            if (_updateProgressTrack != null)
                _updateProgressTrack.style.display = DisplayStyle.None;
            if (_updateActionBtn != null)
            {
                _updateActionBtn.text          = "RETRY";
                _updateActionBtn.style.display = DisplayStyle.Flex;
                _updateActionBtn.SetEnabled(true);
            }
            if (_updateNotesLabel != null)
                _updateNotesLabel.style.display = DisplayStyle.Flex;
        }

        private void OnUpdateAction()
        {
            if (UpdateChecker.Instance == null || _updateBanner == null) return;

            var state = UpdateChecker.Instance.State;
            var info  = UpdateChecker.Instance.LatestRelease;

            if (state == UpdateState.Downloading)
            {
                UpdateChecker.Instance.CancelDownload();
                if (info.HasValue)
                    SetBannerToAvailableState(info.Value.HasDirectDownload);
                if (_updateStatusLabel != null)
                    _updateStatusLabel.style.display = DisplayStyle.None;
                if (_updateNotesLabel != null)
                    _updateNotesLabel.style.display = DisplayStyle.Flex;
                return;
            }

            if (state == UpdateState.UpdateAvailable && info.HasValue)
            {
                if (info.Value.HasDirectDownload)
                {
                    _updateActionBtn.SetEnabled(false);
                    UpdateChecker.Instance.StartDownload();
                }
                else
                {
                    Application.OpenURL(info.Value.ReleasePageUrl);
                }
            }
        }

        private void OnDismiss()
        {
            if (_updateBanner == null) return;
            UpdateChecker.Instance?.CancelDownload();
            _updateBanner.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;

            if (_welcomeLabel != null)
            {
                var name = AccountManager.Instance.LocalPlayer?.Username?.ToUpper() ?? "UNKNOWN";
                _welcomeLabel.text        = $"OPERATIVE: {name}";
                _welcomeLabel.style.color = new StyleColor(TerminalUI.TextMuted);
            }
        }

        public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }

        private async void OnLogout()
        {
            await AccountManager.Instance.LogOutAsync();
            UIManager.Instance.GoTo("Login");
        }
    }
}
