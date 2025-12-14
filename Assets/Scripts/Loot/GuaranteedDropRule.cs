using System;

[Serializable]
public class GuaranteedDropRule
{
    public bool enabled;
    public ItemType category = ItemType.Equipment;
    public ItemRarity minRarity = ItemRarity.Rare;
    public int rolls = 1;
}
