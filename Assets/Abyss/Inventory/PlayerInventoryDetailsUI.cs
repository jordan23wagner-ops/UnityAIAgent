using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;

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

        public void Clear()
        {
            ApplyEmptyState();
        }

        public void Set(ItemDefinition def, string fallbackItemId, int count)
        {
            if (def == null && string.IsNullOrWhiteSpace(fallbackItemId))
            {
                ApplyEmptyState();
                return;
            }

            string displayName = def != null
                ? (string.IsNullOrWhiteSpace(def.displayName) ? ResolveFallbackName(def, fallbackItemId) : def.displayName)
                : (string.IsNullOrWhiteSpace(fallbackItemId) ? string.Empty : fallbackItemId);

            if (nameText != null)
                nameText.text = displayName ?? string.Empty;

            if (countText != null)
                countText.text = $"Count: {Mathf.Max(0, count)}";

            var rarity = def != null ? ItemRarityVisuals.Normalize(def.rarity) : AbyssItemRarity.Common;
            if (rarityText != null)
                rarityText.text = def == null && string.IsNullOrWhiteSpace(fallbackItemId)
                    ? string.Empty
                    : $"Rarity: {ItemRarityVisuals.ToDisplayString(rarity)}";

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

        private void ApplyEmptyState()
        {
            if (nameText != null)
                nameText.text = "Select an item";

            if (rarityText != null)
                rarityText.text = string.Empty;

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
