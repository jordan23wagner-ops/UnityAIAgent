using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    /// <summary>
    /// Public API for showing the unified world tooltip UI from non-WorldInteraction systems.
    /// Safe to call early; lazily creates the tooltip UI if needed.
    /// </summary>
    public static class UnifiedWorldTooltip
    {
        private static readonly Vector2 DefaultOffset = new Vector2(0f, 18f);

        // Minimal override/priority support.
        // When an override source is active (e.g., "Merchant"), Hide() becomes a no-op and Show() forces display.
        private static string s_overrideSource;

        public static void SetOverrideSource(string source, bool active)
        {
            try
            {
                if (active)
                {
                    if (!string.IsNullOrWhiteSpace(source))
                        s_overrideSource = source;
                }
                else
                {
                    if (string.Equals(s_overrideSource, source))
                        s_overrideSource = null;
                }
            }
            catch
            {
                // Never throw
            }
        }

        private static bool HasOverride => !string.IsNullOrWhiteSpace(s_overrideSource);

        public static void Show(string text)
        {
            try
            {
                Show(text, (Vector2)Input.mousePosition + DefaultOffset);
            }
            catch
            {
                // Never throw
            }
        }

        public static void Show(string text, string source)
        {
            try
            {
                Show(text, (Vector2)Input.mousePosition + DefaultOffset, source);
            }
            catch
            {
                // Never throw
            }
        }

        public static void Show(string text, Vector2 screenPos)
        {
            try
            {
                if (HasOverride)
                    WorldHoverHighlighter.ShowExternal(text ?? string.Empty, screenPos, force: true);
                else
                    WorldHoverHighlighter.ShowExternal(text ?? string.Empty, screenPos);
            }
            catch
            {
                // Never throw
            }
        }

        public static void Show(string text, Vector2 screenPos, string source)
        {
            try
            {
                if (HasOverride)
                    WorldHoverHighlighter.ShowExternal(text ?? string.Empty, screenPos, force: true, source: source);
                else
                    WorldHoverHighlighter.ShowExternal(text ?? string.Empty, screenPos, source: source);
            }
            catch
            {
                // Never throw
            }
        }

        public static void Show(string text, Vector2 screenPos, bool force)
        {
            try
            {
                WorldHoverHighlighter.ShowExternal(text ?? string.Empty, screenPos, force || HasOverride);
            }
            catch
            {
                // Never throw
            }
        }

        public static void Show(string text, Vector2 screenPos, bool force, string source)
        {
            try
            {
                WorldHoverHighlighter.ShowExternal(text ?? string.Empty, screenPos, force || HasOverride, source: source);
            }
            catch
            {
                // Never throw
            }
        }

        public static void Hide()
        {
            try
            {
                if (HasOverride)
                    return;

                WorldHoverHighlighter.HideExternal();
            }
            catch
            {
                // Never throw
            }
        }

        public static void Hide(string source)
        {
            try
            {
                if (HasOverride)
                    return;

                WorldHoverHighlighter.HideExternal(source);
            }
            catch
            {
                // Never throw
            }
        }

        public struct TooltipState
        {
            public bool isActive;
            public string currentText;
            public string lastSource;
            public int lastShowFrame;
            public int lastHideFrame;
        }

        public static TooltipState GetState()
        {
            try
            {
                var s = WorldHoverHighlighter.GetState();
                return new TooltipState
                {
                    isActive = s.isActive,
                    currentText = s.currentText,
                    lastSource = s.lastSource,
                    lastShowFrame = s.lastShowFrame,
                    lastHideFrame = s.lastHideFrame,
                };
            }
            catch
            {
                return new TooltipState
                {
                    isActive = false,
                    currentText = string.Empty,
                    lastSource = "Unknown",
                    lastShowFrame = -1,
                    lastHideFrame = -1,
                };
            }
        }
    }
}
