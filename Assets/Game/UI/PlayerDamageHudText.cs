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
    private static readonly Color32 s_BgColor = new Color32(0, 0, 0, 180);

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

        Transform existingTf = null;
        try { existingTf = hudCanvas.transform.Find("DamageText"); } catch { existingTf = null; }
        if (existingTf == null)
        {
            // Fallback: if something reparented it under the HUD canvas, find it recursively.
            existingTf = FindByNameRecursive(hudCanvas.transform, "DamageText");
        }

        GameObject go;
        if (existingTf == null)
        {
            go = new GameObject("DamageText");
            go.transform.SetParent(hudCanvas.transform, false);
        }
        else
        {
            go = existingTf.gameObject;
        }

        if (go == null)
            return;

        // Force both BG + text under the HUD canvas (avoid masked/clipped sub-groups).
        var parent = hudCanvas.transform;
        if (go.transform.parent != parent)
        {
            try { go.transform.SetParent(parent, false); } catch { }
        }

        // Ensure a background panel exists as a sibling directly behind DamageText under the same parent.
        GameObject bgGo = null;
        Transform bgTf = null;
        try { bgTf = parent.Find("DamageTextBG"); } catch { bgTf = null; }
        if (bgTf == null)
        {
            bgTf = FindByNameRecursive(parent, "DamageTextBG");
        }
        bgGo = bgTf != null ? bgTf.gameObject : null;

        if (bgGo == null)
        {
            bgGo = new GameObject("DamageTextBG");
            bgGo.transform.SetParent(parent, false);
        }
        else if (bgGo.transform.parent != parent)
        {
            try { bgGo.transform.SetParent(parent, false); } catch { }
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        // Top-left anchored with padding.
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        // Reposition down to avoid DevCheats overlap.
        rt.anchoredPosition = new Vector2(20f, -60f);
        rt.sizeDelta = new Vector2(260f, 40f);
        rt.localScale = Vector3.one;

        if (bgGo != null)
        {
            var bgRt = bgGo.GetComponent<RectTransform>();
            if (bgRt == null)
                bgRt = bgGo.AddComponent<RectTransform>();

            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = rt.anchoredPosition;
            bgRt.sizeDelta = new Vector2(160f, 40f);
            bgRt.localScale = Vector3.one;

            var img = bgGo.GetComponent<Image>();
            if (img == null)
                img = bgGo.AddComponent<Image>();

            img.color = s_BgColor;
            img.raycastTarget = false;

            // Enforce draw order: BG immediately before the text.
            try
            {
                int textIndex = go.transform.GetSiblingIndex();
                int bgIndex = bgGo.transform.GetSiblingIndex();

                // Put BG directly before text.
                if (bgIndex != textIndex - 1)
                {
                    int desiredBgIndex = Mathf.Max(0, textIndex - 1);
                    bgGo.transform.SetSiblingIndex(desiredBgIndex);
                    go.transform.SetSiblingIndex(Mathf.Min(desiredBgIndex + 1, parent.childCount - 1));
                }
            }
            catch { }
        }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();

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
            ugui.alignment = TextAlignmentOptions.TopLeft;
            ugui.textWrappingMode = TextWrappingModes.NoWrap;
            ugui.raycastTarget = false;

            // High-contrast outline.
            try
            {
                var mat = ugui.fontMaterial;
                if (mat != null)
                {
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
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
