# Equipment Damage MVP - Code Dump

Generated: 2025-12-21 14:41:33

## Files
- Assets/Abyss/Equipment/EquipmentSlotFeedback.cs
- Assets/Abyss/Equipment/InventoryEquipButtonMvpAdapter.cs
- Assets/Abyss/Equipment/PlayerEquipment.cs
- Assets/Abyss/Equipment/PlayerEquipmentUI.cs
- Assets/Abyss/Inventory/InventoryRarityColors.cs
- Assets/Abyss/Inventory/PlayerInventoryDetailsUI.cs
- Assets/Abyss/Inventory/PlayerInventoryRowUI.cs
- Assets/Abyss/Inventory/PlayerInventoryUI.cs
- Assets/Abyss/Inventory/UIEffects/InventoryTileMeshEffect.cs
- Assets/Abyss/Items/ItemDefinition.cs
- Assets/Abyss/Shop/MerchantClickRaycaster.cs
- Assets/Abyss/Shop/MerchantDoorHoverHighlighter.cs
- Assets/Editor/AutoAssignMissingItemIconsEditor.cs
- Assets/Editor/BuildPlayerEquipmentUIEditor.cs
- Assets/Editor/BuildPlayerInventoryUIEditor.cs
- Assets/Editor/GenerateBasicItemIcons.cs
- Assets/Editor/GenerateUiBorderSprite.cs
- Assets/Editor/TownInteractionRestorer.cs
- Assets/Editor/TownLegacyRootCleaner.cs
- Assets/Editor/ValidateUiIconsEditor.cs
- Assets/Game/Dev/DevCheats.cs
- Assets/Game/Input/PlayerInputAuthority.cs
- Assets/Game/Player/PlayerCombatStats.cs
- Assets/Game/Player/SimplePlayerCombat.cs

---

## Assets/Abyss/Equipment/EquipmentSlotFeedback.cs

```csharp
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
```

---

## Assets/Abyss/Equipment/InventoryEquipButtonMvpAdapter.cs

```csharp
using System;
using System.Reflection;
using Abyss.Inventory;
using Abyss.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Equipment
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class InventoryEquipButtonMvpAdapter : MonoBehaviour
    {
        private PlayerInventoryUI _inventoryUi;
        private Button _equipButton;

        private FieldInfo _selectedDefField;
        private FieldInfo _selectedItemIdField;

        private bool _wired;

        private void Awake()
        {
#if UNITY_2022_2_OR_NEWER
            _inventoryUi = FindFirstObjectByType<PlayerInventoryUI>();
#else
            _inventoryUi = FindObjectOfType<PlayerInventoryUI>();
#endif

            TryCacheReflectionFields();
        }

        private void Update()
        {
            EnsureButtonRef();
            EnsureWired();
        }

        private void EnsureButtonRef()
        {
            if (_equipButton != null)
                return;

            if (_inventoryUi == null)
            {
#if UNITY_2022_2_OR_NEWER
                _inventoryUi = FindFirstObjectByType<PlayerInventoryUI>();
#else
                _inventoryUi = FindObjectOfType<PlayerInventoryUI>();
#endif
                if (_inventoryUi == null)
                    return;
            }

            // The inventory UI creates this under its details panel.
            // We only override the click behavior; visibility/enabled state stays owned by PlayerInventoryUI.
            try
            {
                var t = _inventoryUi.transform.Find("Details/EquipButton");
                if (t == null)
                {
                    // Fallback: look for EquipButton anywhere under the inventory UI.
                    var buttons = _inventoryUi.GetComponentsInChildren<Button>(true);
                    foreach (var b in buttons)
                    {
                        if (b != null && b.name == "EquipButton")
                        {
                            _equipButton = b;
                            break;
                        }
                    }
                }
                else
                {
                    _equipButton = t.GetComponent<Button>();
                }
            }
            catch { }
        }

        private void EnsureWired()
        {
            if (_wired)
                return;

            if (_equipButton == null)
                return;

            try
            {
                _equipButton.onClick.RemoveAllListeners();
                _equipButton.onClick.AddListener(OnEquipPressedMvp);
                _wired = true;
            }
            catch { }
        }

        private void OnEquipPressedMvp()
        {
            if (_inventoryUi == null)
                return;

            var selectedDef = GetSelectedDef();
            if (selectedDef == null)
                return;

            if (selectedDef.equipmentSlot == EquipmentSlot.None)
                return;

            var equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
            if (equipment == null)
                return;

            // MVP rule: do NOT consume/remove from inventory yet.
            // TODO: Later, replace this with the inventory-consuming flow once inventory/equipment is authoritative.
            if (equipment.TryEquip(selectedDef, out var message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                    Debug.Log($"[Equipment] {message}");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(message))
                    Debug.LogWarning($"[Equipment] {message}");
            }
        }

        private ItemDefinition GetSelectedDef()
        {
            TryCacheReflectionFields();

            try
            {
                if (_selectedDefField != null)
                    return _selectedDefField.GetValue(_inventoryUi) as ItemDefinition;
            }
            catch { }

            // Fallback: try itemId -> resolve via resources.
            try
            {
                var itemId = _selectedItemIdField != null ? _selectedItemIdField.GetValue(_inventoryUi) as string : null;
                if (string.IsNullOrWhiteSpace(itemId))
                    return null;

                var loaded = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                if (loaded == null)
                    return null;

                for (int i = 0; i < loaded.Length; i++)
                {
                    var def = loaded[i];
                    if (def == null) continue;
                    if (string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase) || string.Equals(def.name, itemId, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
            }
            catch { }

            return null;
        }

        private void TryCacheReflectionFields()
        {
            if (_inventoryUi == null)
                return;

            if (_selectedDefField != null && _selectedItemIdField != null)
                return;

            try
            {
                var t = _inventoryUi.GetType();
                _selectedDefField ??= t.GetField("_selectedDef", BindingFlags.Instance | BindingFlags.NonPublic);
                _selectedItemIdField ??= t.GetField("_selectedItemId", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            catch { }
        }
    }
}
```

---

## Assets/Abyss/Equipment/PlayerEquipment.cs

```csharp
using System;
using Abyss.Items;
using UnityEngine;

namespace Abyss.Equipment
{
    [DisallowMultipleComponent]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [Header("Equipped Item IDs")]
        [SerializeField] private string helm;
        [SerializeField] private string chest;
        [SerializeField] private string legs;
        [SerializeField] private string belt;
        [SerializeField] private string gloves;
        [SerializeField] private string cape;
        [SerializeField] private string ammo;
        [SerializeField] private string leftHand;
        [SerializeField] private string rightHand;
        [SerializeField] private string ring1;
        [SerializeField] private string ring2;
        [SerializeField] private string amulet;
        [SerializeField] private string artifact;

        public event Action Changed;

        public string Get(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Helm => helm,
                EquipmentSlot.Chest => chest,
                EquipmentSlot.Legs => legs,
                EquipmentSlot.Belt => belt,
                EquipmentSlot.Gloves => gloves,
                EquipmentSlot.Cape => cape,
                EquipmentSlot.Ammo => ammo,
                EquipmentSlot.LeftHand => leftHand,
                EquipmentSlot.RightHand => rightHand,
                EquipmentSlot.Ring1 => ring1,
                EquipmentSlot.Ring2 => ring2,
                EquipmentSlot.Amulet => amulet,
                EquipmentSlot.Artifact => artifact,
                _ => null,
            };
        }

        public void Set(EquipmentSlot slot, string itemId)
        {
            switch (slot)
            {
                case EquipmentSlot.Helm: helm = itemId; break;
                case EquipmentSlot.Chest: chest = itemId; break;
                case EquipmentSlot.Legs: legs = itemId; break;
                case EquipmentSlot.Belt: belt = itemId; break;
                case EquipmentSlot.Gloves: gloves = itemId; break;
                case EquipmentSlot.Cape: cape = itemId; break;
                case EquipmentSlot.Ammo: ammo = itemId; break;
                case EquipmentSlot.LeftHand: leftHand = itemId; break;
                case EquipmentSlot.RightHand: rightHand = itemId; break;
                case EquipmentSlot.Ring1: ring1 = itemId; break;
                case EquipmentSlot.Ring2: ring2 = itemId; break;
                case EquipmentSlot.Amulet: amulet = itemId; break;
                case EquipmentSlot.Artifact: artifact = itemId; break;
            }
        }

        // MVP-friendly API: equip without consuming inventory.
        // This is intentionally additive and does not change the existing inventory-consuming flow.
        public bool TryEquip(Func<string, ItemDefinition> resolve, string itemId, out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(itemId))
            {
                message = "No item selected.";
                return false;
            }

            var def = resolve != null ? resolve(itemId) : null;
            if (def == null)
            {
                message = $"No ItemDefinition loaded for '{itemId}'.";
                return false;
            }

            return TryEquip(def, out message);
        }

        // MVP-friendly API: equip without consuming inventory.
        public bool TryEquip(ItemDefinition def, out string message)
        {
            message = string.Empty;

            if (def == null)
            {
                message = "No item selected.";
                return false;
            }

            var itemId = !string.IsNullOrWhiteSpace(def.itemId) ? def.itemId : def.name;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                message = "Item cannot be equipped (missing itemId).";
                return false;
            }

            // Resolve slots the same way as the full inventory path.
            if (!TryResolveTargetSlots(def, out var primarySlot, out var secondarySlot))
            {
                message = "That item is not equippable.";
                return false;
            }

            // Clear conflicts (visual-only; does NOT return items to inventory).
            if (secondarySlot.HasValue)
            {
                Set(EquipmentSlot.LeftHand, null);
                Set(EquipmentSlot.RightHand, null);
            }
            else
            {
                Set(primarySlot, null);
            }

            // Rings: if preferred ring slot occupied and the other ring slot is empty, use the empty one.
            if (!secondarySlot.HasValue && primarySlot == EquipmentSlot.Ring1)
            {
                if (!string.IsNullOrWhiteSpace(ring1) && string.IsNullOrWhiteSpace(ring2))
                    primarySlot = EquipmentSlot.Ring2;
            }
            if (!secondarySlot.HasValue && primarySlot == EquipmentSlot.Ring2)
            {
                if (!string.IsNullOrWhiteSpace(ring2) && string.IsNullOrWhiteSpace(ring1))
                    primarySlot = EquipmentSlot.Ring1;
            }

            // Hands: allow alternate slot when preferred is occupied.
            if (!secondarySlot.HasValue && (primarySlot == EquipmentSlot.RightHand || primarySlot == EquipmentSlot.LeftHand))
            {
                if (!string.IsNullOrWhiteSpace(Get(primarySlot)))
                {
                    var alt = primarySlot == EquipmentSlot.RightHand ? EquipmentSlot.LeftHand : EquipmentSlot.RightHand;
                    if (string.IsNullOrWhiteSpace(Get(alt)))
                        primarySlot = alt;
                    else
                        Set(alt, null);
                }
            }

            Set(primarySlot, itemId);
            if (secondarySlot.HasValue)
                Set(secondarySlot.Value, itemId);

            RaiseChanged();
            return true;
        }

        // MVP-friendly: unequip without returning to inventory.
        public bool TryUnequip(Func<string, ItemDefinition> resolve, EquipmentSlot slot)
        {
            string itemId = Get(slot);
            if (string.IsNullOrWhiteSpace(itemId)) return false;

            // If this is a two-handed item, clear both hands.
            bool clearBothHands = false;
            if (slot == EquipmentSlot.LeftHand || slot == EquipmentSlot.RightHand)
            {
                var def = resolve != null ? resolve(itemId) : null;
                if (def != null && def.weaponHandedness == WeaponHandedness.TwoHanded)
                    clearBothHands = true;

                // Also treat "same id in both hands" as two-handed.
                if (!clearBothHands && !string.IsNullOrWhiteSpace(leftHand) && leftHand == rightHand)
                    clearBothHands = true;
            }

            if (clearBothHands)
            {
                Set(EquipmentSlot.LeftHand, null);
                Set(EquipmentSlot.RightHand, null);
            }
            else
            {
                Set(slot, null);
            }

            RaiseChanged();
            return true;
        }

        public bool TryEquipFromInventory(PlayerInventory inventory, Func<string, ItemDefinition> resolve, string itemId, out string message)
        {
            message = string.Empty;
            if (inventory == null)
            {
                message = "No PlayerInventory.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                message = "No item selected.";
                return false;
            }

            if (!inventory.Has(itemId, 1))
            {
                message = $"You don't have '{itemId}'.";
                return false;
            }

            var def = resolve != null ? resolve(itemId) : null;
            if (def == null)
            {
                message = $"No ItemDefinition loaded for '{itemId}'.";
                return false;
            }

            // Unequip conflicts first (returns items to inventory).
            if (!TryResolveTargetSlots(def, out var primarySlot, out var secondarySlot))
            {
                message = "That item is not equippable.";
                return false;
            }

            if (secondarySlot.HasValue)
            {
                // Two-handed: clear both hands.
                TryUnequipToInventory(inventory, resolve, EquipmentSlot.LeftHand);
                TryUnequipToInventory(inventory, resolve, EquipmentSlot.RightHand);
            }
            else
            {
                // One-slot: clear that slot first.
                TryUnequipToInventory(inventory, resolve, primarySlot);
            }

            // Rings: if Ring1 requested but Ring1 occupied and Ring2 empty, use Ring2.
            if (!secondarySlot.HasValue && primarySlot == EquipmentSlot.Ring1)
            {
                if (!string.IsNullOrWhiteSpace(ring1) && string.IsNullOrWhiteSpace(ring2))
                    primarySlot = EquipmentSlot.Ring2;
            }
            if (!secondarySlot.HasValue && primarySlot == EquipmentSlot.Ring2)
            {
                if (!string.IsNullOrWhiteSpace(ring2) && string.IsNullOrWhiteSpace(ring1))
                    primarySlot = EquipmentSlot.Ring1;
            }

            // Hands: allow offhand/onehand to use alternate slot when preferred is occupied.
            if (!secondarySlot.HasValue && (primarySlot == EquipmentSlot.RightHand || primarySlot == EquipmentSlot.LeftHand))
            {
                if (!string.IsNullOrWhiteSpace(Get(primarySlot)))
                {
                    var alt = primarySlot == EquipmentSlot.RightHand ? EquipmentSlot.LeftHand : EquipmentSlot.RightHand;
                    if (string.IsNullOrWhiteSpace(Get(alt)))
                        primarySlot = alt;
                    else
                    {
                        // Both occupied; ensure at least one hand is clear.
                        TryUnequipToInventory(inventory, resolve, alt);
                    }
                }
            }

            // Consume item from inventory and equip.
            if (!inventory.TryConsume(itemId, 1))
            {
                message = "Failed to remove item from inventory.";
                return false;
            }

            Set(primarySlot, itemId);
            if (secondarySlot.HasValue)
                Set(secondarySlot.Value, itemId);

            RaiseChanged();
            return true;
        }

        public bool TryUnequipToInventory(PlayerInventory inventory, Func<string, ItemDefinition> resolve, EquipmentSlot slot)
        {
            if (inventory == null) return false;

            string itemId = Get(slot);
            if (string.IsNullOrWhiteSpace(itemId)) return false;

            // If this is a two-handed item, clear both hands.
            bool clearBothHands = false;
            if (slot == EquipmentSlot.LeftHand || slot == EquipmentSlot.RightHand)
            {
                var def = resolve != null ? resolve(itemId) : null;
                if (def != null && def.weaponHandedness == WeaponHandedness.TwoHanded)
                    clearBothHands = true;

                // Also treat "same id in both hands" as two-handed.
                if (!clearBothHands && !string.IsNullOrWhiteSpace(leftHand) && leftHand == rightHand)
                    clearBothHands = true;
            }

            if (clearBothHands)
            {
                Set(EquipmentSlot.LeftHand, null);
                Set(EquipmentSlot.RightHand, null);
            }
            else
            {
                Set(slot, null);
            }

            inventory.Add(itemId, 1);
            RaiseChanged();
            return true;
        }

        private bool TryResolveTargetSlots(ItemDefinition def, out EquipmentSlot primary, out EquipmentSlot? secondary)
        {
            primary = EquipmentSlot.None;
            secondary = null;

            if (def == null) return false;

            // Two-handed always occupies both hands.
            if (def.weaponHandedness == WeaponHandedness.TwoHanded)
            {
                primary = EquipmentSlot.RightHand;
                secondary = EquipmentSlot.LeftHand;
                return true;
            }

            // Offhand defaults to LeftHand.
            if (def.weaponHandedness == WeaponHandedness.Offhand)
            {
                primary = EquipmentSlot.LeftHand;
                return true;
            }

            // One-handed defaults to RightHand.
            if (def.weaponHandedness == WeaponHandedness.OneHanded)
            {
                primary = EquipmentSlot.RightHand;
                return true;
            }

            primary = def.equipmentSlot;
            return primary != EquipmentSlot.None;
        }

        private void RaiseChanged()
        {
            try { Changed?.Invoke(); } catch { }
        }
    }
}
```

---

## Assets/Abyss/Equipment/PlayerEquipmentUI.cs

```csharp
using System;
using System.Collections.Generic;
using Abyss.Inventory;
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

                // Inventory UI rule: never show placeholders for empty slots.
                bool hasIcon = hasItem && def != null && def.icon != null;

                if (w.iconImage != null)
                {
                    w.iconImage.sprite = hasIcon ? def.icon : null;
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
                    bool showStrip = hasItem && def != null;
                    var c = showStrip ? InventoryRarityColors.GetColor(def.rarity) : Color.white;

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
                        ? (def != null && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : itemId)
                        : string.Empty;
                }

                // Keep interactable so we can still hover to see the slot label.
                w.button.interactable = true;

                EnsureSlotFeedback(w);
            }
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
```

---

## Assets/Abyss/Inventory/InventoryRarityColors.cs

```csharp
using UnityEngine;

namespace Abyss.Inventory
{
    public static class InventoryRarityColors
    {
        public static Color GetColor(Abyss.Items.ItemRarity rarity)
        {
            // Centralized mapping for inventory UI (strip + any future highlights).
            // Keep this as the single source of truth to avoid scattered hardcoded colors.
            rarity = Abyss.Items.ItemRarityVisuals.Normalize(rarity);

            return rarity switch
            {
                Abyss.Items.ItemRarity.Common => new Color(0.75f, 0.75f, 0.75f, 1f),
                Abyss.Items.ItemRarity.Uncommon => new Color(0.35f, 0.85f, 0.35f, 1f),
                Abyss.Items.ItemRarity.Magic => new Color(0.35f, 0.65f, 1.00f, 1f),
                Abyss.Items.ItemRarity.Rare => new Color(0.30f, 0.55f, 1.00f, 1f),
                Abyss.Items.ItemRarity.Epic => new Color(0.78f, 0.35f, 0.95f, 1f),
                Abyss.Items.ItemRarity.Legendary => new Color(1.00f, 0.65f, 0.15f, 1f),
                Abyss.Items.ItemRarity.Set => new Color(1.00f, 0.40f, 0.40f, 1f),
                Abyss.Items.ItemRarity.Radiant => new Color(1.00f, 0.95f, 0.45f, 1f),
                _ => new Color(0.75f, 0.75f, 0.75f, 1f),
            };
        }
    }
}
```

---

## Assets/Abyss/Inventory/PlayerInventoryDetailsUI.cs

```csharp
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
```

---

