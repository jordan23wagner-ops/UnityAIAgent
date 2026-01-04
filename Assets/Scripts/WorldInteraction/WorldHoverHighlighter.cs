using System;
using UnityEngine;
using UnityEngine.UI;

namespace Abyssbound.WorldInteraction
{
    public sealed class WorldHoverHighlighter : MonoBehaviour
    {
        private const bool DEBUG_HOVER = false;
        private const bool DEBUG_EXTERNAL = false;

        [Header("Stability")]
        [SerializeField] private float switchDistanceEpsilon = 0.75f;
        [SerializeField] private float lostTargetGraceSeconds = 0.20f;

        [Header("Visuals")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.6f, 0f);

        [Header("Tooltip UI")]
        [SerializeField] private int tooltipFontSize = 22;
        [SerializeField] private Color tooltipBackground = new Color(0f, 0f, 0f, 0.80f);
        [SerializeField] private Vector2 tooltipPaddingPx = new Vector2(10f, 6f);
        [SerializeField] private Vector2 tooltipScreenOffsetPx = new Vector2(0f, 18f);

        [Header("Debug")]
        [SerializeField] private bool debugTooltipTrace = false;

        private WorldInteractable current;
        private float currentDistance;
        private float lastSeenTime;
        private Vector3 lastHitPoint;

        private MaterialPropertyBlock _mpb;

        private sealed class TooltipUi
        {
            public GameObject root;
            public RectTransform panel;
            public Image background;
            public Text text;
            public Canvas canvas;
        }

        private static TooltipUi s_ui;

        // External tooltip support (for other hover systems like merchants/waypoints).
        private static WorldHoverHighlighter s_primary;
        private static bool s_externalVisible;
        private static string s_externalText;
        private static Vector2 s_externalScreenPos;
        private static bool s_externalForce;

        private static string s_lastSource = "None";
        private static int s_lastShowFrame = -1;
        private static int s_lastHideFrame = -1;

        private static bool s_lastUiActive;
        private static string s_lastUiText;
        private static string s_lastShowSignature;

        private static string s_worldHideReasonOverride;

        private string _lastHoverDebugName;

        public WorldInteractable Current => current;

        public static WorldInteractable CurrentWorldHover => s_primary != null ? s_primary.Current : null;

        private void OnEnable()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (s_primary == null)
                s_primary = this;
        }

        private void Awake()
        {
            if (s_primary == null)
                s_primary = this;

            EnsureLabelCreated();
            SetLabelVisible(false);
        }

        public static void ShowExternal(string text, Vector2 screenPos)
        {
            ShowExternal(text, screenPos, force: false, source: "External");
        }

        public static void ShowExternal(string text, Vector2 screenPos, string source)
        {
            ShowExternal(text, screenPos, force: false, source: source);
        }

        public static void ShowExternal(string text, Vector2 screenPos, bool force)
        {
            ShowExternal(text, screenPos, force, source: "External");
        }

        public static void ShowExternal(string text, Vector2 screenPos, bool force, string source)
        {
            bool activeBefore = s_ui != null && s_ui.root != null && s_ui.root.activeSelf;

            s_externalVisible = true;
            s_externalText = text ?? string.Empty;
            s_externalScreenPos = screenPos;
            s_externalForce = force;

            s_lastSource = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
            s_lastShowFrame = Time.frameCount;

            ApplyExternalState();

            bool activeAfter = s_ui != null && s_ui.root != null && s_ui.root.activeSelf;
            TraceShowIfChanged(s_lastSource, s_externalText, activeBefore, activeAfter);
        }

        public static void HideExternal()
        {
            HideExternal("External");
        }

