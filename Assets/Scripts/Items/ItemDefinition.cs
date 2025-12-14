using UnityEngine;

[CreateAssetMenu(menuName="Abyssbound/Loot/Item Definition", fileName="NewItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;

    [Header("Classification")]
    public ItemType itemType = ItemType.None;
    public ItemRarity rarity = ItemRarity.Common;

    [Header("Visuals (optional)")]
    public Sprite icon;
}