## Assets/Abyss/Inventory/PlayerInventoryRowUI.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
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
        [Header("Fill (Raycast Target)")]
        [SerializeField] private Image background;

        [Header("Borders (Legacy / Disabled)")]
        [SerializeField] private Image hoverBorderImage;
        [SerializeField] private Image selectedBorderImage;

        [Header("Borders (4-Line, Grid Mode)")]
        [SerializeField] private RectTransform borderRoot;
        [SerializeField] private Image borderTop;
        [SerializeField] private Image borderBottom;
        [SerializeField] private Image borderLeft;
        [SerializeField] private Image borderRight;

        [Header("Optional Visuals")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityStrip;

        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;

        [SerializeField] private Button button;

        private Action _onClick;
        private bool _colorsInitialized;
        private Color _listBaseColor;
        private Color _listHoverColor;

        private bool _isHovered;
        private bool _isSelected;
        private bool _isGridMode;

        private bool _hasItem;
        private int _boundCount;

        // [INV] Debug context (set by PlayerInventoryUI during RefreshList)
        private int _debugSlotIndex = -1;
        private bool _debugIsEmpty;

        public int SlotIndex { get; private set; } = -1;
        public bool IsEmpty => !_hasItem;

        public void SetSlotIndex(int index)
        {
            SlotIndex = index;
        }

        private Color _baseNameColor;

        // Grid visuals (explicit OSRS-style rules)
        private static readonly Color GridEmptyFill = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color GridOccupiedFill = new(0.24f, 0.24f, 0.24f, 1f);
        // User requirement: Selected > Hover > Normal with explicit alphas.
        private static readonly Color GridBorderNormalColor = new(1f, 1f, 1f, 0.18f);
        private static readonly Color GridHoverBorderColor = new(1f, 1f, 1f, 0.45f);
        private static readonly Color GridSelectedBorderColor = new(1f, 1f, 1f, 0.90f);
        private const float GridSelectedFillBrighten = 0.06f;

        // User requirement: consistent borders across all resolutions.
        // Force thickness to 2px (avoid 1px subpixel disappearance).
        private const float BorderThicknessNormal = 2f;
        private const float BorderThicknessSelected = 2f;

        private bool _gridBordersInitialized;

        private static Sprite s_WhiteSprite;

        // ItemDefinition lookup cache (covers cases where PlayerInventoryUI provides only a string id/name).
        // We keep this local to the row so icon binding can't silently fail.
        private static Dictionary<string, ItemDefinition> s_ItemDefByKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<string> s_WarnedMissingIconImageByItemId = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> s_WarnedMissingIconByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static void WarnOnce(HashSet<string> cache, string key, string message, UnityEngine.Object context)
        {
            try
            {
                if (cache == null) return;
                key ??= "(null)";
                if (cache.Contains(key)) return;
                cache.Add(key);
                Debug.LogWarning(message, context);
            }
            catch { }
        }
#endif

        private static ItemDefinition ResolveItemDefinitionFallback(string keyA, string keyB)
        {
            try
            {
                if (s_ItemDefByKey == null)
                {
                    s_ItemDefByKey = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

                    var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                    if (defs != null)
                    {
                        foreach (var def in defs)
                        {
                            if (def == null) continue;

                            string id = null;
                            try { id = def.itemId; } catch { }

                            string displayName = null;
                            try { displayName = def.displayName; } catch { }

                            TryAddDefKey(id, def);
                            TryAddDefKey(displayName, def);
                            TryAddDefKey(def.name, def);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(keyA) && s_ItemDefByKey.TryGetValue(keyA, out var a) && a != null)
                    return a;

                if (!string.IsNullOrWhiteSpace(keyB) && s_ItemDefByKey.TryGetValue(keyB, out var b) && b != null)
                    return b;
            }
            catch { }

            return null;
        }

        private static void TryAddDefKey(string key, ItemDefinition def)
        {
            if (string.IsNullOrWhiteSpace(key) || def == null)
                return;

            try
            {
                // Prefer a definition that has an icon.
                if (s_ItemDefByKey.TryGetValue(key, out var existing) && existing != null)
                {
                    bool existingHasIcon = false;
                    bool defHasIcon = false;
                    try { existingHasIcon = existing.icon != null; } catch { }
                    try { defHasIcon = def.icon != null; } catch { }

                    if (!existingHasIcon && defHasIcon)
                        s_ItemDefByKey[key] = def;

                    return;
                }

                s_ItemDefByKey[key] = def;
            }
            catch { }
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            if (s_WhiteSprite != null)
                return s_WhiteSprite;

            // Guaranteed runtime sprite (avoids built-in UI resources that may not exist).
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "RuntimeWhiteSpriteTex",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            s_WhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            s_WhiteSprite.name = "RuntimeWhiteSprite";
            s_WhiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_WhiteSprite;
        }

        public void SetDebugContext(int slotIndex, bool isEmpty)
        {
            _debugSlotIndex = slotIndex;
            _debugIsEmpty = isEmpty;
        }

        private void Awake()
        {
            EnsureDefaultColors();
            if (_isGridMode)
                EnsureGridBorderLines();
            RenderState();

            if (nameText != null)
                _baseNameColor = nameText.color;
        }

        private void OnEnable()
        {
            // Snap to integer pixel grid (both immediately and after layout has positioned us).
            // GridLayoutGroup positions are applied after instantiation, so do both.
            try
            {
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    SnapToPixelGrid(rt);
                    StartCoroutine(SnapAfterLayout(rt));
                }
            }
            catch { }
        }

        private IEnumerator SnapAfterLayout(RectTransform rt)
        {
            yield return null;

            try
            {
                if (rt != null)
                    SnapToPixelGrid(rt);

                if (_isGridMode)
                {
                    EnsureGridBorderLines();
                    RenderState();
                }
            }
            catch { }
        }

        private static void SnapToPixelGrid(RectTransform rt)
        {
            if (rt == null) return;
            var p = rt.anchoredPosition;
            p.x = Mathf.Round(p.x);
            p.y = Mathf.Round(p.y);
            rt.anchoredPosition = p;
        }

        public void Bind(ItemDefinition def, string fallbackItemId, int count, Action onClick)
        {
            ResolveBackgroundImage();
            EnsureDefaultColors();

            if (_isGridMode)
                EnsureGridElements();

            string display = def != null
                ? (string.IsNullOrWhiteSpace(def.displayName) ? ResolveFallbackName(def, fallbackItemId) : def.displayName)
                : (string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId);

            // Grid mode requirement: no item name text inside the cell.
            if (!_isGridMode)
            {
                if (nameText != null) nameText.text = display;
            }
            else
            {
                if (nameText != null) nameText.text = string.Empty;
            }

            int safeCount = Mathf.Max(0, count);
            _boundCount = safeCount;
            _hasItem = def != null || !string.IsNullOrWhiteSpace(fallbackItemId);

            if (countText != null)
            {
                // UI requirement: show stack count only if > 1.
                countText.text = safeCount > 1 ? $"x{safeCount}" : string.Empty;
            }

            // Resolve ItemDefinition even if caller only supplies a string key (some inventories use display name keys).
            var resolvedDef = def != null ? def : ResolveItemDefinitionFallback(fallbackItemId, display);

            Sprite icon = null;
            AbyssItemRarity rarity = AbyssItemRarity.Common;
            try
            {
                if (resolvedDef != null)
                {
                    icon = resolvedDef.icon;
                    rarity = ItemRarityVisuals.Normalize(resolvedDef.rarity);
                }
            }
            catch { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hasItem)
            {
                string id = null;
                try { id = resolvedDef != null && !string.IsNullOrWhiteSpace(resolvedDef.itemId) ? resolvedDef.itemId : fallbackItemId; } catch { id = fallbackItemId; }
                if (string.IsNullOrWhiteSpace(id)) id = display;

                if (iconImage == null)
                    WarnOnce(s_WarnedMissingIconImageByItemId, id, $"[INV][ICON] IconImage NULL for {id} row={gameObject.name}", this);

                if (icon == null)
                    WarnOnce(s_WarnedMissingIconByItemId, id, $"[INV][ICON] icon NULL for {id} row={gameObject.name}", this);
            }
#endif

            ApplyVisuals(icon, rarity);

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
            }

            _isHovered = false;
            RenderState();
        }

        public void BindEmpty()
        {
            BindEmpty(null);
        }

        public void BindEmpty(Action onClick)
        {
            ResolveBackgroundImage();
            EnsureDefaultColors();

            if (_isGridMode)
                EnsureGridElements();

            if (nameText != null) nameText.text = string.Empty;
            if (countText != null) countText.text = string.Empty;

            _boundCount = 0;
            _hasItem = false;

            _isHovered = false;
            _isSelected = false;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
                if (iconImage.gameObject.activeSelf)
                    iconImage.gameObject.SetActive(false);
            }

            if (rarityStrip != null)
            {
                rarityStrip.enabled = false;
                var rc = rarityStrip.color;
                rarityStrip.color = new Color(rc.r, rc.g, rc.b, 0f);
            }

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (_onClick != null)
                    button.onClick.AddListener(() => _onClick?.Invoke());

                // In grid mode we want empty slots hoverable + clickable.
                button.interactable = _isGridMode;
            }

            RenderState();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RenderState();
        }

        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            RenderState();
        }

        public void SetGridMode(bool enabled)
        {
            _isGridMode = enabled;

            if (_isGridMode)
            {
                ResolveBackgroundImage();
                EnsureGridElements();
                EnsureGridBorderLines();
                DisableLegacyBorders();
            }

            // In grid mode we prefer icon + count (name shown in details panel).
            if (nameText != null)
                nameText.gameObject.SetActive(!enabled);

            // Also hard-hide any other TMP labels in the slot (prevents cramped names if a template has extras).
            if (enabled)
            {
                try
                {
                    var tmps = GetComponentsInChildren<TMP_Text>(true);
                    if (tmps != null)
                    {
                        for (int i = 0; i < tmps.Length; i++)
                        {
                            var t = tmps[i];
                            if (t == null) continue;
                            if (t == countText) continue;
                            t.gameObject.SetActive(false);
                        }
                    }
                }
                catch { }
            }

            // Rarity strip is now driven by binding (enabled for items, disabled for empty).

            if (enabled)
            {
                try
                {
                    if (iconImage != null)
                    {
                        var rt = iconImage.rectTransform;
                        // ~70% size with padding so it doesn't touch borders.
                        rt.anchorMin = new Vector2(0.15f, 0.15f);
                        rt.anchorMax = new Vector2(0.85f, 0.85f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        iconImage.preserveAspect = true;
                        iconImage.raycastTarget = false;
                    }

                    if (countText != null)
                    {
                        var rt = countText.rectTransform;
                        rt.anchorMin = new Vector2(1f, 0f);
                        rt.anchorMax = new Vector2(1f, 0f);
                        rt.pivot = new Vector2(1f, 0f);
                        rt.anchoredPosition = new Vector2(-4f, 4f);

                        if (countText.fontSize > 16f)
                            countText.fontSize = 16f;

                        countText.textWrappingMode = TextWrappingModes.NoWrap;
                        countText.alignment = TextAlignmentOptions.BottomRight;
                        countText.raycastTarget = false;

                        var shadow = countText.GetComponent<Shadow>();
                        if (shadow == null) shadow = countText.gameObject.AddComponent<Shadow>();
                        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                        shadow.effectDistance = new Vector2(1f, -1f);
                        shadow.useGraphicAlpha = true;
                    }
                }
                catch { }
            }
            else
            {
                if (button != null)
                    button.interactable = true;

                if (nameText != null)
                    nameText.gameObject.SetActive(true);
            }

            RenderState();
        }

        private void EnsureGridElements()
        {
            // Ensure we have an icon + count text for grid slots.
            // Keep this lightweight: only searches/creates when references are missing.

            try
            {
                if (iconImage == null)
                {
                    var t = transform.Find("Icon");
                    if (t != null) iconImage = t.GetComponent<Image>();
                }
            }
            catch { }

            try
            {
                if (countText == null)
                {
                    var t = transform.Find("Count");
                    if (t != null) countText = t.GetComponent<TMP_Text>();
                }
            }
            catch { }

            // If the template doesn't have these (older scenes), create them.
            if (iconImage == null)
            {
                try
                {
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    iconImage = go.GetComponent<Image>();
                    iconImage.preserveAspect = true;
                }
                catch { }
            }

            if (countText == null)
            {
                try
                {
                    var go = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    tmp.text = string.Empty;
                    tmp.fontSize = 16f;
                    tmp.alignment = TextAlignmentOptions.BottomRight;
                    tmp.raycastTarget = false;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    countText = tmp;
                }
                catch { }
            }

            try { if (iconImage != null) iconImage.raycastTarget = false; } catch { }
            try { if (countText != null) countText.raycastTarget = false; } catch { }
        }

        private void ApplyVisuals(Sprite icon, AbyssItemRarity rarity)
        {
            if (iconImage != null)
            {
                // User requirement: never use the runtime white sprite (borders) as an icon placeholder.
                // If icon is null, disable the icon image so we don't show gray squares.
                bool hasIcon = icon != null;

                iconImage.sprite = icon;
                iconImage.enabled = hasIcon;
                if (iconImage.gameObject.activeSelf != hasIcon)
                    iconImage.gameObject.SetActive(hasIcon);

                if (hasIcon)
                {
                    try
                    {
                        var iconCol = iconImage.color;
                        iconImage.color = new Color(iconCol.r, iconCol.g, iconCol.b, 1f);
                    }
                    catch { }
                    try
                    {
                        iconImage.type = Image.Type.Simple;
                        iconImage.preserveAspect = true;
                        iconImage.raycastTarget = false;
                    }
                    catch { }

                    try { iconImage.SetAllDirty(); } catch { }
                }
            }

            rarity = ItemRarityVisuals.Normalize(rarity);
            var rarityColor = InventoryRarityColors.GetColor(rarity);

            if (rarityStrip != null)
            {
                rarityStrip.enabled = _hasItem;
                rarityStrip.color = _hasItem
                    ? rarityColor
                    : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0f);
                if (!rarityStrip.gameObject.activeSelf)
                    rarityStrip.gameObject.SetActive(true);
            }
            else if (nameText != null)
            {
                if (_baseNameColor.a <= 0f)
                    _baseNameColor = nameText.color;

                nameText.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, _baseNameColor.a);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RenderState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RenderState();
        }

        private void EnsureDefaultColors()
        {
            if (_colorsInitialized)
                return;

            var baseC = background != null ? background.color : default;
            if (baseC.a <= 0f)
                baseC = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            _listBaseColor = baseC;
            _listHoverColor = AddRgb(baseC, 0.10f);

            _colorsInitialized = true;
        }

        private Image ResolveBackgroundImage()
        {
            if (background != null)
                return background;

            // Prefer a dedicated inner fill image.
            try
            {
                var t = transform.Find("InnerBackground");
                if (t != null)
                {
                    var img = t.GetComponent<Image>();
                    if (img != null)
                    {
                        background = img;
                        return background;
                    }
                }
            }
            catch { }

            background = GetComponent<Image>();
            if (background != null)
                return background;

            // Otherwise, find the first suitable Image in children.
            try
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img == null) continue;
                        if (img == iconImage) continue;
                        if (img == rarityStrip) continue;
                        if (img == hoverBorderImage) continue;
                        if (img == selectedBorderImage) continue;
                        if (string.Equals(img.name, "HoverBorderImage", StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(img.name, "SelectedBorderImage", StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(img.name, "InnerBackground", StringComparison.OrdinalIgnoreCase)) continue;

                        background = img;
                        return background;
                    }
                }
            }
            catch { }

            return null;
        }

        private Image EnsureChildImage(string name)
        {
            return EnsureChildImage(transform, name);
        }

        private Image EnsureChildImage(Transform parent, string name)
        {
            try
            {
                var t = parent != null ? parent.Find(name) : null;
                if (t != null)
                {
                    var existing = t.GetComponent<Image>();
                    if (existing != null)
                        return existing;
                }
            }
            catch { }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent != null ? parent : transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go.GetComponent<Image>();
        }

        private RectTransform EnsureBorderRoot()
        {
            if (borderRoot != null)
                return borderRoot;

            try
            {
                var t = transform.Find("BorderRoot");
                if (t != null)
                {
                    borderRoot = t as RectTransform;
                    if (borderRoot == null) borderRoot = t.GetComponent<RectTransform>();
                    return borderRoot;
                }
            }
            catch { }

            var go = new GameObject("BorderRoot", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            borderRoot = go.GetComponent<RectTransform>();
            borderRoot.anchorMin = Vector2.zero;
            borderRoot.anchorMax = Vector2.one;
            borderRoot.pivot = new Vector2(0.5f, 0.5f);
            borderRoot.offsetMin = Vector2.zero;
            borderRoot.offsetMax = Vector2.zero;
            return borderRoot;
        }

        private void EnsureGridInnerBackground()
        {
            // Avoid offsetting the slot root RectTransform (GridLayoutGroup owns it).
            // Force every slot to use the SAME fill hierarchy: an InnerBackground child inset by 2px.
            // This prevents mixed hierarchies (root Image vs child Image) from causing border inconsistencies.

            var inner = EnsureChildImage("InnerBackground");
            inner.raycastTarget = true;
            inner.type = Image.Type.Simple;

            // If we already had a background reference that isn't InnerBackground, migrate its visuals.
            try
            {
                if (background != null && background != inner)
                {
                    inner.sprite = background.sprite;
                    inner.material = background.material;
                    inner.color = background.color;
                }
                else
                {
                    var rootImg = GetComponent<Image>();
                    if (rootImg != null && rootImg != inner)
                    {
                        inner.sprite = rootImg.sprite;
                        inner.material = rootImg.material;
                        inner.color = rootImg.color;
                    }
                }
            }
            catch { }

            // Hide any root Image so it cannot cover the border lines.
            try
            {
                var rootImg = GetComponent<Image>();
                if (rootImg != null)
                {
                    rootImg.color = new Color(1f, 1f, 1f, 0f);
                    rootImg.raycastTarget = false;
                }
            }
            catch { }

            try
            {
                var rt = inner.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-2f, -2f);
            }
            catch { }

            // Keep fill underneath everything else.
            try { inner.transform.SetAsFirstSibling(); } catch { }

            background = inner;
        }

        private Image FindOrCreateUniqueBorderLine(string name, RectTransform desiredParent, ref Image cache)
        {
            if (cache != null)
            {
                try
                {
                    if (desiredParent != null && cache.transform.parent != desiredParent)
                        cache.transform.SetParent(desiredParent, false);
                    cache.gameObject.SetActive(true);
                }
                catch { }
                return cache;
            }

            Image found = null;
            try
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img == null) continue;
                        if (!string.Equals(img.name, name, StringComparison.Ordinal))
                            continue;

                        if (found == null)
                        {
                            found = img;
                        }
                        else
                        {
                            // Disable duplicates to ensure every slot uses ONE consistent border set.
                            try { img.gameObject.SetActive(false); } catch { }
                        }
                    }
                }
            }
            catch { }

            if (found == null)
            {
                found = EnsureChildImage(desiredParent != null ? desiredParent.transform : transform, name);
            }

            cache = found;
            try
            {
                if (desiredParent != null && cache.transform.parent != desiredParent)
                    cache.transform.SetParent(desiredParent, false);
                cache.gameObject.SetActive(true);
            }
            catch { }

            return cache;
        }


        private void DisableLegacyBorders()
        {
            // Disable any previously attempted border approaches to prevent conflicts.
            try
            {
                if (hoverBorderImage != null) hoverBorderImage.enabled = false;
                if (selectedBorderImage != null) selectedBorderImage.enabled = false;

                var legacyHover = transform.Find("HoverBorderImage");
                if (legacyHover != null)
                {
                    var img = legacyHover.GetComponent<Image>();
                    if (img != null) img.enabled = false;
                }

                var legacySelected = transform.Find("SelectedBorderImage");
                if (legacySelected != null)
                {
                    var img = legacySelected.GetComponent<Image>();
                    if (img != null) img.enabled = false;
                }

                var oldHover = transform.Find("HoverBorder");
                if (oldHover != null) oldHover.gameObject.SetActive(false);

                var oldSelected = transform.Find("SelectedBorder");
                if (oldSelected != null) oldSelected.gameObject.SetActive(false);

                // Hard-disable by name as well (prefab leftovers / older runtime systems).
                var legacyNames = new[] { "HoverBorderImage", "SelectedBorderImage", "HoverBorder", "SelectedBorder" };
                for (int i = 0; i < legacyNames.Length; i++)
                {
                    var t = transform.Find(legacyNames[i]);
                    if (t != null) t.gameObject.SetActive(false);
                }
            }
            catch { }

            // Ensure any Outline on the root/background is off.
            try
            {
                var o1 = GetComponent<Outline>();
                if (o1 != null) o1.enabled = false;
            }
            catch { }

            try
            {
                if (background != null)
                {
                    var o2 = background.GetComponent<Outline>();
                    if (o2 != null) o2.enabled = false;
                }
            }
            catch { }
        }

        private void EnsureGridBorderLines()
        {
            if (!_isGridMode)
                return;

            ResolveBackgroundImage();
            EnsureGridInnerBackground();

            if (!_gridBordersInitialized)
            {
                // Make sure no legacy systems remain active.
                DisableLegacyBorders();
                _gridBordersInitialized = true;
            }

            var br = EnsureBorderRoot();
            try { if (br != null) br.transform.SetAsLastSibling(); } catch { }

            // IMPORTANT: reuse ANY existing border lines (even if they were created under a different parent)
            // and disable duplicates. This guarantees a single border construction codepath for every slot.
            borderTop = FindOrCreateUniqueBorderLine("BorderTop", br, ref borderTop);
            borderBottom = FindOrCreateUniqueBorderLine("BorderBottom", br, ref borderBottom);
            borderLeft = FindOrCreateUniqueBorderLine("BorderLeft", br, ref borderLeft);
            borderRight = FindOrCreateUniqueBorderLine("BorderRight", br, ref borderRight);

            SetupLine(borderTop);
            SetupLine(borderBottom);
            SetupLine(borderLeft);
            SetupLine(borderRight);

            // Ensure line geometry (anchors/pivots) is correct.
            try
            {
                ConfigureTop(borderTop.rectTransform, BorderThicknessNormal);
                ConfigureBottom(borderBottom.rectTransform, BorderThicknessNormal);
                ConfigureLeft(borderLeft.rectTransform, BorderThicknessNormal);
                ConfigureRight(borderRight.rectTransform, BorderThicknessNormal);
            }
            catch { }

            // Draw borders above everything but never block clicks.
            try
            {
                if (borderTop != null) borderTop.transform.SetAsLastSibling();
                if (borderBottom != null) borderBottom.transform.SetAsLastSibling();
                if (borderLeft != null) borderLeft.transform.SetAsLastSibling();
                if (borderRight != null) borderRight.transform.SetAsLastSibling();
            }
            catch { }
        }

        private static void SetupLine(Image img)
        {
            if (img == null)
                return;

            img.enabled = true;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;

            // Ensure a valid source sprite so the Image actually draws.
            if (img.sprite == null)
                img.sprite = GetOrCreateWhiteSprite();
        }

        private static void ConfigureTop(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, -thickness);
            rt.offsetMax = new Vector2(0f, 0f);
        }

        private static void ConfigureBottom(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, thickness);
        }

        private static void ConfigureLeft(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(thickness, 0f);
        }

        private static void ConfigureRight(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(-thickness, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
        }

        private void RenderState()
        {
            ResolveBackgroundImage();
            if (background == null)
                return;

            EnsureDefaultColors();

            bool isEmpty = !_hasItem;
            bool isHovered = _isHovered;
            bool isSelected = _isSelected;

            // Count visibility.
            if (countText != null)
            {
                if (isEmpty)
                {
                    if (!string.IsNullOrEmpty(countText.text))
                        countText.text = string.Empty;

                    if (_isGridMode)
                    {
                        try { if (countText.gameObject.activeSelf) countText.gameObject.SetActive(false); } catch { }
                    }
                }
                else
                {
                    if (_isGridMode)
                    {
                        if (_boundCount > 1)
                        {
                            countText.text = $"x{Mathf.Max(0, _boundCount)}";
                            try { if (!countText.gameObject.activeSelf) countText.gameObject.SetActive(true); } catch { }
                        }
                        else
                        {
                            countText.text = string.Empty;
                            try { if (countText.gameObject.activeSelf) countText.gameObject.SetActive(false); } catch { }
                        }
                    }
                    else
                    {
                        // UI requirement: show stack count only if > 1.
                        countText.text = _boundCount > 1 ? $"x{Mathf.Max(0, _boundCount)}" : string.Empty;
                    }
                }
            }

            // Icon visibility.
            if (_isGridMode && iconImage != null && isEmpty)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
                if (iconImage.gameObject.activeSelf)
                    iconImage.gameObject.SetActive(false);
            }

            // Ensure raycast target graphic.
            try
            {
                if (button == null)
                    button = GetComponent<Button>();

                if (_isGridMode)
                {
                    EnsureGridBorderLines();
                    DisableLegacyBorders();

                    // Force borders to top every frame.
                    try { if (borderRoot != null) borderRoot.transform.SetAsLastSibling(); } catch { }

                    background.raycastTarget = true;
                    if (button != null) button.targetGraphic = background;
                }
                else
                {
                    if (background != null)
                        background.raycastTarget = true;
                    if (button != null && button.targetGraphic == null)
                        button.targetGraphic = background;

                    // Hide grid borders in list mode.
                    if (borderTop != null) borderTop.enabled = false;
                    if (borderBottom != null) borderBottom.enabled = false;
                    if (borderLeft != null) borderLeft.enabled = false;
                    if (borderRight != null) borderRight.enabled = false;
                }
            }
            catch { }

            if (!_isGridMode)
            {
                background.color = isHovered ? _listHoverColor : _listBaseColor;
                return;
            }

            // Grid fill.
            var baseFill = isEmpty ? GridEmptyFill : GridOccupiedFill;
            background.color = isSelected ? AddRgb(baseFill, GridSelectedFillBrighten) : baseFill;

            // Borders: Selected > Hover > Normal. Geometry stays constant.
            try
            {
                float thickness = BorderThicknessNormal;

                if (borderTop != null)
                {
                    borderTop.enabled = true;
                    ConfigureTop(borderTop.rectTransform, thickness);
                }
                if (borderBottom != null)
                {
                    borderBottom.enabled = true;
                    ConfigureBottom(borderBottom.rectTransform, thickness);
                }
                if (borderLeft != null)
                {
                    borderLeft.enabled = true;
                    ConfigureLeft(borderLeft.rectTransform, thickness);
                }
                if (borderRight != null)
                {
                    borderRight.enabled = true;
                    ConfigureRight(borderRight.rectTransform, thickness);
                }

                Color borderColor = GridBorderNormalColor;
                if (isSelected) borderColor = GridSelectedBorderColor;
                else if (isHovered) borderColor = GridHoverBorderColor;

                if (borderTop != null) borderTop.color = borderColor;
                if (borderBottom != null) borderBottom.color = borderColor;
                if (borderLeft != null) borderLeft.color = borderColor;
                if (borderRight != null) borderRight.color = borderColor;
            }
            catch { }
        }

        private static Color AddRgb(Color c, float amount)
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
        public bool IsGridMode => _isGridMode;
    }
}
```

---

## Assets/Abyss/Inventory/PlayerInventoryUI.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Abyss.Items;
using Abyss.Equipment;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using AbyssItemRarity = Abyss.Items.ItemRarity;
using AbyssItemType = Abyss.Items.ItemType;

namespace Abyss.Inventory
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerInventoryUI : MonoBehaviour
    {
        private enum InventoryTab
        {
            WeaponsGear = 0,
            Materials = 1,
            Consumables = 2,
            Skilling = 3,
        }

        private const bool INVENTORY_UI_DEBUG = false;
        private static bool InventoryUiDebugEnabled => INVENTORY_UI_DEBUG;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const bool INV_DIAGNOSTICS = true;
    #else
        private const bool INV_DIAGNOSTICS = false;
    #endif

        // OSRS-style inventory grid.
        private const int InventoryGridColumns = 4;
        private const int InventoryGridRows = 7;
        private const int InventoryGridSlots = InventoryGridColumns * InventoryGridRows;

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;

        [Header("Character Tabs (optional)")]
        [SerializeField] private Button characterInventoryTabButton;
        [SerializeField] private Button characterEquipmentTabButton;

        [Header("Top")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;

        [Header("List")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private PlayerInventoryRowUI rowTemplate;

        [Header("Tabs (optional)")]
        [Tooltip("If not set, a simple tab bar will be created at runtime above the list.")]
        [SerializeField] private RectTransform tabsRoot;

        [Header("Details")]
        [SerializeField] private PlayerInventoryDetailsUI detailsUI;

        private Game.Input.PlayerInputAuthority _inputAuthority;
        private PlayerInventory _inventory;
        private Abyss.Shop.PlayerGoldWallet _wallet;
        private PlayerEquipment _equipment;
        private Abyss.Equipment.PlayerEquipmentUI _equipmentUi;

        private string _inventorySource;
        private int _lastInventoryInstanceId;
        private bool _loggedInventoryForThisOpen;
        private bool _loggedScrollWiringForThisOpen;
        private bool _loggedFirstRowVisibilityThisOpen;

        private readonly List<GameObject> _spawnedRows = new();
        private readonly List<PlayerInventoryRowUI> _spawnedSlotViews = new();
        private Dictionary<string, ItemDefinition> _itemDefById;

        private string _selectedItemId;
        private ItemDefinition _selectedDef;
        private int _selectedCount;

        // UI-only selection index for visuals (grid slot index 0..27, or -1 none)
        private int _selectedSlotIndex = -1;

        private InventoryTab _activeTab = InventoryTab.WeaponsGear;

        private Button _tabWeapons;
        private Button _tabMaterials;
        private Button _tabConsumables;
        private Button _tabSkilling;

        private TMP_Text _tabWeaponsText;
        private TMP_Text _tabMaterialsText;
        private TMP_Text _tabConsumablesText;
        private TMP_Text _tabSkillingText;

        private Button _equipButton;
        private TMP_Text _equipButtonText;

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private readonly Dictionary<Image, Color> _forcedOpaqueImages = new();

        private Image _backdropImage;
        private Color _backdropOriginalColor;
        private bool _backdropOriginalCaptured;

        private int _lastRefreshFrame = -1;
        private bool _refreshQueued;

        private bool _warnedContentLayoutConflict;

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
                titleText.text = "Inventory";

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;

            // Resolve inventory on-demand to avoid wrong instance bindings.
            _inventory = null;

            detailsUI?.Clear();

            WireCharacterTabs();
        }

        private void Update()
        {
            // Some scenes/scripts may toggle the inventory root active without calling Open()/Close().
            // Keep _isOpen in sync so hotkeys/buttons still work.
            SyncOpenStateFromRoot();

            if (_refreshQueued)
            {
                _refreshQueued = false;
                RefreshAll();
            }

            // TASK 1: fallback input: E equips selected item while inventory is open.
            if (_isOpen && WasEquipPressed() && !Abyss.Shop.MerchantShopUI.IsOpen)
                TryEquipSelected();

            if (!WasTogglePressed())
                return;

            // Avoid fighting with merchant UI.
            if (Abyss.Shop.MerchantShopUI.IsOpen)
                return;

            if (_isOpen) Close();
            else Open();
        }

        private bool WasEquipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private void SyncOpenStateFromRoot()
        {
            if (root == null)
                return;

            // If the root is visible, treat as open.
            if (root.activeSelf)
            {
                if (!_isOpen)
                {
                    _isOpen = true;
                    EnsureEquipButton();
                    EnsureInventory();
                    EnsureEquipment();
                    RefreshDetails();
                }
            }
            else
            {
                if (_isOpen)
                    _isOpen = false;
            }
        }

        private static EquipmentSlot GuessEquipSlot(ItemDefinition def)
        {
            if (def == null) return EquipmentSlot.None;

            try
            {
                if (def.equipmentSlot != EquipmentSlot.None)
                    return def.equipmentSlot;
            }
            catch { }

            return EquipmentSlot.None;
        }

        private bool CanEquipSelected(ItemDefinition def)
        {
            if (def == null)
                return false;

            try
            {
                if (def.equipmentSlot != EquipmentSlot.None)
                    return true;
            }
            catch { }

            return false;
        }

        private static string SanitizeReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "";

            reason = reason.Replace("\r", " ").Replace("\n", " ");
            while (reason.Contains("  "))
                reason = reason.Replace("  ", " ");
            return reason.Trim();
        }

        private static void LogEquipAttempt(string itemId, EquipmentSlot slot, bool success, string reason)
        {
            itemId ??= "";
            reason = SanitizeReason(reason);
            var ok = success.ToString().ToLowerInvariant();
            Debug.Log($"[EQUIP] itemId={itemId} slot={slot} success={ok} reason={reason}");
        }

        private void TryEquipSelected()
        {
            EnsureEquipment();

            var def = _selectedDef;
            var itemId = _selectedItemId;
            var slot = GuessEquipSlot(def);

            // TASK 1: single log line per attempt, exactly matching requested format.
            if (_equipment == null)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No PlayerEquipment");
                return;
            }

            if (def == null || string.IsNullOrWhiteSpace(itemId))
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No item selected");
                return;
            }

            // Spec: equippable only when equipmentSlot != None.
            if (slot == EquipmentSlot.None)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "Not equippable (equipmentSlot=None)");
                return;
            }

            // MVP: equip visually only (do NOT consume inventory yet).
            bool ok = _equipment.TryEquip(def, out var message);
            string reason = string.IsNullOrWhiteSpace(message) ? (ok ? "OK" : "Failed") : message;
            LogEquipAttempt(itemId, slot, ok, reason);
        }

        public void Open()
        {
            if (root == null || _isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Open frame={Time.frameCount}", this);
#endif

            _isOpen = true;
            root.SetActive(true);

            HideStrayLegacyCategoryTexts();
            EnsureBackdropIsTransparent();
            ForceOpaqueBackground(true);

            _loggedInventoryForThisOpen = false;
            _loggedScrollWiringForThisOpen = false;
            _loggedFirstRowVisibilityThisOpen = false;

            EnsureCanvasVisibility();
            EnsureScrollRectWiring();
            EnsureScrollViewLayoutHard(); // NEW: enforce known-good layout up front

            EnsureTabs();

            EnsureEquipButton();

            BringListToFront();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] BringListToFront parent='{scrollRect.transform.parent?.name}' siblingIndex={scrollRect.transform.GetSiblingIndex()} childCount={scrollRect.transform.parent?.childCount}", this);
#endif

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;
            if (_wallet != null)
            {
                _wallet.GoldChanged -= OnGoldChanged;
                _wallet.GoldChanged += OnGoldChanged;
            }

            EnsureInventory();
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.Changed += OnInventoryChanged;
            }

            EnsureEquipment();

            if (_equipmentUi == null)
            {
                try
                {
#if UNITY_2022_2_OR_NEWER
                    _equipmentUi = FindFirstObjectByType<Abyss.Equipment.PlayerEquipmentUI>();
#else
                    _equipmentUi = FindObjectOfType<Abyss.Equipment.PlayerEquipmentUI>();
#endif
                }
                catch { }
            }

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            // Build the grid immediately so we don't show a blank/flashy intermediate frame.
            _refreshQueued = false;
            RefreshAll();
        }

        private void WireCharacterTabs()
        {
            // Inventory tab is "selected" while this window is open.
            if (characterInventoryTabButton != null)
                characterInventoryTabButton.interactable = false;

            if (characterEquipmentTabButton != null)
            {
                characterEquipmentTabButton.onClick.RemoveAllListeners();
                characterEquipmentTabButton.onClick.AddListener(() =>
                {
                    Close();
                    try { _equipmentUi?.Open(); } catch { }
                });
            }
        }

        private void EnsureBackdropIsTransparent()
        {
            if (root == null)
                return;

            try
            {
                if (_backdropImage == null)
                {
                    var t = FindDeepChild(root.transform, "Backdrop");
                    if (t != null)
                        _backdropImage = t.GetComponent<Image>();
                }

                if (_backdropImage == null)
                    return;

                if (!_backdropOriginalCaptured)
                {
                    _backdropOriginalCaptured = true;
                    _backdropOriginalColor = _backdropImage.color;
                }

                // User request: see the game behind the inventory. Keep the backdrop for raycast-blocking,
                // but make it visually transparent.
                var c = _backdropImage.color;
                if (c.a > 0.001f)
                    _backdropImage.color = new Color(c.r, c.g, c.b, 0f);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void ForceOpaqueBackground(bool enabled)
        {
            if (root == null)
                return;

            if (!enabled)
            {
                if (_forcedOpaqueImages.Count == 0)
                    return;

                foreach (var kv in _forcedOpaqueImages)
                {
                    try
                    {
                        if (kv.Key != null)
                            kv.Key.color = kv.Value;
                    }
                    catch { }
                }

                _forcedOpaqueImages.Clear();
                return;
            }

            if (_forcedOpaqueImages.Count > 0)
                return;

            // Preferred: known names created by BuildPlayerInventoryUIEditor.
            TryForceOpaqueByName("Panel");
            TryForceOpaqueByName("ItemsScrollView");
            TryForceOpaqueByName("DetailsPanel");

            // Fallback: if we couldn't find any of the conventional panels, force opaque on large semi-transparent
            // images under the inventory root (excluding item tiles/tabs/details).
            if (_forcedOpaqueImages.Count == 0)
                TryForceOpaqueHeuristic();
        }

        private void TryForceOpaqueByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || root == null)
                return;

            var t = FindDeepChild(root.transform, name);
            if (t == null)
                return;

            var img = t.GetComponent<Image>();
            if (img == null)
                return;

            ForceOpaque(img);
        }

        private void TryForceOpaqueHeuristic()
        {
            try
            {
                var images = root.GetComponentsInChildren<Image>(true);
                if (images == null || images.Length == 0)
                    return;

                for (int i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (img == null)
                        continue;

                    // Never force the full-screen backdrop opaque; we want the world visible.
                    if (string.Equals(img.gameObject.name, "Backdrop", StringComparison.Ordinal))
                        continue;

                    if (contentRoot != null && img.transform.IsChildOf(contentRoot))
                        continue;
                    if (tabsRoot != null && img.transform.IsChildOf(tabsRoot))
                        continue;
                    if (detailsUI != null && img.transform.IsChildOf(detailsUI.transform))
                        continue;
                    if (rowTemplate != null && img.transform.IsChildOf(rowTemplate.transform))
                        continue;

                    var rt = img.rectTransform;
                    if (rt == null)
                        continue;

                    float area = Mathf.Abs(rt.rect.width * rt.rect.height);
                    if (area < 20000f)
                        continue;

                    ForceOpaque(img);
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void ForceOpaque(Image img)
        {
            if (img == null)
                return;

            try
            {
                var c = img.color;
                if (c.a >= 0.999f)
                    return;

                if (!_forcedOpaqueImages.ContainsKey(img))
                    _forcedOpaqueImages.Add(img, c);

                c.a = 1f;
                img.color = c;
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;

                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;

                var found = FindDeepChild(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void HideStrayLegacyCategoryTexts()
        {
            if (root == null)
                return;

            try
            {
                var texts = root.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                    return;

                for (int i = 0; i < texts.Length; i++)
                {
                    var t = texts[i];
                    if (t == null) continue;

                    // Keep known UI text elements.
                    if (t == titleText || t == goldText)
                        continue;

                    if (detailsUI != null && t.transform.IsChildOf(detailsUI.transform))
                        continue;

                    // Don't hide tab button labels.
                    if (tabsRoot != null && t.transform.IsChildOf(tabsRoot))
                        continue;

                    // Don't hide inventory slot row text.
                    if (rowTemplate != null && t.transform.IsChildOf(rowTemplate.transform))
                        continue;
                    if (contentRoot != null && t.transform.IsChildOf(contentRoot))
                        continue;

                    var s = t.text;
                    if (string.IsNullOrWhiteSpace(s))
                        continue;

                    s = s.Trim();
                    var lower = s.ToLowerInvariant();

                    // Legacy category label(s) that shouldn't float over the grid.
                    if (lower.Contains("weapon") && (lower.Contains("util") || lower.Contains("utility") || lower.Contains("utilities")))
                    {
                        t.gameObject.SetActive(false);
                        continue;
                    }
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        public void Close()
        {
            if (!_isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Close frame={Time.frameCount}", this);
#endif

            _isOpen = false;

            ForceOpaqueBackground(false);

            // Restore original backdrop tint (if any).
            try
            {
                if (_backdropImage != null && _backdropOriginalCaptured)
                    _backdropImage.color = _backdropOriginalColor;
            }
            catch { }

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

            if (_wallet != null)
                _wallet.GoldChanged -= OnGoldChanged;

            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            detailsUI?.Clear();

            if (root != null)
                root.SetActive(false);
        }

        private void RefreshAll()
        {
            RefreshGold();
            RefreshList();
            RefreshDetails();
        }

        private void OnGoldChanged(int newGold)
        {
            RefreshGold();
        }

        private void OnInventoryChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] OnInventoryChanged frame={Time.frameCount}", this);
#endif
            // If inventory changes while open, refresh list/details.
            if (!_isOpen) return;
            RefreshList();
            RefreshDetails();
        }

        private void RefreshGold()
        {
            if (goldText == null)
                return;

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;
            int g = _wallet != null ? _wallet.Gold : 0;
            goldText.text = $"Gold: {g}";
        }

        private void RefreshList()
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] RefreshList ENTER frame={Time.frameCount}", this);
#endif

            // Prevent multiple rebuilds in the same frame
            if (_lastRefreshFrame == Time.frameCount)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] RefreshList SKIPPED (already ran this frame)", this);
#endif
                return;
            }

            _lastRefreshFrame = Time.frameCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList BEGIN frame=" + Time.frameCount, this);
#endif

            if (contentRoot == null || rowTemplate == null)
                return;

            BringListToFront();

            EnsureScrollRectWiring();
            EnsureScrollViewLayoutHard(); // NEW: enforce layout every refresh (safe, cheap)

            EnsureInventory();
            if (_inventory == null)
            {
                ClearRows();
                return;
            }

            _itemDefById ??= BuildItemDefinitionIndex();

            // Keep template under contentRoot and disabled.
            if (rowTemplate.transform != null && rowTemplate.transform.parent != contentRoot)
                rowTemplate.transform.SetParent(contentRoot, false);
            if (rowTemplate.gameObject.activeSelf)
                rowTemplate.gameObject.SetActive(false);

            ClearRows();

            var snap = _inventory.GetAllItemsSnapshot();
            if (snap == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList snapshotCount=" + snap.Count + " frame=" + Time.frameCount, this);
#endif

            // One-time diagnostics per open (no spam).
            if (_isOpen)
            {
                try
                {
                    int stacks = snap.Count;
                    int total = 0;
                    foreach (var kv in snap)
                        total += Mathf.Max(0, kv.Value);

                    int instanceId = 0;
                    try { instanceId = _inventory != null ? _inventory.GetInstanceID() : 0; } catch { }

                    if (!_loggedInventoryForThisOpen || instanceId != _lastInventoryInstanceId)
                    {
                        _loggedInventoryForThisOpen = true;
                        _lastInventoryInstanceId = instanceId;
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Inventory source={_inventorySource ?? "(unknown)"} instanceId={instanceId} stacks={stacks} totalItems={total}", this);
                    }
                }
                catch { }
            }

            // Deterministic iteration to avoid flicker.
            var keys = new List<string>(snap.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            PlayerInventoryRowUI first = null;
            string firstId = null;
            int firstCount = 0;
            int renderedStacks = 0;
            int createdRowCount = 0;
            int rowIndex = 0;

            // Build a filtered list of stacks, then place them into a fixed 4x7 grid.
            var visibleStacks = new List<(string itemId, int count, ItemDefinition def)>(keys.Count);
            foreach (var itemId in keys)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                int count = snap.TryGetValue(itemId, out var c) ? c : 0;
                if (count <= 0)
                    continue;

                var def = ResolveItemDefinition(itemId);
                if (!PassesTabFilter(def, itemId))
                    continue;

                visibleStacks.Add((itemId, count, def));
            }

            // Sync selected slot index based on currently selected item id (UI-only).
            _selectedSlotIndex = FindSelectedSlotIndexInVisibleStacks(visibleStacks, _selectedItemId);

            if (visibleStacks.Count > InventoryGridSlots)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerInventoryUI] Inventory has {visibleStacks.Count} stacks but UI is fixed to {InventoryGridSlots} slots; truncating display.", this);
#endif
            }

            // Prefer square cells sized to fit the viewport.
            Vector2 cellSize = new Vector2(64f, 64f);
            try
            {
                if (contentRoot != null)
                {
                    var grid = contentRoot.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                        cellSize = grid.cellSize;
                }
            }
            catch { }

            for (int slotIndex = 0; slotIndex < InventoryGridSlots; slotIndex++)
            {
                bool hasItem = slotIndex < visibleStacks.Count;
                string itemId = hasItem ? visibleStacks[slotIndex].itemId : null;
                int count = hasItem ? visibleStacks[slotIndex].count : 0;
                var def = hasItem ? visibleStacks[slotIndex].def : null;

                int capturedSlotIndex = slotIndex;

                var go = Instantiate(rowTemplate.gameObject, contentRoot, false);
                createdRowCount++;
                go.name = hasItem ? $"Row_{itemId}" : $"EmptySlot_{slotIndex}";
                go.SetActive(true);

                var capturedGo = go;

                // IMPORTANT: put the row into grid mode immediately so it never renders a list-mode
                // (often-white) background for a frame.
                var row = go.GetComponent<PlayerInventoryRowUI>();
                if (row != null)
                    row.SetGridMode(true);

                // Requirement: stable slot index stored on each row.
                if (row != null)
                {
                    try { row.SetSlotIndex(slotIndex); } catch { }
                }

                if (row != null)
                {
                    if (_spawnedSlotViews.Count <= capturedSlotIndex)
                    {
                        while (_spawnedSlotViews.Count <= capturedSlotIndex)
                            _spawnedSlotViews.Add(null);
                    }
                    _spawnedSlotViews[capturedSlotIndex] = row;
                }

                // Ensure stable layout metadata.
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                le.preferredWidth = cellSize.x;
                le.preferredHeight = cellSize.y;
                le.minWidth = cellSize.x;
                le.minHeight = cellSize.y;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // GridLayoutGroup controls positioning and size.
                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;

                    // Normalize anchors so layout calculations are consistent.
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

                // Brute-force visibility so â€œclickable but invisibleâ€ canâ€™t happen.
                ForceRowVisible(go);

                bool isSelected = hasItem && _selectedSlotIndex == capturedSlotIndex;
                ApplyRowVisualStyling(go, rowIndex, isSelected);

                // --- [INV] Debug: build-time diagnostics (one log per slot) ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (INV_DIAGNOSTICS)
                {
                    try
                    {
                        var btn = go.GetComponent<Button>();
                        var img = go.GetComponent<Image>();
                        Debug.Log($"[INV][BUILD] Slot {slotIndex} | empty={!hasItem} | hasButton={(btn != null)} | hasImage={(img != null)} | raycast={(img != null && img.raycastTarget)}", this);
                    }
                    catch { }
                }
#endif

                if (row != null)
                {
                    // Debug context for hover logs.
                    try { row.SetDebugContext(slotIndex, !hasItem); } catch { }

                    if (hasItem)
                    {
                        string capturedId = itemId;
                        int capturedCount = count;
                        row.Bind(def, capturedId, capturedCount, () =>
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][CLICK] slotIndex={capturedSlotIndex} empty=false", this);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (INV_DIAGNOSTICS)
                            {
                                try
                                {
                                    Debug.Log($"[INV][CLICK] Slot {slotIndex} empty=false", this);
                                    Debug.Log($"[INV][CLICK ITEM] Selecting itemId={capturedId}", this);
                                    Debug.Log($"[INV][RAYCAST] currentSelected={((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject?.name : "(no EventSystem)")}", this);

                                    var btn = go.GetComponent<Button>();
                                    var img = go.GetComponent<Image>();
                                    Debug.Log($"[INV][RAYCAST TARGETS] hasButton={(btn != null)} targetGraphic={(btn != null && btn.targetGraphic != null ? btn.targetGraphic.name : "(null)")} hasImage={(img != null)} imgRaycast={(img != null && img.raycastTarget)}", this);
                                }
                                catch { }
                            }
#endif
                            _selectedSlotIndex = capturedSlotIndex;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SEL SET] _selectedSlotIndex={_selectedSlotIndex}", this);
#endif
                            Select(capturedId, capturedCount);
                            UpdateSelectionVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SELECT] slot={_selectedSlotIndex}", this);
#endif
                        });

                        if (first == null)
                        {
                            first = row;
                            firstId = capturedId;
                            firstCount = capturedCount;
                        }

                        renderedStacks++;
                    }
                    else
                    {
                        row.BindEmpty(() =>
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][CLICK] slotIndex={capturedSlotIndex} empty=true", this);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (INV_DIAGNOSTICS)
                            {
                                try
                                {
                                    Debug.Log($"[INV][CLICK] Slot {slotIndex} empty=true", this);
                                    Debug.Log("[INV][CLICK EMPTY] Clearing selection", this);
                                    Debug.Log($"[INV][RAYCAST] currentSelected={((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject?.name : "(no EventSystem)")}", this);

                                    var btn = go.GetComponent<Button>();
                                    var img = go.GetComponent<Image>();
                                    Debug.Log($"[INV][RAYCAST TARGETS] hasButton={(btn != null)} targetGraphic={(btn != null && btn.targetGraphic != null ? btn.targetGraphic.name : "(null)")} hasImage={(img != null)} imgRaycast={(img != null && img.raycastTarget)}", this);
                                }
                                catch { }
                            }
#endif
                            _selectedSlotIndex = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SEL SET] _selectedSlotIndex={_selectedSlotIndex}", this);
#endif
                            ClearSelection();
                            UpdateSelectionVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SELECT] slot={_selectedSlotIndex}", this);
#endif
                        });
                    }
                }

                rowIndex++;
                _spawnedRows.Add(go);
            }

            // Keep selection valid.
            // If nothing is selected, keep it empty (do not auto-select first).
            if (!string.IsNullOrWhiteSpace(_selectedItemId))
            {
                // If selected item no longer exists, fall back to first.
                int selCount = snap.TryGetValue(_selectedItemId, out var sc) ? sc : 0;
                if (selCount <= 0 && first != null)
                {
                    Select(firstId, firstCount);
                    _selectedSlotIndex = 0;
                }
                else if (_selectedSlotIndex < 0)
                {
                    // Selection exists in inventory but is filtered out by current tab.
                    if (first != null)
                    {
                        Select(firstId, firstCount);
                        _selectedSlotIndex = 0;
                    }
                    else
                    {
                        _selectedItemId = null;
                        _selectedDef = null;
                        _selectedCount = 0;
                        detailsUI?.Clear();
                        _selectedSlotIndex = -1;
                    }
                }
            }
            else
            {
                _selectedSlotIndex = -1;
            }

            // Ensure selection visuals survive RefreshList rebuild.
            UpdateSelectionVisuals();

            // Force rebuild now that children exist.
            try
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
                if (scrollRect != null && scrollRect.viewport != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
                Canvas.ForceUpdateCanvases();
            }
            catch { }

            BringListToFront();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                RectTransform firstRowRt = null;
                for (int i = 0; i < contentRoot.childCount; i++)
                {
                    var ch = contentRoot.GetChild(i);
                    if (ch == null) continue;
                    if (rowTemplate != null && ch == rowTemplate.transform) continue;
                    firstRowRt = ch as RectTransform;
                    if (firstRowRt != null) break;
                }

                var anchors = $"({contentRoot.anchorMin}->{contentRoot.anchorMax})";
                var pivot = contentRoot.pivot;

                string firstRowInfo = "(no rows)";
                if (firstRowRt != null)
                    firstRowInfo = $"firstRow='{firstRowRt.name}' localPos={firstRowRt.localPosition} anchoredPos={firstRowRt.anchoredPosition}";

                if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] PostRebuild content anchors={anchors} pivot={pivot} {firstRowInfo}", this);
            }
            catch { }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int rowsUnderContent = 0;
                for (int i = 0; i < contentRoot.childCount; i++)
                {
                    var ch = contentRoot.GetChild(i);
                    if (ch == null) continue;
                    if (rowTemplate != null && ch == rowTemplate.transform) continue;
                    rowsUnderContent++;
                }

                var vpSize = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : Vector2.zero;
                var cSize = contentRoot != null ? contentRoot.rect.size : Vector2.zero;

                if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Rendered={renderedStacks} rowsUnderContent={rowsUnderContent} viewportSize={vpSize} contentRectSize={cSize}", this);
            }
            catch { }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList END frame=" + Time.frameCount + " rowsCreated=" + createdRowCount, this);
#endif
        }

        private void RefreshDetails()
        {
            if (detailsUI == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedItemId))
            {
                detailsUI.Clear();
                RefreshEquipButtonState(null);
                return;
            }

            EnsureInventory();
            int count = _inventory != null ? _inventory.Count(_selectedItemId) : _selectedCount;
            var def = ResolveItemDefinition(_selectedItemId);

            detailsUI.Set(def, _selectedItemId, count);
            RefreshEquipButtonState(def);
        }

        private void Select(string itemId, int count)
        {
            _selectedItemId = itemId;
            _selectedCount = Mathf.Max(0, count);
            _selectedDef = ResolveItemDefinition(itemId);

            // Update highlight immediately without rebuilding the list.
            UpdateSelectionHighlightVisuals();

            RefreshDetails();
        }

        private void ClearSelection()
        {
            _selectedItemId = null;
            _selectedDef = null;
            _selectedCount = 0;

            UpdateSelectionHighlightVisuals();
            RefreshDetails();
        }

        private void EnsureEquipment()
        {
            if (_equipment != null)
                return;

            try
            {
                _equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
            }
            catch
            {
                _equipment = null;
            }
        }

        private void EnsureEquipButton()
        {
            if (_equipButton != null)
                return;

            if (detailsUI == null)
                return;

            var existing = detailsUI.transform.Find("EquipButton");
            if (existing != null)
            {
                _equipButton = existing.GetComponent<Button>();
                _equipButtonText = existing.GetComponentInChildren<TMP_Text>(true);
                if (_equipButton != null)
                {
                    _equipButton.onClick.RemoveAllListeners();
                    _equipButton.onClick.AddListener(OnEquipPressed);
                }
                return;
            }

            var go = new GameObject("EquipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(detailsUI.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.01f);
            rt.anchorMax = new Vector2(0.94f, 0.07f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = Color.white;

            _equipButton = go.GetComponent<Button>();
            _equipButton.onClick.RemoveAllListeners();
            _equipButton.onClick.AddListener(OnEquipPressed);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "Equip";
            tmp.fontSize = 22;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            _equipButtonText = tmp;
            RefreshEquipButtonState(_selectedDef);
        }

        private void RefreshEquipButtonState(ItemDefinition selectedDef)
        {
            if (_equipButton == null)
                return;

            bool canEquip = selectedDef != null && CanEquipSelected(selectedDef);

            // UX: only show the button when it can actually do something.
            bool show = canEquip;
            try { _equipButton.gameObject.SetActive(show); } catch { }

            if (!show)
                return;

            _equipButton.interactable = true;
            if (_equipButtonText != null)
                _equipButtonText.text = "Equip";
        }

        private void OnEquipPressed()
        {
            EnsureEquipment();

            // Same equip attempt as hotkey.
            TryEquipSelected();
        }

        private void ClearRows()
        {
            if (contentRoot == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int diagChildCountBefore = contentRoot.childCount;
                int diagSnapCount = -1;
                try
                {
                    if (_inventory != null)
                    {
                        var s = _inventory.GetAllItemsSnapshot();
                        diagSnapCount = s != null ? s.Count : 0;
                    }
                    else
                    {
                        diagSnapCount = 0;
                    }
                }
                catch { }

                var st = new System.Diagnostics.StackTrace(true).ToString();
                if (InventoryUiDebugEnabled)
                {
                    Debug.Log(
                        "[InventoryUI TRACE] ClearRows() frame=" + Time.frameCount +
                        " contentChildCountBefore=" + diagChildCountBefore +
                        " snapshotCount=" + diagSnapCount +
                        "\n" + st,
                        this);
                }
            }
            catch { }
#endif

            var templateTf = rowTemplate != null ? rowTemplate.transform : null;

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i);
                if (child == null) continue;
                if (templateTf != null && child == templateTf) continue;
                Destroy(child.gameObject);
            }

            _spawnedRows.Clear();
            _spawnedSlotViews.Clear();
        }

        private int FindSelectedSlotIndexInVisibleStacks(List<(string itemId, int count, ItemDefinition def)> visibleStacks, string selectedItemId)
        {
            if (string.IsNullOrWhiteSpace(selectedItemId) || visibleStacks == null)
                return -1;

            for (int i = 0; i < visibleStacks.Count && i < InventoryGridSlots; i++)
            {
                if (string.Equals(visibleStacks[i].itemId, selectedItemId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void UpdateSelectionVisualsBySlotIndex(int selectedIndexOrMinus1)
        {
            // Iterate instantiated slot views and set selection state.
            for (int i = 0; i < _spawnedSlotViews.Count; i++)
            {
                var row = _spawnedSlotViews[i];
                if (row == null) continue;
                row.SetSelected(i == selectedIndexOrMinus1);
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (contentRoot == null)
                return;

            // Requirement: iterate all PlayerInventoryRowUI instances under contentRoot.
            try
            {
                var rows = contentRoot.GetComponentsInChildren<PlayerInventoryRowUI>(includeInactive: false);
                if (rows == null)
                    return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[INV][SEL APPLY] applying selectedSlot={_selectedSlotIndex} to rows={rows.Length}", this);
#endif

                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    if (row == null) continue;
                    row.SetSelected(row.SlotIndex == _selectedSlotIndex);
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void EnsureInventory()
        {
            var inv = ResolveInventory(out var source);
            _inventorySource = source;

            if (_inventory == inv)
                return;

            // Swap subscription when inventory instance changes.
            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            _inventory = inv;

            if (_inventory != null && _isOpen)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.Changed += OnInventoryChanged;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isOpen)
            {
                try
                {
                    var id = _inventory != null ? _inventory.GetInstanceID() : 0;
                    var goName = _inventory != null ? _inventory.gameObject.name : "(null)";
                    if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Open resolved inventory instanceId={id} goPath='{goName}'", this);
                }
                catch { }
            }
#endif
        }

        // IMPORTANT: This resolver matches your current project approach; keep it.
        // If you later finalize a centralized resolver class, you can simplify this to call it.
        private PlayerInventory ResolveInventory(out string source)
        {
            source = null;

            // 1) Prefer player authority chain.
            try
            {
                if (_inputAuthority != null)
                {
                    var inv = _inputAuthority.GetComponentInParent<PlayerInventory>();
                    if (inv == null) inv = _inputAuthority.GetComponentInChildren<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority(chain)";
                        return inv;
                    }
                }
            }
            catch { }

            // 2) Try best-by-items among active inventories.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var all = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
#else
                var all = FindObjectsOfType<PlayerInventory>();
#endif
                if (all != null && all.Length > 0)
                {
                    PlayerInventory best = null;
                    int bestTotalItems = -1;
                    int bestStacks = -1;

                    foreach (var inv in all)
                    {
                        if (inv == null || !inv.isActiveAndEnabled) continue;

                        int stacks = 0;
                        int totalItems = 0;

                        try
                        {
                            var snap = inv.GetAllItemsSnapshot();
                            if (snap != null)
                            {
                                stacks = snap.Count;
                                foreach (var kv in snap)
                                    totalItems += Mathf.Max(0, kv.Value);
                            }
                        }
                        catch { }

                        if (totalItems > bestTotalItems || (totalItems == bestTotalItems && stacks > bestStacks))
                        {
                            best = inv;
                            bestTotalItems = totalItems;
                            bestStacks = stacks;
                        }
                    }

                    if (best != null)
                    {
                        source = all.Length == 1 ? "FindObjects(single)" : "FindObjects(best-by-items)";
                        return best;
                    }
                }
            }
            catch { }

            // 3) Last resort.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var inv = FindFirstObjectByType<PlayerInventory>();
#else
                var inv = FindObjectOfType<PlayerInventory>();
#endif
                if (inv != null)
                {
                    source = "FindFirstObjectByType";
                    return inv;
                }
            }
            catch { }

            return null;
        }

        private void EnsureScrollRectWiring()
        {
            if (scrollRect == null)
                return;

            // Try to infer contentRoot if missing.
            try
            {
                if (scrollRect.content == null && contentRoot != null)
                    scrollRect.content = contentRoot;

                if (scrollRect.content == null && contentRoot == null)
                    contentRoot = scrollRect.content;
            }
            catch { }

            // Infer viewport if missing.
            try
            {
                if (scrollRect.viewport == null)
                {
                    RectTransform candidate = null;

                    try
                    {
                        var p = contentRoot != null ? contentRoot.parent as RectTransform : null;
                        if (p != null && (p.name == "Viewport" || p.GetComponent<Mask>() != null || p.GetComponent<RectMask2D>() != null))
                            candidate = p;
                    }
                    catch { }

                    if (candidate == null)
                    {
                        try
                        {
                            var t = scrollRect.transform.Find("Viewport") as RectTransform;
                            if (t != null) candidate = t;
                        }
                        catch { }
                    }

                    if (candidate != null)
                        scrollRect.viewport = candidate;
                }

                if (scrollRect.viewport != null)
                    MakeViewportTransparent(scrollRect.viewport);
            }
            catch { }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isOpen && !_loggedScrollWiringForThisOpen)
            {
                _loggedScrollWiringForThisOpen = true;
                try
                {
                    var vp = scrollRect.viewport;
                    var vpName = vp != null ? vp.name : "(null)";
                    if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] ScrollRect wiring viewport={vpName} content={(scrollRect.content != null ? scrollRect.content.name : "(null)")} scrollRect={(scrollRect != null ? scrollRect.name : "(null)")}", this);

                    if (vp != null)
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Viewport Rect anchors=({vp.anchorMin}->{vp.anchorMax}) sizeDelta={vp.sizeDelta} offsetMin={vp.offsetMin} offsetMax={vp.offsetMax} pivot={vp.pivot}", this);

                    if (contentRoot != null)
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Content Rect anchors=({contentRoot.anchorMin}->{contentRoot.anchorMax}) pos={contentRoot.anchoredPosition} sizeDelta={contentRoot.sizeDelta} offsetMin={contentRoot.offsetMin} offsetMax={contentRoot.offsetMax} pivot={contentRoot.pivot}", this);
                }
                catch { }
            }
#endif
        }

        private void BringListToFront()
        {
            try
            {
                if (scrollRect == null) return;
                var t = scrollRect.transform;
                // Bring the whole scroll view to the front of its parent (Unity UI draws later siblings on top)
                t.SetAsLastSibling();

                // Also bring viewport/content just in case theyâ€™re nested under a weird layout wrapper
                if (scrollRect.viewport != null) scrollRect.viewport.SetAsLastSibling();
                if (contentRoot != null) contentRoot.SetAsLastSibling();
            }
            catch { }
        }

        /// <summary>
        /// Hard-enforces a known-good ScrollView layout so rows can't exist-but-not-render.
        /// This intentionally overrides bad inspector values during dev.
        /// </summary>
        private void EnsureScrollViewLayoutHard()
        {
            if (scrollRect == null || contentRoot == null)
                return;

            // Viewport must stretch and must mask.
            if (scrollRect.viewport != null)
            {
                var vp = scrollRect.viewport;
                try
                {
                    vp.anchorMin = new Vector2(0f, 0f);
                    vp.anchorMax = new Vector2(1f, 1f);
                    vp.pivot = new Vector2(0.5f, 0.5f);
                    vp.offsetMin = Vector2.zero;
                    vp.offsetMax = Vector2.zero;
                }
                catch { }

                var vpGo = vp.gameObject;

                // Force Viewport masking to RectMask2D (remove Mask).
                try
                {
                    var mask = vpGo.GetComponent<Mask>();
                    if (mask != null)
                    {
                        if (Application.isPlaying) Destroy(mask);
                        else DestroyImmediate(mask);
                    }
                }
                catch { }

                try
                {
                    if (vpGo.GetComponent<RectMask2D>() == null)
                        vpGo.AddComponent<RectMask2D>();

                    // RectMask2D does not require an Image. If one exists, force it fully transparent
                    // to avoid a one-frame white flash on open.
                    var img = vpGo.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        if (c.a > 0.001f)
                            img.color = new Color(c.r, c.g, c.b, 0f);
                    }
                }
                catch { }
            }

            // Content should stretch (GridLayoutGroup will align items).
            try
            {
                contentRoot.anchorMin = new Vector2(0f, 0f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.pivot = new Vector2(0.5f, 0.5f);
                contentRoot.offsetMin = Vector2.zero;
                contentRoot.offsetMax = Vector2.zero;
            }
            catch { }

            // Layout components on Content: fixed 4x7 grid.
            try
            {
                // Remove list layout components if present.
                var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    // IMPORTANT: in play mode we must remove immediately; Destroy() is deferred and will
                    // prevent adding GridLayoutGroup in the same frame.
                    try { DestroyImmediate(vlg); } catch { if (Application.isPlaying) Destroy(vlg); else DestroyImmediate(vlg); }
                }

                var csf = contentRoot.GetComponent<ContentSizeFitter>();
                if (csf != null)
                {
                    // IMPORTANT: same reasoning as above.
                    try { DestroyImmediate(csf); } catch { if (Application.isPlaying) Destroy(csf); else DestroyImmediate(csf); }
                }

                // If something still blocks, bail out (and warn once) rather than spamming the console.
                if (contentRoot.GetComponent<VerticalLayoutGroup>() != null || contentRoot.GetComponent<ContentSizeFitter>() != null)
                {
                    if (!_warnedContentLayoutConflict)
                    {
                        _warnedContentLayoutConflict = true;
                        Debug.LogWarning("[PlayerInventoryUI] ContentRoot still has list-layout components; cannot ensure GridLayoutGroup this frame.", this);
                    }
                    return;
                }

                var grid = contentRoot.GetComponent<GridLayoutGroup>();
                if (grid == null) grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();

                // Border around the grid pane (not per-slot lines).
                EnsureGridPaneBorder(scrollRect.viewport);

                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = InventoryGridColumns;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.spacing = new Vector2(4f, 4f);
                grid.padding = new RectOffset(8, 8, 8, 8);

                // Compute a TRUE square cell size that fits 4x7 in the viewport.
                float cell = 64f;
                try
                {
                    var vp = scrollRect.viewport;
                    if (vp != null)
                    {
                        float padX = grid.padding.left + grid.padding.right;
                        float padY = grid.padding.top + grid.padding.bottom;
                        float availW = Mathf.Max(0f, vp.rect.width - padX - grid.spacing.x * (InventoryGridColumns - 1));
                        float availH = Mathf.Max(0f, vp.rect.height - padY - grid.spacing.y * (InventoryGridRows - 1));

                        float cw = Mathf.Floor(availW / InventoryGridColumns);
                        float ch = Mathf.Floor(availH / InventoryGridRows);
                        cell = Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 48f, 220f);
                    }
                }
                catch { }

                // Pixel-perfect (prefer even) to keep 1px/2px outlines consistent.
                int size = Mathf.FloorToInt(cell);
                size = Mathf.Clamp(size, 48, 220);
                if ((size % 2) == 1) size -= 1;
                if (size < 48) size = 48;
                cell = size;

                grid.cellSize = new Vector2(cell, cell);

                // No scrolling for a fixed 4x7 inventory.
                scrollRect.horizontal = false;
                scrollRect.vertical = false;
            }
            catch { }

            // Ensure scrollRect.content is contentRoot.
            try
            {
                if (scrollRect.content != contentRoot)
                    scrollRect.content = contentRoot;
            }
            catch { }
        }

        private static void EnsureGridPaneBorder(RectTransform viewport)
        {
            if (viewport == null)
                return;

            try
            {
                // Create (or reuse) a simple border overlay in the viewport.
                Transform borderTf = null;
                try { borderTf = viewport.Find("GridPaneBorder"); } catch { }

                GameObject borderGo;
                if (borderTf == null)
                {
                    borderGo = new GameObject("GridPaneBorder", typeof(RectTransform));
                    borderGo.transform.SetParent(viewport, false);
                }
                else
                {
                    borderGo = borderTf.gameObject;
                }

                var brt = borderGo.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                brt.pivot = new Vector2(0.5f, 0.5f);

                // Keep border above content but not blocking interaction.
                borderGo.transform.SetAsLastSibling();

                const float thickness = 1f;
                var lineColor = new Color(1f, 1f, 1f, 0.65f);

                EnsureBorderLine(borderGo.transform, "TopLine", lineColor,
                    anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                    pivot: new Vector2(0.5f, 1f), sizeDelta: new Vector2(0f, thickness), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "BottomLine", lineColor,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                    pivot: new Vector2(0.5f, 0f), sizeDelta: new Vector2(0f, thickness), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "LeftLine", lineColor,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                    pivot: new Vector2(0f, 0.5f), sizeDelta: new Vector2(thickness, 0f), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "RightLine", lineColor,
                    anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
                    pivot: new Vector2(1f, 0.5f), sizeDelta: new Vector2(thickness, 0f), anchoredPos: Vector2.zero);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static void EnsureBorderLine(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPos)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
                return;

            Transform tf = null;
            try { tf = parent.Find(name); } catch { }

            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = tf.gameObject;
                if (go.GetComponent<Image>() == null)
                    go.AddComponent<Image>();
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>
        /// This is the missing piece in your current setup:
        /// your rows exist and are clickable, but their graphics/text are invisible.
        /// We force them visible here to eliminate alpha/canvasgroup regressions.
        /// </summary>
        private void ForceRowVisible(GameObject rowGo)
        {
            if (rowGo == null) return;

            // CanvasGroups can hide everything.
            try
            {
                var cgs = rowGo.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    cg.ignoreParentGroups = false;
                }
            }
            catch { }

            // Force all UI Graphics visible.
            try
            {
                var graphics = rowGo.GetComponentsInChildren<Graphic>(true);
                foreach (var g in graphics)
                {
                    if (g == null) continue;
                    var c = g.color;
                    if (c.a < 0.99f)
                        g.color = new Color(c.r, c.g, c.b, 1f);
                    g.raycastTarget = true; // keep clicking working
                    g.enabled = true;
                    if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
                }
            }
            catch { }

            // Force all TMP text visible and non-tiny.
            try
            {
                var tmps = rowGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;

                    // Ensure active/enabled.
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    t.enabled = true;

                    // Ensure alpha is 1.
                    t.alpha = 1f;

                    // Ensure color alpha is 1.
                    var c = t.color;
                    if (c.a < 0.99f)
                        t.color = new Color(c.r, c.g, c.b, 1f);

                    // If the text is effectively black-on-black, nudge to white (dev-safe).
                    // We do this only if it's very dark.
                    if (t.color.r < 0.15f && t.color.g < 0.15f && t.color.b < 0.15f)
                        t.color = new Color(1f, 1f, 1f, 1f);

                    // Font size floor.
                    if (t.fontSize < 18f)
                        t.fontSize = 22f;
                }
            }
            catch { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // One-time â€œwhat was invisible?â€ report per open.
            if (_isOpen && !_loggedFirstRowVisibilityThisOpen)
            {
                _loggedFirstRowVisibilityThisOpen = true;
                try
                {
                    var anyTmp = rowGo.GetComponentInChildren<TMP_Text>(true);
                    var anyGraphic = rowGo.GetComponentInChildren<Graphic>(true);
                    var cg = rowGo.GetComponentInChildren<CanvasGroup>(true);

                    if (InventoryUiDebugEnabled)
                    {
                        Debug.Log(
                            $"[PlayerInventoryUI] FirstRowVisibilityReport row='{rowGo.name}' " +
                            $"hasTMP={(anyTmp != null)} tmpText='{(anyTmp != null ? anyTmp.text : "(null)")}' tmpColor={(anyTmp != null ? anyTmp.color.ToString() : "(n/a)")} tmpAlpha={(anyTmp != null ? anyTmp.alpha : -1f)} " +
                            $"hasGraphic={(anyGraphic != null)} graphicColor={(anyGraphic != null ? anyGraphic.color.ToString() : "(n/a)")} " +
                            $"hasCanvasGroup={(cg != null)} cgAlpha={(cg != null ? cg.alpha : -1f)}",
                            this);
                    }
                }
                catch { }
            }
#endif
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static TMP_Text FindTmpByNameHint(GameObject rootGo, params string[] nameHints)
        {
            if (rootGo == null || nameHints == null || nameHints.Length == 0)
                return null;

            TMP_Text best = null;

            try
            {
                var tmps = rootGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;
                    var n = t.gameObject != null ? t.gameObject.name : null;
                    if (string.IsNullOrWhiteSpace(n)) continue;

                    for (int i = 0; i < nameHints.Length; i++)
                    {
                        var hint = nameHints[i];
                        if (string.IsNullOrWhiteSpace(hint)) continue;
                        if (EqualsIgnoreCase(n, hint) || n.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                            return t;
                    }

                    // fallback if nothing matches: take the largest font TMP as best guess
                    if (best == null || t.fontSize > best.fontSize)
                        best = t;
                }
            }
            catch { }

            return best;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }

        private Color GetRowBaseColor()
        {
            try
            {
                if (rowTemplate != null)
                {
                    var img = rowTemplate.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        // Avoid fully transparent base; default to white if it is.
                        if (c.a <= 0.001f)
                            return Color.white;
                        return new Color(c.r, c.g, c.b, 1f);
                    }
                }
            }
            catch { }

            return Color.white;
        }

        private Image EnsureRowBackgroundImage(GameObject rowGo)
        {
            if (rowGo == null) return null;

            Image img = null;
            try { img = rowGo.GetComponent<Image>(); } catch { }
            if (img == null)
            {
                try { img = rowGo.AddComponent<Image>(); } catch { }
            }

            if (img != null)
            {
                try
                {
                    img.raycastTarget = true;

                    // Ensure a sane baseline RGB, but leave alpha to styling.
                    var baseC = GetRowBaseColor();
                    img.color = new Color(baseC.r, baseC.g, baseC.b, img.color.a);
                }
                catch { }
            }

            return img;
        }

        private static void EnsureSelectedBar(GameObject rowGo, bool enabled, Color baseColor)
        {
            if (rowGo == null) return;

            Transform barTf = null;
            try { barTf = rowGo.transform.Find("SelectedBar"); } catch { }

            if (!enabled)
            {
                if (barTf != null)
                {
                    try { barTf.gameObject.SetActive(false); } catch { }
                }
                return;
            }

            GameObject barGo = null;
            if (barTf == null)
            {
                try
                {
                    barGo = new GameObject("SelectedBar", typeof(RectTransform), typeof(Image));
                    barGo.transform.SetParent(rowGo.transform, false);
                    barTf = barGo.transform;
                }
                catch { return; }
            }
            else
            {
                barGo = barTf.gameObject;
            }

            try { barGo.SetActive(true); } catch { }

            try
            {
                var rt = barTf as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(4f, 0f);
                }
            }
            catch { }

            try
            {
                var img = barGo.GetComponent<Image>();
                if (img != null)
                {
                    img.raycastTarget = false;
                    img.color = WithAlpha(baseColor, 0.90f);
                }
            }
            catch { }
        }

        private static void ConfigureButtonColors(Button btn, Color normal)
        {
            if (btn == null) return;

            try
            {
                var cb = btn.colors;

                // Subtle adjustments: slightly brighter on hover, slightly darker on press.
                var highlighted = normal;
                highlighted.r = Mathf.Clamp01(highlighted.r + 0.06f);
                highlighted.g = Mathf.Clamp01(highlighted.g + 0.06f);
                highlighted.b = Mathf.Clamp01(highlighted.b + 0.06f);
                highlighted.a = Mathf.Clamp01(highlighted.a + 0.06f);

                var pressed = normal;
                pressed.r = Mathf.Clamp01(pressed.r - 0.05f);
                pressed.g = Mathf.Clamp01(pressed.g - 0.05f);
                pressed.b = Mathf.Clamp01(pressed.b - 0.05f);
                pressed.a = Mathf.Clamp01(pressed.a - 0.06f);

                cb.normalColor = normal;
                cb.highlightedColor = highlighted;
                cb.pressedColor = pressed;
                cb.selectedColor = highlighted;
                cb.disabledColor = new Color(normal.r, normal.g, normal.b, Mathf.Clamp01(normal.a * 0.6f));

                btn.colors = cb;

                if (btn.transition == Selectable.Transition.None)
                    btn.transition = Selectable.Transition.ColorTint;
            }
            catch { }
        }

        private static void ApplyTextReadability(GameObject rowGo)
        {
            if (rowGo == null) return;

            // Grid slots are icon+count only; don't modify TMP sizing/colors here.
            try
            {
                var rowUi = rowGo.GetComponent<PlayerInventoryRowUI>();
                if (rowUi != null && rowUi.IsGridMode)
                    return;
            }
            catch { }

            try
            {
                var nameTmp = FindTmpByNameHint(rowGo, "Name", "ItemName", "Title", "Label");
                if (nameTmp != null)
                {
                    // Keep names readable but don't blow up grid tiles.
                    if (nameTmp.fontSize < 16f)
                        nameTmp.fontSize = 16f;

                    var c = nameTmp.color;
                    nameTmp.color = new Color(c.r, c.g, c.b, 1f);

                    var m = nameTmp.margin;
                    if (m.x < 12f) m.x = 12f;
                    nameTmp.margin = m;
                }

                var countTmp = FindTmpByNameHint(rowGo, "Count", "Qty", "Quantity", "Stack");
                if (countTmp != null)
                {
                    if (countTmp.fontSize < 14f)
                        countTmp.fontSize = 14f;
                    var c2 = countTmp.color;
                    countTmp.color = new Color(c2.r, c2.g, c2.b, 0.95f);
                }
            }
            catch { }
        }

        private void ApplyRowVisualStyling(GameObject rowGo, int rowIndex, bool isSelected)
        {
            if (rowGo == null) return;

            bool isGrid = false;
            try
            {
                var rowUi = rowGo.GetComponent<PlayerInventoryRowUI>();
                isGrid = rowUi != null && rowUi.IsGridMode;
            }
            catch { }

            // Normal shading
            const float evenAlpha = 0.18f;
            const float oddAlpha = 0.26f;
            const float selectedAlpha = 0.45f;

            float normalAlpha = (rowIndex % 2 == 0) ? evenAlpha : oddAlpha;
            float a = isSelected ? selectedAlpha : normalAlpha;

            var bg = EnsureRowBackgroundImage(rowGo);
            var baseColor = GetRowBaseColor();

            // Raycast fix: tiles must always have a raycastable graphic.
            try
            {
                if (bg != null)
                    bg.raycastTarget = true;
            }
            catch { }

            // For grid tiles, background/hover is handled by PlayerInventoryRowUI.
            // Driving bg.color here can cause one-frame flashes on open.
            if (!isGrid && bg != null)
            {
                try
                {
                    bg.color = WithAlpha(new Color(baseColor.r, baseColor.g, baseColor.b, 1f), a);
                }
                catch { }
            }

            // Optional accent bar for selected
            if (!isGrid)
                EnsureSelectedBar(rowGo, isSelected, baseColor);
            else
                EnsureSelectedBar(rowGo, false, baseColor);

            // Grid slot border (subtle) + stronger when selected.
            if (isGrid)
            {
                // Border/hover/selection visuals are handled by PlayerInventoryRowUI.
            }

            // Hover styling only if there is a Button
            try
            {
                var btn = rowGo.GetComponent<Button>();
                if (btn != null)
                {
                    if (bg != null && btn.targetGraphic == null)
                        btn.targetGraphic = bg;

                    if (isGrid)
                    {
                        // Prevent Unity's Selectable tinting from flashing tiles.
                        btn.transition = Selectable.Transition.None;
                    }
                    else
                    {
                        ConfigureButtonColors(btn, bg != null ? bg.color : WithAlpha(baseColor, a));
                    }
                }
            }
            catch { }

            ApplyTextReadability(rowGo);
        }

        private void UpdateSelectionHighlightVisuals()
        {
            if (contentRoot == null)
                return;

            int rowIndex = 0;
            var templateTf = rowTemplate != null ? rowTemplate.transform : null;

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                var ch = contentRoot.GetChild(i);
                if (ch == null) continue;
                if (templateTf != null && ch == templateTf) continue;

                var go = ch.gameObject;
                bool isSelected = !string.IsNullOrWhiteSpace(_selectedItemId) && go != null && go.name == $"Row_{_selectedItemId}";
                ApplyRowVisualStyling(go, rowIndex, isSelected);

                try
                {
                    var rowUi = go != null ? go.GetComponent<PlayerInventoryRowUI>() : null;
                    if (rowUi != null)
                    {
                        // Grid selection is slot-index based (stable), not name-based.
                        if (rowUi.IsGridMode)
                            rowUi.SetSelected(rowUi.SlotIndex == _selectedSlotIndex);
                        else
                            rowUi.SetSelected(isSelected);
                    }
                }
                catch { }

                rowIndex++;
            }
        }

        private void EnsureCanvasVisibility()
        {
            // Make sure root is active and visible if someone zeroed a CanvasGroup higher up.
            try
            {
                if (root == null) return;

                var cgs = root.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    if (cg.alpha < 1f) cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
            catch { }
        }

        private static void MakeViewportTransparent(RectTransform viewport)
        {
            if (viewport == null)
                return;

            try
            {
                var img = viewport.GetComponent<Image>();
                if (img == null)
                    return;

                var c = img.color;
                if (c.a > 0.001f)
                    img.color = new Color(c.r, c.g, c.b, 0f);
            }
            catch { }
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            static bool HasIcon(ItemDefinition d)
            {
                try { return d != null && d.icon != null; } catch { return false; }
            }

            try
            {
#if UNITY_2022_2_OR_NEWER
                var shops = FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
                var shops = FindObjectsOfType<Abyss.Shop.MerchantShop>();
#endif
                if (shops != null)
                {
                    foreach (var s in shops)
                    {
                        if (s == null || s.shopInventory == null || s.shopInventory.entries == null) continue;
                        foreach (var e in s.shopInventory.entries)
                        {
                            if (e == null || e.item == null) continue;
                            var def = e.item;
                            var id = ResolveItemId(def);
                            if (string.IsNullOrWhiteSpace(id))
                                continue;

                            if (!map.TryGetValue(id, out var existing) || existing == null)
                            {
                                map[id] = def;
                            }
                            else
                            {
                                // Prefer the definition that actually has an icon assigned.
                                if (!HasIcon(existing) && HasIcon(def))
                                    map[id] = def;
                            }
                        }
                    }
                }

                var loaded = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                if (loaded != null)
                {
                    foreach (var def in loaded)
                    {
                        if (def == null) continue;
                        var id = ResolveItemId(def);
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        if (!map.TryGetValue(id, out var existing) || existing == null)
                        {
                            map[id] = def;
                        }
                        else
                        {
                            if (!HasIcon(existing) && HasIcon(def))
                                map[id] = def;
                        }
                    }
                }
            }
            catch { }

            return map;
        }

        private ItemDefinition ResolveItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            _itemDefById ??= BuildItemDefinitionIndex();
            if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var def))
            {
                // If we found a definition but it doesn't have an icon, it may be a stale/duplicate instance.
                // Rebuild the index once and retry.
                try
                {
                    if (def != null && def.icon == null)
                    {
                        _itemDefById = BuildItemDefinitionIndex();
                        if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var refreshed))
                            return refreshed;
                    }
                }
                catch { }

                return def;
            }

            return null;
        }

        private string ResolveItemId(ItemDefinition def)
        {
            if (def == null) return null;

            // Best-effort: match your existing item ID convention.
            // If your ItemDefinition has a canonical ID field, prefer that.
            try
            {
                // Common patterns: def.id, def.itemId, def.name
                var t = def.GetType();

                var f = t.GetField("id");
                if (f != null)
                {
                    var v = f.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                var p = t.GetProperty("id");
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                f = t.GetField("itemId");
                if (f != null)
                {
                    var v = f.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                p = t.GetProperty("itemId");
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }

            // Fallback: sanitized name.
            return def.name != null ? def.name.Trim() : null;
        }

        private bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }

        private void EnsureTabs()
        {
            if (scrollRect == null || root == null)
                return;

            if (tabsRoot == null)
            {
                // Try to find an existing tabs root.
                var found = root.transform.Find("Tabs");
                if (found != null)
                    tabsRoot = found as RectTransform;
            }

            if (tabsRoot == null)
            {
                // Create a minimal tab bar above the ScrollRect.
                var parent = scrollRect.transform.parent as RectTransform;
                if (parent == null)
                    parent = root.transform as RectTransform;
                if (parent == null)
                    return;

                var tabsGo = new GameObject("Tabs", typeof(RectTransform));
                tabsRoot = tabsGo.GetComponent<RectTransform>();
                tabsRoot.SetParent(parent, false);

                // Insert just above the scroll rect if possible.
                try
                {
                    int idx = scrollRect.transform.GetSiblingIndex();
                    tabsRoot.SetSiblingIndex(Mathf.Max(0, idx));
                }
                catch { }

                var hlg = tabsGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childForceExpandHeight = false;
                hlg.childForceExpandWidth = true;
                hlg.childControlHeight = true;
                hlg.childControlWidth = true;
                hlg.spacing = 6f;
                hlg.padding = new RectOffset(8, 8, 6, 6);

                var fitter = tabsGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Background tint to separate from list.
                var bg = tabsGo.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.08f, 0.65f);
            }

            if (_tabWeapons != null && _tabMaterials != null && _tabConsumables != null && _tabSkilling != null)
            {
                RefreshTabVisuals();
                return;
            }

            CreateOrBindTabButtons();
            RefreshTabVisuals();
        }

        private void CreateOrBindTabButtons()
        {
            if (tabsRoot == null)
                return;

            // If children already exist, attempt to bind by name.
            if (tabsRoot.childCount > 0)
            {
                _tabWeapons = FindButtonUnder(tabsRoot, "Tab_WeaponsGear") ?? _tabWeapons;
                _tabMaterials = FindButtonUnder(tabsRoot, "Tab_Materials") ?? _tabMaterials;
                _tabConsumables = FindButtonUnder(tabsRoot, "Tab_Consumables") ?? _tabConsumables;
                _tabSkilling = FindButtonUnder(tabsRoot, "Tab_Skilling") ?? _tabSkilling;
            }

            _tabWeapons ??= CreateTabButton(tabsRoot, "Tab_WeaponsGear", "Weapons/Gear", out _tabWeaponsText);
            _tabMaterials ??= CreateTabButton(tabsRoot, "Tab_Materials", "Materials", out _tabMaterialsText);
            _tabConsumables ??= CreateTabButton(tabsRoot, "Tab_Consumables", "Consumables", out _tabConsumablesText);
            _tabSkilling ??= CreateTabButton(tabsRoot, "Tab_Skilling", "Skilling", out _tabSkillingText);

            WireTab(_tabWeapons, InventoryTab.WeaponsGear);
            WireTab(_tabMaterials, InventoryTab.Materials);
            WireTab(_tabConsumables, InventoryTab.Consumables);
            WireTab(_tabSkilling, InventoryTab.Skilling);
        }

        private void WireTab(Button button, InventoryTab tab)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_activeTab == tab) return;
                _activeTab = tab;
                RefreshTabVisuals();
                RefreshList();
                RefreshDetails();
            });
        }

        private void RefreshTabVisuals()
        {
            ApplyTabVisual(_tabWeapons, _tabWeaponsText, _activeTab == InventoryTab.WeaponsGear);
            ApplyTabVisual(_tabMaterials, _tabMaterialsText, _activeTab == InventoryTab.Materials);
            ApplyTabVisual(_tabConsumables, _tabConsumablesText, _activeTab == InventoryTab.Consumables);
            ApplyTabVisual(_tabSkilling, _tabSkillingText, _activeTab == InventoryTab.Skilling);
        }

        private static void ApplyTabVisual(Button button, TMP_Text label, bool selected)
        {
            if (button != null)
            {
                var img = button.GetComponent<Image>();
                if (img != null)
                    img.color = selected
                        ? new Color(0.18f, 0.18f, 0.18f, 0.95f)
                        : new Color(0.12f, 0.12f, 0.12f, 0.80f);
            }

            if (label != null)
                label.color = selected ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private Button CreateTabButton(RectTransform parent, string name, string label, out TMP_Text labelText)
        {
            labelText = null;
            if (parent == null) return null;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.80f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.minHeight = 34f;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;

            // Label
            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontSize = 18f;
            tmp.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            if (titleText != null && titleText.font != null)
                tmp.font = titleText.font;

            labelText = tmp;
            return btn;
        }

        private static Button FindButtonUnder(RectTransform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            if (t == null) return null;
            return t.GetComponent<Button>();
        }

        private bool PassesTabFilter(ItemDefinition def, string itemId)
        {
            // Unknown items: keep visible under Weapons/Gear so they don't disappear.
            if (def == null)
                return _activeTab == InventoryTab.WeaponsGear;

            return _activeTab switch
            {
                InventoryTab.WeaponsGear => def.itemType == AbyssItemType.Weapon || def.itemType == AbyssItemType.Misc,
                InventoryTab.Materials => def.itemType == AbyssItemType.Workshop,
                InventoryTab.Consumables => def.itemType == AbyssItemType.Consumable,
                InventoryTab.Skilling => def.itemType == AbyssItemType.Skilling,
                _ => true
            };
        }
    }
}
```

