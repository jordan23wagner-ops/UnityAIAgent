using UnityEngine;

public class WeaponsGearMerchant : MonoBehaviour
{
    [Header("Weapons & Gear Inventory")]
    public string[] items = new string[]
    {
        "Bronze Sword", "Wooden Shield", "Leather Armor", "Iron Dagger", "Shortbow"
    };

    public void Interact()
    {
        Debug.Log("[Merchant] Weapons & Gear: Open shop UI (placeholder)", this);
    }
}
