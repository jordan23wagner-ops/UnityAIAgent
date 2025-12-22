using System;
using Abyss.Items;
using Game.Systems;
using UnityEngine;

public sealed class WorldLootPickup : MonoBehaviour
{
    [Header("Loot")]
    [SerializeField] private ItemDefinition itemDefinition;
    [SerializeField, Min(1)] private int count = 1;

    [Header("Rolled Instance (optional)")]
    [SerializeField] private Abyss.Items.ItemRarity rarityOverride;
    [SerializeField] private bool useRarityOverride;
    [SerializeField] private Abyss.Loot.LootAffixRoll[] rolledAffixes;

    [Header("Pickup")]
    [Tooltip("If true, object is SetActive(false) on pickup. If false, it is Destroyed.")]
    [SerializeField] private bool disableInsteadOfDestroy = true;

    [Header("Tooltip (optional)")]
    [SerializeField] private bool showTooltipOnHover = true;

    private ItemTooltipUI _tooltip;

    public void Initialize(ItemDefinition def, int qty, Abyss.Loot.LootItemInstance instance = null)
    {
        itemDefinition = def;
        count = Mathf.Max(1, qty);

        if (instance != null)
        {
            try
            {
                rarityOverride = instance.rarity;
                useRarityOverride = true;

                if (instance.affixes != null && instance.affixes.Count > 0)
                {
                    rolledAffixes = new Abyss.Loot.LootAffixRoll[instance.affixes.Count];
                    for (int i = 0; i < instance.affixes.Count; i++)
                        rolledAffixes[i] = instance.affixes[i];
                }
                else
                {
                    rolledAffixes = null;
                }
            }
            catch
            {
                useRarityOverride = false;
                rolledAffixes = null;
            }
        }
        else
        {
            useRarityOverride = false;
            rolledAffixes = null;
        }

        try
        {
            if (itemDefinition != null)
                gameObject.name = $"Pickup_{(string.IsNullOrWhiteSpace(itemDefinition.itemId) ? itemDefinition.name : itemDefinition.itemId)}";
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

    private static bool IsPlayer(Component c)
    {
        if (c == null) return false;

        try
        {
            if (c.CompareTag("Player")) return true;
        }
        catch { }

        try
        {
            var go = c.gameObject;
            return go != null && string.Equals(go.name, "Player_Hero", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void TryPickup()
    {
        if (itemDefinition == null) return;

        var inv = PlayerInventoryResolver.GetOrFind();
        if (inv == null) return;

        string itemId = null;
        try { itemId = itemDefinition.itemId; } catch { itemId = null; }
        if (string.IsNullOrWhiteSpace(itemId)) return;

        inv.Add(itemId, Mathf.Max(1, count));

        if (disableInsteadOfDestroy)
            gameObject.SetActive(false);
        else
            Destroy(gameObject);

        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    private void OnMouseEnter()
    {
        if (!showTooltipOnHover) return;

        var canvas = FindAnyCanvas();
        if (canvas == null) return;

        _tooltip = ItemTooltipUI.GetOrCreateUnder(canvas.transform);
        if (_tooltip == null) return;

        string rarityLine = null;
        try
        {
            var r = useRarityOverride ? rarityOverride : (itemDefinition != null ? itemDefinition.rarity : default);
            rarityLine = r.ToString();
        }
        catch { rarityLine = null; }

        string affixLines = BuildAffixLines();

        _tooltip.ShowExtended(this, itemDefinition, itemDefinition != null ? itemDefinition.itemId : null, count, EquipmentSlot.None, rarityLine, affixLines);
    }

    private void OnMouseExit()
    {
        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    private string BuildAffixLines()
    {
        if (rolledAffixes == null || rolledAffixes.Length == 0) return null;

        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            for (int i = 0; i < rolledAffixes.Length; i++)
            {
                var line = rolledAffixes[i].ToStatLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.Append(line).Append('\n');
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length -= 1;
            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static Canvas FindAnyCanvas()
    {
        try
        {
#if UNITY_2022_2_OR_NEWER
            var c = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
            if (c != null) return c;
#else
            var c = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (c != null) return c;
#endif
        }
        catch { }

        return null;
    }
}