---

## Assets/Abyss/Inventory/UIEffects/InventoryTileMeshEffect.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Inventory.UIEffects
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class InventoryTileMeshEffect : BaseMeshEffect
    {
        [Header("Gradient")]
        [SerializeField] private Color topColor = new(0.30f, 0.30f, 0.30f, 1f);
        [SerializeField] private Color bottomColor = new(0.40f, 0.40f, 0.40f, 1f);

        [Header("Inner Shadow")]
        [SerializeField] private bool innerShadowEnabled = true;
        [SerializeField] private Color innerShadowColor = new(0f, 0f, 0f, 0.22f);
        [SerializeField] private float innerShadowSize = 4f;

        private static readonly List<UIVertex> _verts = new(256);

        public void SetGradient(Color top, Color bottom)
        {
            topColor = top;
            bottomColor = bottom;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public void SetInnerShadow(bool enabled, Color color, float size)
        {
            innerShadowEnabled = enabled;
            innerShadowColor = color;
            innerShadowSize = size;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null)
                return;

            _verts.Clear();
            vh.GetUIVertexStream(_verts);
            if (_verts.Count == 0)
                return;

            var rect = graphic != null && graphic.rectTransform != null
                ? graphic.rectTransform.rect
                : default;

            float yMin = rect.yMin;
            float yMax = rect.yMax;
            float height = Mathf.Max(1f, yMax - yMin);

            // Apply vertical gradient by vertex Y.
            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                float t = Mathf.Clamp01((v.position.y - yMin) / height);
                var c = Color.Lerp(bottomColor, topColor, t);
                v.color = c;
                _verts[i] = v;
            }

            // Add simple inner shadow quads (single strip per edge).
            if (innerShadowEnabled && innerShadowColor.a > 0.001f)
            {
                float xMin = rect.xMin;
                float xMax = rect.xMax;
                float width = xMax - xMin;
                float s = Mathf.Clamp(innerShadowSize, 0f, Mathf.Min(width, height) * 0.5f);

                if (s > 0.01f)
                {
                    // Left
                    AddQuadTriangleStream(_verts, rect, xMin, yMin, xMin + s, yMax, innerShadowColor);
                    // Right
                    AddQuadTriangleStream(_verts, rect, xMax - s, yMin, xMax, yMax, innerShadowColor);
                    // Top
                    AddQuadTriangleStream(_verts, rect, xMin, yMax - s, xMax, yMax, innerShadowColor);
                    // Bottom
                    AddQuadTriangleStream(_verts, rect, xMin, yMin, xMax, yMin + s, innerShadowColor);
                }
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }

        private static void AddQuadTriangleStream(List<UIVertex> stream, Rect rect, float x0, float y0, float x1, float y1, Color color)
        {
            float u0 = Mathf.InverseLerp(rect.xMin, rect.xMax, x0);
            float u1 = Mathf.InverseLerp(rect.xMin, rect.xMax, x1);
            float v0 = Mathf.InverseLerp(rect.yMin, rect.yMax, y0);
            float v1 = Mathf.InverseLerp(rect.yMin, rect.yMax, y1);

            var a = UIVertex.simpleVert;
            a.position = new Vector3(x0, y0, 0f);
            a.uv0 = new Vector2(u0, v0);
            a.color = color;

            var b = UIVertex.simpleVert;
            b.position = new Vector3(x0, y1, 0f);
            b.uv0 = new Vector2(u0, v1);
            b.color = color;

            var c = UIVertex.simpleVert;
            c.position = new Vector3(x1, y1, 0f);
            c.uv0 = new Vector2(u1, v1);
            c.color = color;

            var d = UIVertex.simpleVert;
            d.position = new Vector3(x1, y0, 0f);
            d.uv0 = new Vector2(u1, v0);
            d.color = color;

            // Two triangles: a-b-c and c-d-a
            stream.Add(a);
            stream.Add(b);
            stream.Add(c);

            stream.Add(c);
            stream.Add(d);
            stream.Add(a);
        }
    }
}
```

---

## Assets/Abyss/Items/ItemDefinition.cs

```csharp
using UnityEngine;
using UnityEngine.Serialization;

