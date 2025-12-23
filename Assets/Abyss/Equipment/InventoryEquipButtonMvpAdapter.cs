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
