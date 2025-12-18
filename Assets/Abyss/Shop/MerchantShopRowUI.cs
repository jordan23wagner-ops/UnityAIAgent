using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace Abyss.Shop
{
    public class MerchantShopRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button button;

        private Action _onClick;

        private string _itemId;
        private int _price;

        private Color _baseColor;
        private Color _hoverColor;
        private Color _selectedColor;
        private bool _isHovered;
        private bool _isSelected;

        private void Awake()
        {
            EnsureDefaultColors();
            RefreshBackgroundColor();
        }

        public void Bind(string itemName, int price, Action onClick)
        {
            // Backwards-compatible: itemName doubles as itemId.
            Bind(itemName, price, itemName, onClick);
        }

        public void Bind(string displayName, int price, string itemId, Action onClick)
        {
            if (background == null)
                background = GetComponent<Image>();

            EnsureDefaultColors();

            if (nameText != null) nameText.text = displayName;
            if (priceText != null) priceText.text = price.ToString();

            _itemId = itemId;
            _price = price;

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
            }

            // Default state.
            _isHovered = false;
            SetSelected(false);
        }

        public void ConfigureColors(Color baseColor, Color hoverColor, Color selectedColor)
        {
            _baseColor = baseColor;
            _hoverColor = hoverColor;
            _selectedColor = selectedColor;
            RefreshBackgroundColor();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RefreshBackgroundColor();

            var outline = GetComponent<Outline>();
            if (outline != null)
                outline.enabled = selected;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshBackgroundColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshBackgroundColor();
        }

        private void EnsureDefaultColors()
        {
            if (background == null)
                background = GetComponent<Image>();

            // If already configured, keep.
            if (_baseColor.a > 0f || _hoverColor.a > 0f || _selectedColor.a > 0f)
                return;

            // Base: current background if present, else subtle dark.
            _baseColor = background != null ? background.color : new Color(0.10f, 0.10f, 0.10f, 0.85f);
            if (_baseColor.a <= 0f)
                _baseColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            _hoverColor = Brighten(_baseColor, 0.08f);
            _selectedColor = Brighten(_baseColor, 0.18f);
        }

        private void RefreshBackgroundColor()
        {
            if (background == null)
                background = GetComponent<Image>();
            if (background == null)
                return;

            if (_isSelected)
                background.color = _selectedColor;
            else if (_isHovered)
                background.color = _hoverColor;
            else
                background.color = _baseColor;
        }

        private static Color Brighten(Color c, float amount)
        {
            // Keep alpha; brighten RGB slightly.
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        public void ButtonSelect()
        {
            if (button != null)
                button.Select();
        }

        public string ItemId => _itemId;
        public int Price => _price;
        public Button Button => button;
    }
}