namespace Abyss.Items
{
    [CreateAssetMenu(menuName = "Abyss/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;

        [TextArea]
        [FormerlySerializedAs("_description")]
        [FormerlySerializedAs("Description")]
        [FormerlySerializedAs("desc")]
        [FormerlySerializedAs("itemDescription")]
        public string description;

        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Common;
        public ItemType itemType;
        public int baseValue;

        [Header("Equipment (optional)")]
        public EquipmentSlot equipmentSlot = EquipmentSlot.None;
        public WeaponHandedness weaponHandedness = WeaponHandedness.None;

        [Tooltip("Bonus damage applied when this item is equipped as a weapon. Only meaningful for ItemType == Weapon.")]
        public int DamageBonus = 0;
    }
}
```

---

## Assets/Abyss/Shop/MerchantClickRaycaster.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyss.Shop
{
    /// <summary>
    /// Global click handler that opens MerchantShop when you click any collider under it.
    /// Avoids Unity OnMouseDown quirks.
    /// </summary>
    public sealed class MerchantClickRaycaster : MonoBehaviour
    {
        private Camera _cam;
        private Game.Input.PlayerInputAuthority _input;

        private void Awake()
        {
            EnsureCamera();
#if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
        }

        private void Update()
        {
            // Left click only
            if (!WasLeftClickPressed()) return;

            if (_input == null)
            {
#if UNITY_2022_2_OR_NEWER
                _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
                _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            }

            // If any UI has locked gameplay input (inventory/equipment/shop/etc), don't process world clicks.
            if (_input != null && _input.IsUiInputLocked)
                return;

            // If the shop UI is open, DO NOT raycast into the world (prevents immediate reopen on Exit click).
            if (MerchantShopUI.IsOpen) return;

            // If pointer is over interactive UI, do not raycast into the world.
            // (Non-interactive overlays like HUD panels should not block world interaction.)
            if (IsPointerOverInteractiveUI())
                return;

            EnsureCamera();
            if (_cam == null) return;

            if (!TryGetMousePosition(out var mousePos))
                return;

            var ray = _cam.ScreenPointToRay(mousePos);

            // RaycastAll so terrain/ground colliders can't block merchants after town moves.
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
                return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                // Walk up parent chain to find MerchantShop
                var shop = hit.collider.GetComponentInParent<MerchantShop>();
                if (shop == null) continue;

                // Open the inspector-driven UI with the resolved shop reference.
                MerchantShopUI.Open(shop);
                return;
            }
        }

        private void EnsureCamera()
        {
            if (_cam != null && _cam.isActiveAndEnabled)
                return;

            // Prefer a tagged main camera.
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                _cam = main;
                return;
            }

            // Otherwise, pick the enabled camera most likely to render world/interactables.
            var cams = Camera.allCameras;
            if (cams == null || cams.Length == 0)
            {
                _cam = null;
                return;
            }

            int defaultLayer = 0;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            int desiredMask = 1 << defaultLayer;
            if (interactableLayer >= 0)
                desiredMask |= 1 << interactableLayer;

            Camera best = null;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if ((c.cullingMask & desiredMask) == 0) continue;
                if (best == null || c.depth > best.depth) best = c;
            }

            if (best == null)
            {
                for (int i = 0; i < cams.Length; i++)
                {
                    var c = cams[i];
                    if (c == null || !c.isActiveAndEnabled) continue;
                    if (best == null || c.depth > best.depth) best = c;
                }
            }

            _cam = best;
        }

        private static bool WasLeftClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasPressedThisFrame;
#endif
            return Input.GetMouseButtonDown(0);
        }

        private static bool TryGetMousePosition(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pos = Mouse.current.position.ReadValue();
                return true;
            }
#endif
            pos = Input.mousePosition;
            return true;
        }

        private static bool IsPointerOverInteractiveUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            // Fast path: if not over any UI, we're fine.
            if (!es.IsPointerOverGameObject())
                return false;

            // If over UI, only block if the top UI under mouse is actually interactive.
            var eventData = new PointerEventData(es)
            {
                position = TryGetMousePosition(out var p) ? p : (Vector2)Input.mousePosition
            };

            var results = new List<RaycastResult>(16);
            es.RaycastAll(eventData, results);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;

                // Buttons/toggles/input fields/etc.
                if (go.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<MerchantClickRaycaster>();
            if (existing != null) return;

            var go = new GameObject("MerchantClickRaycaster");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantClickRaycaster>();
        }
    }
}
```

---

## Assets/Abyss/Shop/MerchantDoorHoverHighlighter.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyss.Shop
{
    /// <summary>
    /// Runtime hover highlight: only highlights door click targets (not whole buildings).
    /// </summary>
    public sealed class MerchantDoorHoverHighlighter : MonoBehaviour
    {
        private Camera _cam;
        private MerchantDoorClickTarget _current;

        private Game.Input.PlayerInputAuthority _input;

        private TextMeshPro _label;
        private static readonly Color LabelColor = Color.blue;
        [SerializeField] private float labelFontSize = 14f;

        private void Awake()
        {
            EnsureCamera();
#if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
        }

        private void Update()
        {
            if (_input == null)
            {
#if UNITY_2022_2_OR_NEWER
                _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
                _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            }

            // If any UI has locked gameplay input (inventory/equipment/shop/etc), don't show world hover labels.
            if (_input != null && _input.IsUiInputLocked)
            {
                Clear();
                return;
            }

            // If the shop UI is open, don't highlight anything.
            if (MerchantShopUI.IsOpen)
            {
                Clear();
                return;
            }

            // If pointer is over interactive UI, don't highlight world.
            // (Non-interactive overlays like HUD panels should not block highlighting.)
            if (IsPointerOverInteractiveUI())
            {
                Clear();
                return;
            }

            EnsureCamera();
            if (_cam == null) return;

            if (!TryGetMousePosition(out var mousePos))
                return;

            var ray = _cam.ScreenPointToRay(mousePos);

            // RaycastAll so nearby colliders (ground/terrain) don't block hover targets.
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                Clear();
                return;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            MerchantDoorClickTarget target = null;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                // Prefer direct component on collider object.
                target = hit.collider.GetComponent<MerchantDoorClickTarget>();
                if (target == null)
                    target = hit.collider.GetComponentInParent<MerchantDoorClickTarget>();

                if (target != null)
                    break;
            }
            if (target == _current) return;

            if (_current != null)
                _current.SetHighlighted(false);

            _current = target;

            if (_current != null)
            {
                _current.SetHighlighted(true);
                ShowLabelFor(_current);
            }
            else
            {
                HideLabel();
            }
        }

        private void EnsureCamera()
        {
            if (_cam != null && _cam.isActiveAndEnabled)
                return;

            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                _cam = main;
                return;
            }

            var cams = Camera.allCameras;
            if (cams == null || cams.Length == 0)
            {
                _cam = null;
                return;
            }

            int defaultLayer = 0;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            int desiredMask = 1 << defaultLayer;
            if (interactableLayer >= 0)
                desiredMask |= 1 << interactableLayer;

            Camera best = null;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if ((c.cullingMask & desiredMask) == 0) continue;
                if (best == null || c.depth > best.depth) best = c;
            }