        public static void HideExternal(string source)
        {
            if (!s_externalVisible)
                return;

            bool activeBefore = s_ui != null && s_ui.root != null && s_ui.root.activeSelf;

            s_lastSource = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
            s_lastHideFrame = Time.frameCount;

            s_externalVisible = false;
            s_externalText = string.Empty;
            s_externalForce = false;

            // Only hide if WorldInteraction is not currently using the tooltip.
            if (s_primary == null || s_primary.Current == null)
            {
                if (s_ui != null && s_ui.root != null)
                    s_ui.root.SetActive(false);
            }

            bool activeAfter = s_ui != null && s_ui.root != null && s_ui.root.activeSelf;
            TraceHideIfChanged(s_lastSource, activeBefore, activeAfter, reason: "externalHide");
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
            string text = string.Empty;
            bool active = false;

            try
            {
                active = s_ui != null && s_ui.root != null && s_ui.root.activeSelf;
                if (active)
                {
                    if (s_ui != null && s_ui.text != null)
                        text = s_ui.text.text;
                }
            }
            catch
            {
                // ignore
            }

            return new TooltipState
            {
                isActive = active,
                currentText = text ?? string.Empty,
                lastSource = s_lastSource ?? "None",
                lastShowFrame = s_lastShowFrame,
                lastHideFrame = s_lastHideFrame,
            };
        }

        public void UpdateHoverCandidate(WorldInteractable candidate, Vector3 hitPoint, float hitDistance, Camera cam)
        {
            bool sawCandidate = candidate != null;
            if (sawCandidate)
            {
                lastSeenTime = Time.unscaledTime;
                lastHitPoint = hitPoint;
            }

            if (current == null)
            {
                if (sawCandidate)
                    SetCurrent(candidate, hitDistance, cam);

                UpdateLabel(cam);
                return;
            }

            if (sawCandidate)
            {
                if (candidate == current)
                {
                    currentDistance = hitDistance;
                }
                else
                {
                    // Switch only if the new target is meaningfully closer.
                    if (hitDistance + switchDistanceEpsilon < currentDistance)
                    {
                        SetCurrent(candidate, hitDistance, cam);
                    }
                }
            }
            else
            {
                // Lost target grace.
                if (Time.unscaledTime - lastSeenTime > lostTargetGraceSeconds)
                {
                    ClearCurrent();
                }
            }

            UpdateLabel(cam);
        }

        private void SetCurrent(WorldInteractable next, float distance, Camera cam)
        {
            if (next == current)
                return;

            ClearCurrent();

            current = next;
            currentDistance = distance;

            if (DEBUG_HOVER)
            {
                var text = current != null ? current.GetHoverText() : string.Empty;
                var name = current != null ? current.name : "(none)";
                if (!string.Equals(_lastHoverDebugName, name, StringComparison.Ordinal))
                {
                    _lastHoverDebugName = name;
                    Debug.Log($"[Hover] hovered={name} text='{text}'", this);
                }
            }

            ApplyHighlight(current, enabled: true);
            EnsureLabelCreated();
            var resolved = ResolveHoverTextDetailed(current, out var provider, out var why);
            if (debugTooltipTrace)
            {
                if (!string.IsNullOrWhiteSpace(resolved))
                    UnityEngine.Debug.Log($"[TooltipTrace] resolved='{resolved}' provider={provider}", this);
                else
                    UnityEngine.Debug.Log($"[TooltipTrace] resolved=<empty> why={why} provider={provider}", this);
            }

            SetLabelText(resolved);
            SetLabelVisible(true);

            UpdateLabel(cam);
        }

        private void ClearCurrent()
        {
            if (current != null)
            {
                ApplyHighlight(current, enabled: false);
            }

            current = null;
            currentDistance = float.PositiveInfinity;

            if (!s_externalVisible)
                s_worldHideReasonOverride = "noHover";

            SetLabelVisible(false);

            // If an external hover system requested the tooltip, show it now.
            ApplyExternalState();
        }

