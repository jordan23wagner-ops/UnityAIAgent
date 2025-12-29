using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abyssbound.Threat
{
    [DisallowMultipleComponent]
    public sealed class ThreatHUD : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Image[] skullBackgrounds = new Image[5];
        [SerializeField] private Image[] skullFills = new Image[5];
        [SerializeField] private TMP_Text threatValueText;

        [Header("Visual")]
        public Sprite skullSprite;

        [SerializeField] private Image backgroundPanel;

        [Header("Options")]
        [SerializeField] private bool showNumericThreat;

        [Header("Debug")]
        public bool debugLogColors = false;

        private ThreatService _service;

        // Runtime-generated placeholder sprite (1x1 white).
        private static Sprite _fallbackSprite;
        private static Texture2D _fallbackTexture;

        private bool _sanitizedTmp;
        private bool _raycastsDisabled;

        private static Gradient _vividGradient;
        private float _lastLoggedThreat = float.NaN;

        private void Awake()
        {
            SanitizeUnicodeSkullTmpOnce();
            DisableLegacySkullTextIfPresent();
            AutoWireFromChildrenIfNeeded();
            EnsureSpritesAssigned();
            EnsureBackgroundPanelReadable();
            DisableAllRaycastTargetsOnce();

            ResolveService();
            ApplyNumericVisibility();
            Refresh();
        }

        private void OnEnable()
        {
            DisableLegacySkullTextIfPresent();
            AutoWireFromChildrenIfNeeded();
            EnsureSpritesAssigned();
            EnsureBackgroundPanelReadable();
            DisableAllRaycastTargetsOnce();

            ResolveService();

            if (_service != null)
            {
                try { _service.OnThreatChanged += HandleThreatChanged; } catch { }
            }

            ApplyNumericVisibility();
            Refresh();
        }

        private void OnDisable()
        {
            if (_service != null)
            {
                try { _service.OnThreatChanged -= HandleThreatChanged; } catch { }
            }
        }

        // New preferred wiring signature.
        public void Wire(Image[] backgrounds, Image[] fills, TMP_Text valueText)
        {
            skullBackgrounds = backgrounds != null ? backgrounds : skullBackgrounds;
            skullFills = fills != null ? fills : skullFills;
            threatValueText = valueText != null ? valueText : threatValueText;

            DisableLegacySkullTextIfPresent();
            EnsureSpritesAssigned();
            EnsureBackgroundPanelReadable();
            ApplyNumericVisibility();
            Refresh();
        }

        // Back-compat for earlier setup menu versions. The TMP fallback is ignored and will be disabled.
        public void Wire(Image[] backgrounds, Image[] fills, TMP_Text unusedFallbackText, TMP_Text valueText)
        {
            Wire(backgrounds, fills, valueText);

            try
            {
                if (unusedFallbackText != null)
                {
                    unusedFallbackText.text = string.Empty;
                    unusedFallbackText.gameObject.SetActive(false);
                }
            }
            catch { }
        }

        private void HandleThreatChanged(float threat)
        {
            Refresh();
            LogThreatColorOncePerChange(threat);
        }

        private void ResolveService()
        {
            if (_service != null)
                return;

            try
            {
#if UNITY_2022_2_OR_NEWER
                _service = UnityEngine.Object.FindFirstObjectByType<ThreatService>(FindObjectsInactive.Exclude);
#else
                _service = UnityEngine.Object.FindObjectOfType<ThreatService>();
#endif
            }
            catch
            {
                _service = null;
            }
        }

        private void ApplyNumericVisibility()
        {
            try
            {
                if (threatValueText != null)
                    threatValueText.gameObject.SetActive(showNumericThreat);
            }
            catch { }
        }

        private void Refresh()
        {
            float threat = _service != null ? _service.CurrentThreat : 0f;

            float maxThreat = TryGetMaxThreatFromService(_service);
            Color tint = EvaluateVividThreatColor(threat, maxThreat);

            if (showNumericThreat && threatValueText != null)
            {
                try { threatValueText.text = threat.ToString("0.0"); } catch { }
                try { threatValueText.color = tint; } catch { }
                try { threatValueText.raycastTarget = false; } catch { }
            }

            RenderSkulls(threat, tint);
        }

        private void RenderSkulls(float threat, Color tint)
        {
            float remaining = threat;

            // Strong tint for readability.
            tint.a = 1f;

            const float emptyAlpha = 0.25f;
            const float halfAlpha = 0.70f;
            const float fullAlpha = 1.00f;

            for (int i = 0; i < 5; i++)
            {
                float fill = 0f;
                if (remaining >= 1f) fill = 1f;
                else if (remaining >= 0.5f) fill = 0.5f;

                remaining -= 1f;

                var bg = skullBackgrounds != null && i < skullBackgrounds.Length ? skullBackgrounds[i] : null;
                var fg = skullFills != null && i < skullFills.Length ? skullFills[i] : null;

                float alpha = fill <= 0f ? emptyAlpha : (fill < 1f ? halfAlpha : fullAlpha);
                var c = tint;
                c.a = alpha;

                if (bg != null)
                {
                    try
                    {
                        bg.gameObject.SetActive(true);
                        bg.raycastTarget = false;
                        bg.preserveAspect = true;

                        // Guard against custom materials/shaders ignoring tint.
                        bg.material = null;
                        bg.type = Image.Type.Simple;

                        // Always apply the vivid hue (even when empty).
                        bg.color = c;
                    }
                    catch { }
                }

                if (fg != null)
                {
                    try
                    {
                        fg.gameObject.SetActive(true);
                        fg.raycastTarget = false;
                        fg.preserveAspect = true;

                        // Guard against custom materials/shaders ignoring tint.
                        fg.material = null;

                        fg.type = Image.Type.Filled;
                        fg.fillMethod = Image.FillMethod.Horizontal;
                        fg.fillOrigin = 0;
                        fg.fillAmount = fill;

                        // Always apply the vivid hue (even when fillAmount is 0).
                        fg.color = c;
                    }
                    catch { }
                }
            }
        }

        private static Color EvaluateVividThreatColor(float threat, float maxThreat)
        {
            maxThreat = Mathf.Max(0.0001f, maxThreat);
            float t = Mathf.Clamp01(threat / maxThreat);

            var g = GetOrCreateVividGradient();
            var c = g != null ? g.Evaluate(t) : Color.red;
            c.a = 1f;
            return c;
        }

        private static Gradient GetOrCreateVividGradient()
        {
            if (_vividGradient != null)
                return _vividGradient;

            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    // High-saturation, high-contrast ramp (no "pastel" washout).
                    new GradientColorKey(new Color(0.05f, 1.00f, 0.20f, 1f), 0.00f), // green
                    new GradientColorKey(new Color(1.00f, 0.90f, 0.10f, 1f), 0.60f), // yellow/orange
                    new GradientColorKey(new Color(1.00f, 0.25f, 0.10f, 1f), 1.00f), // red/orange
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                }
            );

            _vividGradient = g;
            return _vividGradient;
        }

        private static float TryGetMaxThreatFromService(ThreatService svc)
        {
            if (svc == null)
                return 5f;

            // ThreatService.maxThreat is private; reflect it so HUD matches configuration.
            try
            {
                var fi = typeof(ThreatService).GetField("maxThreat", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    object v = fi.GetValue(svc);
                    if (v is float f)
                        return Mathf.Max(0.0001f, f);
                }
            }
            catch { }

            return 5f;
        }

        private void LogThreatColorOncePerChange(float threat)
        {
            if (!debugLogColors)
                return;

            if (!float.IsNaN(_lastLoggedThreat) && Mathf.Approximately(_lastLoggedThreat, threat))
                return;

            _lastLoggedThreat = threat;

            float maxThreat = TryGetMaxThreatFromService(_service);
            float t = Mathf.Clamp01(threat / Mathf.Max(0.0001f, maxThreat));
            Color c = EvaluateVividThreatColor(threat, maxThreat);

            try
            {
                Debug.Log($"[ThreatHUD] Threat={threat:0.0} t={t:0.0} color=#{ColorUtility.ToHtmlStringRGBA(c)}", this);
            }
            catch { }
        }

        private void AutoWireFromChildrenIfNeeded()
        {
            try
            {
                if (backgroundPanel == null)
                    backgroundPanel = GetComponent<Image>();
            }
            catch { }

            bool ok = skullBackgrounds != null && skullBackgrounds.Length == 5 && skullFills != null && skullFills.Length == 5;
            if (ok)
            {
                bool anyMissing = false;
                for (int i = 0; i < 5; i++)
                {
                    if (skullBackgrounds[i] == null || skullFills[i] == null)
                    {
                        anyMissing = true;
                        break;
                    }
                }

                if (!anyMissing)
                    return;
            }

            try
            {
                var row = transform.Find("SkullRow");
                if (row == null)
                    return;

                var bgs = new Image[5];
                var fgs = new Image[5];

                for (int i = 0; i < 5; i++)
                {
                    var slot = row.Find($"Skull{i + 1}");
                    if (slot == null) continue;

                    var bgT = slot.Find("BG");
                    var fgT = slot.Find("Fill");

                    bgs[i] = bgT != null ? bgT.GetComponent<Image>() : null;
                    fgs[i] = fgT != null ? fgT.GetComponent<Image>() : null;
                }

                skullBackgrounds = bgs;
                skullFills = fgs;
            }
            catch { }
        }

        private void EnsureBackgroundPanelReadable()
        {
            if (backgroundPanel == null)
                return;

            try { backgroundPanel.raycastTarget = false; } catch { }

            // Neutral dark background; must not affect skull tint.
            try { backgroundPanel.material = null; } catch { }

            var fallback = GetOrCreateFallbackSprite();
            if (fallback != null)
            {
                try
                {
                    if (backgroundPanel.sprite == null)
                        backgroundPanel.sprite = fallback;
                }
                catch { }
            }

            try
            {
                backgroundPanel.color = new Color(0f, 0f, 0f, 0.60f);
            }
            catch { }
        }

        private void DisableAllRaycastTargetsOnce()
        {
            if (_raycastsDisabled)
                return;
            _raycastsDisabled = true;

            try
            {
                var graphics = GetComponentsInChildren<Graphic>(true);
                if (graphics == null) return;
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    if (g == null) continue;
                    g.raycastTarget = false;
                }
            }
            catch { }
        }

        private void EnsureSpritesAssigned()
        {
            var fallback = GetOrCreateFallbackSprite();

            // Try to resolve the generated skull sprite in editor play mode.
#if UNITY_EDITOR
            try
            {
                if (skullSprite == null)
                {
                    const string path = "Assets/Art/UI/Threat/Skull_16.png";
                    skullSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            catch { }
#endif

            var desired = skullSprite != null ? skullSprite : fallback;
            if (desired == null)
                return;

            try
            {
                if (skullBackgrounds != null)
                {
                    for (int i = 0; i < skullBackgrounds.Length; i++)
                    {
                        var img = skullBackgrounds[i];
                        if (img == null) continue;
                        img.raycastTarget = false;
                        img.preserveAspect = true;
                        try { img.material = null; } catch { }
                        if (img.sprite != desired)
                            img.sprite = desired;
                    }
                }

                if (skullFills != null)
                {
                    for (int i = 0; i < skullFills.Length; i++)
                    {
                        var img = skullFills[i];
                        if (img == null) continue;
                        img.raycastTarget = false;
                        img.preserveAspect = true;
                        try { img.material = null; } catch { }
                        if (img.sprite != desired)
                            img.sprite = desired;
                    }
                }
            }
            catch { }
        }

        private static Sprite GetOrCreateFallbackSprite()
        {
            if (_fallbackSprite != null)
                return _fallbackSprite;

            try
            {
                if (_fallbackTexture == null)
                {
                    _fallbackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                    _fallbackTexture.hideFlags = HideFlags.HideAndDontSave;
                    _fallbackTexture.name = "ThreatHUD_FallbackTex";

                    _fallbackTexture.SetPixel(0, 0, Color.white);
                    _fallbackTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                }

                _fallbackSprite = Sprite.Create(
                    _fallbackTexture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 1f);

                if (_fallbackSprite != null)
                {
                    _fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
                    _fallbackSprite.name = "ThreatHUD_FallbackSprite";
                }

                return _fallbackSprite;
            }
            catch
            {
                return null;
            }
        }

        private void DisableLegacySkullTextIfPresent()
        {
            // Old versions created a TMP Text named SkullText with unicode ☠, which can spam glyph warnings.
            try
            {
                var t = transform.Find("SkullText");
                if (t == null)
                    return;

                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null)
                    tmp.text = string.Empty;

                t.gameObject.SetActive(false);
            }
            catch { }
        }

        private void SanitizeUnicodeSkullTmpOnce()
        {
            if (_sanitizedTmp)
                return;
            _sanitizedTmp = true;

            ThreatLegacyTmpSkullSanitizer.RunOnce(this);
        }
    }

    internal static class ThreatLegacyTmpSkullSanitizer
    {
        private const char Skull = '\u2620';

        private static bool _hooked;
        private static bool _ran;
        private static bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (_hooked)
                return;
            _hooked = true;

            // Important: hook before the scene loads so this runs before TMP's own willRender handlers,
            // preventing the glyph warning from ever being logged.
            try { Canvas.willRenderCanvases += OnWillRenderCanvasesOnce; } catch { }
        }

        private static void OnWillRenderCanvasesOnce()
        {
            try { Canvas.willRenderCanvases -= OnWillRenderCanvasesOnce; } catch { }
            RunOnce(null);
        }

        internal static void RunOnce(UnityEngine.Object context)
        {
            if (_ran)
                return;
            _ran = true;

            bool disabledAny = false;

            try
            {
                var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
                if (all == null || all.Length == 0)
                    return;

                for (int i = 0; i < all.Length; i++)
                {
                    var tmp = all[i];
                    if (tmp == null) continue;

                    // Skip non-scene/persistent assets (prefabs, font assets, etc.).
                    try
                    {
                        if (tmp.gameObject == null) continue;
                        if (!tmp.gameObject.scene.IsValid()) continue;
                    }
                    catch { continue; }

                    string objectName = string.Empty;
                    try { objectName = tmp.gameObject.name; } catch { objectName = string.Empty; }

                    bool nameLooksLikeLegacy = !string.IsNullOrEmpty(objectName)
                        && objectName.IndexOf("SkullText", StringComparison.OrdinalIgnoreCase) >= 0;

                    string text = null;
                    try { text = tmp.text; } catch { text = null; }

                    bool containsSkull = !string.IsNullOrEmpty(text) && text.IndexOf(Skull) >= 0;

                    if (!nameLooksLikeLegacy && !containsSkull)
                        continue;

                    disabledAny = true;

                    try { tmp.text = string.Empty; } catch { }
                    try { tmp.enabled = false; } catch { }

                    if (nameLooksLikeLegacy)
                    {
                        try { tmp.gameObject.SetActive(false); } catch { }
                    }
                }
            }
            catch { }

            if (disabledAny && !_logged)
            {
                _logged = true;
                try
                {
                    if (context != null)
                        Debug.Log("[ThreatHUD] Disabled legacy TMP skull text to avoid font warnings.", context);
                    else
                        Debug.Log("[ThreatHUD] Disabled legacy TMP skull text to avoid font warnings.");
                }
                catch { }
            }
        }
    }
}
