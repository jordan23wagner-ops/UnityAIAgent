using System;
using System.Collections.Generic;
using Abyss.Items;
using Game.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyss.Equipment
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class PlayerEquipmentUI : MonoBehaviour
    {
        [Serializable]
        private struct SlotWidget
        {
            public EquipmentSlot slot;
            public Button button;
            public Image iconImage;
            public TMP_Text labelText;
            public Sprite emptyIcon;
        }

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;

        [Header("Top")]
        [SerializeField] private TMP_Text titleText;

        [Header("Layout")]
        [SerializeField] private Image paperDollSilhouette;
        [SerializeField] private SlotWidget[] slots;

        private PlayerEquipment _equipment;
        private PlayerInventory _inventory;
        private Dictionary<string, ItemDefinition> _itemDefById;

        private bool _isOpen;

        private void Awake()
        {
            if (root != null)
                root.SetActive(false);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Equipment";

            WireSlotButtons();
        }

        private void Update()
        {
            if (!WasTogglePressed())
                return;

            // Avoid fighting with merchant UI.
            if (Abyss.Shop.MerchantShopUI.IsOpen)
                return;

            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen || root == null)
                return;

            _isOpen = true;
            root.SetActive(true);

            EnsureRefs();
            Refresh();
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;

            if (_equipment != null)
                _equipment.Changed -= OnEquipmentChanged;

            if (root != null)
                root.SetActive(false);
        }

        private void OnEquipmentChanged()
        {
            if (!_isOpen) return;
            Refresh();
        }

        private void EnsureRefs()
        {
            _inventory = PlayerInventoryResolver.GetOrFind();
            _equipment = PlayerEquipmentResolver.GetOrFindOrCreate();

            if (_equipment != null)
            {
                _equipment.Changed -= OnEquipmentChanged;
                _equipment.Changed += OnEquipmentChanged;
            }

            _itemDefById ??= BuildItemDefinitionIndex();
        }

        private void Refresh()
        {
            if (_equipment == null)
                return;

            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                var w = slots[i];
                if (w.button == null) continue;

                var itemId = _equipment.Get(w.slot);
                var def = ResolveItemDefinition(itemId);

                bool hasItem = !string.IsNullOrWhiteSpace(itemId);

                if (w.iconImage != null)
                {
                    // Prefer equipped item icon; otherwise show the slot's empty silhouette if provided.
                    var equippedSprite = def != null ? def.icon : null;
                    w.iconImage.sprite = hasItem ? equippedSprite : w.emptyIcon;
                    w.iconImage.enabled = w.iconImage.sprite != null;
                    w.iconImage.preserveAspect = true;
                    // Keep silhouettes visible and white for now (we can lighten/alpha-tune later).
                    w.iconImage.color = Color.white;
                }

                if (w.labelText != null)
                {
                    // Icons only (like the reference). Keep text hidden.
                    w.labelText.gameObject.SetActive(false);
                }

                w.button.interactable = hasItem;
            }
        }

        private void WireSlotButtons()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                var w = slots[index];
                if (w.button == null) continue;

                w.button.onClick.RemoveAllListeners();
                w.button.onClick.AddListener(() =>
                {
                    if (_inventory == null || _equipment == null) return;
                    var slot = slots[index].slot;
                    if (string.IsNullOrWhiteSpace(_equipment.Get(slot))) return;
                    _equipment.TryUnequipToInventory(_inventory, ResolveItemDefinition, slot);
                });
            }
        }



        private ItemDefinition ResolveItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;

            _itemDefById ??= BuildItemDefinitionIndex();
            if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var def) && def != null)
                return def;

            return null;
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var loaded = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                if (loaded != null)
                {
                    foreach (var def in loaded)
                    {
                        if (def == null) continue;
                        var id = string.IsNullOrWhiteSpace(def.itemId) ? def.name : def.itemId;
                        if (!string.IsNullOrWhiteSpace(id) && !map.ContainsKey(id))
                            map[id] = def;
                    }
                }
            }
            catch { }

            return map;
        }

        private bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.O);
#endif
        }
    }
}
