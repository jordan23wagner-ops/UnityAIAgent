using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss.Equipment
{
    [DisallowMultipleComponent]
    public sealed class EquipmentSlotFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Wiring")]
        [SerializeField] private Image background;
        [SerializeField] private Outline outline;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text labelText;

        [Header("Behavior")]
        [SerializeField] private bool hasItem;

        private bool _hovered;
        private bool _selected;

        private Color _baseOutlineColor;
        private Color _baseBackgroundColor;
        private bool _captured;

        private Color _hoverOutlineColor = new Color(1f, 1f, 1f, 0.65f);
        private Color _selectedOutlineColor = new Color(1f, 1f, 1f, 0.95f);

        private float _iconAlphaEmpty = 0.30f;
        private float _iconAlphaEquipped = 1f;

        public void Configure(Image targetBackground, Outline targetOutline, Image targetIcon, TMP_Text targetLabel)
        {
            background = targetBackground;
            outline = targetOutline;
            iconImage = targetIcon;
            labelText = targetLabel;

            CaptureBaseIfNeeded();
            RenderState();
        }

        public void SetHasItem(bool equipped)
        {
            hasItem = equipped;
            RenderState();
        }

        public void SetLabel(string text)
        {
            if (labelText == null)
                return;

            labelText.text = text ?? string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            RenderState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            RenderState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            RenderState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            RenderState();
        }

        private void CaptureBaseIfNeeded()
        {
            if (_captured)
                return;

            if (outline == null)
                outline = GetComponent<Outline>();

            if (background == null)
                background = GetComponent<Image>();

            if (iconImage == null)
            {
                var t = transform.Find("Icon");
                if (t != null)
                    iconImage = t.GetComponent<Image>();
            }

            _baseOutlineColor = outline != null ? outline.effectColor : Color.clear;
            _baseBackgroundColor = background != null ? background.color : new Color(0.34f, 0.32f, 0.29f, 1f);
            _captured = true;
        }

        private void RenderState()
        {
            CaptureBaseIfNeeded();

            // Ensure decorative elements don't block clicks.
            if (iconImage != null)
                iconImage.raycastTarget = false;
            if (labelText != null)
                labelText.raycastTarget = false;

            bool showLabel = _hovered || _selected;
            if (labelText != null && labelText.gameObject.activeSelf != showLabel)
                labelText.gameObject.SetActive(showLabel);

            if (outline != null)
            {
                outline.useGraphicAlpha = false;
                outline.enabled = true;

                if (_selected)
                {
                    outline.effectDistance = new Vector2(2f, -2f);
                    outline.effectColor = _selectedOutlineColor;
                }
                else if (_hovered)
                {
                    outline.effectDistance = new Vector2(1f, -1f);
                    outline.effectColor = _hoverOutlineColor;
                }
                else
                {
                    outline.effectDistance = new Vector2(1f, -1f);
                    outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
                }
            }

            if (background != null)
            {
                // Hover should not change fill; selection brightens fill.
                var fill = _baseBackgroundColor;
                if (!hasItem)
                    fill = new Color(fill.r * 0.90f, fill.g * 0.90f, fill.b * 0.90f, fill.a);
                if (_selected)
                    fill = new Color(Mathf.Clamp01(fill.r + 0.06f), Mathf.Clamp01(fill.g + 0.06f), Mathf.Clamp01(fill.b + 0.06f), fill.a);
                background.color = fill;
            }

            if (iconImage != null)
            {
                var c = iconImage.color;
                c.a = hasItem ? _iconAlphaEquipped : _iconAlphaEmpty;
                iconImage.color = c;
            }
        }
    }
}
