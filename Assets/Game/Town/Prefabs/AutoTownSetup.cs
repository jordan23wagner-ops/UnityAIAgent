using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AutoTownSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Create Town Merchants & Workshop Objects")]
    public void CreateTownObjects()
    {
        CreateMerchant("WeaponsMerchantNPC", typeof(WeaponsGearMerchant), new Vector3(-10,0,10));
        CreateMerchant("ConsumablesMerchantNPC", typeof(ConsumablesMerchant), new Vector3(0,0,10));
        CreateMerchant("SkillingSuppliesMerchantNPC", typeof(SkillingSuppliesMerchant), new Vector3(10,0,10));
        CreateMerchant("WorkshopMerchantNPC", typeof(WorkshopMerchant), new Vector3(20,0,10));

        CreateInteractable("Forge", typeof(ForgeInteractable), new Vector3(20,0,12));
        CreateInteractable("SmithingStand", typeof(SmithingStandInteractable), new Vector3(22,0,10));
        CreateInteractable("Workshop", typeof(WorkshopInteractable), new Vector3(20,0,8));
        CreateInteractable("Bonfire", typeof(BonfireInteractable), new Vector3(18,0,10));
    }

    private void CreateMerchant(string name, System.Type script, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().isTrigger = true;
        go.AddComponent(script);
    }

    private void CreateInteractable(string name, System.Type script, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().isTrigger = true;
        go.AddComponent(script);
    }
#endif
}