            if (best == null)
            {
                for (int i = 0; i < cams.Length; i++)
                {
                    var c = cams[i];
                    if (c == null || !c.isActiveAndEnabled) continue;
                    if (best == null || c.depth > best.depth) best = c;
                }
            }

            _cam = best;
        }

        private static bool TryGetMousePosition(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pos = Mouse.current.position.ReadValue();
                return true;
            }
#endif
            pos = Input.mousePosition;
            return true;
        }

        private static bool IsPointerOverInteractiveUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            if (!es.IsPointerOverGameObject())
                return false;

            var eventData = new PointerEventData(es)
            {
                position = TryGetMousePosition(out var p) ? p : (Vector2)Input.mousePosition
            };

            var results = new List<RaycastResult>(16);
            es.RaycastAll(eventData, results);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;

                if (go.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        private void Clear()
        {
            if (_current != null)
            {
                _current.SetHighlighted(false);
                _current = null;
            }

            HideLabel();
        }

        private void EnsureLabel()
        {
            if (_label != null) return;

            var go = new GameObject("MerchantHoverLabel");
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMeshPro>();
            _label.text = string.Empty;
            _label.fontSize = labelFontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = LabelColor;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.gameObject.SetActive(false);
        }

        private void ShowLabelFor(MerchantDoorClickTarget target)
        {
            EnsureLabel();

            string name = target != null ? target.GetDisplayName() : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                name = "Shop";

            _label.text = name;

            if (target != null && target.TryGetBounds(out var b))
            {
                var p = b.center;
                p.y = b.max.y + 0.6f;
                _label.transform.position = p;
            }

            _label.gameObject.SetActive(true);
        }

        private void HideLabel()
        {
            if (_label != null)
                _label.gameObject.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<MerchantDoorHoverHighlighter>();
            if (existing != null) return;

            var go = new GameObject("MerchantDoorHoverHighlighter");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantDoorHoverHighlighter>();
        }
    }
}
```

---

## Assets/Editor/AutoAssignMissingItemIconsEditor.cs

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

public static class AutoAssignMissingItemIconsEditor
{
    [MenuItem("Tools/UI/Auto-Assign Missing Item Icons (Placeholders)")]
    public static void AutoAssignMissingItemIcons()
    {
        var placeholderByCategory = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase)
        {
            { "Consumable", LoadPlaceholderSprite("Icon_HealthPotion", "Assets/UI/Generated/Icons/Icon_HealthPotion.png") },
            { "Weapon",      LoadPlaceholderSprite("Icon_ManaPotion",   "Assets/UI/Generated/Icons/Icon_ManaPotion.png") },
            { "Equipment",   LoadPlaceholderSprite("Icon_ManaPotion",   "Assets/UI/Generated/Icons/Icon_ManaPotion.png") },
            { "Tool",        LoadPlaceholderSprite("Icon_TownScroll",   "Assets/UI/Generated/Icons/Icon_TownScroll.png") },
            { "Material",    LoadPlaceholderSprite("Icon_TownScroll",   "Assets/UI/Generated/Icons/Icon_TownScroll.png") },
            { "Generic",     LoadPlaceholderSprite("Icon_TownScroll",   "Assets/UI/Generated/Icons/Icon_TownScroll.png") },
        };

        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int scanned = 0;
        int assigned = 0;

        var changes = new List<string>(256);

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null)
                continue;

            scanned++;

            // Requirement: do NOT overwrite existing icons.
            if (def.icon != null)
                continue;

            var category = MapItemTypeToCategory(def.itemType);
            if (!placeholderByCategory.TryGetValue(category, out var placeholder) || placeholder == null)
                placeholderByCategory.TryGetValue("Generic", out placeholder);

            if (placeholder == null)
            {
                Debug.LogWarning($"[Auto-Assign Missing Item Icons] Missing placeholder sprite for itemType={def.itemType} category={category}. Skipping {path} (itemId='{def.itemId}', displayName='{def.displayName}').", def);
                continue;
            }

            def.icon = placeholder;
            EditorUtility.SetDirty(def);
            assigned++;
            changes.Add($"UPDATED: {path} | itemId='{def.itemId}' | itemType={def.itemType} | placeholder={placeholder.name}");
        }

        if (assigned > 0)
            AssetDatabase.SaveAssets();

        var summary = $"[Auto-Assign Missing Item Icons] Scanned={scanned} AutoAssigned={assigned}";
        if (changes.Count == 0)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.Log(summary + "\n" + string.Join("\n", changes));
        }
    }

    private static string MapItemTypeToCategory(Abyss.Items.ItemType itemType)
    {
        // Map differing enum values to the closest logical category.
        // Current enum is Abyss.Items.ItemType, but keep string-based mapping resilient.
        var name = itemType.ToString();
        if (name.IndexOf("Consum", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Consumable";
        if (name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Weapon";
        if (name.IndexOf("Equip", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Equipment";
        if (name.IndexOf("Tool", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Tool";
        if (name.IndexOf("Mat", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Material";

        return "Generic";
    }

    private static Sprite LoadPlaceholderSprite(string spriteName, string preferredAssetPath)
    {
        // 1) Prefer known generated icon paths.
        if (!string.IsNullOrWhiteSpace(preferredAssetPath))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(preferredAssetPath);
            if (sprite != null)
                return sprite;
        }

        // 2) Fallback: find any Sprite asset by name.
        if (!string.IsNullOrWhiteSpace(spriteName))
        {
            var guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    return sprite;
            }
        }

        return null;
    }
}
#endif
```

---

