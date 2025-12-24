using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;
using Abyssbound.Loot;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Inventory
{
    public sealed class PlayerInventoryDetailsUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text descriptionText;

        private bool _capturedDefaultColors;
        private Color _nameDefaultColor;
        private Color _rarityDefaultColor;

        private void CaptureDefaultColorsIfNeeded()
        {
            if (_capturedDefaultColors)
                return;

            _capturedDefaultColors = true;
            try { _nameDefaultColor = nameText != null ? nameText.color : Color.white; } catch { _nameDefaultColor = Color.white; }
            try { _rarityDefaultColor = rarityText != null ? rarityText.color : Color.white; } catch { _rarityDefaultColor = Color.white; }
        }

        public void Clear()
        {
            CaptureDefaultColorsIfNeeded();
            ApplyEmptyState();
        }

        public void Set(ItemDefinition def, string fallbackItemId, int count)
        {
            CaptureDefaultColorsIfNeeded();

            if (def == null && string.IsNullOrWhiteSpace(fallbackItemId))
            {
                ApplyEmptyState();
                return;
            }

            string displayName = def != null
                ? (string.IsNullOrWhiteSpace(def.displayName) ? ResolveFallbackName(def, fallbackItemId) : def.displayName)
                : (string.IsNullOrWhiteSpace(fallbackItemId) ? string.Empty : fallbackItemId);

            var normalizedRarity = def != null ? ItemRarityVisuals.Normalize(def.rarity) : AbyssItemRarity.Common;

            if (nameText != null)
            {
                nameText.text = displayName ?? string.Empty;
                nameText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(normalizedRarity, _nameDefaultColor);
            }

            if (countText != null)
                countText.text = $"Count: {Mathf.Max(0, count)}";

            if (rarityText != null)
            {
                if (def == null && string.IsNullOrWhiteSpace(fallbackItemId))
                {
                    rarityText.text = string.Empty;
                }
                else
                {
                    var rarityStr = ItemRarityVisuals.ToDisplayString(normalizedRarity);
                    string slotStr = string.Empty;
                    try
                    {
                        if (def != null && def.equipmentSlot != EquipmentSlot.None)
                            slotStr = def.equipmentSlot.ToString();
                    }
                    catch { slotStr = string.Empty; }

                    rarityText.text = string.IsNullOrWhiteSpace(slotStr)
                        ? $"Rarity: {rarityStr}"
                        : $"Rarity: {rarityStr} | Slot: {slotStr}";
                }

                // Keep readability: tint only the rarity line, not the long description/stats.
                rarityText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(normalizedRarity, _rarityDefaultColor);
            }

            if (descriptionText != null)
                descriptionText.text = def != null
                    ? (string.IsNullOrWhiteSpace(def.description) ? "No description." : def.description)
                    : "No description.";

            if (iconImage != null)
            {
                var icon = def != null ? def.icon : null;
                bool hasIcon = icon != null;
                iconImage.sprite = icon;
                iconImage.enabled = hasIcon;
                if (iconImage.gameObject.activeSelf != hasIcon)
                    iconImage.gameObject.SetActive(hasIcon);
            }
        }

        public void SetLootInstance(ItemInstance instance, LootRegistryRuntime registry, int count)
        {
            CaptureDefaultColorsIfNeeded();

            if (instance == null || registry == null)
            {
                ApplyEmptyState();
                return;
            }

            registry.TryGetItem(instance.baseItemId, out var baseItem);
            registry.TryGetRarity(instance.rarityId, out var rarity);

            string displayName = baseItem != null
                ? (!string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.displayName : baseItem.id)
                : (!string.IsNullOrWhiteSpace(instance.baseItemId) ? instance.baseItemId : "(Unknown Item)");

            if (nameText != null)
            {
                nameText.text = displayName ?? string.Empty;
                nameText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(instance.rarityId, _nameDefaultColor);
            }

            if (countText != null)
                countText.text = $"Count: {Mathf.Max(0, count)}";

            if (rarityText != null)
            {
                string rarityStr = rarity != null
                    ? (!string.IsNullOrWhiteSpace(rarity.displayName) ? rarity.displayName : rarity.id)
                    : (!string.IsNullOrWhiteSpace(instance.rarityId) ? instance.rarityId : "");

                string slotStr = string.Empty;
                try { slotStr = baseItem != null ? baseItem.slot.ToString() : string.Empty; } catch { slotStr = string.Empty; }

                if (!string.IsNullOrWhiteSpace(rarityStr) && !string.IsNullOrWhiteSpace(slotStr))
                    rarityText.text = $"Rarity: {rarityStr} | Slot: {slotStr}";
                else if (!string.IsNullOrWhiteSpace(rarityStr))
                    rarityText.text = $"Rarity: {rarityStr}";
                else if (!string.IsNullOrWhiteSpace(slotStr))
                    rarityText.text = $"Slot: {slotStr}";
                else
                    rarityText.text = string.Empty;

                rarityText.color = Abyssbound.Loot.RarityColorMap.GetColorOrDefault(instance.rarityId, _rarityDefaultColor);
            }

            if (iconImage != null)
            {
                var icon = baseItem != null ? baseItem.icon : null;
                bool hasIcon = icon != null;
                iconImage.sprite = icon;
                iconImage.enabled = hasIcon;
                if (iconImage.gameObject.activeSelf != hasIcon)
                    iconImage.gameObject.SetActive(hasIcon);
            }

            if (descriptionText != null)
            {
                var sb = new System.Text.StringBuilder(256);
                int ilvl = instance.itemLevel > 0 ? instance.itemLevel : 1;
                sb.Append("iLvl: ").Append(ilvl);

                // Base stats first
                if (baseItem != null && baseItem.baseStats != null && baseItem.baseStats.Count > 0)
                {
                    float scalar = Mathf.Max(0f, instance.baseScalar);
                    sb.Append("\n\n");
                    for (int i = 0; i < baseItem.baseStats.Count; i++)
                    {
                        var m = baseItem.baseStats[i];
                        AppendStatLine(sb, m.stat, m.value * scalar, m.percent);
                    }
                }

                // Affixes second
                if (instance.affixes != null && instance.affixes.Count > 0)
                {
                    // Separate base stats from affixes only if base stats were written.
                    if (baseItem != null && baseItem.baseStats != null && baseItem.baseStats.Count > 0)
                        sb.Append("\n");
                    else
                        sb.Append("\n\n");

                    for (int i = 0; i < instance.affixes.Count; i++)
                    {
                        var roll = instance.affixes[i];
                        if (string.IsNullOrWhiteSpace(roll.affixId)) continue;
                        if (!registry.TryGetAffix(roll.affixId, out var affixDef) || affixDef == null) continue;
                        AppendStatLine(sb, affixDef.stat, roll.value, affixDef.percent);
                    }
                }

                var set = baseItem != null ? baseItem.set : null;
                if (set != null)
                {
                    var tracker = EquippedSetTracker.GetOrCreate();
                    int equipped = tracker != null ? tracker.GetEquippedSetCount(set) : 0;
                    int total = tracker != null ? tracker.GetTotalSetPieces(set) : (set.pieces != null ? set.pieces.Count : 0);

                    string setName = !string.IsNullOrWhiteSpace(set.displayName) ? set.displayName : set.setId;
                    if (string.IsNullOrWhiteSpace(setName)) setName = set.name;

                    sb.Append("\n\n");
                    sb.Append(setName).Append(" (").Append(equipped).Append('/').Append(total).Append(')');

                    if (set.pieces != null)
                    {
                        for (int i = 0; i < set.pieces.Count; i++)
                        {
                            var piece = set.pieces[i];
                            if (piece == null) continue;

                            string pieceName = !string.IsNullOrWhiteSpace(piece.displayName) ? piece.displayName : piece.id;
                            if (string.IsNullOrWhiteSpace(pieceName)) pieceName = piece.name;

                            bool isEquipped = tracker != null && tracker.IsBaseItemEquipped(piece.id);
                            sb.Append('\n').Append(isEquipped ? "[X] " : "[ ] ").Append(pieceName);
                        }
                    }

                    // Tier bonuses (Phase 2)
                    if (set.bonuses != null && set.bonuses.Count > 0)
                    {
                        sb.Append("\n\nSet Bonuses");

                        var tiers = set.bonuses;
                        var ordered = new List<ItemSetDefinitionSO.SetBonusTier>(tiers.Count);
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
                                : SetBonusRuntime.FormatMods(tier.modifiers);

                            if (string.IsNullOrWhiteSpace(desc))
                                desc = "(No bonus)";

                            sb.Append('\n').Append(status).Append(' ').Append(req).Append("pc: ").Append(desc);
                        }
                    }
                }

                descriptionText.text = sb.ToString();
            }
        }

        private static void AppendStatLine(System.Text.StringBuilder sb, Abyssbound.Loot.StatType stat, float value, bool percent)
        {
            if (sb == null) return;

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
                sb.Append(label).Append(' ').Append(value >= 0 ? "+" : "").Append(value.ToString("0.##")).Append('%');
            else
                sb.Append(label).Append(' ').Append(value >= 0 ? "+" : "").Append(value.ToString("0.##"));

            sb.Append('\n');
        }

        private void ApplyEmptyState()
        {
            if (nameText != null)
            {
                nameText.text = "Select an item";
                nameText.color = _capturedDefaultColors ? _nameDefaultColor : nameText.color;
            }

            if (rarityText != null)
            {
                rarityText.text = string.Empty;
                rarityText.color = _capturedDefaultColors ? _rarityDefaultColor : rarityText.color;
            }

            if (countText != null)
                countText.text = string.Empty;

            if (descriptionText != null)
                descriptionText.text = "Select an item to view its details.";

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
                if (iconImage.gameObject.activeSelf)
                    iconImage.gameObject.SetActive(false);
            }
        }

        private static string ResolveFallbackName(ItemDefinition def, string fallbackItemId)
        {
            if (def == null) return string.IsNullOrWhiteSpace(fallbackItemId) ? "" : fallbackItemId;
            if (!string.IsNullOrWhiteSpace(def.itemId)) return def.itemId;
            if (!string.IsNullOrWhiteSpace(def.name)) return def.name;
            return string.IsNullOrWhiteSpace(fallbackItemId) ? "" : fallbackItemId;
        }
    }
}
