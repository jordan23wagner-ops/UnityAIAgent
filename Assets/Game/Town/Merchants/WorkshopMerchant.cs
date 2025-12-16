using UnityEngine;

public class WorkshopMerchant : MonoBehaviour
{
    [Header("Workshop Services")]
    public string[] services = new string[]
    {
        "Forge", "Smithing Stand", "Workshop", "Bonfire (Cook Raw Fish)"
    };

    public void Interact()
    {
        Debug.Log("[Merchant] Workshop: Open workshop UI (placeholder)", this);
    }
}
