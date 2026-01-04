using System;
using Abyss.Equipment;
using Abyssbound.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerStatsHudPanel : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text drText;

    [Header("Colors")]
    [SerializeField] private Color32 statsTextColor = new Color32(245, 215, 110, 255);

    [Header("References")]
    [SerializeField] private PlayerCombatStats combatStats;
    [SerializeField] private PlayerHealth playerHealth;

    [SerializeField] private PlayerStatsRuntime statsRuntime;

    private PlayerEquipment _equipment;
    private bool _warnedMissingRefs;
    private float _nextPollTime;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureHudStatsPanel()
    {
        Canvas hudCanvas = null;
        try { hudCanvas = HudFactory.EnsureHudCanvas(); } catch { hudCanvas = null; }
        if (hudCanvas == null)
            return;

        var canvasTf = hudCanvas.transform;

        Transform rootTf = null;
        try { rootTf = canvasTf.Find("StatsHudRoot"); } catch { rootTf = null; }
        if (rootTf == null)
            rootTf = FindByNameRecursive(canvasTf, "StatsHudRoot");

        GameObject rootGo;
        if (rootTf == null)
        {
            rootGo = new GameObject("StatsHudRoot");
            rootGo.transform.SetParent(canvasTf, false);
        }
        else
        {
            rootGo = rootTf.gameObject;
        }

        if (rootGo == null)
            return;

        if (rootGo.transform.parent != canvasTf)
        {
            try { rootGo.transform.SetParent(canvasTf, false); } catch { }
        }

        var rootRt = rootGo.GetComponent<RectTransform>();
        if (rootRt == null)
            rootRt = rootGo.AddComponent<RectTransform>();

        // Root: top-right. Place below the existing DMG box.
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(-20f, -60f);
        // Compact panel: only DR (HP is shown in the health bar; DMG is shown by the existing DMG HUD box).
        rootRt.sizeDelta = new Vector2(180f, 42f);
        rootRt.localScale = Vector3.one;

        // Background
        Transform bgTf = null;
        try { bgTf = rootGo.transform.Find("StatsHudBG"); } catch { bgTf = null; }
        if (bgTf == null)
            bgTf = FindByNameRecursive(canvasTf, "StatsHudBG");

        GameObject bgGo;
        if (bgTf == null)
        {
            bgGo = new GameObject("StatsHudBG");
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

        // Text children (combat HUD only)
        var dr = EnsureLineText(rootGo.transform, "Stats_DR", new Vector2(10f, -8f));

        // If older versions left these behind, hide them so DMG isn't duplicated and skilling isn't shown here.
        TryDisableChild(rootGo.transform, "Stats_DMG");
        TryDisableChild(rootGo.transform, "Stats_HP");
        TryDisableChild(rootGo.transform, "Stats_Primary");

        // Enforce order: BG first, then texts.
        try
        {
            if (bgGo != null)
                bgGo.transform.SetSiblingIndex(0);
        }
        catch { }

        // Ensure behaviour exists and is wired.
        var panel = rootGo.GetComponent<PlayerStatsHudPanel>();
        if (panel == null)
            panel = rootGo.AddComponent<PlayerStatsHudPanel>();

        if (panel.drText == null)
            panel.drText = dr;

        panel.ApplyStyle();
        panel.Refresh();
    }

    private static void TryDisableChild(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name))
            return;

        Transform tf = null;
        try { tf = parent.Find(name); } catch { tf = null; }
        if (tf == null)
            return;

        try { tf.gameObject.SetActive(false); } catch { }
    }

    private static TextMeshProUGUI EnsureLineText(Transform parent, string name, Vector2 anchoredPos)
    {
        if (parent == null)
            return null;

        Transform tf = null;
        try { tf = parent.Find(name); } catch { tf = null; }

        GameObject go;
        if (tf == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = tf.gameObject;
        }

        if (go == null)
            return null;

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        // Left-aligned text within the panel.
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(-20f, 28f);
        rt.localScale = Vector3.one;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();

        tmp.raycastTarget = false;
        return tmp;
    }

    private static TextMeshProUGUI EnsureBlockText(Transform parent, string name, Vector2 anchoredPos, float height)
    {
        if (parent == null)
            return null;

        Transform tf = null;
        try { tf = parent.Find(name); } catch { tf = null; }

        GameObject go;
        if (tf == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = tf.gameObject;
        }

        if (go == null)
            return null;

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(-20f, Mathf.Max(40f, height));
        rt.localScale = Vector3.one;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();

        tmp.raycastTarget = false;
        return tmp;
    }

    private void OnEnable()
    {
        ResolveTextRefsByName();
        ResolveRefs();
        HookEquipmentChanged();
        HookHealthChanged();

        ApplyStyle();
        Refresh();

        _nextPollTime = Time.unscaledTime + 0.25f;
    }

    private void OnDisable()
    {
        if (_equipment != null)
            _equipment.Changed -= OnEquipmentChanged;
        _equipment = null;

        if (playerHealth != null)
            playerHealth.HealthChanged -= OnHealthChanged;
    }

    private void Update()
    {
        // Fallback polling (4x/sec) in case references/events are missing.
        if (Time.unscaledTime < _nextPollTime)
            return;

        _nextPollTime = Time.unscaledTime + 0.25f;

        if (combatStats == null || playerHealth == null)
            ResolveRefs();

        if (_equipment == null)
            HookEquipmentChanged();

        if (playerHealth != null)
            HookHealthChanged();

        Refresh();
    }

    private void ResolveTextRefsByName()
    {
        if (drText == null)
        {
            try { drText = transform.Find("Stats_DR")?.GetComponent<TMP_Text>(); } catch { drText = null; }
        }
    }

    private void ResolveRefs()
    {
        if (combatStats == null)
        {
            try
            {
                combatStats = GetComponentInParent<PlayerCombatStats>();
                if (combatStats == null) combatStats = GetComponent<PlayerCombatStats>();
            }
            catch { combatStats = null; }

            if (combatStats == null)
            {
                try
                {
#if UNITY_2023_1_OR_NEWER
                    combatStats = UnityEngine.Object.FindAnyObjectByType<PlayerCombatStats>(FindObjectsInactive.Exclude);
#else
                    combatStats = UnityEngine.Object.FindObjectOfType<PlayerCombatStats>();
#endif
                }
                catch { combatStats = null; }
            }
        }

        if (playerHealth == null)
        {
            try
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
                if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            }
            catch { playerHealth = null; }

            if (playerHealth == null)
            {
                try
                {
#if UNITY_2023_1_OR_NEWER
                    playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
                    playerHealth = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
#endif
                }
                catch { playerHealth = null; }
            }
        }

        if (statsRuntime == null)
        {
            try
            {
                if (combatStats != null)
                {
                    statsRuntime = combatStats.GetComponentInParent<PlayerStatsRuntime>();
                    if (statsRuntime == null) statsRuntime = combatStats.GetComponent<PlayerStatsRuntime>();
                }
            }
            catch { statsRuntime = null; }

            if (statsRuntime == null)
            {
                try
                {
                    if (playerHealth != null)
                    {
                        statsRuntime = playerHealth.GetComponentInParent<PlayerStatsRuntime>();
                        if (statsRuntime == null) statsRuntime = playerHealth.GetComponent<PlayerStatsRuntime>();
                    }
                }
                catch { statsRuntime = null; }
            }

            if (statsRuntime == null)
            {
                try
                {
#if UNITY_2023_1_OR_NEWER
                    statsRuntime = UnityEngine.Object.FindAnyObjectByType<PlayerStatsRuntime>(FindObjectsInactive.Exclude);
#else
                    statsRuntime = UnityEngine.Object.FindObjectOfType<PlayerStatsRuntime>();
#endif
                }
                catch { statsRuntime = null; }
            }
        }

        if (!_warnedMissingRefs && (combatStats == null || playerHealth == null))
        {
            _warnedMissingRefs = true;
            Debug.LogWarning("[HUD] PlayerStatsHudPanel missing refs (PlayerCombatStats or PlayerHealth); polling until available.", this);
        }
    }

    private void HookEquipmentChanged()
    {
        if (_equipment != null)
            return;

        PlayerEquipment equipment = null;

        if (combatStats != null)
        {
            try { equipment = combatStats.GetComponentInParent<PlayerEquipment>(); } catch { equipment = null; }
            if (equipment == null)
            {
                try { equipment = combatStats.GetComponent<PlayerEquipment>(); } catch { equipment = null; }
            }
        }

        if (equipment == null && playerHealth != null)
        {
            try { equipment = playerHealth.GetComponentInParent<PlayerEquipment>(); } catch { equipment = null; }
            if (equipment == null)
            {
                try { equipment = playerHealth.GetComponent<PlayerEquipment>(); } catch { equipment = null; }
            }
        }

        if (equipment == null)
        {
            try { equipment = PlayerEquipmentResolver.GetOrFindOrCreate(); } catch { equipment = null; }
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
            catch { equipment = null; }
        }

        if (equipment == null)
            return;

        _equipment = equipment;
        _equipment.Changed -= OnEquipmentChanged;
        _equipment.Changed += OnEquipmentChanged;
    }

    private void HookHealthChanged()
    {
        if (playerHealth == null)
            return;

        playerHealth.HealthChanged -= OnHealthChanged;
        playerHealth.HealthChanged += OnHealthChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        Refresh();
    }

    private void OnEquipmentChanged()
    {
        ResolveRefs();
        Refresh();
    }

    private void ApplyStyle()
    {
        ApplyTextStyle(drText, 22f, statsTextColor);

        if (drText != null)
            drText.text = "DR: ?";
    }

    private static void ApplyTextStyle(TMP_Text t, float fontSize, Color32 color)
    {
        if (t == null)
            return;

        t.color = color;

        if (t is TextMeshProUGUI ugui)
        {
            ugui.fontSize = fontSize;
            ugui.alignment = TextAlignmentOptions.TopRight;
            ugui.textWrappingMode = TextWrappingModes.NoWrap;
            ugui.raycastTarget = false;

            // High-contrast outline.
            try
            {
                var mat = ugui.fontMaterial;
                if (mat != null)
                {
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, s_OutlineColor);
                    ugui.fontMaterial = mat;
                }
            }
            catch { }
        }
    }

    public void Refresh()
    {
        // Keep colors stable in Play Mode (avoid editor tweaks being overwritten by other runtime code).
        if (drText != null) { try { drText.color = statsTextColor; } catch { } }

        if (!_warnedMissingRefs && drText == null)
        {
            _warnedMissingRefs = true;
            Debug.LogWarning("[HUD] PlayerStatsHudPanel missing text refs (DR).", this);
        }

        if (drText != null)
        {
            if (playerHealth != null)
                drText.text = $"DR: {playerHealth.TotalDamageReductionFlat}";
            else
                drText.text = "DR: ?";
        }
    }
}