## Assets/Editor/BuildPlayerEquipmentUIEditor.cs

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Equipment.EditorTools
{
    public static class BuildPlayerEquipmentUIEditor
    {
        private const string SilhouetteIconFolder = "Assets/Abyss/Equipment/Icons/";

        [MenuItem("Tools/Build Player Equipment UI (Editor)")]
        public static void Build()
        {
            DestroySceneObjectsByName("PlayerEquipmentUICanvas");
            DestroySceneObjectsByName("PlayerEquipmentUIRoot");
            DestroySceneObjectsByName("PlayerEquipmentUI");

            var canvasGO = new GameObject("PlayerEquipmentUICanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create PlayerEquipmentUICanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            StretchFullScreen(canvasGO.GetComponent<RectTransform>());

            var root = new GameObject("PlayerEquipmentUIRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create PlayerEquipmentUIRoot");
            root.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(root.GetComponent<RectTransform>());

            // Backdrop
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(root.transform, false);
            StretchFullScreen(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            // Taller panel so vertical grid has more real pixels (prevents lower slots touching).
            panelRt.sizeDelta = new Vector2(700, 760);
            // Warmer/darker backdrop like the reference.
            panel.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.07f, 0.96f);
            {
                var outline = panel.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.04f, 0.02f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // UI controller host
            var uiGO = new GameObject("PlayerEquipmentUI", typeof(RectTransform), typeof(Abyss.Equipment.PlayerEquipmentUI));
            uiGO.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(uiGO.GetComponent<RectTransform>());

            // Header title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0.04f, 0.90f), new Vector2(0.70f, 0.98f));
            SetOffsets(titleRt, 0, 0, 0, 0);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Equipment";
            titleTmp.fontSize = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Left;

            // Character tabs (Inventory / Equipment)
            var characterTabs = new GameObject("CharacterTabs", typeof(RectTransform));
            characterTabs.transform.SetParent(panel.transform, false);
            var tabsRt = characterTabs.GetComponent<RectTransform>();
            SetAnchors(tabsRt, new Vector2(0.52f, 0.90f), new Vector2(0.86f, 0.98f));
            SetOffsets(tabsRt, 0, 0, 0, 0);

            var tabInventoryGo = new GameObject("Tab_Inventory", typeof(RectTransform), typeof(Image), typeof(Button));
            tabInventoryGo.transform.SetParent(characterTabs.transform, false);
            var tabInvRt = tabInventoryGo.GetComponent<RectTransform>();
            SetAnchors(tabInvRt, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            SetOffsets(tabInvRt, 0, 0, 0, 0);
            tabInventoryGo.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.11f, 0.96f);
            var tabInvBtn = tabInventoryGo.GetComponent<Button>();
            EnsureButtonLabel(tabInventoryGo, "Inventory", 18);

            var tabEquipmentGo = new GameObject("Tab_Equipment", typeof(RectTransform), typeof(Image), typeof(Button));
            tabEquipmentGo.transform.SetParent(characterTabs.transform, false);
            var tabEqRt = tabEquipmentGo.GetComponent<RectTransform>();
            SetAnchors(tabEqRt, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
            SetOffsets(tabEqRt, 0, 0, 0, 0);
            tabEquipmentGo.GetComponent<Image>().color = new Color(0.18f, 0.15f, 0.09f, 0.96f);
            var tabEqBtn = tabEquipmentGo.GetComponent<Button>();
            EnsureButtonLabel(tabEquipmentGo, "Equipment", 18);

            // Close button
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            SetAnchors(closeRt, new Vector2(0.86f, 0.92f), new Vector2(0.98f, 0.98f));
            SetOffsets(closeRt, 0, 0, 0, 0);
            closeGo.GetComponent<Image>().color = Color.white;
            var closeBtn = closeGo.GetComponent<Button>();
            EnsureButtonLabel(closeGo, "X", 28);

            // Frame background ("box")
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(panel.transform, false);
            var frameRt = frame.GetComponent<RectTransform>();
            SetAnchors(frameRt, new Vector2(0.04f, 0.06f), new Vector2(0.98f, 0.88f));
            SetOffsets(frameRt, 0, 0, 0, 0);
            frame.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.11f, 0.96f);
            {
                var outline = frame.AddComponent<Outline>();
                outline.effectColor = new Color(0.07f, 0.05f, 0.03f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // Central silhouette placeholder
            var silhouette = new GameObject("Silhouette", typeof(RectTransform), typeof(Image));
            silhouette.transform.SetParent(frame.transform, false);
            var silRt = silhouette.GetComponent<RectTransform>();
            SetAnchors(silRt, new Vector2(0.33f, 0.16f), new Vector2(0.67f, 0.84f));
            SetOffsets(silRt, 0, 0, 0, 0);
            var silImg = silhouette.GetComponent<Image>();
            silImg.color = new Color(1f, 1f, 1f, 0.06f);

            // Slot boxes (uniform spacing grid)
            // Slightly smaller + more vertical spacing so center column doesn't touch.
            const float slotSize = 68f;
            const float xLeft = 0.34f;
            const float xCenter = 0.50f;
            const float xRight = 0.66f;
            // Right-side jewelry column (separate cluster).
            const float xJewelry = 0.84f;

            // Evenly spaced center column like the reference.
            // With amulet moved to the jewelry column, keep the top row one grid-step above chest.
            const float yHelm = 0.70f;
            const float yChest = 0.56f;
            const float yBelt = 0.42f;
            const float yLegs = 0.28f;

            // Jewelry column vertical positions (separate from main gear)
            const float yJewelryAmulet = 0.78f;
            const float yJewelryRing1 = 0.60f;
            const float yJewelryRing2 = 0.42f;
            const float yJewelryArtifact = 0.24f;

            var slotHelm = BuildSlotBox(frame.transform, "Slot_Helm", new Vector2(xCenter, yHelm), slotSize);
            var slotCape = BuildSlotBox(frame.transform, "Slot_Cape", new Vector2(xLeft, yHelm), slotSize);
            var slotAmmo = BuildSlotBox(frame.transform, "Slot_Ammo", new Vector2(xRight, yHelm), slotSize);

            // Jewelry cluster (separate from the rest of the gear)
            var slotAmulet = BuildSlotBox(frame.transform, "Slot_Amulet", new Vector2(xJewelry, yJewelryAmulet), slotSize);
            var slotRing1 = BuildSlotBox(frame.transform, "Slot_Ring1", new Vector2(xJewelry, yJewelryRing1), slotSize);
            var slotRing2 = BuildSlotBox(frame.transform, "Slot_Ring2", new Vector2(xJewelry, yJewelryRing2), slotSize);
            var slotArtifact = BuildSlotBox(frame.transform, "Slot_Artifact", new Vector2(xJewelry, yJewelryArtifact), slotSize);

            // OSRS-style: weapon on the left, shield/offhand on the right.
            var slotRightHand = BuildSlotBox(frame.transform, "Slot_RightHand", new Vector2(xLeft, yChest), slotSize);
            var slotChest = BuildSlotBox(frame.transform, "Slot_Chest", new Vector2(xCenter, yChest), slotSize);
            var slotLeftHand = BuildSlotBox(frame.transform, "Slot_LeftHand", new Vector2(xRight, yChest), slotSize);

            // Per request: Gloves below Left, next to Belt.
            var slotGloves = BuildSlotBox(frame.transform, "Slot_Gloves", new Vector2(xLeft, yBelt), slotSize);
            var slotBelt = BuildSlotBox(frame.transform, "Slot_Belt", new Vector2(xCenter, yBelt), slotSize);
            var slotLegs = BuildSlotBox(frame.transform, "Slot_Legs", new Vector2(xCenter, yLegs), slotSize);

            // Connector lines (simple orthogonal connectors like the reference)
            var connectors = new GameObject("Connectors", typeof(RectTransform));
            connectors.transform.SetParent(frame.transform, false);
            var conRt = connectors.GetComponent<RectTransform>();
            StretchFullScreen(conRt);

            const float t = 5f;
            // Inset lines so they only occupy the gaps between boxes.
            const float inset = 0.058f;

            // Top row
            BuildHLineInset(connectors.transform, "Line_Cape_Helm", xLeft, xCenter, yHelm, t, inset);
            BuildHLineInset(connectors.transform, "Line_Helm_Ammo", xCenter, xRight, yHelm, t, inset);

            // Vertical spine (inset so it doesn't run through boxes)
            BuildVLineInset(connectors.transform, "Line_Helm_Chest", xCenter, yChest, yHelm, t, inset);
            BuildVLineInset(connectors.transform, "Line_Chest_Belt", xCenter, yBelt, yChest, t, inset);
            BuildVLineInset(connectors.transform, "Line_Belt_Legs", xCenter, yLegs, yBelt, t, inset);

            // Arms
            BuildHLineInset(connectors.transform, "Line_Left_Chest", xLeft, xCenter, yChest, t, inset);
            BuildHLineInset(connectors.transform, "Line_Chest_Right", xCenter, xRight, yChest, t, inset);

            // Left column down to gloves and across to belt
            BuildVLineInset(connectors.transform, "Line_Left_Gloves", xLeft, yBelt, yChest, t, inset);
            BuildHLineInset(connectors.transform, "Line_Gloves_Belt", xLeft, xCenter, yBelt, t, inset);

            // Jewelry connectors: ONLY vertical between these 4 items.
            BuildVLineInset(connectors.transform, "Line_Jewelry_Amulet_Ring1", xJewelry, yJewelryRing1, yJewelryAmulet, t, inset);
            BuildVLineInset(connectors.transform, "Line_Jewelry_Ring1_Ring2", xJewelry, yJewelryRing2, yJewelryRing1, t, inset);
            BuildVLineInset(connectors.transform, "Line_Jewelry_Ring2_Artifact", xJewelry, yJewelryArtifact, yJewelryRing2, t, inset);

            // Wire references
            var ui = uiGO.GetComponent<Abyss.Equipment.PlayerEquipmentUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("characterInventoryTabButton").objectReferenceValue = tabInvBtn;
            so.FindProperty("characterEquipmentTabButton").objectReferenceValue = tabEqBtn;
            so.FindProperty("titleText").objectReferenceValue = titleTmp;

            so.FindProperty("paperDollSilhouette").objectReferenceValue = silImg;

            var slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = 0;

            AddSlotWidget(slotsProp, 0, Abyss.Items.EquipmentSlot.Helm, slotHelm.button, slotHelm.icon, slotHelm.rarityStrip, slotHelm.label);
            AddSlotWidget(slotsProp, 1, Abyss.Items.EquipmentSlot.Cape, slotCape.button, slotCape.icon, slotCape.rarityStrip, slotCape.label);
            AddSlotWidget(slotsProp, 2, Abyss.Items.EquipmentSlot.Amulet, slotAmulet.button, slotAmulet.icon, slotAmulet.rarityStrip, slotAmulet.label);
            AddSlotWidget(slotsProp, 3, Abyss.Items.EquipmentSlot.LeftHand, slotLeftHand.button, slotLeftHand.icon, slotLeftHand.rarityStrip, slotLeftHand.label);
            AddSlotWidget(slotsProp, 4, Abyss.Items.EquipmentSlot.Chest, slotChest.button, slotChest.icon, slotChest.rarityStrip, slotChest.label);
            AddSlotWidget(slotsProp, 5, Abyss.Items.EquipmentSlot.RightHand, slotRightHand.button, slotRightHand.icon, slotRightHand.rarityStrip, slotRightHand.label);
            AddSlotWidget(slotsProp, 6, Abyss.Items.EquipmentSlot.Gloves, slotGloves.button, slotGloves.icon, slotGloves.rarityStrip, slotGloves.label);
            AddSlotWidget(slotsProp, 7, Abyss.Items.EquipmentSlot.Belt, slotBelt.button, slotBelt.icon, slotBelt.rarityStrip, slotBelt.label);
            AddSlotWidget(slotsProp, 8, Abyss.Items.EquipmentSlot.Legs, slotLegs.button, slotLegs.icon, slotLegs.rarityStrip, slotLegs.label);
            AddSlotWidget(slotsProp, 9, Abyss.Items.EquipmentSlot.Ammo, slotAmmo.button, slotAmmo.icon, slotAmmo.rarityStrip, slotAmmo.label);
            AddSlotWidget(slotsProp, 10, Abyss.Items.EquipmentSlot.Ring1, slotRing1.button, slotRing1.icon, slotRing1.rarityStrip, slotRing1.label);
            AddSlotWidget(slotsProp, 11, Abyss.Items.EquipmentSlot.Ring2, slotRing2.button, slotRing2.icon, slotRing2.rarityStrip, slotRing2.label);
            AddSlotWidget(slotsProp, 12, Abyss.Items.EquipmentSlot.Artifact, slotArtifact.button, slotArtifact.icon, slotArtifact.rarityStrip, slotArtifact.label);

            so.ApplyModifiedProperties();

            // Default inactive
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[BuildPlayerEquipmentUIEditor] Built Player Equipment UI. connectorInset={inset:0.000} slotSize={slotSize:0.#}");
        }

        private struct BuiltSlot
        {
            public Button button;
            public Image icon;
            public Image rarityStrip;
            public TextMeshProUGUI label;
        }

        private static BuiltSlot BuildSlotBox(Transform parent, string name, Vector2 anchor01, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor01;
            rt.anchorMax = anchor01;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            var bg = go.GetComponent<Image>();
            // Stone-ish slot tiles.
            bg.color = new Color(0.34f, 0.32f, 0.29f, 1f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.10f, 0.08f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = bg.color;
            cb.highlightedColor = new Color(0.40f, 0.38f, 0.34f, 1f);
            cb.pressedColor = new Color(0.28f, 0.26f, 0.23f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.28f, 0.26f, 0.23f, 0.75f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.12f, 0.12f);
            iconRt.anchorMax = new Vector2(0.88f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.enabled = false;
            icon.raycastTarget = false;

            // Rarity strip (hidden when empty; colored when equipped)
            var rarityStripGo = new GameObject("RarityStrip", typeof(RectTransform), typeof(Image));
            rarityStripGo.transform.SetParent(go.transform, false);
            var stripRt = rarityStripGo.GetComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0.0f, 0.90f);
            stripRt.anchorMax = new Vector2(1.0f, 1.0f);
            stripRt.offsetMin = Vector2.zero;
            stripRt.offsetMax = Vector2.zero;
            var stripImg = rarityStripGo.GetComponent<Image>();
            stripImg.color = new Color(1f, 1f, 1f, 0f);
            stripImg.enabled = false;
            stripImg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            // Bottom caption area (keeps icon readable).
            labelRt.anchorMin = new Vector2(0.06f, 0.02f);
            labelRt.anchorMax = new Vector2(0.94f, 0.32f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = name;
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.70f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.SetActive(true);

            return new BuiltSlot
            {
                button = go.GetComponent<Button>(),
                icon = icon,
                rarityStrip = stripImg,
                label = label,
            };
        }

        private static void AddSlotWidget(SerializedProperty arrayProp, int index, Abyss.Items.EquipmentSlot slot, Button button, Image icon, Image rarityStrip, TextMeshProUGUI label)
        {
            arrayProp.arraySize = index + 1;
            var el = arrayProp.GetArrayElementAtIndex(index);
            // IMPORTANT: use intValue (underlying enum), not enumValueIndex (name index).
            el.FindPropertyRelative("slot").intValue = (int)slot;
            el.FindPropertyRelative("button").objectReferenceValue = button;
            el.FindPropertyRelative("iconImage").objectReferenceValue = icon;
            el.FindPropertyRelative("rarityStrip").objectReferenceValue = rarityStrip;
            el.FindPropertyRelative("labelText").objectReferenceValue = label;
            el.FindPropertyRelative("emptyIcon").objectReferenceValue = GetEmptySilhouetteForSlot(slot);
        }

        private static Sprite GetEmptySilhouetteForSlot(Abyss.Items.EquipmentSlot slot)
        {
            string iconName = slot switch
            {
                Abyss.Items.EquipmentSlot.RightHand => "sil_sword",
                Abyss.Items.EquipmentSlot.LeftHand => "sil_shield",

                Abyss.Items.EquipmentSlot.Helm => "sil_helm",
                Abyss.Items.EquipmentSlot.Cape => "sil_cape",
                Abyss.Items.EquipmentSlot.Ammo => "sil_arrows",

                Abyss.Items.EquipmentSlot.Ring1 => "sil_ring",
                Abyss.Items.EquipmentSlot.Ring2 => "sil_ring",
                Abyss.Items.EquipmentSlot.Amulet => "sil_amulet",
                Abyss.Items.EquipmentSlot.Artifact => "sil_orb",

                Abyss.Items.EquipmentSlot.Chest => "sil_chest",
                Abyss.Items.EquipmentSlot.Belt => "sil_belt",
                Abyss.Items.EquipmentSlot.Gloves => "sil_gloves",
                Abyss.Items.EquipmentSlot.Legs => "sil_boots",

                _ => null,
            };

            if (string.IsNullOrEmpty(iconName))
                return null;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SilhouetteIconFolder + iconName + ".png");
            if (sprite == null)
            {
                Debug.LogWarning($"[BuildPlayerEquipmentUIEditor] Missing silhouette sprite for {slot}: expected '{SilhouetteIconFolder}{iconName}.png'. Run Tools/Equipment/Generate Silhouette Icons.");
            }
            return sprite;
        }

        private static void BuildVLine(Transform parent, string name, float x01, float yMin01, float yMax01, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x01, Mathf.Min(yMin01, yMax01));
            rt.anchorMax = new Vector2(x01, Mathf.Max(yMin01, yMax01));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(thickness, 0f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.09f, 0.07f, 0.95f);
        }

        private static void BuildHLine(Transform parent, string name, float xMin01, float xMax01, float y01, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(Mathf.Min(xMin01, xMax01), y01);
            rt.anchorMax = new Vector2(Mathf.Max(xMin01, xMax01), y01);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, thickness);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.09f, 0.07f, 0.95f);
        }

        private static void BuildVLineInset(Transform parent, string name, float x01, float yMin01, float yMax01, float thickness, float inset01)
        {
            var y0 = Mathf.Min(yMin01, yMax01) + inset01;
            var y1 = Mathf.Max(yMin01, yMax01) - inset01;
            if (y1 <= y0) return;
            BuildVLine(parent, name, x01, y0, y1, thickness);
        }

        private static void BuildHLineInset(Transform parent, string name, float xMin01, float xMax01, float y01, float thickness, float inset01)
        {
            var x0 = Mathf.Min(xMin01, xMax01) + inset01;
            var x1 = Mathf.Max(xMin01, xMax01) - inset01;
            if (x1 <= x0) return;
            BuildHLine(parent, name, x0, x1, y01, thickness);
        }

        private static void EnsureButtonLabel(GameObject btnGo, string text, int fontSize)
        {
            var existing = btnGo.transform.Find("Label");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(btnGo.transform, false);
            var rt = label.GetComponent<RectTransform>();
            StretchFullScreen(rt);

            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
        }

        private static void DestroySceneObjectsByName(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            Undo.DestroyObjectImmediate(go);
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        private static void SetOffsets(RectTransform rt, float left, float right, float top, float bottom)
        {
            if (rt == null) return;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
```

---

## Assets/Editor/BuildPlayerInventoryUIEditor.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Inventory.EditorTools
{
    public static class BuildPlayerInventoryUIEditor
    {
        [MenuItem("Tools/Build Player Inventory UI (Editor)")]
        public static void Build()
        {
            if (TryUpgradeExisting(out var upgradeSummary))
            {
                Debug.Log(upgradeSummary);
                return;
            }

            BuildFresh();
        }

        private static void BuildFresh()
        {
            DestroySceneObjectsByName("PlayerInventoryUICanvas");
            DestroySceneObjectsByName("PlayerInventoryUIRoot");
            DestroySceneObjectsByName("PlayerInventoryUI");

            var canvasGO = new GameObject("PlayerInventoryUICanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create PlayerInventoryUICanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // keep above most gameplay UI
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            StretchFullScreen(canvasGO.GetComponent<RectTransform>());

            var root = new GameObject("PlayerInventoryUIRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create PlayerInventoryUIRoot");
            root.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(root.GetComponent<RectTransform>());

            // Backdrop
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(root.transform, false);
            StretchFullScreen(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panelRt.sizeDelta = new Vector2(1100, 650);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // UI controller host
            var uiGO = new GameObject("PlayerInventoryUI", typeof(RectTransform), typeof(Abyss.Inventory.PlayerInventoryUI));
            uiGO.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(uiGO.GetComponent<RectTransform>());

            // MVP: equip button should equip visually only (no inventory consumption yet).
            try
            {
                if (uiGO.GetComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>() == null)
                    uiGO.AddComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>();
            }
            catch { }

            // Header title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0.04f, 0.90f), new Vector2(0.50f, 0.98f));
            SetOffsets(titleRt, 0, 0, 0, 0);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Inventory";
            titleTmp.fontSize = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Left;

            // Character tabs (Inventory / Equipment)
            var characterTabs = new GameObject("CharacterTabs", typeof(RectTransform));
            characterTabs.transform.SetParent(panel.transform, false);
            var tabsRt = characterTabs.GetComponent<RectTransform>();
            SetAnchors(tabsRt, new Vector2(0.52f, 0.90f), new Vector2(0.88f, 0.98f));
            SetOffsets(tabsRt, 0, 0, 0, 0);

            var tabInventoryGo = new GameObject("Tab_Inventory", typeof(RectTransform), typeof(Image), typeof(Button));
            tabInventoryGo.transform.SetParent(characterTabs.transform, false);
            var tabInvRt = tabInventoryGo.GetComponent<RectTransform>();
            SetAnchors(tabInvRt, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            SetOffsets(tabInvRt, 0, 0, 0, 0);
            tabInventoryGo.GetComponent<Image>().color = new Color(0.20f, 0.20f, 0.20f, 0.95f);
            var tabInvBtn = tabInventoryGo.GetComponent<Button>();
            EnsureButtonLabel(tabInventoryGo, "Inventory", 18);

            var tabEquipmentGo = new GameObject("Tab_Equipment", typeof(RectTransform), typeof(Image), typeof(Button));
            tabEquipmentGo.transform.SetParent(characterTabs.transform, false);
            var tabEqRt = tabEquipmentGo.GetComponent<RectTransform>();
            SetAnchors(tabEqRt, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
            SetOffsets(tabEqRt, 0, 0, 0, 0);
            tabEquipmentGo.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f, 0.95f);
            var tabEqBtn = tabEquipmentGo.GetComponent<Button>();
            EnsureButtonLabel(tabEquipmentGo, "Equipment", 18);

            // Close button
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            SetAnchors(closeRt, new Vector2(0.90f, 0.92f), new Vector2(0.98f, 0.98f));
            SetOffsets(closeRt, 0, 0, 0, 0);
            closeGo.GetComponent<Image>().color = Color.white;
            var closeBtn = closeGo.GetComponent<Button>();
            EnsureButtonLabel(closeGo, "X", 28);

            // Gold text
            var goldGo = new GameObject("GoldText", typeof(RectTransform), typeof(TextMeshProUGUI));
            goldGo.transform.SetParent(panel.transform, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            SetAnchors(goldRt, new Vector2(0.04f, 0.84f), new Vector2(0.50f, 0.90f));
            SetOffsets(goldRt, 0, 0, 0, 0);
            var goldTmp = goldGo.GetComponent<TextMeshProUGUI>();
            goldTmp.text = "Gold: 0";
            goldTmp.fontSize = 24;
            goldTmp.color = Color.white;
            goldTmp.alignment = TextAlignmentOptions.Left;

            // ScrollView (left)
            var scrollView = new GameObject("ItemsScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(panel.transform, false);
            var svRt = scrollView.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(svRt, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.84f));
            scrollView.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollView.transform, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            StretchFullScreen(viewportRt);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            EnsureGridInsetPanel(viewport.transform);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            ConfigureContentRect(contentRt);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;

            // Prevent scroll container from being shrunk by layout components.
            RemoveIfPresent<ContentSizeFitter>(scrollView);
            RemoveIfPresent<LayoutElement>(scrollView);
            RemoveIfPresent<HorizontalLayoutGroup>(scrollView);
            RemoveIfPresent<VerticalLayoutGroup>(scrollView);

            // Details (right)
            var details = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Image), typeof(Abyss.Inventory.PlayerInventoryDetailsUI));
            details.transform.SetParent(panel.transform, false);
            var detailsRt = details.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(detailsRt, new Vector2(0.62f, 0.06f), new Vector2(0.98f, 0.84f));
            details.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            // Details icon
            var dIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
            dIcon.transform.SetParent(details.transform, false);
            var dIconRt = dIcon.GetComponent<RectTransform>();
            SetAnchors(dIconRt, new Vector2(0.06f, 0.80f), new Vector2(0.22f, 0.94f));
            SetOffsets(dIconRt, 0, 0, 0, 0);
            var dIconImg = dIcon.GetComponent<Image>();
            dIconImg.color = Color.white;
            dIconImg.preserveAspect = true;
            dIcon.SetActive(false);

            var dName = CreateDetailsText(details.transform, "DetailName", new Vector2(0.26f, 0.87f), new Vector2(0.94f, 0.96f), 28, FontStyles.Bold);
            var dRarity = CreateDetailsText(details.transform, "DetailRarity", new Vector2(0.26f, 0.80f), new Vector2(0.94f, 0.86f), 20, FontStyles.Normal);
            var dCount = CreateDetailsText(details.transform, "DetailCount", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.80f), 20, FontStyles.Normal);

            var dDesc = CreateDetailsText(details.transform, "DetailDescription", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.72f), 18, FontStyles.Normal);
            dDesc.textWrappingMode = TextWrappingModes.Normal;
            dDesc.alignment = TextAlignmentOptions.TopLeft;

            // Row template (disabled)
            var rowTemplate = BuildRowTemplate(content.transform);
            rowTemplate.SetActive(false);

            // Wire references
            var ui = uiGO.GetComponent<Abyss.Inventory.PlayerInventoryUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("characterInventoryTabButton").objectReferenceValue = tabInvBtn;
            so.FindProperty("characterEquipmentTabButton").objectReferenceValue = tabEqBtn;
            so.FindProperty("titleText").objectReferenceValue = titleTmp;
            so.FindProperty("goldText").objectReferenceValue = goldTmp;
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("contentRoot").objectReferenceValue = contentRt;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
            so.FindProperty("detailsUI").objectReferenceValue = details.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>();
            so.ApplyModifiedProperties();

            var detailsSo = new SerializedObject(details.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>());
            detailsSo.FindProperty("iconImage").objectReferenceValue = dIconImg;
            detailsSo.FindProperty("nameText").objectReferenceValue = dName;
            detailsSo.FindProperty("rarityText").objectReferenceValue = dRarity;
            detailsSo.FindProperty("countText").objectReferenceValue = dCount;
            detailsSo.FindProperty("descriptionText").objectReferenceValue = dDesc;
            detailsSo.ApplyModifiedProperties();

            // Default inactive
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildPlayerInventoryUIEditor] Built Player Inventory UI.");
        }

        private static bool TryUpgradeExisting(out string summary)
        {
            summary = string.Empty;

            var all = Resources.FindObjectsOfTypeAll<Abyss.Inventory.PlayerInventoryUI>();
            if (all == null || all.Length == 0)
                return false;

            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            if (sceneUis.Length == 0)
                return false;

            int wired = 0;
            int created = 0;

            foreach (var ui in sceneUis)
            {
                if (ui == null) continue;

                // MVP: ensure Equip uses visual-only equipment state (no inventory consumption yet).
                try
                {
                    if (ui.GetComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>() == null)
                    {
                        ui.gameObject.AddComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>();
                        created++;
                    }
                }
                catch { }

                if (TryWireByName(ui, ref created))
                    wired++;
            }

            summary = $"[BuildPlayerInventoryUIEditor] Upgraded existing PlayerInventoryUI(s): uiWired={wired}/{sceneUis.Length}, sceneObjectsCreated={created}.";
            return true;
        }

        private static bool TryWireByName(Abyss.Inventory.PlayerInventoryUI ui, ref int created)
        {
            // Find the canvas/root/panel by convention.
            var canvas = GameObject.Find("PlayerInventoryUICanvas");
            var root = GameObject.Find("PlayerInventoryUIRoot");
            if (canvas == null || root == null)
                return false;

            var panel = FindDeepChild(root.transform, "Panel");
            if (panel == null)
                return false;

            var closeBtn = FindDeepChild(panel, "CloseButton")?.GetComponent<Button>();
            var title = FindDeepChild(panel, "Title")?.GetComponent<TextMeshProUGUI>();
            var gold = FindDeepChild(panel, "GoldText")?.GetComponent<TextMeshProUGUI>();

            var scrollRect = FindDeepChild(panel, "ItemsScrollView")?.GetComponent<ScrollRect>();
            var content = FindDeepChild(panel, "Content")?.GetComponent<RectTransform>();

            if (scrollRect != null)
                UpgradeScrollHierarchy(panel.gameObject, scrollRect, ref created);

            if (content != null)
            {
                EnsureListContentLayoutComponents(content, ref created);
                ConfigureContentRect(content);
            }

            if (content != null)
                EnsureListContentLayoutComponents(content, ref created);

            var detailsPanel = FindDeepChild(panel, "DetailsPanel");
            var details = detailsPanel != null ? detailsPanel.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>() : null;

            // Ensure row template exists.
            GameObject rowTemplate = FindDeepChild(panel, "RowTemplate") != null ? FindDeepChild(panel, "RowTemplate").gameObject : null;
            if (rowTemplate == null && content != null)
            {
                rowTemplate = BuildRowTemplate(content);
                rowTemplate.name = "RowTemplate";
                rowTemplate.SetActive(false);
                created++;
            }
            else if (rowTemplate != null)
            {
                var rt = rowTemplate.GetComponent<RectTransform>();
                ConfigureRowTemplateRect(rt);

                var le = rowTemplate.GetComponent<LayoutElement>();
                if (le == null) le = rowTemplate.AddComponent<LayoutElement>();
                le.minHeight = 56f;
                le.preferredHeight = 56f;
                le.flexibleHeight = 0f;

                if (rowTemplate.activeSelf)
                    rowTemplate.SetActive(false);
            }

            // Ensure RowTemplate's PlayerInventoryRowUI has Icon wired (child named "Icon").
            try
            {
                if (rowTemplate != null)
                {
                    var rowUi = rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
                    var iconImg = rowTemplate.transform.Find("Icon") != null
                        ? rowTemplate.transform.Find("Icon").GetComponent<Image>()
                        : null;

                    var rarityStripImg = rowTemplate.transform.Find("RarityStrip") != null
                        ? rowTemplate.transform.Find("RarityStrip").GetComponent<Image>()
                        : null;

                    if (rowUi != null && iconImg != null)
                    {
                        var rowSo = new SerializedObject(rowUi);
                        var p = rowSo.FindProperty("iconImage");
                        if (p != null && p.objectReferenceValue == null)
                        {
                            p.objectReferenceValue = iconImg;
                            rowSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(rowUi);
                        }
                    }

                    if (rowUi != null && rarityStripImg != null)
                    {
                        var rowSo = new SerializedObject(rowUi);
                        var p = rowSo.FindProperty("rarityStrip");
                        if (p != null && p.objectReferenceValue == null)
                        {
                            p.objectReferenceValue = rarityStripImg;
                            rowSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(rowUi);
                        }
                    }
                }
            }
            catch { }

            if (detailsPanel != null && details == null)
                details = detailsPanel.gameObject.AddComponent<Abyss.Inventory.PlayerInventoryDetailsUI>();

            // Ensure detail children exist.
            if (detailsPanel != null)
                EnsureDetailsChildren(detailsPanel.gameObject, ref created);

            var rootGo = root;

            var so = new SerializedObject(ui);
            if (so.FindProperty("root") != null) so.FindProperty("root").objectReferenceValue = rootGo;
            if (so.FindProperty("closeButton") != null) so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            if (so.FindProperty("titleText") != null) so.FindProperty("titleText").objectReferenceValue = title;
            if (so.FindProperty("goldText") != null) so.FindProperty("goldText").objectReferenceValue = gold;
            if (so.FindProperty("scrollRect") != null) so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            if (so.FindProperty("contentRoot") != null) so.FindProperty("contentRoot").objectReferenceValue = content;
            if (so.FindProperty("rowTemplate") != null) so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate != null ? rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>() : null;
            if (so.FindProperty("detailsUI") != null) so.FindProperty("detailsUI").objectReferenceValue = details;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (details != null)
            {
                var icon = FindDeepChild(detailsPanel, "DetailIcon")?.GetComponent<Image>();
                var dName = FindDeepChild(detailsPanel, "DetailName")?.GetComponent<TextMeshProUGUI>();
                var dRarity = FindDeepChild(detailsPanel, "DetailRarity")?.GetComponent<TextMeshProUGUI>();
                var dCount = FindDeepChild(detailsPanel, "DetailCount")?.GetComponent<TextMeshProUGUI>();
                var dDesc = FindDeepChild(detailsPanel, "DetailDescription")?.GetComponent<TextMeshProUGUI>();

                var dso = new SerializedObject(details);
                dso.FindProperty("iconImage").objectReferenceValue = icon;
                dso.FindProperty("nameText").objectReferenceValue = dName;
                dso.FindProperty("rarityText").objectReferenceValue = dRarity;
                dso.FindProperty("countText").objectReferenceValue = dCount;
                dso.FindProperty("descriptionText").objectReferenceValue = dDesc;
                dso.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(details);
            }

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        private static void EnsureDetailsChildren(GameObject detailsPanel, ref int created)
        {
            if (detailsPanel == null) return;

            if (FindDeepChild(detailsPanel, "DetailIcon") == null)
            {
                var dIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
                dIcon.transform.SetParent(detailsPanel.transform, false);
                var rt = dIcon.GetComponent<RectTransform>();
                SetAnchors(rt, new Vector2(0.06f, 0.80f), new Vector2(0.22f, 0.94f));
                SetOffsets(rt, 0, 0, 0, 0);
                var img = dIcon.GetComponent<Image>();
                img.color = Color.white;
                img.preserveAspect = true;
                dIcon.SetActive(false);
                created++;
            }

            EnsureText(detailsPanel.transform, "DetailName", new Vector2(0.26f, 0.87f), new Vector2(0.94f, 0.96f), 28, FontStyles.Bold, ref created);
            EnsureText(detailsPanel.transform, "DetailRarity", new Vector2(0.26f, 0.80f), new Vector2(0.94f, 0.86f), 20, FontStyles.Normal, ref created);
            EnsureText(detailsPanel.transform, "DetailCount", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.80f), 20, FontStyles.Normal, ref created);

            var desc = EnsureText(detailsPanel.transform, "DetailDescription", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.72f), 18, FontStyles.Normal, ref created);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.alignment = TextAlignmentOptions.TopLeft;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyles style, ref int created)
        {
            var existing = FindDeepChild(parent, name);
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                SetAnchors(rt, min, max);
                SetOffsets(rt, 0, 0, 0, 0);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.text = string.Empty;
                tmp.fontSize = size;
                tmp.fontStyle = style;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Left;
                created++;
                return tmp;
            }

            return existing.GetComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI CreateDetailsText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, min, max);
            SetOffsets(rt, 0, 0, 0, 0);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static GameObject BuildRowTemplate(Transform contentParent)
        {
            var row = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(Abyss.Inventory.PlayerInventoryRowUI));
            row.transform.SetParent(contentParent, false);

            var rt = row.GetComponent<RectTransform>();
            ConfigureRowTemplateRect(rt);

            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 56f;
            layout.preferredHeight = 56f;
            layout.flexibleHeight = 0f;

            var bg = row.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            // Outline (used for hover/selected borders in grid mode)
            var outline = row.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            // Rarity strip
            var strip = new GameObject("RarityStrip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(row.transform, false);
            var stripRt = strip.GetComponent<RectTransform>();
            SetAnchors(stripRt, new Vector2(0f, 0f), new Vector2(0f, 1f));
            stripRt.sizeDelta = new Vector2(6, 0);
            stripRt.anchoredPosition = new Vector2(3, 0);
            strip.GetComponent<Image>().color = Color.white;

            // Icon
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(row.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            SetAnchors(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconRt.sizeDelta = new Vector2(40, 40);
            iconRt.anchoredPosition = new Vector2(32, 0);
            var iconImg = icon.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            icon.SetActive(false);

            // Name
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(row.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            SetAnchors(nameRt, new Vector2(0.10f, 0f), new Vector2(0.78f, 1f));
            SetOffsets(nameRt, 0, 0, 0, 0);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = "Item";
            nameTmp.fontSize = 22;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.raycastTarget = false;

            // Count
            var countGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGo.transform.SetParent(row.transform, false);
            var countRt = countGo.GetComponent<RectTransform>();
            SetAnchors(countRt, new Vector2(0.78f, 0f), new Vector2(0.97f, 1f));
            SetOffsets(countRt, 0, 0, 0, 0);
            var countTmp = countGo.GetComponent<TextMeshProUGUI>();
            countTmp.text = "x1";
            countTmp.fontSize = 22;
            countTmp.color = Color.white;
            countTmp.alignment = TextAlignmentOptions.Right;
            countTmp.raycastTarget = false;

            // Wire row UI
            var rowUi = row.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
            var so = new SerializedObject(rowUi);
            so.FindProperty("background").objectReferenceValue = bg;
            // PlayerInventoryRowUI no longer stores an Outline reference; it self-manages borders at runtime.
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("rarityStrip").objectReferenceValue = strip.GetComponent<Image>();
            so.FindProperty("nameText").objectReferenceValue = nameTmp;
            so.FindProperty("countText").objectReferenceValue = countTmp;
            so.FindProperty("button").objectReferenceValue = row.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }

        private static void ConfigureRowTemplateRect(RectTransform rt)
        {
            if (rt == null) return;
            // Stretch horizontally for VerticalLayoutGroup.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 56f);
        }

        private static void EnsureListContentLayoutComponents(RectTransform content, ref int created)
        {
            if (content == null) return;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                created++;
            }

            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = vlg.padding ?? new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                created++;
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private static void UpgradeScrollHierarchy(GameObject panel, ScrollRect scrollRect, ref int created)
        {
            if (panel == null || scrollRect == null) return;

            var scrollGo = scrollRect.gameObject;
            var svRt = scrollGo.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(svRt, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.84f));

            // Remove layout components that can collapse size.
            RemoveIfPresent<ContentSizeFitter>(scrollGo);
            RemoveIfPresent<LayoutElement>(scrollGo);
            RemoveIfPresent<HorizontalLayoutGroup>(scrollGo);
            RemoveIfPresent<VerticalLayoutGroup>(scrollGo);

            // Ensure viewport exists and stretches.
            GameObject viewportGo = FindDeepChild(scrollGo, "Viewport");
            if (viewportGo == null)
            {
                viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                created++;
            }

            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFullScreen(viewportRt);
            var vpImg = viewportGo.GetComponent<Image>();
            if (vpImg == null) vpImg = viewportGo.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            var mask = viewportGo.GetComponent<Mask>();
            if (mask == null) mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            EnsureGridInsetPanel(viewportGo.transform);

            // Ensure content is child of viewport.
            var contentGo = FindDeepChild(scrollGo, "Content");
            if (contentGo == null)
            {
                contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                created++;
            }
            if (contentGo != null && contentGo.transform.parent != viewportGo.transform)
                contentGo.transform.SetParent(viewportGo.transform, false);

            scrollRect.viewport = viewportRt;
            var contentRt = contentGo != null ? contentGo.GetComponent<RectTransform>() : null;
            if (contentRt != null)
            {
                ConfigureContentRect(contentRt);
                int localCreated = 0;
                EnsureListContentLayoutComponents(contentRt, ref localCreated);
                if (localCreated > 0) created += localCreated;

                scrollRect.content = contentRt;
            }

            // Ensure scroll directions are correct.
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }

        private static void ConfigureSplitRegionRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            SetOffsets(rt, 0, 0, 0, 0);
        }

        private static void ConfigureContentRect(RectTransform contentRt)
        {
            if (contentRt == null) return;

            // Stretch to fill the viewport; vertical position is driven by layout.
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
        }

        private static void RemoveIfPresent<T>(GameObject go) where T : Component
        {
            if (go == null) return;
            var c = go.GetComponent<T>();
            if (c == null) return;
            UnityEngine.Object.DestroyImmediate(c);
        }

        private static void EnsureButtonLabel(GameObject buttonGo, string text, int fontSize)
        {
            if (buttonGo == null) return;
            var existing = buttonGo.transform.Find("Text");
            GameObject labelGo = existing != null ? existing.gameObject : null;
            if (labelGo == null)
            {
                labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(buttonGo.transform, false);
                StretchFullScreen(labelGo.GetComponent<RectTransform>());
            }

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.black;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.extraPadding = true;
        }

        private static void EnsureGridInsetPanel(Transform viewport)
        {
            if (viewport == null) return;

            var existing = viewport.Find("GridInsetPanel");
            GameObject panelGo;
            if (existing != null)
                panelGo = existing.gameObject;
            else
                panelGo = new GameObject("GridInsetPanel", typeof(RectTransform), typeof(Image), typeof(Outline));

            panelGo.transform.SetParent(viewport, false);
            panelGo.transform.SetAsFirstSibling(); // behind Content

            var rt = panelGo.GetComponent<RectTransform>();
            StretchFullScreen(rt);

            var img = panelGo.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.18f);
            img.raycastTarget = false;

            var outline = panelGo.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.10f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        private static GameObject FindDeepChild(GameObject root, string name)
        {
            if (root == null) return null;
            return FindDeepChild(root.transform, name)?.gameObject;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void DestroySceneObjectsByName(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            Undo.DestroyObjectImmediate(go);
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        private static void SetOffsets(RectTransform rt, float left, float right, float top, float bottom)
        {
            if (rt == null) return;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
```

---

## Assets/Editor/GenerateBasicItemIcons.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

public static class GenerateBasicItemIcons
{
    private const int Size = 64;
    private const string OutputFolder = "Assets/UI/Generated/Icons";

    [MenuItem("Tools/UI/Generate Basic Item Icons")]
    public static void Generate()
    {
        // Requirement: generate PNG bytes from an in-memory Texture2D only (never encode an imported asset).
        EnsureFolders(OutputFolder);

        var outputs = new List<(Color32 bg, Color32 fg, string letter, string fileName)>
        {
            (new Color32(150, 30, 30, 255),   new Color32(255, 255, 255, 255), "H", "Icon_HealthPotion.png"),
            (new Color32(35, 75, 170, 255),   new Color32(255, 255, 255, 255), "M", "Icon_ManaPotion.png"),
            (new Color32(210, 180, 130, 255), new Color32(45, 30, 10, 255),    "S", "Icon_TownScroll.png"),
        };

        var generatedAssetPaths = new List<string>(outputs.Count);
        foreach (var o in outputs)
        {
            var assetPath = (OutputFolder + "/" + o.fileName).Replace('\\', '/');

            Texture2D tex = null;
            try
            {
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false)
                {
                    name = o.fileName,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                Fill(tex, o.bg);
                DrawBorder(tex, new Color32(0, 0, 0, 60));
                DrawLetter(tex, o.letter, o.fg);

                // IMPORTANT: keep readable for EncodeToPNG.
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                var png = ImageConversion.EncodeToPNG(tex);
                File.WriteAllBytes(assetPath, png);
            }
            finally
            {
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
            }

            // Requirement: explicitly import after writing.
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImportedSprite(assetPath);
            generatedAssetPaths.Add(assetPath);
        }

        var iconPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "potion_health", OutputFolder + "/Icon_HealthPotion.png" },
            { "potion_mana",   OutputFolder + "/Icon_ManaPotion.png" },
            { "scroll_town",   OutputFolder + "/Icon_TownScroll.png" },
        };

        AssignIconsToItems(iconPaths);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GenerateBasicItemIcons] Done.");
    }

    private static void ConfigureImportedSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.sRGBTexture = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void EnsureFolders(string assetFolder)
    {
        // Creates nested folders under Assets/ using AssetDatabase so Unity recognizes them.
        if (string.IsNullOrWhiteSpace(assetFolder))
            return;

        assetFolder = assetFolder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        var parts = assetFolder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        string current = parts[0];
        if (!string.Equals(current, "Assets", StringComparison.Ordinal))
            return;

        current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void AssignIconsToItems(Dictionary<string, string> itemIdToIconPath)
    {
        if (itemIdToIconPath == null || itemIdToIconPath.Count == 0)
            return;

        int updated = 0;
        int missing = 0;

        foreach (var kv in itemIdToIconPath)
        {
            var itemId = kv.Key;
            var iconPath = kv.Value;
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[GenerateBasicItemIcons] Could not load sprite at '{iconPath}'");
                continue;
            }

            var def = FindItemDefinitionById(itemId);
            if (def == null)
            {
                missing++;
                Debug.LogWarning($"[GenerateBasicItemIcons] Could not find ItemDefinition with itemId='{itemId}'");
                continue;
            }

            def.icon = icon;
            EditorUtility.SetDirty(def);
            updated++;
            Debug.Log($"[GenerateBasicItemIcons] Assigned icon '{icon.name}' to ItemDefinition '{def.displayName}' ({def.itemId})");
        }

        Debug.Log($"[GenerateBasicItemIcons] Icon assignment complete. Updated={updated} MissingDefs={missing}");
    }

    private static ItemDefinition FindItemDefinitionById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;
            if (string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                return def;
        }

        return null;
    }

    private static void Fill(Texture2D tex, Color32 c)
    {
        var pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = c;
        tex.SetPixels32(pixels);
    }

    private static void DrawBorder(Texture2D tex, Color32 c)
    {
        for (int x = 0; x < Size; x++)
        {
            tex.SetPixel(x, 0, c);
            tex.SetPixel(x, Size - 1, c);
        }

        for (int y = 0; y < Size; y++)
        {
            tex.SetPixel(0, y, c);
            tex.SetPixel(Size - 1, y, c);
        }
    }

    private static void DrawLetter(Texture2D tex, string letter, Color32 color)
    {
        // Simple 5x7 block font. Each "1" becomes a filled square.
        var glyph = GetGlyph(letter);
        int glyphW = glyph[0].Length;
        int glyphH = glyph.Length;

        // Scale up so it looks crisp at 64x64.
        int cell = 8; // 5*8=40, 7*8=56
        int w = glyphW * cell;
        int h = glyphH * cell;
        int startX = (Size - w) / 2;
        int startY = (Size - h) / 2;

        for (int gy = 0; gy < glyphH; gy++)
        {
            for (int gx = 0; gx < glyphW; gx++)
            {
                if (glyph[gy][gx] != '1')
                    continue;

                DrawRect(tex, startX + gx * cell, startY + (glyphH - 1 - gy) * cell, cell, cell, color);
            }
        }
    }

    private static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color32 c)
    {
        int xMax = Mathf.Min(Size, x + w);
        int yMax = Mathf.Min(Size, y + h);
        int xMin = Mathf.Max(0, x);
        int yMin = Mathf.Max(0, y);

        for (int yy = yMin; yy < yMax; yy++)
        {
            for (int xx = xMin; xx < xMax; xx++)
            {
                tex.SetPixel(xx, yy, c);
            }
        }
    }

    private static string[] GetGlyph(string letter)
    {
        switch ((letter ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "H":
                return new[]
                {
                    "10001",
                    "10001",
                    "10001",
                    "11111",
                    "10001",
                    "10001",
                    "10001",
                };

            case "M":
                return new[]
                {
                    "10001",
                    "11011",
                    "10101",
                    "10001",
                    "10001",
                    "10001",
                    "10001",
                };

            case "S":
            default:
                return new[]
                {
                    "01111",
                    "10000",
                    "10000",
                    "01110",
                    "00001",
                    "00001",
                    "11110",
                };
        }
    }
}
```

---

## Assets/Editor/GenerateUiBorderSprite.cs

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateUiBorderSprite
{
    private const int Size = 16;
    private const string AssetPath = "Assets/UI/Generated/UIBorder_16.png";
    private const string ResourcesPath = "Assets/Resources/UI/Generated/UIBorder_16.png";

    [MenuItem("Tools/UI/Generate Border Sprite")]
    public static void Generate()
    {
        GenerateAtPath(AssetPath);
        GenerateAtPath(ResourcesPath);

        AssetDatabase.Refresh();
        ConfigureImporter(AssetPath);
        ConfigureImporter(ResourcesPath);

        Debug.Log($"[UI] Generated border sprite at '{AssetPath}' (and Resources copy).", AssetDatabase.LoadAssetAtPath<Object>(AssetPath));
    }

    [InitializeOnLoadMethod]
    private static void AutoGenerateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AssetPath) || !File.Exists(ResourcesPath))
                Generate();
        };
    }

    private static void GenerateAtPath(string path)
    {
        // If you want to skip regeneration when the file already exists, flip this to false.
        const bool overwriteIfExists = true;

        if (!overwriteIfExists && File.Exists(path))
            return;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false)
        {
            name = Path.GetFileNameWithoutExtension(path)
        };

        var clear = new Color(0f, 0f, 0f, 0f);
        var white = new Color(1f, 1f, 1f, 1f);

        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                bool border = x == 0 || y == 0 || x == (Size - 1) || y == (Size - 1);
                pixels[y * Size + x] = border ? white : clear;
            }
        }

        tex.SetPixels(pixels);
        // IMPORTANT:
        // - Keep the in-memory texture readable until AFTER EncodeToPNG.
        // - Do NOT call EncodeToPNG on the imported asset texture.
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        byte[] png = ImageConversion.EncodeToPNG(tex);
        Object.DestroyImmediate(tex);

        if (png == null || png.Length == 0)
            throw new IOException($"Failed to encode PNG for '{path}'.");

        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(1f, 1f, 1f, 1f);

        importer.SaveAndReimport();
    }
}
```

---

## Assets/Editor/TownInteractionRestorer.cs

```csharp
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    public static class TownInteractionRestorer
    {
        private const string MenuFix = "Tools/Abyss/Town/Fix Merchant Interactions (After Move)";
        private const string MenuValidate = "Tools/Abyss/Town/Validate Merchant Interactions";

        [MenuItem(MenuFix)]
        public static void FixMerchantInteractions()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[TownInteractionRestorer] No valid active scene.");
                return;
            }

            EnsureMerchantUiExists();
            EnsureMerchantWorldInteractionExists();

            int fixedByEdgeville = 0;
            try
            {
                // If the Edgeville hub exists, use the existing robust rebuild.
                if (GameObject.Find("EdgevilleHub_Root") != null)
                {
                    Abyss.EditorTools.EdgevilleTownBuilder.RebuildDoorsAndClickTargetsSafe();
                    fixedByEdgeville = 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TownInteractionRestorer] Edgeville rebuild failed; falling back to generic fixer. " + e.Message);
            }

            int shops = 0;
            int createdTargets = 0;
            int updatedTargets = 0;
            int addedColliders = 0;
            int addedHighlighters = 0;

#if UNITY_2022_2_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<Abyss.Shop.MerchantShop>();
#endif
            if (found != null)
            {
                foreach (var shop in found)
                {
                    if (shop == null) continue;
                    shops++;

                    // Ensure a collider exists somewhere under the shop so clicks/hovers work.
                    var anyCollider = shop.GetComponentInChildren<Collider>(true);
                    if (anyCollider == null)
                    {
                        var bc = Undo.AddComponent<BoxCollider>(shop.gameObject);
                        bc.isTrigger = false;
                        bc.center = new Vector3(0f, 1f, 0f);
                        bc.size = new Vector3(1.2f, 2.0f, 1.2f);
                        addedColliders++;
                    }
                    else
                    {
                        anyCollider.isTrigger = false;
                    }

                    // Ensure a dedicated click target exists (matches the builder name).
                    var clickTf = shop.transform.Find("BuildingClickTarget");
                    bool created = false;
                    if (clickTf == null)
                    {
                        var go = new GameObject("BuildingClickTarget");
                        Undo.RegisterCreatedObjectUndo(go, "Create BuildingClickTarget");
                        go.transform.SetParent(shop.transform, false);
                        clickTf = go.transform;
                        created = true;
                        createdTargets++;
                    }

                    // Determine bounds for sizing/positioning.
                    var renderers = shop.GetComponentsInChildren<Renderer>(true);
                    Bounds b;
                    bool hasBounds = TryGetBounds(renderers, out b);
                    if (!hasBounds)
                    {
                        // Fallback bounds around the shop transform.
                        b = new Bounds(shop.transform.position + Vector3.up, new Vector3(6f, 4f, 6f));
                    }

                    // Place click target at bounds center and keep it axis-aligned.
                    clickTf.position = b.center;
                    clickTf.rotation = Quaternion.identity;

                    // Remove any renderer on the click target (it should be invisible).
                    TryRemoveRenderer(clickTf.gameObject);

                    // Box collider fits the bounds.
                    var box = clickTf.GetComponent<BoxCollider>();
                    if (box == null) box = Undo.AddComponent<BoxCollider>(clickTf.gameObject);
                    box.isTrigger = false;
                    box.center = Vector3.zero;
                    box.size = b.size;

                    // Highlight hookup.
                    var target = clickTf.GetComponent<Abyss.Shop.MerchantDoorClickTarget>();
                    if (target == null)
                    {
                        target = Undo.AddComponent<Abyss.Shop.MerchantDoorClickTarget>(clickTf.gameObject);
                        addedHighlighters++;
                    }

                    if (renderers != null && renderers.Length > 0)
                    {
                        target.SetHighlightRenderers(renderers);
                        target.SetHighlightColor(Color.red);
                    }

                    // Layer
                    int interactableLayer = LayerMask.NameToLayer("Interactable");
                    if (interactableLayer >= 0)
                        clickTf.gameObject.layer = interactableLayer;

                    if (!created)
                        updatedTargets++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[TownInteractionRestorer] Done. edgevilleRebuild={(fixedByEdgeville == 1)} shops={shops} " +
                $"createdTargets={createdTargets} updatedTargets={updatedTargets} addedColliders={addedColliders} addedHighlighters={addedHighlighters}"
            );
        }

        [MenuItem(MenuValidate)]
        public static void ValidateMerchantInteractions()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[TownInteractionRestorer] No valid active scene.");
                return;
            }

            int shops = 0;
            int missingUi = 0;
            int missingWorldRaycasters = 0;
            int missingClickTarget = 0;
            int missingClickCollider = 0;
            int missingHighlighter = 0;
            int disabledClickCollider = 0;
            int ignoreRaycastLayerTargets = 0;

#if UNITY_2022_2_OR_NEWER
            var ui = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantShopUI>();
#else
            var ui = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantShopUI>();
#endif
            if (ui == null) missingUi = 1;

            // These are runtime scripts spawned via RuntimeInitializeOnLoadMethod, but that can be skipped
            // when certain Enter Play Mode options are enabled. Validate presence so we can fix in-scene.
#if UNITY_2022_2_OR_NEWER
            var clickRaycaster = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#else
            var clickRaycaster = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#endif
            if (clickRaycaster == null || hoverHighlighter == null)
                missingWorldRaycasters = 1;

#if UNITY_2022_2_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<Abyss.Shop.MerchantShop>();
#endif

            if (found != null)
            {
                foreach (var shop in found)
                {
                    if (shop == null) continue;
                    shops++;

                    var clickTf = shop.transform.Find("BuildingClickTarget");
                    if (clickTf == null)
                    {
                        missingClickTarget++;
                        continue;
                    }

                    var col = clickTf.GetComponent<Collider>();
                    if (col == null) missingClickCollider++;
                    else if (!col.enabled) disabledClickCollider++;

                    if (clickTf.gameObject.layer == 2) // Ignore Raycast
                        ignoreRaycastLayerTargets++;

                    var hi = clickTf.GetComponent<Abyss.Shop.MerchantDoorClickTarget>();
                    if (hi == null) missingHighlighter++;
                }
            }

            Debug.Log(
                $"[TownInteractionRestorer] Validate: shops={shops} missingUi={(missingUi == 1)} " +
                $"missingWorldRaycasters={(missingWorldRaycasters == 1)} " +
                $"missingClickTarget={missingClickTarget} missingClickCollider={missingClickCollider} disabledClickCollider={disabledClickCollider} " +
                $"missingHighlighter={missingHighlighter} ignoreRaycastLayerTargets={ignoreRaycastLayerTargets}\n" +
                "If any are missing, run: Tools/Abyss/Town/Fix Merchant Interactions (After Move)"
            );
        }

        private static void EnsureMerchantWorldInteractionExists()
        {
            // Ensure these exist in the active scene so merchant hover/click works regardless of
            // RuntimeInitializeOnLoadMethod behavior.
#if UNITY_2022_2_OR_NEWER
            var clickRaycaster = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#else
            var clickRaycaster = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#endif

            if (clickRaycaster != null && hoverHighlighter != null)
                return;

            var root = GameObject.Find("MerchantWorldInteraction");
            if (root == null)
            {
                root = new GameObject("MerchantWorldInteraction");
                Undo.RegisterCreatedObjectUndo(root, "Create MerchantWorldInteraction");
            }

            if (clickRaycaster == null)
            {
                Undo.AddComponent<Abyss.Shop.MerchantClickRaycaster>(root);
            }

            if (hoverHighlighter == null)
            {
                Undo.AddComponent<Abyss.Shop.MerchantDoorHoverHighlighter>(root);
            }

            Debug.Log("[TownInteractionRestorer] Ensured MerchantWorldInteraction (raycaster + hover highlighter)." );
        }

        private static void EnsureMerchantUiExists()
        {
#if UNITY_2022_2_OR_NEWER
            var ui = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantShopUI>();
#else
            var ui = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantShopUI>();
#endif
            if (ui != null)
                return;

            try
            {
                BuildMerchantShopUIEditor.BuildMerchantShopUI();
                Debug.Log("[TownInteractionRestorer] Built MerchantShopUI (was missing).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TownInteractionRestorer] MerchantShopUI missing and auto-build failed: " + e.Message);
            }
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            if (renderers == null || renderers.Length == 0)
                return false;

            bool hasAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!hasAny) { bounds = r.bounds; hasAny = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return hasAny;
        }

        private static void TryRemoveRenderer(GameObject go)
        {
            if (go == null) return;
            try
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) UnityEngine.Object.DestroyImmediate(r);
            }
            catch { }
        }
    }
}
```

---

## Assets/Editor/TownLegacyRootCleaner.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    public static class TownLegacyRootCleaner
    {
        private const string MenuDeleteSelected = "Tools/Abyss/Town/Safely Delete Selected Root";

        [MenuItem(MenuDeleteSelected)]
        public static void SafelyDeleteSelectedRoot()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[TownLegacyRootCleaner] Select the old town root GameObject you want to delete (e.g. the broken 'Town/EdgevilleHub_Root').");
                return;
            }

            var scene = root.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[TownLegacyRootCleaner] Selected object is not part of a loaded scene.");
                return;
            }

            // Gather all UnityEngine.Objects under the selected root that might be referenced.
            var subtreeIds = new HashSet<int>();
            try
            {
                subtreeIds.Add(root.GetInstanceID());
                subtreeIds.Add(root.transform.GetInstanceID());

                var comps = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;
                    subtreeIds.Add(c.GetInstanceID());
                    if (c.gameObject != null) subtreeIds.Add(c.gameObject.GetInstanceID());
                }

                var transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    if (t == null) continue;
                    subtreeIds.Add(t.GetInstanceID());
                    if (t.gameObject != null) subtreeIds.Add(t.gameObject.GetInstanceID());
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[TownLegacyRootCleaner] Failed to scan selection subtree: " + e.Message);
                return;
            }

            // Scan all scene components for serialized references into that subtree.
            var refs = new List<SerializedRef>(64);
#if UNITY_2022_2_OR_NEWER
            var allComponents = UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var allComponents = UnityEngine.Object.FindObjectsOfType<Component>(true);
#endif
            for (int i = 0; i < allComponents.Length; i++)
            {
                var comp = allComponents[i];
                if (comp == null) continue;

                // Only consider references from this same scene.
                if (comp.gameObject == null || comp.gameObject.scene != scene)
                    continue;

                // Skip anything under the root (self-references are fine).
                if (comp.transform != null && comp.transform.IsChildOf(root.transform))
                    continue;

                try
                {
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    bool enterChildren = true;
                    while (it.NextVisible(enterChildren))
                    {
                        enterChildren = false;

                        if (it.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        var obj = it.objectReferenceValue;
                        if (obj == null)
                            continue;

                        if (!subtreeIds.Contains(obj.GetInstanceID()))
                            continue;

                        refs.Add(new SerializedRef
                        {
                            Source = comp,
                            PropertyPath = it.propertyPath,
                            Target = obj
                        });

                        // Avoid spamming duplicate properties on huge components.
                        if (refs.Count > 2000)
                            break;
                    }
                }
                catch
                {
                    // Some components may not be serializable; ignore.
                }

                if (refs.Count > 2000)
                    break;
            }

            if (refs.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[TownLegacyRootCleaner] Found {refs.Count} serialized reference(s) into '{GetScenePath(root)}'.");
                sb.AppendLine("Delete is NOT recommended unless you understand these references:");

                int show = Mathf.Min(refs.Count, 30);
                for (int i = 0; i < show; i++)
                {
                    var r = refs[i];
                    sb.AppendLine($"- {FormatSource(r.Source)} -> {r.PropertyPath} => {FormatTarget(r.Target)}");
                }

                if (refs.Count > show)
                    sb.AppendLine($"(Only showing first {show} refs; see full list in Console by rerunning after narrowing selection.)");

                Debug.LogWarning(sb.ToString());

                int choice = EditorUtility.DisplayDialogComplex(
                    "Safely Delete Selected Root",
                    $"Found {refs.Count} serialized reference(s) pointing into the selected root.\n\n" +
                    "Recommended: do NOT delete until those references are fixed.\n\n" +
                    "What do you want to do?",
                    "Cancel",
                    "Delete Anyway",
                    "Select First Reference");

                if (choice == 2)
                {
                    Selection.activeObject = refs[0].Source;
                    EditorGUIUtility.PingObject(refs[0].Source);
                    return;
                }

                if (choice != 1)
                    return;
            }
            else
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Safely Delete Selected Root",
                    "No serialized references into the selected root were found in this scene.\n\nDelete it now?",
                    "Delete",
                    "Cancel");

                if (!ok)
                    return;
            }

            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[TownLegacyRootCleaner] Deleted '{root.name}' via Undo.");
        }

        private struct SerializedRef
        {
            public Component Source;
            public string PropertyPath;
            public UnityEngine.Object Target;
        }

        private static string FormatSource(Component c)
        {
            if (c == null) return "<null>";
            return $"{c.GetType().Name} on '{GetScenePath(c.gameObject)}'";
        }

        private static string FormatTarget(UnityEngine.Object o)
        {
            if (o == null) return "<null>";

            if (o is Component comp && comp != null)
                return $"{comp.GetType().Name} on '{GetScenePath(comp.gameObject)}'";

            if (o is GameObject go && go != null)
                return $"GameObject '{GetScenePath(go)}'";

            return $"{o.GetType().Name} '{o.name}'";
        }

        private static string GetScenePath(GameObject go)
        {
            if (go == null) return "<null>";
            var t = go.transform;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
```

---

## Assets/Editor/ValidateUiIconsEditor.cs

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Abyss.Equipment;
using Abyss.Inventory;
using Abyss.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ValidateUiIconsEditor
{
    [MenuItem("Tools/UI/Validate Item & UI Icons")]
    public static void ValidateItemAndUiIcons()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int checkedCount = 0;
        int iconMissing = 0;
        int itemIdMissing = 0;
        int displayNameMissing = 0;

        var failures = new List<string>(256);

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null)
                continue;

            checkedCount++;

            bool fail = false;
            string itemId = null;
            string displayName = null;
            Sprite icon = null;

            try { itemId = def.itemId; } catch { }
            try { displayName = def.displayName; } catch { }
            try { icon = def.icon; } catch { }

            if (icon == null)
            {
                iconMissing++;
                fail = true;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemIdMissing++;
                fail = true;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayNameMissing++;
                fail = true;
            }

            if (fail)
            {
                failures.Add($"FAIL: {path} | itemId='{itemId ?? ""}' | displayName='{displayName ?? ""}' | icon={(icon != null ? icon.name : "NULL")}");
            }
        }

        int failedAssets = failures.Count;
        var summary =
            $"[Validate Item & UI Icons] Checked={checkedCount} Failed={failedAssets} " +
            $"(IconNull={iconMissing}, ItemIdEmpty={itemIdMissing}, DisplayNameEmpty={displayNameMissing})";

        if (failedAssets == 0)
        {
            Debug.Log(summary);
            return;
        }

        Debug.LogWarning(summary + "\n" + string.Join("\n", failures));
    }

    [MenuItem("Tools/UI/Validate Inventory Row Prefab")]
    public static void ValidateInventoryRowTemplate()
    {
        // This project typically uses a scene RowTemplate (built/managed by BuildPlayerInventoryUIEditor),
        // not necessarily a standalone prefab. So validate both:
        // 1) Active scene PlayerInventoryUI instances
        // 2) Prefab assets containing PlayerInventoryUI

        var results = new List<string>(128);
        int pass = 0;
        int fail = 0;

        void ValidateRowTemplate(PlayerInventoryUI ui, string context)
        {
            if (ui == null)
            {
                fail++;
                results.Add($"FAIL: {context} | PlayerInventoryUI NULL");
                return;
            }

            PlayerInventoryRowUI rowTemplate = null;
            try
            {
                var so = new SerializedObject(ui);
                var p = so.FindProperty("rowTemplate");
                rowTemplate = p != null ? p.objectReferenceValue as PlayerInventoryRowUI : null;
            }
            catch { }

            if (rowTemplate == null)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate is NULL");
                return;
            }

            // Validate background field exists and is wired (required by row rendering).
            Image bg = null;
            try
            {
                var soRow = new SerializedObject(rowTemplate);
                var pBg = soRow.FindProperty("background");
                bg = pBg != null ? pBg.objectReferenceValue as Image : null;
            }
            catch { }

            if (bg == null)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | Background reference is NULL");
                return;
            }

            // Validate iconImage field points to a child with Image component.
            Image iconImage = null;
            try
            {
                var soRow = new SerializedObject(rowTemplate);
                var pIcon = soRow.FindProperty("iconImage");
                iconImage = pIcon != null ? pIcon.objectReferenceValue as Image : null;
            }
            catch { }

            if (iconImage == null)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | IconImage reference is NULL");
                return;
            }

            bool isChild = false;
            try
            {
                isChild = iconImage.transform != null && iconImage.transform.IsChildOf(rowTemplate.transform);
            }
            catch { }

            if (!isChild)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | IconImage '{iconImage.name}' is not a child of rowTemplate");
                return;
            }

            // Optional: ensure there is a child named "Icon" and it matches.
            try
            {
                var iconTf = rowTemplate.transform.Find("Icon");
                if (iconTf == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | child 'Icon' not found");
                    return;
                }

                var iconImgChild = iconTf.GetComponent<Image>();
                if (iconImgChild == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | child 'Icon' missing Image component");
                    return;
                }

                if (iconImgChild != iconImage)
                {
                    fail++;
                    results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | iconImage field points to '{iconImage.name}', but child 'Icon' is '{iconImgChild.name}'");
                    return;
                }
            }
            catch
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | exception while validating Icon child");
                return;
            }

            pass++;
            results.Add($"PASS: {context} | rowTemplate={rowTemplate.name} | IconImage={iconImage.name} | Background={bg.name}");
        }

        // 1) Scene instances
        try
        {
            var all = Resources.FindObjectsOfTypeAll<PlayerInventoryUI>();
            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            foreach (var ui in sceneUis)
                ValidateRowTemplate(ui, $"Scene:{ui.gameObject.scene.name}/{ui.gameObject.name}");
        }
        catch { }

        // 2) Prefab assets containing PlayerInventoryUI
        try
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var ui = prefab.GetComponentInChildren<PlayerInventoryUI>(true);
                if (ui == null)
                    continue;

                ValidateRowTemplate(ui, $"Prefab:{path}");
            }
        }
        catch { }

        if (pass > 0 && fail == 0)
        {
            Debug.Log($"[Validate Inventory Row Prefab] PASS ({pass} checked)\n" + string.Join("\n", results));
            return;
        }

        Debug.LogWarning($"[Validate Inventory Row Prefab] FAIL (Pass={pass} Fail={fail})\n" + string.Join("\n", results));
    }

    [MenuItem("Tools/UI/Validate Equipment UI Wiring")]
    public static void ValidateEquipmentUiWiring()
    {
        var results = new List<string>(128);
        int pass = 0;
        int fail = 0;

        static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "(null)";
            var parts = new List<string>(16);
            var cur = t;
            while (cur != null)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Image FindChildImageByNameCaseInsensitive(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;

            // Breadth-first search so we prefer shallow matches.
            var q = new Queue<Transform>();
            q.Enqueue(root);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == null) continue;

                if (string.Equals(cur.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var img = cur.GetComponent<Image>();
                        if (img != null)
                            return img;
                    }
                    catch { }
                }

                for (int i = 0; i < cur.childCount; i++)
                    q.Enqueue(cur.GetChild(i));
            }

            return null;
        }

        void ValidateOne(PlayerEquipmentUI ui, string context)
        {
            if (ui == null)
            {
                fail++;
                results.Add($"FAIL: {context} | PlayerEquipmentUI NULL");
                return;
            }

            SerializedObject so;
            try { so = new SerializedObject(ui); }
            catch
            {
                fail++;
                results.Add($"FAIL: {context} | exception creating SerializedObject");
                return;
            }

            var slotsProp = so.FindProperty("slots");
            if (slotsProp == null || !slotsProp.isArray)
            {
                fail++;
                results.Add($"FAIL: {context} | slots array missing");
                return;
            }

            if (slotsProp.arraySize <= 0)
            {
                fail++;
                results.Add($"FAIL: {context} | slots array is empty");
                return;
            }

            bool didWire = false;

            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                var el = slotsProp.GetArrayElementAtIndex(i);
                var slotInt = el.FindPropertyRelative("slot")?.intValue ?? -1;
                var button = el.FindPropertyRelative("button")?.objectReferenceValue as Button;
                var icon = el.FindPropertyRelative("iconImage")?.objectReferenceValue as Image;
                var strip = el.FindPropertyRelative("rarityStrip")?.objectReferenceValue as Image;

                if (button == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | slots[{i}] slot={(EquipmentSlot)slotInt} | button NULL");
                    continue;
                }

                if (icon == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | {button.name} slot={(EquipmentSlot)slotInt} | iconImage NULL");
                    continue;
                }

                if (strip == null)
                {
                    // TASK A: attempt to auto-wire by finding a child named "RarityStrip" (case-insensitive).
                    try
                    {
                        var found = FindChildImageByNameCaseInsensitive(button.transform, "RarityStrip");
                        if (found != null)
                        {
                            var p = el.FindPropertyRelative("rarityStrip");
                            if (p != null)
                            {
                                p.objectReferenceValue = found;
                                strip = found;
                                didWire = true;
                            }
                        }
                        else
                        {
                            var rootPath = GetHierarchyPath(button.transform);
                            results.Add($"WARN: {context} | {button.name} slot={(EquipmentSlot)slotInt} | rarityStrip NULL and child 'RarityStrip' not found under {rootPath}");
                        }
                    }
                    catch { }

                    if (strip == null)
                    {
                        fail++;
                        results.Add($"FAIL: {context} | {button.name} slot={(EquipmentSlot)slotInt} | rarityStrip NULL");
                        continue;
                    }
                }

                bool iconChildOk = false;
                bool stripChildOk = false;
                try
                {
                    var iconTf = button.transform.Find("Icon");
                    iconChildOk = iconTf != null && iconTf.GetComponent<Image>() == icon;

                    var stripTf = button.transform.Find("RarityStrip");
                    stripChildOk = stripTf != null && stripTf.GetComponent<Image>() == strip;
                }
                catch { }

                if (!iconChildOk)
                {
                    fail++;
                    results.Add($"FAIL: {context} | {button.name} | iconImage is not child 'Icon'");
                    continue;
                }

                if (!stripChildOk)
                {
                    fail++;
                    results.Add($"FAIL: {context} | {button.name} | rarityStrip is not child 'RarityStrip'");
                    continue;
                }
            }

            if (didWire)
            {
                try
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(ui);
                    if (ui.gameObject != null && ui.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
                }
                catch { }
            }

            pass++;
            results.Add($"PASS: {context} | slots={slotsProp.arraySize}");
        }

        // 1) Scene instances
        try
        {
            var all = Resources.FindObjectsOfTypeAll<PlayerEquipmentUI>();
            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            foreach (var ui in sceneUis)
                ValidateOne(ui, $"Scene:{ui.gameObject.scene.name}/{ui.gameObject.name}");
        }
        catch { }

        // 2) Prefab assets containing PlayerEquipmentUI
        try
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var ui = prefab.GetComponentInChildren<PlayerEquipmentUI>(true);
                if (ui == null)
                    continue;

                ValidateOne(ui, $"Prefab:{path}");
            }
        }
        catch { }

        if (pass > 0 && fail == 0)
        {
            Debug.Log($"[Validate Equipment UI Wiring] PASS ({pass} checked)\n" + string.Join("\n", results));
            return;
        }

        Debug.LogWarning($"[Validate Equipment UI Wiring] FAIL (Pass={pass} Fail={fail})\n" + string.Join("\n", results));
    }
}
#endif
```

---

## Assets/Game/Dev/DevCheats.cs

```csharp
using System.Collections.Generic;
using Abyss.Loot;
using Game.Input;
using UnityEngine;

namespace Abyss.Dev
{
    // Editor/Dev-only runtime cheats for fast QA.
    public sealed class DevCheats : MonoBehaviour
    {
        public static DevCheats Instance { get; private set; }

        public static bool GodModeEnabled => Instance != null && Instance.godMode;

        [Header("Toggles")]
        [SerializeField] private bool godMode;
        [SerializeField] private bool showOverlay = true;

        [Header("Hotkeys")]
        public KeyCode toggleGodModeKey = KeyCode.F1;
        public KeyCode spawnEnemyKey = KeyCode.F2;
        public KeyCode killSpawnedKey = KeyCode.F3;

        [Header("Spawning")]
        public List<GameObject> enemyPrefabs = new();
        public EnemyTier spawnTier = EnemyTier.Normal;
        public ZoneLootTable overrideZoneLootTable;
        public float spawnDistance = 4f;
        public int spawnCount = 1;

        private readonly List<GameObject> _spawned = new();
        private int _lastSpawnedCount;

        private PlayerInputAuthority _input;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<PlayerInputAuthority>();
#else
            _input = FindObjectOfType<PlayerInputAuthority>();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (Input.GetKeyDown(toggleGodModeKey))
            {
                godMode = !godMode;
                Debug.Log($"[DevCheats] GodMode={(godMode ? "ON" : "OFF")}");
            }

            if (Input.GetKeyDown(spawnEnemyKey))
                SpawnEnemies();

            if (Input.GetKeyDown(killSpawnedKey))
                KillSpawned();
#endif
        }

        private void SpawnEnemies()
        {
            if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            {
                Debug.LogWarning("[DevCheats] No enemyPrefabs configured.");
                return;
            }

            var prefab = enemyPrefabs[0];
            if (prefab == null)
            {
                Debug.LogWarning("[DevCheats] enemyPrefabs[0] is null.");
                return;
            }

            Transform anchor = FindAnchor();
            Vector3 basePos = anchor != null ? anchor.position : Vector3.zero;
            Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;

            int count = Mathf.Clamp(spawnCount, 1, 50);
            for (int i = 0; i < count; i++)
            {
                Vector3 jitter = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                Vector3 pos = basePos + forward.normalized * Mathf.Max(0.5f, spawnDistance) + jitter;
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = prefab.name;

                ApplyLootOverrides(go);
                _spawned.Add(go);
            }

            _lastSpawnedCount = count;

            Debug.Log($"[DevCheats] Spawned {count}x '{prefab.name}'.");
        }

        private void ApplyLootOverrides(GameObject enemy)
        {
            if (enemy == null) return;

            var drop = enemy.GetComponentInChildren<DropOnDeath>();
            if (drop != null)
            {
                drop.tier = spawnTier;
                if (overrideZoneLootTable != null)
                    drop.zoneLootTable = overrideZoneLootTable;
            }
        }

        private void KillSpawned()
        {
            int killed = 0;
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var go = _spawned[i];
                if (go == null)
                {
                    _spawned.RemoveAt(i);
                    continue;
                }

                var enemyHealth = go.GetComponentInChildren<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(999999);
                    killed++;
                }
            }

            Debug.Log($"[DevCheats] KillSpawned: {killed} enemy(ies) signaled lethal damage.");
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (!showOverlay) return;

            // Avoid drawing debug overlays through/over gameplay UI.
            if (_input == null)
            {
    #if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<PlayerInputAuthority>();
    #else
            _input = FindObjectOfType<PlayerInputAuthority>();
    #endif
            }
            if (_input != null && _input.IsUiInputLocked)
            return;

            const float pad = 10f;
            var rect = new Rect(pad, pad, 520f, 80f);

            string text =
                $"DevCheats  |  GodMode: {(godMode ? "ON" : "OFF")}\n" +
                $"LastSpawn: {_lastSpawnedCount}  ActiveSpawned: {_spawned.Count}\n" +
                $"Keys: {toggleGodModeKey}=GodMode  {spawnEnemyKey}=Spawn  {killSpawnedKey}=Kill";

            GUI.Label(rect, text);
#endif
        }

        private static Transform FindAnchor()
        {
            // Prefer PlayerHealth (player root), then camera.
            var playerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            if (playerHealth != null) return playerHealth.transform;

            if (Camera.main != null) return Camera.main.transform;

            return null;
        }
    }
}
```

---

## Assets/Game/Input/PlayerInputAuthority.cs

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [DisallowMultipleComponent]
    public class PlayerInputAuthority : MonoBehaviour
    {
        public event Action<Vector2> CameraPan;
        public event Action<Vector2> PointerPosition;
        public event Action Click;
        public event Action AttackDebug;

        [Header("Map / Action Names")]
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string cameraPanAction = "CameraPan";
        [SerializeField] private string pointerPosAction = "PointerPosition";
        [SerializeField] private string clickAction = "Click";
        [SerializeField] private string attackDebugAction = "AttackDebug";

        [Header("Blocking")]
        [Tooltip("If true, gameplay clicks/attacks are ignored (useful while UI is open).")]
        [SerializeField] private bool gameplayInputBlocked;

        private PlayerInput _playerInput;
        private InputActionMap _map;
        private InputAction _pan, _pointer, _click, _attackDbg;

        private bool _bound;
        private bool _cachedPointerOverUI;
        private int _cachedPointerFrame = -1;
        private bool _blockedClickLogged = false;

        // UI lock state driven by UI open/close events
        private bool _uiInputLocked = false;
        private bool _ignoreNextWorldClick = false;

        public bool IsUiInputLocked => _uiInputLocked;

        public void SetGameplayInputBlocked(bool blocked) => gameplayInputBlocked = blocked;

        // Support for UI blocking from MerchantShopUI
        public void SetUIBlocked(bool blocked)
        {
            enabled = !blocked;
        }

        // Explicit API for UI to lock/unlock gameplay input. When unlocking, the next world click will be ignored.
        public void SetUiInputLocked(bool locked)
        {
            _uiInputLocked = locked;
            if (!locked)
            {
                _ignoreNextWorldClick = true;
                _blockedClickLogged = false;
            }
        }

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        private void Update()
        {
            // Retry binding until PlayerInput.actions is assigned
            if (!_bound)
                TryBind();

            // Cache pointer-over-UI once per frame to avoid calling IsPointerOverGameObject inside input callbacks
            if (Time.frameCount != _cachedPointerFrame)
            {
                _cachedPointerFrame = Time.frameCount;
                _cachedPointerOverUI = (EventSystem.current != null) && EventSystem.current.IsPointerOverGameObject();
            }
        }

        private void OnEnable()
        {
            Abyss.Shop.MerchantShopUI.OnOpenChanged += HandleMerchantUiOpenChanged;
        }

        private void HandleMerchantUiOpenChanged(bool open)
        {
            _uiInputLocked = open;
            if (!open)
            {
                // next world click (often the click that closed UI) should be ignored
                _ignoreNextWorldClick = true;
                _blockedClickLogged = false;
            }
        }

        private void TryBind()
        {
            if (_playerInput == null)
                _playerInput = GetComponent<PlayerInput>();

            if (_playerInput == null || _playerInput.actions == null)
                return; // wait until bootstrap assigns actions

            _map = _playerInput.actions.FindActionMap(actionMapName, true);
            if (_map == null)
            {
                Debug.LogError($"[InputAuthority] Action map '{actionMapName}' not found.", this);
                return;
            }

            _playerInput.SwitchCurrentActionMap(actionMapName);
            _map.Enable();

            _pan = _map.FindAction(cameraPanAction, true);
            _pointer = _map.FindAction(pointerPosAction, true);
            _click = _map.FindAction(clickAction, true);
            _attackDbg = _map.FindAction(attackDebugAction, false);

            _pan.performed += OnPan;
            _pan.canceled += OnPan;
            _pointer.performed += OnPointer;
            _pointer.canceled += OnPointer;
            _click.performed += OnClick;

            if (_attackDbg != null)
                _attackDbg.performed += OnAttackDebug;

            _bound = true;
            Debug.Log($"[InputAuthority] Active. actions={_playerInput.actions.name} map={_map.name}", this);
        }

        private void OnDisable()
        {
            Abyss.Shop.MerchantShopUI.OnOpenChanged -= HandleMerchantUiOpenChanged;
            if (!_bound) return;

            if (_pan != null) { _pan.performed -= OnPan; _pan.canceled -= OnPan; }
            if (_pointer != null) { _pointer.performed -= OnPointer; _pointer.canceled -= OnPointer; }
            if (_click != null) { _click.performed -= OnClick; }
            if (_attackDbg != null) { _attackDbg.performed -= OnAttackDebug; }

            _map?.Disable();
            _bound = false;
        }

        private void OnPan(InputAction.CallbackContext ctx) => CameraPan?.Invoke(ctx.ReadValue<Vector2>());
        private void OnPointer(InputAction.CallbackContext ctx) => PointerPosition?.Invoke(ctx.ReadValue<Vector2>());

        private void OnClick(InputAction.CallbackContext ctx)
        {
            if (gameplayInputBlocked)
                return;

            // If UI input lock is active, block world clicks unless the click is over UI.
            if (_uiInputLocked)
            {
                if (!_cachedPointerOverUI)
                {
                    if (!_blockedClickLogged)
                    {
                        Debug.Log("[InputAuthority] Click blocked because UI is open", this);
                        _blockedClickLogged = true;
                    }
                    return;
                }
                // if pointer is over UI, allow UI to handle the click and do not process world click
                return;
            }

            // Ignore one world click immediately after UI closes (prevents the "click-out" problem)
            if (_ignoreNextWorldClick)
            {
                _ignoreNextWorldClick = false;
                return;
            }

            // If pointer is over UI, don't process as world click
            if (_cachedPointerOverUI)
                return;

            Click?.Invoke();
        }

        private void OnAttackDebug(InputAction.CallbackContext ctx)
        {
            if (gameplayInputBlocked)
                return;

            AttackDebug?.Invoke();
        }
    }

}
```

---

## Assets/Game/Player/PlayerCombatStats.cs

```csharp
using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using UnityEngine;

