using UnityEngine;

public class ConsumablesMerchant : MonoBehaviour
{
    [Header("T1 Potions, Cooked Fish, T1 Consumables")]
    public string[] items = new string[]
    {
        "T1 Health Potion", "T1 Mana Potion", "Cooked Shrimp", "Cooked Trout", "Bread", "Antidote"
    };

    public void Interact()
    {
        Debug.Log("[Merchant] Consumables: Open shop UI (placeholder)", this);
    }
}