        private void ApplyHighlight(WorldInteractable target, bool enabled)
        {
            if (target == null)
                return;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            var renderers = target.HighlightRenderers;
            if (renderers == null)
                return;

            _mpb.Clear();
            if (enabled)
            {
                _mpb.SetColor("_Color", highlightColor);
                _mpb.SetColor("_BaseColor", highlightColor);
                _mpb.SetColor("_EmissionColor", highlightColor);
            }

            var block = _mpb;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                // Special-case fishing highlight proxies: keep them hidden unless hovered.
                // This avoids always-visible proxy meshes (purple squares) at rest.
                try
                {
                    if (r.gameObject != null && string.Equals(r.gameObject.name, "HighlightProxy", StringComparison.Ordinal))
                        r.enabled = enabled;
                }
                catch { }

                r.SetPropertyBlock(block);
            }
        }

        private void EnsureLabelCreated()
        {
            if (s_ui != null && s_ui.root != null)
                return;

            s_ui = new TooltipUi();

            // Prefer parenting under an existing ScreenSpace canvas so we don't leave stray root objects.
            Transform parent = null;
            try
            {
                var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                if (canvases != null)
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        var c = canvases[i];
                        if (c == null) continue;
                        if (!c.isActiveAndEnabled) continue;
                        if (c.renderMode == RenderMode.WorldSpace) continue;
                        parent = c.transform;
                        break;
                    }
                }
            }
            catch { parent = null; }

            if (parent == null)
            {
                if (s_primary != null)
                    parent = s_primary.transform;
                else
                    parent = transform;
            }

            // Screen-space overlay tooltip so it stays readable on bright backgrounds.
            var root = new GameObject("WorldHoverTooltip");
            s_ui.root = root;
            root.transform.SetParent(parent, worldPositionStays: false);
            root.SetActive(false);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            s_ui.canvas = canvas;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var raycaster = root.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(root.transform, worldPositionStays: false);
            var panel = panelGO.AddComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            s_ui.panel = panel;

            var bg = panelGO.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.color = tooltipBackground;
            s_ui.background = bg;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(panelGO.transform, worldPositionStays: false);
            var tr = textGO.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(tooltipPaddingPx.x, tooltipPaddingPx.y);
            tr.offsetMax = new Vector2(-tooltipPaddingPx.x, -tooltipPaddingPx.y);

            var uiText = textGO.AddComponent<Text>();
            uiText.raycastTarget = false;
            uiText.fontSize = Mathf.Clamp(tooltipFontSize, 20, 24);
            uiText.fontStyle = FontStyle.Bold;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = Color.white;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.text = string.Empty;
            s_ui.text = uiText;

            // Contrast on bright backgrounds.
            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Unity 6: Arial.ttf is no longer a valid built-in font.
            try
            {
                uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                uiText.font = null;
            }
        }

        private void SetLabelText(string text)
        {
            if (s_ui == null || s_ui.text == null)
                return;

            s_ui.text.text = text ?? string.Empty;

            // Resize panel to fit.
            try
            {
                if (s_ui.panel != null && s_ui.text != null)
                {
                    var w = s_ui.text.preferredWidth + tooltipPaddingPx.x * 2f;
                    var h = s_ui.text.preferredHeight + tooltipPaddingPx.y * 2f;
                    s_ui.panel.sizeDelta = new Vector2(Mathf.Clamp(w, 60f, 520f), Mathf.Clamp(h, 24f, 200f));
                }
            }
            catch { }
        }

        private void SetLabelVisible(bool visible)
        {
            if (s_ui != null && s_ui.root != null)
            {
                bool before = s_ui.root.activeSelf;
                if (before == visible)
                    return;

                s_ui.root.SetActive(visible);

                bool after = s_ui.root.activeSelf;
                if (visible)
                {
                    s_lastSource = "WorldHover";
                    s_lastShowFrame = Time.frameCount;
                    TraceShowIfChanged("WorldHover", s_ui.text != null ? s_ui.text.text : string.Empty, before, after);
                    if (IsDebugTooltipTraceEnabled())
                        UnityEngine.Debug.Log($"[TooltipTrace] WORLD_HOVER_SHOW text='{(s_ui.text != null ? s_ui.text.text : string.Empty)}'", s_primary);
                }
                else
                {
                    s_lastSource = "WorldHover";
                    s_lastHideFrame = Time.frameCount;
                    var reason = string.IsNullOrWhiteSpace(s_worldHideReasonOverride) ? "setInvisible" : s_worldHideReasonOverride;
                    s_worldHideReasonOverride = null;
                    TraceHideIfChanged("WorldHover", before, after, reason: reason);
                    if (IsDebugTooltipTraceEnabled())
                        UnityEngine.Debug.Log($"[TooltipTrace] WORLD_HOVER_HIDE reason={reason}", s_primary);
                }
            }
        }

        private void UpdateLabel(Camera cam)
        {
            if (s_ui == null || s_ui.root == null || !s_ui.root.activeSelf)
                return;

            // External systems (e.g. merchant hover) may need to override a stale WorldInteractable hover
            // during grace periods. Only enabled when explicitly forced.
            if (s_externalVisible && s_externalForce)
            {
                SetLabelText(s_externalText);
                if (s_ui.panel != null)
                    s_ui.panel.position = s_externalScreenPos;
                return;
            }

            if (current == null)
            {
                // When no WorldInteractable is hovered, the tooltip may still be owned by an external system.
                if (s_externalVisible)
                {
                    SetLabelText(s_externalText);
                    s_ui.panel.position = s_externalScreenPos;
                }

                return;
            }

            SetLabelText(ResolveHoverTextDetailed(current, out _, out _));

            if (cam == null)
                cam = Camera.main;

            if (cam == null || s_ui.panel == null)
                return;

            // Prefer the actual raycast hit point for tooltip placement.
            // This is critical for grouped interactables (e.g. [FishingSpots]) where the interactable
            // lives on a parent but the collider we hover is on a baked child object.
            var world = lastHitPoint;
            bool hasHitPoint = world != default;
            if (!hasHitPoint)
            {
                var bounds = current.GetHoverBounds();
                world = bounds.center;
                world.y = bounds.max.y;
            }

            world += labelOffset;

            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.01f)
            {
                s_worldHideReasonOverride = "behindCamera";
                SetLabelVisible(false);
                return;
            }

            var screenPos = new Vector2(sp.x + tooltipScreenOffsetPx.x, sp.y + tooltipScreenOffsetPx.y);
            s_ui.panel.position = screenPos;
        }

        private static void ApplyExternalState()
        {
            // Do not override WorldInteraction hover.
            if (!s_externalForce && s_primary != null && s_primary.Current != null)
                return;

            if (!s_externalVisible)
                return;

            // Ensure UI exists.
            if (s_ui == null || s_ui.root == null)
            {
                try
                {
                    var any = s_primary;
                    if (any == null)
                        any = FindFirstObjectByType<WorldHoverHighlighter>();
                    if (any != null)
                        any.EnsureLabelCreated();
                }
                catch { }
            }

            // Last resort: create UI even if no WorldHoverHighlighter instance exists.
            if (s_ui == null || s_ui.root == null)
            {
                try
                {
                    s_ui = new TooltipUi();

                    Transform parent = null;
                    try
                    {
                        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                        if (canvases != null)
                        {
                            for (int i = 0; i < canvases.Length; i++)
                            {
                                var c = canvases[i];
                                if (c == null) continue;
                                if (!c.isActiveAndEnabled) continue;
                                if (c.renderMode == RenderMode.WorldSpace) continue;
                                parent = c.transform;
                                break;
                            }
                        }
                    }
                    catch { parent = null; }

                    var root = new GameObject("WorldHoverTooltip");
                    s_ui.root = root;
                    root.transform.SetParent(parent, worldPositionStays: false);
                    root.SetActive(false);

                    var canvas = root.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 5000;
                    s_ui.canvas = canvas;

                    var scaler = root.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                    var raycaster = root.AddComponent<GraphicRaycaster>();
                    raycaster.enabled = false;

                    var panelGO = new GameObject("Panel");
                    panelGO.transform.SetParent(root.transform, worldPositionStays: false);
                    var panel = panelGO.AddComponent<RectTransform>();
                    panel.anchorMin = new Vector2(0f, 0f);
                    panel.anchorMax = new Vector2(0f, 0f);
                    panel.pivot = new Vector2(0.5f, 0f);
                    s_ui.panel = panel;

                    var bg = panelGO.AddComponent<Image>();
                    bg.raycastTarget = false;
                    bg.color = new Color(0f, 0f, 0f, 0.80f);
                    s_ui.background = bg;

                    var textGO = new GameObject("Text");
                    textGO.transform.SetParent(panelGO.transform, worldPositionStays: false);
                    var tr = textGO.AddComponent<RectTransform>();
                    tr.anchorMin = Vector2.zero;
                    tr.anchorMax = Vector2.one;
                    tr.offsetMin = new Vector2(10f, 6f);
                    tr.offsetMax = new Vector2(-10f, -6f);

                    var uiText = textGO.AddComponent<Text>();
                    uiText.raycastTarget = false;
                    uiText.fontSize = 22;
                    uiText.fontStyle = FontStyle.Bold;
                    uiText.alignment = TextAnchor.MiddleCenter;
                    uiText.color = Color.white;
                    uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    uiText.verticalOverflow = VerticalWrapMode.Overflow;
                    uiText.text = string.Empty;
                    s_ui.text = uiText;

                    var outline = textGO.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
                    outline.effectDistance = new Vector2(1f, -1f);

                    try { uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { uiText.font = null; }
                }
                catch { }
            }

            if (s_ui == null || s_ui.root == null)
                return;

            bool before = s_ui.root.activeSelf;
            s_ui.root.SetActive(true);
            if (s_ui.text != null)
                s_ui.text.text = s_externalText ?? string.Empty;
            if (s_ui.panel != null)
                s_ui.panel.position = s_externalScreenPos;

            bool after = s_ui.root.activeSelf;
            TraceShowIfChanged(s_lastSource, s_externalText, before, after);
        }

        private static bool IsDebugTooltipTraceEnabled()
        {
            try { return s_primary != null && s_primary.debugTooltipTrace; }
            catch { return false; }
        }

        private static void TraceShowIfChanged(string source, string text, bool activeBefore, bool activeAfter)
        {
            if (!IsDebugTooltipTraceEnabled())
                return;

            string sig = $"SHOW|{source}|{activeBefore}->{activeAfter}|{text}";
            if (string.Equals(s_lastShowSignature, sig, StringComparison.Ordinal))
                return;

            s_lastShowSignature = sig;
            UnityEngine.Debug.Log($"[TooltipTrace] SHOW src={source} text='{text}' activeBefore={activeBefore} activeAfter={activeAfter}", s_primary);
        }

        private static void TraceHideIfChanged(string source, bool activeBefore, bool activeAfter, string reason)
        {
            if (!IsDebugTooltipTraceEnabled())
                return;

            string sig = $"HIDE|{source}|{activeBefore}->{activeAfter}|{reason}";
            if (string.Equals(s_lastShowSignature, sig, StringComparison.Ordinal))
                return;

            s_lastShowSignature = sig;
            UnityEngine.Debug.Log($"[TooltipTrace] HIDE src={source} activeBefore={activeBefore} activeAfter={activeAfter} reason={reason}", s_primary);
        }

        // Tooltip comes from WorldInteractable.GetHoverText(), falling back to DisplayName, then GameObject name.
        private static string ResolveHoverTextDetailed(WorldInteractable hovered, out string provider, out string why)
        {
            provider = "<none>";
            why = "unknown";

            if (hovered == null)
            {
                why = "noHoveredInteractable";
                return string.Empty;
            }

            try { provider = hovered.GetType().Name; } catch { provider = "<error>"; }

            string text = null;
            try { text = hovered.GetHoverText(); }
            catch { text = null; }

            if (!string.IsNullOrWhiteSpace(text))
            {
                why = "GetHoverText";
                return text;
            }

            if (text == null)
                why = "GetHoverText<threwOrNull>";
            else
                why = "GetHoverText<empty>";

            if (string.IsNullOrWhiteSpace(text))
            {
                try { text = hovered.DisplayName; }
                catch { text = null; }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    why = "DisplayName";
                    return text;
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                try { text = hovered.gameObject != null ? hovered.gameObject.name : hovered.name; }
                catch { text = hovered.name; }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    why = "GameObjectName";
                    return text;
                }
            }

            why = "allSourcesEmpty";
            return string.Empty;
        }
    }
}
