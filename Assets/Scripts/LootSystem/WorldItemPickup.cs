using Abyssbound.Loot;
using Game.Systems;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItemPickup : MonoBehaviour
{
    [Header("Instance")]
    public ItemInstance instance;

    [Header("Pickup")]
    [SerializeField] private bool disableInsteadOfDestroy = true;
    [SerializeField, Min(1)] private int inventoryCount = 1;

    private ItemTooltipUI _tooltip;

    public void Initialize(ItemInstance inst)
    {
        instance = inst;

        // Name for debugging.
        try
        {
            if (inst != null)
                gameObject.name = $"Pickup_{inst.baseItemId}_{inst.rarityId}";
        }
        catch { }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        TryPickup();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        TryPickup();
    }

    private void OnMouseDown()
    {
        TryPickup();
    }

    private void OnMouseEnter()
    {
        var canvas = FindAnyCanvas();
        if (canvas == null) return;

        _tooltip = ItemTooltipUI.GetOrCreateUnder(canvas.transform);
        if (_tooltip == null) return;

        var reg = LootRegistryRuntime.GetOrCreate();
        _tooltip.ShowLootInstance(this, instance, reg);
    }

    private void OnMouseExit()
    {
        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    private void TryPickup()
    {
        if (instance == null) return;

        var inv = PlayerInventoryResolver.GetOrFind();
        if (inv == null) return;

        var reg = LootRegistryRuntime.GetOrCreate();
        var rolledId = reg.RegisterRolledInstance(instance);
        if (string.IsNullOrWhiteSpace(rolledId)) return;

        inv.Add(rolledId, Mathf.Max(1, inventoryCount));

        if (_tooltip != null)
            _tooltip.Hide(this);

        if (disableInsteadOfDestroy)
            gameObject.SetActive(false);
        else
            Destroy(gameObject);
    }

    private static bool IsPlayer(Component c)
    {
        if (c == null) return false;

        try { if (c.CompareTag("Player")) return true; } catch { }
        try { return c.gameObject != null && c.gameObject.name == "Player_Hero"; } catch { return false; }
    }

    private static Canvas FindAnyCanvas()
    {
        try
        {
#if UNITY_2022_2_OR_NEWER
            var c = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
            if (c != null) return c;
#else
            var c = Object.FindObjectOfType<Canvas>();
            if (c != null) return c;
#endif
        }
        catch { }

        return null;
    }
}
