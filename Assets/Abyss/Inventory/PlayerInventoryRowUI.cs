using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Inventory
{
    public sealed class PlayerInventoryRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;

        [Header("Optional Visuals")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityStrip;

        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;

        [SerializeField] private Button button;

        private Action _onClick;
        private Color _baseColor;
        private Color _hoverColor;
        private bool _isHovered;

        private Color _baseNameColor;

        private void Awake()
        {
            EnsureDefaultColors();
            RefreshBackground();

            if (nameText != null)
                _baseNameColor = nameText.color;
        }

        public void Bind(ItemDefinition def, string fallbackItemId, int count, Action onClick)
        {
            if (background == null)
                background = GetComponent<Image>();

            EnsureDefaultColors();

            string display = def != null
                ? (string.IsNullOrWhiteSpace(def.displayName) ? ResolveFallbackName(def, fallbackItemId) : def.displayName)
                : (string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId);

            if (nameText != null) nameText.text = display;
            if (countText != null) countText.text = $"x{Mathf.Max(0, count)}";

            var icon = def != null ? def.icon : null;
            var rarity = def != null ? ItemRarityVisuals.Normalize(def.rarity) : AbyssItemRarity.Common;
            ApplyVisuals(icon, rarity);

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
            }

            _isHovered = false;
            RefreshBackground();
        }

        private void ApplyVisuals(Sprite icon, AbyssItemRarity rarity)
        {
            if (iconImage != null)
            {
                bool hasIcon = icon != null;
                iconImage.sprite = icon;
                iconImage.enabled = hasIcon;
                if (iconImage.gameObject.activeSelf != hasIcon)
                    iconImage.gameObject.SetActive(hasIcon);
            }

            rarity = ItemRarityVisuals.Normalize(rarity);
            var c = ItemRarityVisuals.GetColor(rarity);

            if (rarityStrip != null)
            {
                rarityStrip.color = c;
                if (!rarityStrip.gameObject.activeSelf)
                    rarityStrip.gameObject.SetActive(true);
            }
            else if (nameText != null)
            {
                if (_baseNameColor.a <= 0f)
                    _baseNameColor = nameText.color;

                nameText.color = new Color(c.r, c.g, c.b, _baseNameColor.a);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshBackground();
        }

        private void EnsureDefaultColors()
        {
            if (_baseColor.a > 0f || _hoverColor.a > 0f)
                return;

            var baseC = background != null ? background.color : new Color(0.10f, 0.10f, 0.10f, 0.85f);
            if (baseC.a <= 0f)
                baseC = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            _baseColor = baseC;
            _hoverColor = Brighten(baseC, 0.10f);
        }

        private void RefreshBackground()
        {
            if (background == null)
                background = GetComponent<Image>();
            if (background == null)
                return;

            background.color = _isHovered ? _hoverColor : _baseColor;
        }

        private static Color Brighten(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        private static string ResolveFallbackName(ItemDefinition def, string fallbackItemId)
        {
            if (def == null) return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId;
            if (!string.IsNullOrWhiteSpace(def.itemId)) return def.itemId;
            if (!string.IsNullOrWhiteSpace(def.name)) return def.name;
            return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId;
        }

        public Button Button => button;
        public bool CanShowIcon => iconImage != null;
    }
}
