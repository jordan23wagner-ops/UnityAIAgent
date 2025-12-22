using Abyss.Items;
using UnityEngine;
using UnityEngine.EventSystems;

// Hover trigger that shows/hides the shared ItemTooltipUI.
[DisallowMultipleComponent]
public sealed class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Bound Data (runtime)")]
    [SerializeField] private ItemDefinition item;
    [SerializeField] private string fallbackItemId;
    [SerializeField] private int count;
    [SerializeField] private EquipmentSlot slotContext = EquipmentSlot.None;

    private ItemTooltipUI _tooltip;
    private Canvas _cachedCanvas;
    private bool _hasBound;

    public void BindInventoryItem(ItemDefinition def, string fallbackId, int itemCount)
    {
        item = def;
        fallbackItemId = fallbackId;
        count = itemCount;
        slotContext = EquipmentSlot.None;

        _hasBound = (def != null) || !string.IsNullOrWhiteSpace(fallbackId);
    }

    public void BindEquipmentSlot(EquipmentSlot slot, ItemDefinition equippedDef, string equippedItemId)
    {
        slotContext = slot;
        item = equippedDef;
        fallbackItemId = equippedItemId;
        count = 0;

        // Always allow showing for equipment slots, even empty.
        _hasBound = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_hasBound)
            return;

        if (_tooltip == null)
        {
            // No per-frame searching; resolve on demand and cache.
            _tooltip = GetComponentInParent<ItemTooltipUI>();

            if (_tooltip == null)
            {
                if (_cachedCanvas == null)
                    _cachedCanvas = GetComponentInParent<Canvas>();

                if (_cachedCanvas != null)
                    _tooltip = _cachedCanvas.GetComponentInChildren<ItemTooltipUI>(true);
            }
        }

        if (_tooltip == null)
            return;

        // If this inventory/equipment entry is a rolled instance (stored as an itemId string),
        // prefer showing the instance tooltip.
        try
        {
            var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
            if (!string.IsNullOrWhiteSpace(fallbackItemId) && reg.TryGetRolledInstance(fallbackItemId, out var inst) && inst != null)
            {
                _tooltip.ShowLootInstance(this, inst, reg);
                return;
            }
        }
        catch { }

        _tooltip.Show(this, item, fallbackItemId, count, slotContext);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip == null)
            return;

        _tooltip.Hide(this);
    }
}
