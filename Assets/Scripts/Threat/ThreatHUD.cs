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
        // QA Checklist (Threat HUD Layout)
        // - Skull row readable on white terrain and sky
        // - Skull colors clearly visible at all threat levels
        // - Distance text readable but secondary
        // - No overlap, clipping, or console warnings

        [Header("Wiring")]
        [SerializeField] private Image[] skullBackgrounds = new Image[5];
        [SerializeField] private Image[] skullFills = new Image[5];
        [SerializeField] private TMP_Text threatValueText;

        [Header("Distance")]
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text farthestDistanceText;
        [SerializeField, Tooltip("Distance text updates per second (not every frame).")]
        private float distanceTextUpdatesPerSecond = 6f;

        [SerializeField] private Color32 threatDistanceTextColor = new Color32(220, 220, 220, 255);

        [Header("Visual")]
        public Sprite skullSprite;

        [SerializeField] private Image backgroundPanel;

        [Header("Options")]
        [SerializeField] private bool showNumericThreat;

        [Header("Debug")]
        public bool debugLogColors = false;

        private ThreatService _service;

        private float _nextDistanceTextUpdateTime;

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
            EnsureLayoutContainers();
            AutoWireFromChildrenIfNeeded();
            EnsureSpritesAssigned();
            EnsureBackgroundPanelReadable();
            EnsureSkullContrastBoost();
            DisableAllRaycastTargetsOnce();

            ResolveService();
            EnsureSkullSlotsMatchMaxThreat();
            AutoWireDistanceTextsIfNeeded();
            ApplyNumericVisibility();
            Refresh();
        }

        private void OnEnable()
        {
            DisableLegacySkullTextIfPresent();
            EnsureLayoutContainers();
            AutoWireFromChildrenIfNeeded();
            EnsureSpritesAssigned();
            EnsureBackgroundPanelReadable();
            EnsureSkullContrastBoost();
            DisableAllRaycastTargetsOnce();

            ResolveService();

            EnsureSkullSlotsMatchMaxThreat();
            AutoWireDistanceTextsIfNeeded();

            if (_service != null)
            {
                try { _service.OnThreatChanged += HandleThreatChanged; } catch { }
            }

            ApplyNumericVisibility();
            Refresh();
        }

        private void Update()
        {
            float interval = distanceTextUpdatesPerSecond <= 0f ? 0.2f : (1f / Mathf.Max(0.1f, distanceTextUpdatesPerSecond));
            if (Time.unscaledTime < _nextDistanceTextUpdateTime)
                return;

            _nextDistanceTextUpdateTime = Time.unscaledTime + interval;
            RefreshDistanceTexts();
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
            RefreshDistanceTexts(tint);
        }

        private void RenderSkulls(float threat, Color tint)
        {
            float remaining = threat;

            // Keep the threat hue ramp as-is for current threat.
            // Adjust visual treatment by state (full/half/empty) using HSV.
            tint.a = 1f;

            Color fullColor = tint;
            fullColor.a = 1f;

            Color.RGBToHSV(fullColor, out float h, out float s, out float v);

            float halfS = Mathf.Clamp01(s * 0.95f);
            float halfV = Mathf.Clamp01(v * 0.65f);
            Color halfColor = Color.HSVToRGB(h, halfS, halfV);
            halfColor.a = 1f;

            float emptyS = Mathf.Clamp01(s * 0.15f);
            float emptyV = Mathf.Clamp01(v * 0.25f);
            Color emptyColor = Color.HSVToRGB(h, emptyS, emptyV);
            emptyColor.a = 0.35f;

            // Base skull silhouette: off-white so skulls stay readable even at low threat.
            // This is applied to the BG layer; the Fill layer gets the threat tint.
            Color baseSkull = new Color(0.929f, 0.929f, 0.929f, 1f); // #EDEDED

            int count = Mathf.Max(0, Mathf.Min(skullBackgrounds != null ? skullBackgrounds.Length : 0, skullFills != null ? skullFills.Length : 0));
            for (int i = 0; i < count; i++)
            {
                float fill = 0f;
                if (remaining >= 1f) fill = 1f;
                else if (remaining >= 0.5f) fill = 0.5f;

                remaining -= 1f;

                var bg = skullBackgrounds != null && i < skullBackgrounds.Length ? skullBackgrounds[i] : null;
                var fg = skullFills != null && i < skullFills.Length ? skullFills[i] : null;

                Color fgColor = fill <= 0f ? emptyColor : (fill < 1f ? halfColor : fullColor);

                // BG stays off-white (silhouette) with a muted alpha; Fill provides threat tint.
                float bgAlpha = fill <= 0f ? 0.30f : (fill < 1f ? 0.22f : 0.14f);
                Color bgColor = new Color(baseSkull.r, baseSkull.g, baseSkull.b, bgAlpha);

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

                        bg.color = bgColor;
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

                        fg.color = fgColor;
                    }
                    catch { }
                }
            }
        }

        private void RefreshDistanceTexts()
        {
            float maxThreat = TryGetMaxThreatFromService(_service);
            Color tint = EvaluateVividThreatColor(_service != null ? _service.CurrentThreat : 0f, maxThreat);
            RefreshDistanceTexts(tint);
        }

        private void RefreshDistanceTexts(Color tint)
        {
            if (distanceText == null && farthestDistanceText == null)
                return;

            float dist = 0f;
            float far = 0f;

            try
            {
                if (_service != null)
                {
                    dist = Mathf.Max(0f, _service.CurrentDistanceMeters);
                    far = Mathf.Max(0f, _service.FarthestDistanceMeters);
                }

                var prov = ThreatDistanceProvider.Instance;
                if (prov != null)
                {
                    dist = Mathf.Max(dist, prov.CurrentDistanceMeters);
                    far = Mathf.Max(far, prov.FarthestDistanceMeters);
                }
            }
            catch { }

            // Secondary metadata styling: stable, serialized color.
            // Keep size/placement controlled by prefab/layout.
            var labelColor = (Color)threatDistanceTextColor;

            if (distanceText != null)
            {
                try { distanceText.text = $"Dist {dist:0}m"; } catch { }
                try { distanceText.color = labelColor; } catch { }
                try { distanceText.raycastTarget = false; } catch { }
            }

            if (farthestDistanceText != null)
            {
                try { farthestDistanceText.text = $"Max {far:0}m"; } catch { }
                try { farthestDistanceText.color = labelColor; } catch { }
                try { farthestDistanceText.raycastTarget = false; } catch { }
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

            bool ok = skullBackgrounds != null && skullBackgrounds.Length > 0 && skullFills != null && skullFills.Length == skullBackgrounds.Length;
            if (ok)
            {
                bool anyMissing = false;
                for (int i = 0; i < skullBackgrounds.Length; i++)
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

                int count = 0;
                for (int i = 0; i < 64; i++)
                {
                    var slot = row.Find($"Skull{i + 1}");
                    if (slot == null)
                        break;
                    count++;
                }

                if (count <= 0)
                    return;

                var bgs = new Image[count];
                var fgs = new Image[count];

                for (int i = 0; i < count; i++)
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

        private void EnsureSkullSlotsMatchMaxThreat()
        {
            float maxThreat = TryGetMaxThreatFromService(_service);
            int desired = Mathf.Max(1, Mathf.CeilToInt(maxThreat));

            try
            {
                var row = transform.Find("SkullRow");
                if (row == null)
                    return;

                int existing = 0;
                for (int i = 0; i < 128; i++)
                {
                    if (row.Find($"Skull{i + 1}") == null) break;
                    existing++;
                }

                if (existing <= 0)
                    return;

                if (existing >= desired)
                {
                    AutoWireFromChildrenIfNeeded();
                    return;
                }

                Transform template = row.Find("Skull1");
                if (template == null)
                    template = row.GetChild(0);

                if (template == null)
                    return;

                for (int i = existing; i < desired; i++)
                {
                    var cloned = Instantiate(template.gameObject, row);
                    cloned.name = $"Skull{i + 1}";
                }

                AutoWireFromChildrenIfNeeded();
                EnsureSpritesAssigned();
            }
            catch { }
        }

        private void AutoWireDistanceTextsIfNeeded()
        {
            try
            {
                if (distanceText == null)
                {
                    var t = transform.Find("DistanceText") ?? transform.Find("DistanceContainer/DistanceText");
                    distanceText = t != null ? t.GetComponent<TMP_Text>() : null;
                }

                if (farthestDistanceText == null)
                {
                    var t = transform.Find("FarthestDistanceText") ?? transform.Find("DistanceContainer/FarthestDistanceText");
                    farthestDistanceText = t != null ? t.GetComponent<TMP_Text>() : null;
                }
            }
            catch { }
        }

        private void EnsureLayoutContainers()
        {
            // UI-only layout enforcement:
            // - Split skulls and distance text into separate vertical sections
            // - Add breathing room without shrinking skulls
            try
            {
                var root = transform as RectTransform;
                if (root == null)
                    return;

                // Ensure panel height has room for two sections.
                try
                {
                    var s = root.sizeDelta;
                    s.y = Mathf.Max(s.y, 128f);
                    root.sizeDelta = s;
                }
                catch { }

                var skullRow = root.Find("SkullRow") as RectTransform;

                var skullContainerT = root.Find("SkullContainer") as RectTransform;
                if (skullContainerT == null)
                {
                    var go = new GameObject("SkullContainer", typeof(RectTransform));
                    skullContainerT = go.GetComponent<RectTransform>();
                    skullContainerT.SetParent(root, false);
                    skullContainerT.SetSiblingIndex(0);
                }

                var distanceContainerT = root.Find("DistanceContainer") as RectTransform;
                if (distanceContainerT == null)
                {
                    var go = new GameObject("DistanceContainer", typeof(RectTransform));
                    distanceContainerT = go.GetComponent<RectTransform>();
                    distanceContainerT.SetParent(root, false);
                    distanceContainerT.SetSiblingIndex(1);
                }

                // Layout: top-anchored sections.
                ConfigureSection(skullContainerT, yTop: -6f, height: 72f);
                ConfigureSection(distanceContainerT, yTop: -6f - 72f - 8f, height: 40f);

                // Skull row belongs to SkullContainer.
                if (skullRow != null && skullRow.parent != skullContainerT)
                    skullRow.SetParent(skullContainerT, worldPositionStays: false);

                // Center skull row within its container.
                if (skullRow != null)
                {
                    skullRow.anchorMin = new Vector2(0.5f, 0.5f);
                    skullRow.anchorMax = new Vector2(0.5f, 0.5f);
                    skullRow.pivot = new Vector2(0.5f, 0.5f);
                    skullRow.anchoredPosition = Vector2.zero;
                    skullRow.sizeDelta = new Vector2(300f, 56f);
                }

                EnsureSkullBackdrop(skullContainerT, skullRow);

                // Move distance texts into DistanceContainer if they exist.
                var distT = (root.Find("DistanceText") as RectTransform) ?? (distanceContainerT.Find("DistanceText") as RectTransform);
                var farT = (root.Find("FarthestDistanceText") as RectTransform) ?? (distanceContainerT.Find("FarthestDistanceText") as RectTransform);
                if (distT != null && distT.parent != distanceContainerT)
                    distT.SetParent(distanceContainerT, false);
                if (farT != null && farT.parent != distanceContainerT)
                    farT.SetParent(distanceContainerT, false);

                // Stack and center in DistanceContainer.
                if (distT != null)
                {
                    distT.anchorMin = new Vector2(0.5f, 1f);
                    distT.anchorMax = new Vector2(0.5f, 1f);
                    distT.pivot = new Vector2(0.5f, 1f);
                    distT.anchoredPosition = new Vector2(0f, 0f);
                    distT.sizeDelta = new Vector2(300f, 16f);
                }
                if (farT != null)
                {
                    farT.anchorMin = new Vector2(0.5f, 1f);
                    farT.anchorMax = new Vector2(0.5f, 1f);
                    farT.pivot = new Vector2(0.5f, 1f);
                    farT.anchoredPosition = new Vector2(0f, -16f);
                    farT.sizeDelta = new Vector2(300f, 16f);
                }
            }
            catch { }
        }

        private static void ConfigureSection(RectTransform rt, float yTop, float height)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yTop);
            rt.sizeDelta = new Vector2(320f, Mathf.Max(0f, height));
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
                // Keep overall panel present but not dominant; skull-only backdrop does the heavy lifting.
                backgroundPanel.color = new Color(0f, 0f, 0f, 0.35f);
            }
            catch { }
        }

        private void EnsureSkullBackdrop(RectTransform skullContainer, RectTransform skullRow)
        {
            if (skullContainer == null)
                return;

            try
            {
                var backdropT = skullContainer.Find("SkullBackdrop") as RectTransform;
                Image img = null;

                if (backdropT == null)
                {
                    var go = new GameObject("SkullBackdrop", typeof(RectTransform), typeof(Image));
                    backdropT = go.GetComponent<RectTransform>();
                    backdropT.SetParent(skullContainer, false);
                    backdropT.SetSiblingIndex(0);
                    img = go.GetComponent<Image>();
                }
                else
                {
                    img = backdropT.GetComponent<Image>();
                    if (img == null)
                        img = backdropT.gameObject.AddComponent<Image>();
                }

                // Ensure backdrop renders behind the skull row.
                if (skullRow != null)
                {
                    int rowIndex = skullRow.GetSiblingIndex();
                    if (backdropT.GetSiblingIndex() > rowIndex)
                        backdropT.SetSiblingIndex(rowIndex);
                }

                backdropT.anchorMin = Vector2.zero;
                backdropT.anchorMax = Vector2.one;
                backdropT.pivot = new Vector2(0.5f, 0.5f);
                backdropT.anchoredPosition = Vector2.zero;

                // Padding around skulls (requested 8–10px).
                backdropT.offsetMin = new Vector2(9f, 9f);
                backdropT.offsetMax = new Vector2(-9f, -9f);

                if (img != null)
                {
                    img.raycastTarget = false;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = false;
                    try { img.material = null; } catch { }

                    var fallback = GetOrCreateFallbackSprite();
                    if (fallback != null)
                    {
                        try { img.sprite = fallback; } catch { }
                    }

                    // Pure black backdrop behind skulls only.
                    img.color = new Color(0f, 0f, 0f, 0.70f);
                }
            }
            catch { }
        }

        private void EnsureSkullContrastBoost()
        {
            // Subtle shadow/outline to help skull readability.
            try
            {
                if (skullFills == null)
                    return;

                for (int i = 0; i < skullFills.Length; i++)
                {
                    var img = skullFills[i];
                    if (img == null)
                        continue;

                    var shadow = img.GetComponent<Shadow>();
                    if (shadow == null)
                        shadow = img.gameObject.AddComponent<Shadow>();

                    shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
                    shadow.effectDistance = new Vector2(1f, -1f);
                    shadow.useGraphicAlpha = true;
                }
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
