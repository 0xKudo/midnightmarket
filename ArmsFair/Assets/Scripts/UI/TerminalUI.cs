using UnityEngine;
using UnityEngine.UIElements;

namespace ArmsFair.UI
{
    public static class TerminalUI
    {
        // Palette
        public static readonly Color TextPrimary  = new Color(0.831f, 0.812f, 0.722f); // rgb(212,207,184)
        public static readonly Color TextMuted    = new Color(0.541f, 0.525f, 0.439f); // rgb(138,134,112)
        public static readonly Color TextDanger   = new Color(0.753f, 0.565f, 0.565f); // rgb(192,144,144)
        public static readonly Color BgButton     = new Color(0.059f, 0.059f, 0.031f); // rgb(15,15,8)
        public static readonly Color BgDark       = new Color(0.051f, 0.051f, 0.051f); // rgb(13,13,13)
        public static readonly Color BorderNormal = new Color(0.353f, 0.353f, 0.227f); // rgb(90,90,58)
        public static readonly Color BorderDanger = new Color(0.353f, 0.165f, 0.165f); // rgb(90,42,42)

        // Hover states
        private static readonly Color HoverBgNormal = new Color(0.831f, 0.812f, 0.722f); // rgb(212,207,184)
        private static readonly Color HoverBgDanger = new Color(0.753f, 0.565f, 0.565f); // rgb(192,144,144)
        private static readonly Color HoverText     = new Color(0.051f, 0.051f, 0.051f); // rgb(13,13,13)

        public static void StyleButton(Button btn)
        {
            if (btn == null) return;
            ApplyButtonBase(btn, TextPrimary, BgButton, BorderNormal);
            RegisterHover(btn, TextPrimary, BgButton, BorderNormal, HoverBgNormal, HoverText, HoverBgNormal);
        }

        public static void StyleDangerButton(Button btn)
        {
            if (btn == null) return;
            ApplyButtonBase(btn, TextDanger, BgButton, BorderDanger);
            RegisterHover(btn, TextDanger, BgButton, BorderDanger, HoverBgDanger, HoverText, HoverBgDanger);
        }

        public static void AddHover(Button btn)
        {
            if (btn == null) return;
            var normalBg     = btn.style.backgroundColor.value;
            var normalText   = btn.style.color.value;
            var normalBorder = btn.style.borderTopColor.value;
            RegisterHover(btn, normalText, normalBg, normalBorder, HoverBgNormal, HoverText, HoverBgNormal);
        }

        public static void StyleLabels(VisualElement root)
        {
            foreach (var label in root.Query<Label>().Build())
            {
                if (label.name == "ErrorLabel") continue;
                if (label.ClassListContains("term-title"))
                    label.style.color = new StyleColor(TextPrimary);
                else if (!label.ClassListContains("term-error"))
                    label.style.color = new StyleColor(TextMuted);
            }
        }

        private static void ApplyButtonBase(Button btn, Color text, Color bg, Color border)
        {
            btn.style.color             = new StyleColor(text);
            btn.style.backgroundColor   = new StyleColor(bg);
            btn.style.borderTopColor    = new StyleColor(border);
            btn.style.borderRightColor  = new StyleColor(border);
            btn.style.borderBottomColor = new StyleColor(border);
            btn.style.borderLeftColor   = new StyleColor(border);
            btn.style.borderTopWidth    = 1;
            btn.style.borderRightWidth  = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth   = 1;
            btn.style.paddingTop        = 8;
            btn.style.paddingBottom     = 8;
            btn.style.paddingLeft       = 12;
            btn.style.paddingRight      = 12;
            btn.style.marginBottom      = 8;
            btn.style.fontSize          = 13;
            btn.style.unityTextAlign    = TextAnchor.MiddleCenter;
            foreach (var child in btn.Children())
                child.style.color = new StyleColor(text);
        }

        private static void RegisterHover(
            Button btn,
            Color normalText, Color normalBg, Color normalBorder,
            Color hoverBg, Color hoverText, Color hoverBorder)
        {
            btn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                btn.style.backgroundColor   = new StyleColor(hoverBg);
                btn.style.borderTopColor    = new StyleColor(hoverBorder);
                btn.style.borderRightColor  = new StyleColor(hoverBorder);
                btn.style.borderBottomColor = new StyleColor(hoverBorder);
                btn.style.borderLeftColor   = new StyleColor(hoverBorder);
                btn.style.color             = new StyleColor(hoverText);
                foreach (var child in btn.Children())
                    child.style.color = new StyleColor(hoverText);
            });

            btn.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                btn.style.backgroundColor   = new StyleColor(normalBg);
                btn.style.borderTopColor    = new StyleColor(normalBorder);
                btn.style.borderRightColor  = new StyleColor(normalBorder);
                btn.style.borderBottomColor = new StyleColor(normalBorder);
                btn.style.borderLeftColor   = new StyleColor(normalBorder);
                btn.style.color             = new StyleColor(normalText);
                foreach (var child in btn.Children())
                    child.style.color = new StyleColor(normalText);
            });
        }
    }
}
