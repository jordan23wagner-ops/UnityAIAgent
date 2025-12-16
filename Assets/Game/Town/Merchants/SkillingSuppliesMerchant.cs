using UnityEngine;

public class SkillingSuppliesMerchant : MonoBehaviour
{
    [Header("Skilling Supplies")]
    public string[] items = new string[]
    {
        "Pickaxe", "Fishing Rod", "Bait", "Woodcutting Axe", "Vials", "T1 Alchemy Mats", "Empty Flask"
    };

    public void Interact()
    {
        Debug.Log("[Merchant] Skilling Supplies: Open shop UI (placeholder)", this);
    }
}
