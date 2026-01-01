using System.Collections.Generic;
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
    [SerializeField] private Image iconImage;
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

    private bool _capturedDefaultColors;
    private Color _titleDefaultColor;
    private Color _subDefaultColor;

    private bool _visible;
    private Object _currentOwner;

    // Prevent immediate re-show (e.g., pointer still over slot during rebuild).
    private int _suppressShowUntilFrame;

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

        CaptureDefaultTextColorsIfNeeded();
    }

    private void CaptureDefaultTextColorsIfNeeded()
    {
        if (_capturedDefaultColors)
            return;

        _capturedDefaultColors = true;
        try { _titleDefaultColor = titleText != null ? titleText.color : Color.white; } catch { _titleDefaultColor = Color.white; }
        try { _subDefaultColor = subText != null ? subText.color : Color.white; } catch { _subDefaultColor = Color.white; }
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
        if (Time.frameCount <= _suppressShowUntilFrame)
            return;

        _currentOwner = owner;

        CaptureDefaultTextColorsIfNeeded();

        ApplyReadabilitySettings();

        try
        {
            Sprite icon = null;
            try { icon = def != null ? def.icon : null; } catch { icon = null; }
            ApplyIcon(icon);
        }
        catch { }

        string name = ResolveName(def, fallbackItemId);
        if (titleText != null)
        {
            titleText.text = name;
            try
            {
                var r = def != null ? ItemRarityVisuals.Normalize(def.rarity) : Abyss.Items.ItemRarity.Common;
                titleText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(r, _titleDefaultColor);
            }
            catch
            {
                titleText.color = _titleDefaultColor;
            }
        }

        if (subText != null)
        {
            // Type / slot line (only if available)
            string typeStr = def != null ? def.itemType.ToString() : string.Empty;
            EquipmentSlot slot = def != null ? def.equipmentSlot : slotContext;

            bool isTwoHanded = false;
            try { isTwoHanded = def != null && def.weaponHandedness == WeaponHandedness.TwoHanded; } catch { isTwoHanded = false; }

            if (slot != EquipmentSlot.None && !string.IsNullOrEmpty(typeStr))
                subText.text = isTwoHanded ? $"{typeStr} • {slot} • Two-Handed" : $"{typeStr} • {slot}";
            else if (slot != EquipmentSlot.None)
                subText.text = isTwoHanded ? $"{slot} • Two-Handed" : slot.ToString();
            else if (!string.IsNullOrEmpty(typeStr))
                subText.text = isTwoHanded ? $"{typeStr} • Two-Handed" : typeStr;
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
        if (Time.frameCount <= _suppressShowUntilFrame)
            return;

        _currentOwner = owner;

        CaptureDefaultTextColorsIfNeeded();

        ApplyReadabilitySettings();

        try
        {
            Sprite icon = null;
            try { icon = def != null ? def.icon : null; } catch { icon = null; }
            ApplyIcon(icon);
        }
        catch { }

        string name = ResolveName(def, fallbackItemId);
        if (titleText != null)
        {
            titleText.text = name;
            try
            {
                var r = def != null ? ItemRarityVisuals.Normalize(def.rarity) : Abyss.Items.ItemRarity.Common;
                titleText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(r, _titleDefaultColor);
            }
            catch
            {
                titleText.color = _titleDefaultColor;
            }
        }

        if (subText != null)
        {
            string typeStr = def != null ? def.itemType.ToString() : string.Empty;
            EquipmentSlot slot = def != null ? def.equipmentSlot : slotContext;

            bool isTwoHanded = false;
            try { isTwoHanded = def != null && def.weaponHandedness == WeaponHandedness.TwoHanded; } catch { isTwoHanded = false; }

            s_Sb.Clear();

            if (!string.IsNullOrWhiteSpace(rarityLine))
                s_Sb.Append(rarityLine);

            if (!string.IsNullOrWhiteSpace(typeStr))
            {
                if (s_Sb.Length > 0) s_Sb.Append("  |  ");
                s_Sb.Append(typeStr);
            }

            if (slot != EquipmentSlot.None)
            {
                if (s_Sb.Length > 0) s_Sb.Append("  |  ");
                s_Sb.Append(slot);
            }

            if (isTwoHanded)
            {
                if (s_Sb.Length > 0) s_Sb.Append("  |  ");
                s_Sb.Append("Two-Handed");
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
        if (Time.frameCount <= _suppressShowUntilFrame)
            return;

        _currentOwner = owner;
        ApplyReadabilitySettings();

        CaptureDefaultTextColorsIfNeeded();

        if (instance == null || registry == null)
        {
            Show(owner, null, "(Invalid Item)", 0, EquipmentSlot.None);
            return;
        }

        Abyssbound.Loot.ItemDefinitionSO baseItem = null;
        Abyssbound.Loot.RarityDefinitionSO rarity = null;
        try { registry.TryGetItem(instance.baseItemId, out baseItem); } catch { baseItem = null; }
        try { registry.TryGetRarity(instance.rarityId, out rarity); } catch { rarity = null; }

        try
        {
            Sprite icon = baseItem != null ? baseItem.icon : null;
            ApplyIcon(icon);
        }
        catch { }

        string title = baseItem != null
            ? (string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName)
            : (string.IsNullOrWhiteSpace(instance.baseItemId) ? "(Unknown Item)" : instance.baseItemId);

        if (titleText != null)
        {
            titleText.text = title;
            try
            {
                titleText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(instance.rarityId, _titleDefaultColor);
            }
            catch
            {
                titleText.color = _titleDefaultColor;
            }
        }

        string rarityLine = rarity != null
            ? (string.IsNullOrWhiteSpace(rarity.displayName) ? rarity.id : rarity.displayName)
            : (string.IsNullOrWhiteSpace(instance.rarityId) ? string.Empty : instance.rarityId);

        if (subText != null)
        {
            string slot = baseItem != null ? baseItem.slot.ToString() : string.Empty;

            bool isTwoHanded = false;
            try
            {
                if (baseItem != null && baseItem.occupiesSlots != null && baseItem.occupiesSlots.Count > 0)
                {
                    bool hasL = false;
                    bool hasR = false;
                    for (int i = 0; i < baseItem.occupiesSlots.Count; i++)
                    {
                        if (baseItem.occupiesSlots[i] == EquipmentSlot.LeftHand) hasL = true;
                        if (baseItem.occupiesSlots[i] == EquipmentSlot.RightHand) hasR = true;
                    }
                    isTwoHanded = hasL && hasR;
                }
            }
            catch { isTwoHanded = false; }

            s_Sb.Clear();

            if (!string.IsNullOrWhiteSpace(rarityLine))
            {
                // Only tint the rarity token; keep slot/extra text default for readability.
                var rarityColor = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(instance.rarityId, _subDefaultColor);
                var hex = Abyssbound.Loot.RarityColorMap.ToHtmlRgb(rarityColor);
                s_Sb.Append("<color=#").Append(hex).Append('>').Append(rarityLine).Append("</color>");
            }

            if (!string.IsNullOrWhiteSpace(slot))
            {
                if (s_Sb.Length > 0) s_Sb.Append("  |  ");
                s_Sb.Append(slot);
            }

            if (isTwoHanded)
            {
                if (s_Sb.Length > 0) s_Sb.Append("  |  ");
                s_Sb.Append("Two-Handed");
            }

            subText.text = s_Sb.ToString();
        }

        if (statsText != null)
        {
            string body = BuildLootStats(instance, registry);

            // Always show item level for Loot V2 instances.
            int ilvl = 1;
            try { ilvl = (instance != null && instance.itemLevel > 0) ? instance.itemLevel : 1; } catch { ilvl = 1; }
            string ilvlLine = $"iLvl: {ilvl}";
            if (!string.IsNullOrWhiteSpace(body))
                body = body + "\n" + ilvlLine;
            else
                body = ilvlLine;

            // Set info + bonuses
            try
            {
                var set = baseItem != null ? baseItem.set : null;
                if (set != null)
                {
                    var tracker = Abyssbound.Loot.EquippedSetTracker.GetOrCreate();
                    int equipped = tracker != null ? tracker.GetEquippedSetCount(set) : 0;
                    int total = tracker != null ? tracker.GetTotalSetPieces(set) : (set.pieces != null ? set.pieces.Count : 0);

                    string setName = string.IsNullOrWhiteSpace(set.displayName) ? set.setId : set.displayName;
                    if (string.IsNullOrWhiteSpace(setName)) setName = set.name;

                    if (!string.IsNullOrWhiteSpace(body))
                        body += "\n\n";

                    body += $"{setName} ({equipped}/{total})";

                    if (set.pieces != null)
                    {
                        for (int i = 0; i < set.pieces.Count; i++)
                        {
                            var piece = set.pieces[i];
                            if (piece == null) continue;

                            string pieceName = !string.IsNullOrWhiteSpace(piece.displayName) ? piece.displayName : piece.id;
                            if (string.IsNullOrWhiteSpace(pieceName)) pieceName = piece.name;

                            bool isEquipped = tracker != null && tracker.IsBaseItemEquipped(piece.id);
                            body += "\n" + (isEquipped ? "[X] " : "[ ] ") + pieceName;
                        }
                    }

                    // Tier bonuses (Phase 2)
                    if (set.bonuses != null && set.bonuses.Count > 0)
                    {
                        body += "\n\nSet Bonuses";

                        // Display in ascending requiredPieces order.
                        var tiers = set.bonuses;
                        var ordered = new List<Abyssbound.Loot.ItemSetDefinitionSO.SetBonusTier>(tiers.Count);
                        for (int i = 0; i < tiers.Count; i++) if (tiers[i] != null) ordered.Add(tiers[i]);
                        ordered.Sort((a, b) => a.requiredPieces.CompareTo(b.requiredPieces));

                        for (int i = 0; i < ordered.Count; i++)
                        {
                            var tier = ordered[i];
                            int req = tier.requiredPieces;
                            if (req <= 0) continue;

                            bool active = equipped >= req;
                            string status = active ? "ACTIVE" : "LOCKED";
                            string desc = !string.IsNullOrWhiteSpace(tier.description)
                                ? tier.description
                                : Abyssbound.Loot.SetBonusRuntime.FormatMods(tier.modifiers);

                            if (string.IsNullOrWhiteSpace(desc))
                                desc = "(No bonus)";

                            body += $"\n{status} {req}pc: {desc}";
                        }
                    }
                }
            }
            catch { }

            statsText.text = body;
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

        try { ApplyIcon(null); } catch { }
    }

    // Public UX hook: hide immediately after consuming an item (double-click use, teleport, etc).
    // This intentionally does not require an owner match.
    public void HideTooltip()
    {
        Hide();
    }

    // Hard stop: hide and suppress showing until next frame so it can't instantly reappear
    // during selection changes / list rebuild while the pointer is still hovering.
    public void HideAndClear()
    {
        try { _suppressShowUntilFrame = Time.frameCount + 1; } catch { _suppressShowUntilFrame = int.MaxValue; }
        Hide();
    }

    private void ApplyIcon(Sprite sprite)
    {
        if (iconImage == null)
            return;

        bool has = sprite != null;
        iconImage.sprite = sprite;
        iconImage.enabled = has;
        if (iconImage.gameObject.activeSelf != has)
            iconImage.gameObject.SetActive(has);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.color = Color.white;
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
                    s_Sb.Append("Max Health ").Append(def.MaxHealthBonus > 0 ? "+" : "").Append(def.MaxHealthBonus).Append('\n');
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

        // Base stats first
        if (!string.IsNullOrWhiteSpace(instance.baseItemId) && registry.TryGetItem(instance.baseItemId, out var baseItem) && baseItem != null)
        {
            try
            {
                if (baseItem.baseStats != null)
                {
                    float scalar = Mathf.Max(0f, instance.baseScalar);
                    for (int i = 0; i < baseItem.baseStats.Count; i++)
                    {
                        var m = baseItem.baseStats[i];
                        AppendStatLine(m.stat, m.value * scalar, m.percent);
                    }
                }
            }
            catch { }
        }

        // Affixes second
        if (instance.affixes != null && instance.affixes.Count > 0)
        {
            bool wroteBase = s_Sb.Length > 0;
            if (wroteBase) s_Sb.Append('\n');

            for (int i = 0; i < instance.affixes.Count; i++)
            {
                var roll = instance.affixes[i];
                if (string.IsNullOrWhiteSpace(roll.affixId)) continue;
                if (!registry.TryGetAffix(roll.affixId, out var affixDef) || affixDef == null) continue;
                AppendStatLine(affixDef.stat, roll.value, affixDef.percent);
            }
        }

        if (s_Sb.Length > 0 && s_Sb[s_Sb.Length - 1] == '\n')
            s_Sb.Length -= 1;

        return s_Sb.ToString();
    }

    private static void AppendStatLine(Abyssbound.Loot.StatType stat, float value, bool percent)
    {
        string label = stat.ToString();

        switch (stat)
        {
            case Abyssbound.Loot.StatType.MeleeDamage: label = "Melee Damage"; break;
            case Abyssbound.Loot.StatType.RangedDamage: label = "Ranged Damage"; break;
            case Abyssbound.Loot.StatType.MagicDamage: label = "Magic Damage"; break;
            case Abyssbound.Loot.StatType.MaxHealth: label = "Max Health"; break;
            case Abyssbound.Loot.StatType.Defense: label = "Defense"; break;
            case Abyssbound.Loot.StatType.AttackSpeed: label = "Attack Speed"; break;
            case Abyssbound.Loot.StatType.MoveSpeed: label = "Move Speed"; break;
            case Abyssbound.Loot.StatType.DefenseSkill: label = "Defense Skill"; break;
            case Abyssbound.Loot.StatType.RangedSkill: label = "Ranged Skill"; break;
            case Abyssbound.Loot.StatType.MagicSkill: label = "Magic Skill"; break;
            case Abyssbound.Loot.StatType.MeleeSkill: label = "Melee Skill"; break;
        }

        if (percent)
            s_Sb.Append(label).Append(' ').Append(value >= 0 ? "+" : "").Append(value.ToString("0.##")).Append('%').Append('\n');
        else
            s_Sb.Append(label).Append(' ').Append(value >= 0 ? "+" : "").Append(value.ToString("0.##")).Append('\n');
    }

    private static string FormatSlotName(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.LeftHand: return "Left Hand";
            case EquipmentSlot.RightHand: return "Right Hand";
            default: return slot.ToString();
        }
    }

    private static string BuildOccupiesLine(Abyssbound.Loot.ItemDefinitionSO baseItem)
    {
        if (baseItem == null || baseItem.occupiesSlots == null) return string.Empty;

        bool hasL = false;
        bool hasR = false;
        var parts = new System.Collections.Generic.List<string>(4);

        for (int i = 0; i < baseItem.occupiesSlots.Count; i++)
        {
            var s = baseItem.occupiesSlots[i];
            if (s == EquipmentSlot.LeftHand) hasL = true;
            else if (s == EquipmentSlot.RightHand) hasR = true;
            else if (s != EquipmentSlot.None) parts.Add(FormatSlotName(s));
        }

        // Stable, readable ordering for hands.
        if (hasL) parts.Insert(0, "Left Hand");
        if (hasR) parts.Insert(hasL ? 1 : 0, "Right Hand");

        if (parts.Count == 0) return string.Empty;
        return "Occupies: " + string.Join(", ", parts);
    }

    private static string TrimTrailingNewline(StringBuilder sb)
    {
        if (sb == null || sb.Length == 0) return string.Empty;
        if (sb[sb.Length - 1] == '\n') sb.Length -= 1;
        return sb.ToString();
    }

    private void BuildRuntimeVisualTree()
    {
        // Minimal layout: Panel(Image) -> (VerticalLayoutGroup) -> (Icon optional) -> Title/Sub/Stats
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

        // Optional icon (only shown when an icon exists)
        try
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(transform, worldPositionStays: false);
            iconImage = iconGo.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            iconGo.SetActive(false);

            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 48f;
            iconLe.preferredHeight = 48f;
            iconLe.minWidth = 48f;
            iconLe.minHeight = 48f;
        }
        catch { iconImage = null; }

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
