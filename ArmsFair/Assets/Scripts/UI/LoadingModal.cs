using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public class LoadingModal
    {
        private readonly VisualElement _root;
        private VisualElement _overlay;
        private Label _statusLabel;
        private Button _retryBtn;
        private VisualElement _btnRow;
        private IVisualElementScheduledItem _dotsJob;
        private IVisualElementScheduledItem _timeoutJob;
        private int _dotCount;
        private string _baseText;
        private Action _onCancel;
        private Action _onRetry;

        public LoadingModal(VisualElement root)
        {
            _root = root;
        }

        public void Show(string message, Action onCancel = null, Action onRetry = null)
        {
            if (_overlay != null) Hide();

            _baseText = message;
            _onCancel = onCancel;
            _onRetry  = onRetry;
            _dotCount = 0;

            Build();
            _root.Add(_overlay);

            _dotsJob = _overlay.schedule.Execute(TickDots);
            _dotsJob.Every(400);
            _timeoutJob = _overlay.schedule.Execute(ShowTimeout);
            _timeoutJob.ExecuteLater(20000);
        }

        public void Hide()
        {
            _dotsJob?.Pause();
            _timeoutJob?.Pause();
            if (_overlay != null && _overlay.parent != null)
                _root.Remove(_overlay);
            _overlay = null;
        }

        private void Build()
        {
            _overlay = new VisualElement();
            _overlay.style.position        = Position.Absolute;
            _overlay.style.top             = _overlay.style.left   = 0;
            _overlay.style.right           = _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.78f));
            _overlay.style.alignItems      = Align.Center;
            _overlay.style.justifyContent  = Justify.Center;
            _overlay.style.flexDirection   = FlexDirection.Column;
            _overlay.pickingMode           = PickingMode.Position;
            _overlay.RegisterCallback<PointerDownEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);
            _overlay.RegisterCallback<WheelEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);

            var box = new VisualElement();
            box.style.backgroundColor   = new StyleColor(new Color(0.051f, 0.051f, 0.031f, 0.97f));
            box.style.borderTopColor    = box.style.borderRightColor  =
            box.style.borderBottomColor = box.style.borderLeftColor   = new StyleColor(TerminalUI.BorderNormal);
            box.style.borderTopWidth    = box.style.borderRightWidth  =
            box.style.borderBottomWidth = box.style.borderLeftWidth   = 1;
            box.style.paddingTop        = box.style.paddingBottom = 32;
            box.style.paddingLeft       = box.style.paddingRight  = 48;
            box.style.alignItems        = Align.Center;
            box.style.minWidth          = 280;

            _statusLabel = new Label(_baseText + ".");
            _statusLabel.style.fontSize       = 18;
            _statusLabel.style.color          = new StyleColor(TerminalUI.TextPrimary);
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _statusLabel.style.marginBottom   = 0;

            _btnRow = new VisualElement();
            _btnRow.style.flexDirection = FlexDirection.Row;
            _btnRow.style.marginTop     = 20;
            _btnRow.style.display       = DisplayStyle.None;

            var cancelBtn = new Button(() => { Hide(); _onCancel?.Invoke(); }) { text = "CANCEL" };
            TerminalUI.StyleButton(cancelBtn);
            cancelBtn.style.marginBottom = 0;
            cancelBtn.style.marginRight  = 8;

            _retryBtn = new Button(() => { Hide(); _onRetry?.Invoke(); }) { text = "RETRY" };
            TerminalUI.StyleButton(_retryBtn);
            _retryBtn.style.marginBottom = 0;
            _retryBtn.style.display      = _onRetry != null ? DisplayStyle.Flex : DisplayStyle.None;

            _btnRow.Add(cancelBtn);
            _btnRow.Add(_retryBtn);

            box.Add(_statusLabel);
            box.Add(_btnRow);
            _overlay.Add(box);
        }

        private void TickDots()
        {
            if (_statusLabel == null) return;
            _dotCount = (_dotCount % 3) + 1;
            _statusLabel.text = _baseText + new string('.', _dotCount);
        }

        private void ShowTimeout()
        {
            if (_statusLabel == null) return;
            _statusLabel.text     = "Taking longer than expected...";
            _btnRow.style.display = DisplayStyle.Flex;
        }
    }
}
