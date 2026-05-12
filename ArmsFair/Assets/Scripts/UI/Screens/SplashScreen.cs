using System;
using System.Collections;
using System.Threading;
using ArmsFair.Hosting;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class SplashScreen : MonoBehaviour, IScreen
    {
        private VisualElement _root;
        private Label         _statusLabel;
        private Label         _percentLabel;
        private VisualElement _barFill;
        private Button        _cancelBtn;

        private float  _displayProgress = 0f;
        private bool   _serverReady     = false;
        private bool   _cancelled       = false;
        private string _statusText      = "INITIALIZING...";

        private CancellationTokenSource _cts;

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

            _root         = docRoot.Q("SplashScreen");
            _statusLabel  = _root.Q<Label>("StatusLabel");
            _percentLabel = _root.Q<Label>("PercentLabel");
            _barFill      = _root.Q<VisualElement>("LoadingBarFill");
            _cancelBtn    = _root.Q<Button>("CancelBtn");

            _cancelBtn.clicked += OnCancel;

            UIManager.Instance.Register("Splash", this);
        }

        private void Start()
        {
            StartCoroutine(RunStartup());
        }

        public void Show() { if (_root != null) _root.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_root != null) _root.style.display = DisplayStyle.None; }

        private IEnumerator RunStartup()
        {
            _serverReady     = false;
            _cancelled       = false;
            _displayProgress = 0f;
            _statusText      = "INITIALIZING...";

            _cancelBtn.style.display = DisplayStyle.None;
            UpdateBar();

            // Show cancel after 3 seconds so it doesn't flash immediately
            yield return new WaitForSeconds(3f);
            _cancelBtn.style.display = DisplayStyle.Flex;

            LaunchServerAsync();

            // Animate bar easing toward 0.85 while server starts
            while (!_serverReady && !_cancelled)
            {
                _displayProgress = Mathf.MoveTowards(_displayProgress, 0.85f, Time.deltaTime * 0.12f);
                if (_statusLabel != null) _statusLabel.text = _statusText;
                UpdateBar();
                yield return null;
            }

            if (_cancelled)
            {
                _cancelBtn.style.display = DisplayStyle.None;
                _statusLabel.text        = "CANCELLED — PRESS RETRY TO TRY AGAIN";
                _cancelBtn.text          = "RETRY";
                _cancelBtn.style.display = DisplayStyle.Flex;
                _cancelBtn.clicked      -= OnCancel;
                _cancelBtn.clicked      += OnRetry;
                yield break;
            }

            // Success — race to 100%
            _cancelBtn.style.display = DisplayStyle.None;
            _statusLabel.text        = "SERVER READY";
            while (_displayProgress < 1f)
            {
                _displayProgress = Mathf.MoveTowards(_displayProgress, 1f, Time.deltaTime * 1.5f);
                UpdateBar();
                yield return null;
            }

            SetProgress(1f, "AUTHENTICATING...");
            yield return new WaitForSeconds(0.3f);

            UIManager.Instance.GoTo("Login");
        }

        private async void LaunchServerAsync()
        {
            _cts = new CancellationTokenSource();
            try
            {
                await ServerHostManager.Instance.StartAndGetInviteCodeAsync(
                    _cts.Token,
                    status => _statusText = status);
                _serverReady = true;
            }
            catch (OperationCanceledException)
            {
                _cancelled = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SplashScreen] Server launch failed: {ex.Message}");
                _statusText = $"ERROR: {(ex.Message.Length > 50 ? ex.Message[..50] + "..." : ex.Message)}";
                _cancelled  = true;
            }
        }

        private void OnCancel()
        {
            _cts?.Cancel();
            ServerHostManager.Instance.StopServer();
        }

        private void OnRetry()
        {
            _cancelBtn.clicked -= OnRetry;
            _cancelBtn.clicked += OnCancel;
            StartCoroutine(RunStartup());
        }

        private void SetProgress(float t, string status)
        {
            _displayProgress = t;
            UpdateBar();
            if (_statusLabel != null) _statusLabel.text = status;
        }

        private void UpdateBar()
        {
            float pct = Mathf.Clamp01(_displayProgress) * 100f;
            if (_barFill      != null) _barFill.style.width = new StyleLength(Length.Percent(pct));
            if (_percentLabel != null) _percentLabel.text   = $"{Mathf.RoundToInt(pct)}%";
        }
    }
}