using AbyssItemType = Abyss.Items.ItemType;

[DisallowMultipleComponent]
public sealed class PlayerCombatStats : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private int baseDamage = 3;

    public int BaseDamage => baseDamage;

    public int EquipmentDamageBonus { get; private set; }

    public int DamageFinal => Mathf.Max(1, BaseDamage + EquipmentDamageBonus);

    private PlayerEquipment _equipment;

    private static Dictionary<string, ItemDefinition> s_DefById;

    private void OnEnable()
    {
        EnsureEquipment();
        Recompute();
    }

    private void OnDisable()
    {
        if (_equipment != null)
            _equipment.Changed -= OnEquipmentChanged;
    }

    private void EnsureEquipment()
    {
        if (_equipment != null)
            return;

        try
        {
            _equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
        }
        catch
        {
            _equipment = null;
        }

        if (_equipment != null)
        {
            _equipment.Changed -= OnEquipmentChanged;
            _equipment.Changed += OnEquipmentChanged;
        }
    }

    private void OnEquipmentChanged()
    {
        Recompute();
    }

    private void Recompute()
    {
        EnsureEquipment();

        int bonus = 0;

        if (_equipment != null)
        {
            // Simplest consistent rule:
            // - Sum DamageBonus across both hands.
            // - If a two-handed item is represented by the same itemId in both hands, count it once.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AccumulateSlot(EquipmentSlot.RightHand, seen, ref bonus);
            AccumulateSlot(EquipmentSlot.LeftHand, seen, ref bonus);
        }

        EquipmentDamageBonus = bonus;
        Debug.Log($"[STATS] Base={BaseDamage} EquipBonus={EquipmentDamageBonus} Final={DamageFinal}", this);
    }

    private void AccumulateSlot(EquipmentSlot slot, HashSet<string> seen, ref int bonus)
    {
        if (_equipment == null)
            return;

        string itemId = null;
        try { itemId = _equipment.Get(slot); } catch { itemId = null; }

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (seen != null && !seen.Add(itemId))
            return;

        var def = ResolveItemDefinition(itemId);
        if (def == null)
            return;

        try
        {
            if (def.itemType != AbyssItemType.Weapon)
                return;
        }
        catch { }

        try
        {
            bonus += Mathf.Max(0, def.DamageBonus);
        }
        catch { }
    }

    private static ItemDefinition ResolveItemDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        s_DefById ??= BuildIndex();
        if (s_DefById != null && s_DefById.TryGetValue(itemId, out var def) && def != null)
            return def;

        // Best-effort: rebuild once (covers domain reload / asset load order).
        s_DefById = BuildIndex();
        if (s_DefById != null && s_DefById.TryGetValue(itemId, out var refreshed))
            return refreshed;

        return null;
    }

    private static Dictionary<string, ItemDefinition> BuildIndex()
    {
        var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            if (defs == null)
                return map;

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null)
                    continue;

                string id = null;
                try { id = def.itemId; } catch { id = null; }

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!map.ContainsKey(id))
                    map[id] = def;
            }
        }
        catch { }

        return map;
    }
}
```

---

## Assets/Game/Player/SimplePlayerCombat.cs

```csharp
using UnityEngine;
using Abyss.Dev;

