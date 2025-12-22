using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Abyss.Items;

[DisallowMultipleComponent]
public sealed class ItemTooltipUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMP_Text statsText;

    [Header("Layout (Inspector Driven)")]
    [SerializeField] private int padding = 16;
    [SerializeField] private float spacing = 8f;
    [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.88f;
    [Tooltip("Preferred max width for wrapping (0 = no preference).")]
    [SerializeField] private float preferredMaxWidth = 340f;

    [Header("Text (Inspector Driven)")]
    [SerializeField] private float titleFontSize = 24f;
    [SerializeField] private float subFontSize = 17f;
    [SerializeField] private float statFontSize = 17f;
    [SerializeField] private float statLineSpacing = 6f;

    [Header("Behavior")]
    [SerializeField] private Vector2 screenOffset = new(16f, -16f);

    private Canvas _canvas;
    private RectTransform _canvasRect;

    private bool _visible;
    private Object _currentOwner;

    private static readonly StringBuilder s_Sb = new(256);

    public static ItemTooltipUI GetOrCreateUnder(Transform uiRoot)
    {
        if (uiRoot == null)
            return null;

        var existing = uiRoot.GetComponentInChildren<ItemTooltipUI>(true);
        if (existing != null)
            return existing;

        // Create a minimal tooltip UI hierarchy at runtime (no scene hacks required).
        var go = new GameObject("ItemTooltipUI", typeof(RectTransform), typeof(CanvasGroup), typeof(ItemTooltipUI));
        go.transform.SetParent(uiRoot, worldPositionStays: false);

        var ui = go.GetComponent<ItemTooltipUI>();
        ui.BuildRuntimeVisualTree();
        ui.Hide();
        return ui;
    }

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

        if (panel == null)
            panel = transform as RectTransform;

        // Disabled by default.
        if (panel != null && panel.gameObject.activeSelf)
            panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_visible)
            return;

        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
        }

        if (_canvasRect == null || panel == null)
            return;

        Vector2 screen = (Vector2)Input.mousePosition + screenOffset;

        // Convert screen -> local position on the canvas.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera, out var local))
        {
            panel.anchoredPosition = local;
            panel.SetAsLastSibling();
        }
    }

    public void Show(Object owner, ItemDefinition def, string fallbackItemId, int count, EquipmentSlot slotContext = EquipmentSlot.None)
    {
        _currentOwner = owner;

        ApplyReadabilitySettings();

        string name = ResolveName(def, fallbackItemId);
        if (titleText != null)
            titleText.text = name;

        if (subText != null)
        {
            // Type / slot line (only if available)
            string typeStr = def != null ? def.itemType.ToString() : string.Empty;
            EquipmentSlot slot = def != null ? def.equipmentSlot : slotContext;

            if (slot != EquipmentSlot.None && !string.IsNullOrEmpty(typeStr))
                subText.text = $"{typeStr} • {slot}";
            else if (slot != EquipmentSlot.None)
                subText.text = slot.ToString();
            else if (!string.IsNullOrEmpty(typeStr))
                subText.text = typeStr;
            else
                subText.text = string.Empty;
        }

        if (statsText != null)
        {
            statsText.text = BuildStats(def, count, extraLines: null);
            statsText.gameObject.SetActive(!string.IsNullOrEmpty(statsText.text));
        }

        if (panel != null && !panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        _visible = true;
    }

    public void ShowExtended(
        Object owner,
        ItemDefinition def,
        string fallbackItemId,
        int count,
        EquipmentSlot slotContext,
        string rarityLine,
        string extraStatLines)
    {
        _currentOwner = owner;

        ApplyReadabilitySettings();

        string name = ResolveName(def, fallbackItemId);
        if (titleText != null)
            titleText.text = name;

        if (subText != null)
        {
            string typeStr = def != null ? def.itemType.ToString() : string.Empty;
            EquipmentSlot slot = def != null ? def.equipmentSlot : slotContext;

            s_Sb.Clear();

            if (!string.IsNullOrWhiteSpace(rarityLine))
                s_Sb.Append(rarityLine);

            if (!string.IsNullOrWhiteSpace(typeStr))
            {
                if (s_Sb.Length > 0) s_Sb.Append("  ");
                s_Sb.Append(typeStr);
            }

            if (slot != EquipmentSlot.None)
            {
                if (s_Sb.Length > 0) s_Sb.Append("  ");
                s_Sb.Append(slot);
            }

            subText.text = s_Sb.ToString();
        }

        if (statsText != null)
        {
            statsText.text = BuildStats(def, count, extraStatLines);
            statsText.gameObject.SetActive(!string.IsNullOrEmpty(statsText.text));
        }

        if (panel != null && !panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        _visible = true;
    }

    public void ShowLootInstance(Object owner, Abyssbound.Loot.ItemInstance instance, Abyssbound.Loot.LootRegistryRuntime registry)
    {
        _currentOwner = owner;
        ApplyReadabilitySettings();

        if (instance == null || registry == null)
        {
            Show(owner, null, "(Invalid Item)", 0, EquipmentSlot.None);
            return;
        }

        Abyssbound.Loot.ItemDefinitionSO baseItem = null;
        Abyssbound.Loot.RarityDefinitionSO rarity = null;
        try { registry.TryGetItem(instance.baseItemId, out baseItem); } catch { baseItem = null; }
        try { registry.TryGetRarity(instance.rarityId, out rarity); } catch { rarity = null; }

        string title = baseItem != null
            ? (string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName)
            : (string.IsNullOrWhiteSpace(instance.baseItemId) ? "(Unknown Item)" : instance.baseItemId);

        if (titleText != null)
            titleText.text = title;

        string rarityLine = rarity != null
            ? (string.IsNullOrWhiteSpace(rarity.displayName) ? rarity.id : rarity.displayName)
            : (string.IsNullOrWhiteSpace(instance.rarityId) ? string.Empty : instance.rarityId);

        if (subText != null)
        {
            string slot = baseItem != null ? baseItem.slot.ToString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(rarityLine) && !string.IsNullOrWhiteSpace(slot))
                subText.text = $"{rarityLine}  {slot}";
            else if (!string.IsNullOrWhiteSpace(rarityLine))
                subText.text = rarityLine;
            else
                subText.text = slot;
        }

        if (statsText != null)
        {
            statsText.text = BuildLootStats(instance, registry);
            statsText.gameObject.SetActive(!string.IsNullOrEmpty(statsText.text));
        }

        if (panel != null && !panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        _visible = true;
    }

    public void Hide(Object owner = null)
    {
        if (owner != null && _currentOwner != null && !ReferenceEquals(owner, _currentOwner))
            return;

        _currentOwner = null;
        _visible = false;

        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    private static string ResolveName(ItemDefinition def, string fallbackItemId)
    {
        if (def != null)
        {
            if (!string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;
            if (!string.IsNullOrWhiteSpace(def.itemId))
                return def.itemId;
            if (!string.IsNullOrWhiteSpace(def.name))
                return def.name;
        }

        return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown Item)" : fallbackItemId;
    }

    private static string BuildStats(ItemDefinition def, int count, string extraLines)
    {
        s_Sb.Clear();

        if (def != null)
        {
            // Only show lines that exist (non-zero).
            try
            {
                if (def.DamageBonus != 0)
                    s_Sb.Append("Damage ").Append(def.DamageBonus > 0 ? "+" : "").Append(def.DamageBonus).Append('\n');
            }
            catch { }

            try
            {
                if (def.DamageReductionFlat != 0)
                    s_Sb.Append("Defense ").Append(def.DamageReductionFlat > 0 ? "+" : "").Append(def.DamageReductionFlat).Append('\n');
            }
            catch { }

            try
            {
                if (def.MaxHealthBonus != 0)
                    s_Sb.Append("Health ").Append(def.MaxHealthBonus > 0 ? "+" : "").Append(def.MaxHealthBonus).Append('\n');
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(extraLines))
        {
            // Separate base stats from affixes (when both exist).
            if (s_Sb.Length > 0 && s_Sb[s_Sb.Length - 1] != '\n')
                s_Sb.Append('\n');

            if (s_Sb.Length > 0)
                s_Sb.Append('\n');

            s_Sb.Append(extraLines).Append('\n');
        }

        // Quantity for inventory items (optional, but useful).
        if (count > 1)
            s_Sb.Append("Count ").Append(count).Append('\n');

        // Trim trailing newline.
        if (s_Sb.Length > 0 && s_Sb[s_Sb.Length - 1] == '\n')
            s_Sb.Length -= 1;

        return s_Sb.ToString();
    }

    private static string BuildLootStats(Abyssbound.Loot.ItemInstance instance, Abyssbound.Loot.LootRegistryRuntime registry)
    {
        if (instance == null || registry == null)
            return string.Empty;

        s_Sb.Clear();

        var mods = instance.GetAllStatMods(registry);
        if (mods == null || mods.Count == 0)
            return string.Empty;

        for (int i = 0; i < mods.Count; i++)
        {
            var m = mods[i];
            string label = m.stat.ToString();
            float v = m.value;

            // Simple readable labels for common stats.
            switch (m.stat)
            {
                case Abyssbound.Loot.StatType.MeleeDamage: label = "Melee Damage"; break;
                case Abyssbound.Loot.StatType.RangedDamage: label = "Ranged Damage"; break;
                case Abyssbound.Loot.StatType.MagicDamage: label = "Magic Damage"; break;
                case Abyssbound.Loot.StatType.MaxHealth: label = "Health"; break;
                case Abyssbound.Loot.StatType.Defense: label = "Defense"; break;
                case Abyssbound.Loot.StatType.AttackSpeed: label = "Attack Speed"; break;
                case Abyssbound.Loot.StatType.MoveSpeed: label = "Move Speed"; break;
                case Abyssbound.Loot.StatType.DefenseSkill: label = "Defense Skill"; break;
                case Abyssbound.Loot.StatType.RangedSkill: label = "Ranged Skill"; break;
                case Abyssbound.Loot.StatType.MagicSkill: label = "Magic Skill"; break;
                case Abyssbound.Loot.StatType.MeleeSkill: label = "Melee Skill"; break;
            }

            if (m.percent)
            {
                // Stored only for now; display as percent.
                s_Sb.Append(label).Append(' ').Append(v >= 0 ? "+" : "").Append(v.ToString("0.##")).Append('%').Append('\n');
            }
            else
            {
                s_Sb.Append(label).Append(' ').Append(v >= 0 ? "+" : "").Append(v.ToString("0.##")).Append('\n');
            }
        }

        if (s_Sb.Length > 0 && s_Sb[s_Sb.Length - 1] == '\n')
            s_Sb.Length -= 1;

        return s_Sb.ToString();
    }

    private void BuildRuntimeVisualTree()
    {
        // Minimal layout: Panel(Image) -> (VerticalLayoutGroup) -> Title/Sub/Stats
        var rt = transform as RectTransform;
        if (rt == null)
            return;

        panel = rt;

        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.raycastTarget = false;
        bg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(backgroundAlpha));

        var layout = gameObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = gameObject.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.Clamp(padding, 0, 64);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = Mathf.Max(0f, spacing);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        var fitter = gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = gameObject.GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredMaxWidth > 0f ? preferredMaxWidth : -1f;

        titleText = CreateText("Title", fontSize: Mathf.RoundToInt(titleFontSize), bold: true);
        subText = CreateText("Sub", fontSize: Mathf.RoundToInt(subFontSize), bold: false);
        statsText = CreateText("Stats", fontSize: Mathf.RoundToInt(statFontSize), bold: false);

        statsText.gameObject.SetActive(false);

        ApplyReadabilitySettings();

        // Reasonable anchor/pivot for mouse-follow.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);

        TMP_Text CreateText(string name, int fontSize, bool bold)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(transform, worldPositionStays: false);

            var t = child.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            t.fontSize = fontSize;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.text = string.Empty;
            t.color = Color.white;
            if (bold) t.fontStyle = FontStyles.Bold;

            return t;
        }
    }

    private void ApplyReadabilitySettings()
    {
        // UI-only: apply TMP styling when showing (no per-frame resizing).
        try
        {
            if (panel == null)
                panel = transform as RectTransform;

            var bg = GetComponent<Image>();
            if (bg != null)
            {
                var c = bg.color;
                bg.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(backgroundAlpha));
            }

            var layout = GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                int pad = Mathf.Clamp(padding, 0, 64);
                layout.padding = new RectOffset(pad, pad, pad, pad);
                layout.spacing = Mathf.Max(0f, spacing);
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
            }

            var le = GetComponent<LayoutElement>();
            if (le != null)
                le.preferredWidth = preferredMaxWidth > 0f ? preferredMaxWidth : -1f;

            if (titleText != null)
            {
                titleText.fontSize = Mathf.Max(8f, titleFontSize);
                titleText.fontStyle = FontStyles.Bold;
                titleText.textWrappingMode = TextWrappingModes.Normal;
            }

            if (subText != null)
            {
                subText.fontSize = Mathf.Max(8f, subFontSize);
                subText.fontStyle = FontStyles.Normal;
                subText.textWrappingMode = TextWrappingModes.Normal;
            }

            if (statsText != null)
            {
                statsText.fontSize = Mathf.Max(8f, statFontSize);
                statsText.lineSpacing = Mathf.Clamp(statLineSpacing, 0f, 20f);
                statsText.textWrappingMode = TextWrappingModes.Normal;
            }
        }
        catch
        {
            // silent
        }
    }
}
