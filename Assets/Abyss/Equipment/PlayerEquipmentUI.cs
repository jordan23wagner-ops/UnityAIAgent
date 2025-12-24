using System;
using System.Collections.Generic;
using Abyss.Inventory;
using Abyss.Items;
using Game.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Abyssbound.Loot;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyss.Equipment
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class PlayerEquipmentUI : MonoBehaviour
    {
#pragma warning disable 0649 // Assigned by Unity serialization (inspector / prefab)
        [Serializable]
        private struct SlotWidget
        {
            public EquipmentSlot slot;
            public Button button;
            public Image iconImage;
            public Image rarityStrip;
            public TMP_Text labelText;
            public Sprite emptyIcon;
        }
#pragma warning restore 0649

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;

        [Header("Character Tabs (optional)")]
        [SerializeField] private Button characterInventoryTabButton;
        [SerializeField] private Button characterEquipmentTabButton;

        [Header("Top")]
        [SerializeField] private TMP_Text titleText;

        [Header("Layout")]
        [SerializeField] private Image paperDollSilhouette;
        [SerializeField] private SlotWidget[] slots;

        private PlayerEquipment _equipment;
        private PlayerInventory _inventory;
        private Dictionary<string, ItemDefinition> _itemDefById;

        private bool _isOpen;

        private Game.Input.PlayerInputAuthority _inputAuthority;

        private Abyss.Inventory.PlayerInventoryUI _inventoryUi;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
#if UNITY_2022_2_OR_NEWER
            _inputAuthority = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _inputAuthority = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif

            if (root != null)
                root.SetActive(false);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Equipment";

            WireCharacterTabs();

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

            // Ensure a single tooltip instance exists under this UI root.
            try { ItemTooltipUI.GetOrCreateUnder(root.transform); } catch { }

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            EnsureRefs();
            Refresh();
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

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

            if (_inventoryUi == null)
            {
#if UNITY_2022_2_OR_NEWER
                _inventoryUi = FindFirstObjectByType<Abyss.Inventory.PlayerInventoryUI>();
#else
                _inventoryUi = FindObjectOfType<Abyss.Inventory.PlayerInventoryUI>();
#endif
            }

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

                // Rolled loot items may not have an Abyss.Items.ItemDefinition asset.
                Sprite lootIcon = null;
                string lootName = null;
                if (hasItem && def == null)
                {
                    try
                    {
                        var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                        if (reg != null && reg.TryResolveDisplay(itemId, out var dn, out var ic))
                        {
                            lootName = dn;
                            lootIcon = ic;
                        }
                    }
                    catch { }
                }

                // Inventory UI rule: never show placeholders for empty slots.
                bool hasIcon = hasItem && ((def != null && def.icon != null) || lootIcon != null);

                if (w.iconImage != null)
                {
                    w.iconImage.sprite = hasIcon ? (def != null ? def.icon : lootIcon) : null;
                    w.iconImage.enabled = hasIcon;
                    if (w.iconImage.gameObject.activeSelf != hasIcon)
                        w.iconImage.gameObject.SetActive(hasIcon);
                    w.iconImage.preserveAspect = true;
                    // Alpha tuned by EquipmentSlotFeedback (empty vs equipped).
                    w.iconImage.color = Color.white;
                    w.iconImage.raycastTarget = false;
                }

                if (w.rarityStrip != null)
                {
                    bool showStrip = false;
                    var c = Color.white;

                    if (hasItem)
                    {
                        if (def != null)
                        {
                            showStrip = true;
                            var normalized = ItemRarityVisuals.Normalize(def.rarity);
                            var rc = RarityColorMap.GetColorOrDefault(normalized, Color.white);
                            c = new Color(rc.r, rc.g, rc.b, 1f);
                        }
                        else
                        {
                            // Rolled Loot V2 item: color strip by rolled rarity id.
                            try
                            {
                                var reg = LootRegistryRuntime.GetOrCreate();
                                if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                                {
                                    showStrip = true;
                                    var rc = RarityColorMap.GetColorOrDefault(inst.rarityId, Color.white);
                                    c = new Color(rc.r, rc.g, rc.b, 1f);
                                }
                            }
                            catch { }
                        }
                    }

                    w.rarityStrip.enabled = showStrip;
                    w.rarityStrip.color = showStrip
                        ? c
                        : new Color(c.r, c.g, c.b, 0f);

                    if (!w.rarityStrip.gameObject.activeSelf)
                        w.rarityStrip.gameObject.SetActive(true);

                    try { w.rarityStrip.raycastTarget = false; } catch { }
                }

                if (w.labelText != null)
                {
                    // Optional: item name; cleared when empty.
                    w.labelText.text = hasItem
                        ? (def != null && !string.IsNullOrWhiteSpace(def.displayName)
                            ? def.displayName
                            : (!string.IsNullOrWhiteSpace(lootName) ? lootName : itemId))
                        : string.Empty;
                }

                EnsureSlotTooltip(w, def, itemId);

                // Keep interactable so we can still hover to see the slot label.
                w.button.interactable = true;

                EnsureSlotFeedback(w);

                // Rarity-colored slot borders (outline) for both legacy and rolled loot items.
                try
                {
                    var feedback = w.button.GetComponent<EquipmentSlotFeedback>();
                    if (feedback != null)
                    {
                        var rgb = Color.white;

                        if (hasItem)
                        {
                            if (def != null)
                            {
                                var normalized = ItemRarityVisuals.Normalize(def.rarity);
                                var c = RarityColorMap.GetColorOrDefault(normalized, Color.white);
                                rgb = new Color(c.r, c.g, c.b, 1f);
                            }
                            else
                            {
                                var reg = LootRegistryRuntime.GetOrCreate();
                                if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                                {
                                    var c = RarityColorMap.GetColorOrDefault(inst.rarityId, Color.white);
                                    rgb = new Color(c.r, c.g, c.b, 1f);
                                }
                            }
                        }

                        feedback.SetRarityOutlineRgb(rgb);
                    }
                }
                catch { }
            }
        }

        private void EnsureSlotTooltip(SlotWidget w, ItemDefinition def, string itemId)
        {
            if (w.button == null)
                return;

            try
            {
                var trigger = w.button.GetComponent<ItemTooltipTrigger>();
                if (trigger == null)
                    trigger = w.button.gameObject.AddComponent<ItemTooltipTrigger>();

                trigger.BindEquipmentSlot(w.slot, def, itemId);
            }
            catch { }
        }

        private void EnsureSlotFeedback(SlotWidget w)
        {
            if (w.button == null)
                return;

            try
            {
                var feedback = w.button.GetComponent<EquipmentSlotFeedback>();
                if (feedback == null)
                    feedback = w.button.gameObject.AddComponent<EquipmentSlotFeedback>();

                // Prevent Selectable from tinting the fill on hover; we do border-only.
                w.button.transition = Selectable.Transition.None;

                var outline = w.button.GetComponent<Outline>();
                var bg = w.button.GetComponent<Image>();
                feedback.Configure(bg, outline, w.iconImage, w.labelText);

                bool hasItem = _equipment != null && !string.IsNullOrWhiteSpace(_equipment.Get(w.slot));
                feedback.SetHasItem(hasItem);

                if (w.labelText != null)
                    w.labelText.raycastTarget = false;
            }
            catch { }
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
                    if (_equipment == null) return;
                    var slot = slots[index].slot;
                    if (string.IsNullOrWhiteSpace(_equipment.Get(slot))) return;
                    _equipment.TryUnequip(ResolveItemDefinition, slot);
                });
            }
        }

        private void WireCharacterTabs()
        {
            // Equipment tab is "selected" while this window is open.
            if (characterEquipmentTabButton != null)
                characterEquipmentTabButton.interactable = false;

            if (characterInventoryTabButton != null)
            {
                characterInventoryTabButton.onClick.RemoveAllListeners();
                characterInventoryTabButton.onClick.AddListener(() =>
                {
                    // Switch to inventory window.
                    Close();
                    try { _inventoryUi?.Open(); } catch { }
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