public class SimplePlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldownSeconds = 0.6f;
    [SerializeField] private float range = 1.75f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Target (optional)")]
    [SerializeField] private EnemyHealth selectedTarget;

    private float _nextAttackTime;

    private PlayerCombatStats _stats;
    private bool _warnedMissingStats;

    public float Range => range;

    public EnemyHealth SelectedTarget
    {
        get => selectedTarget;
        set => selectedTarget = value;
    }

    public void SetSelectedTarget(EnemyHealth target)
    {
        SelectedTarget = target;
    }

    public void TryAttack()
    {
        if (Time.time < _nextAttackTime)
            return;

        if (SelectedTarget != null)
        {
            var attackedTarget = SelectedTarget;
            if (!TryAttackSelectedTarget())
                return;

            _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldownSeconds);
            if (attackedTarget != null)
                Debug.Log($"[Combat] You attacked {attackedTarget.name}", this);
            return;
        }

        var hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, range), hitMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return;

        EnemyHealth best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            var eh = c.GetComponentInParent<EnemyHealth>();
            if (eh == null) continue;

            float d = (eh.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = eh;
            }
        }

        if (best == null)
            return;

        _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldownSeconds);
        var hitPos = best.transform.position + Vector3.up * 1.2f;
        int dealt = Mathf.Max(1, GetDamageForAttack());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevCheats.GodModeEnabled)
            dealt = 999999;
#endif
        best.TakeDamage(dealt, hitPos);
    }

    private bool TryAttackSelectedTarget()
    {
        if (selectedTarget == null)
            return false;

        if (selectedTarget.IsDead)
            return false;

        // Match CombatLoopController: XZ plane only (ignore Y).
        Vector3 myPos = transform.position;
        Vector3 targetPos = selectedTarget.transform.position;
        float dx = targetPos.x - myPos.x;
        float dz = targetPos.z - myPos.z;
        float distSq = (dx * dx) + (dz * dz);
        float rangeSq = range * range;
        if (distSq > rangeSq)
        {
            if (debugLogs)
                Debug.Log($"[Combat] Attack rejected: out of range. xzDist={Mathf.Sqrt(distSq):0.00} range={range:0.00}", this);
            return false;
        }

        var hitPos = selectedTarget.transform.position + Vector3.up * 1.2f;
        int dealt = Mathf.Max(1, GetDamageForAttack());
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevCheats.GodModeEnabled)
            dealt = 999999;
    #endif
        selectedTarget.TakeDamage(dealt, hitPos);
        return true;
    }

    private int GetDamageForAttack()
    {
        // MVP: if PlayerCombatStats exists, use it; otherwise preserve legacy behavior.
        if (_stats == null)
        {
            try
            {
                _stats = GetComponent<PlayerCombatStats>();
                if (_stats == null) _stats = GetComponentInParent<PlayerCombatStats>();
            }
            catch { _stats = null; }
        }

        if (_stats != null)
            return _stats.DamageFinal;

        if (!_warnedMissingStats)
        {
            _warnedMissingStats = true;
            Debug.LogWarning($"[STATS] PlayerCombatStats missing; using fallback damage={damage}", this);
        }

        return damage;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, range));
    }
#endif
}
```
