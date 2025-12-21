using System;
using Abyss.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerDamageHudText : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private PlayerCombatStats stats;

    private PlayerEquipment _equipment;
    private bool _warnedMissingStats;
    private float _nextPollTime;

    private static readonly Color32 s_DamageColor = new Color32(245, 215, 110, 255);
    private static readonly Color32 s_OutlineColor = new Color32(0, 0, 0, 255);
    private static readonly Color32 s_BgColor = new Color32(0, 0, 0, 190);

    private static Transform FindByNameRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        try
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var found = FindByNameRecursive(child, name);
                if (found != null)
                    return found;
            }
        }
        catch { }

        return null;
    }

    // Create/ensure HUD element exists without requiring scene/prefab edits.
    // This reuses the existing HUD canvas created by HudFactory.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureHudDamageText()
    {
        Canvas hudCanvas = null;
        try { hudCanvas = HudFactory.EnsureHudCanvas(); } catch { hudCanvas = null; }
        if (hudCanvas == null)
            return;

        // Always create/find a dedicated root so BG + text are guaranteed and not clipped by other groups.
        // Abyss_HUDCanvas
        //   DamageHudRoot
        //     DamageTextBG (Image)
        //     DamageText   (TextMeshProUGUI + PlayerDamageHudText)
        var canvasTf = hudCanvas.transform;

        Transform rootTf = null;
        try { rootTf = canvasTf.Find("DamageHudRoot"); } catch { rootTf = null; }
        if (rootTf == null)
            rootTf = FindByNameRecursive(canvasTf, "DamageHudRoot");

        GameObject rootGo;
        if (rootTf == null)
        {
            rootGo = new GameObject("DamageHudRoot");
            rootGo.transform.SetParent(canvasTf, false);
        }
        else
        {
            rootGo = rootTf.gameObject;
        }

        if (rootGo == null)
            return;

        // Keep it in the HUD canvas top layer.
        if (rootGo.transform.parent != canvasTf)
        {
            try { rootGo.transform.SetParent(canvasTf, false); } catch { }
        }

        var rootRt = rootGo.GetComponent<RectTransform>();
        if (rootRt == null)
            rootRt = rootGo.AddComponent<RectTransform>();

        // Root: top-right.
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(-20f, -20f);
        rootRt.sizeDelta = new Vector2(180f, 40f);
        rootRt.localScale = Vector3.one;

        // Find/reparent any existing objects.
        Transform textTf = null;
        try { textTf = rootGo.transform.Find("DamageText"); } catch { textTf = null; }
        if (textTf == null)
        {
            textTf = FindByNameRecursive(canvasTf, "DamageText");
        }

        GameObject go;
        if (textTf == null)
        {
            go = new GameObject("DamageText");
            go.transform.SetParent(rootGo.transform, false);
        }
        else
        {
            go = textTf.gameObject;
            if (go.transform.parent != rootGo.transform)
            {
                try { go.transform.SetParent(rootGo.transform, false); } catch { }
            }
        }

        if (go == null)
            return;

        Transform bgTf = null;
        try { bgTf = rootGo.transform.Find("DamageTextBG"); } catch { bgTf = null; }
        if (bgTf == null)
        {
            bgTf = FindByNameRecursive(canvasTf, "DamageTextBG");
        }

        GameObject bgGo;
        if (bgTf == null)
        {
            bgGo = new GameObject("DamageTextBG");
            bgGo.transform.SetParent(rootGo.transform, false);
        }
        else
        {
            bgGo = bgTf.gameObject;
            if (bgGo.transform.parent != rootGo.transform)
            {
                try { bgGo.transform.SetParent(rootGo.transform, false); } catch { }
            }
        }

        // BG: stretches to root and renders behind.
        if (bgGo != null)
        {
            var bgRt = bgGo.GetComponent<RectTransform>();
            if (bgRt == null)
                bgRt = bgGo.AddComponent<RectTransform>();

            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = Vector2.zero;
            bgRt.localScale = Vector3.one;

            var img = bgGo.GetComponent<Image>();
            if (img == null)
                img = bgGo.AddComponent<Image>();

            img.color = s_BgColor;
            img.raycastTarget = false;
        }

        // Text: stretches to root with padding.
        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = new Vector2(10f, 6f);
        rt.offsetMax = new Vector2(-10f, -6f);
        rt.localScale = Vector3.one;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();

        // Enforce order: BG first, then text.
        try
        {
            if (bgGo != null)
                bgGo.transform.SetSiblingIndex(0);
            go.transform.SetSiblingIndex(Mathf.Min(1, rootGo.transform.childCount - 1));
        }
        catch { }

        // Ensure behaviour exists and is wired.
        var hud = go.GetComponent<PlayerDamageHudText>();
        if (hud == null)
            hud = go.AddComponent<PlayerDamageHudText>();

        if (hud.damageText == null)
            hud.damageText = tmp;

        // Apply deterministic styling so it survives scene reload.
        hud.ApplyStyle();
        hud.Refresh();
    }

    private void OnEnable()
    {
        ResolveStats();
        HookEquipmentChanged();
        ApplyStyle();
        Refresh();

        _nextPollTime = Time.unscaledTime + 0.25f;
    }

    private void OnDisable()
    {
        if (_equipment != null)
            _equipment.Changed -= OnEquipmentChanged;

        _equipment = null;
    }

    private void Update()
    {
        // If we couldn't subscribe to equipment changes (or stats isn't available yet), poll at most 4x/sec.
        if (Time.unscaledTime < _nextPollTime)
            return;

        _nextPollTime = Time.unscaledTime + 0.25f;

        if (stats == null)
            ResolveStats();

        if (_equipment == null)
            HookEquipmentChanged();

        Refresh();
    }

    private void ResolveStats()
    {
        if (stats != null)
            return;

        // Try local hierarchy first.
        stats = GetComponentInParent<PlayerCombatStats>();
        if (stats != null)
            return;

        // Robust fallback.
        try
        {
#if UNITY_2023_1_OR_NEWER
            stats = UnityEngine.Object.FindAnyObjectByType<PlayerCombatStats>(FindObjectsInactive.Exclude);
#else
            stats = UnityEngine.Object.FindObjectOfType<PlayerCombatStats>();
#endif
        }
        catch
        {
            stats = null;
        }
    }

    private void HookEquipmentChanged()
    {
        if (_equipment != null)
            return;

        PlayerEquipment equipment = null;

        if (stats != null)
        {
            try { equipment = stats.GetComponentInParent<PlayerEquipment>(); } catch { equipment = null; }
            if (equipment == null)
            {
                try { equipment = stats.GetComponent<PlayerEquipment>(); } catch { equipment = null; }
            }
        }

        if (equipment == null)
        {
            try
            {
#if UNITY_2023_1_OR_NEWER
                equipment = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>(FindObjectsInactive.Exclude);
#else
                equipment = UnityEngine.Object.FindObjectOfType<PlayerEquipment>();
#endif
            }
            catch
            {
                equipment = null;
            }
        }

        if (equipment == null)
            return;

        _equipment = equipment;
        _equipment.Changed -= OnEquipmentChanged;
        _equipment.Changed += OnEquipmentChanged;
    }

    private void OnEquipmentChanged()
    {
        ResolveStats();
        Refresh();
    }

    private void ApplyStyle()
    {
        if (damageText == null)
            return;

        damageText.text = "DMG: ?";
        damageText.color = s_DamageColor;

        // Styling requirements.
        if (damageText is TextMeshProUGUI ugui)
        {
            ugui.fontSize = 24f;
            ugui.alignment = TextAlignmentOptions.TopRight;
            ugui.textWrappingMode = TextWrappingModes.NoWrap;
            ugui.raycastTarget = false;

            // High-contrast outline.
            try
            {
                var mat = ugui.fontMaterial;
                if (mat != null)
                {
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, s_OutlineColor);
                    ugui.fontMaterial = mat;
                }
            }
            catch { }
        }
    }

    public void Refresh()
    {
        if (damageText == null)
            return;

        damageText.color = s_DamageColor;

        if (stats == null)
        {
            damageText.text = "DMG: ?";

            if (!_warnedMissingStats)
            {
                _warnedMissingStats = true;
                Debug.LogWarning("[HUD] PlayerCombatStats missing; DamageText shows '?'.", this);
            }

            return;
        }

        damageText.text = $"DMG: {stats.DamageFinal}";
    }
}
