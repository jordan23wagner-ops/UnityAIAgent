using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Game.Town;

public class AutoTownSetup : MonoBehaviour
{
    public bool enableAutoSpawn = false;

#if UNITY_EDITOR
    [ContextMenu("Create Town Merchants & Workshop Objects")]
    public void CreateTownObjects()
    {
        var registry = TownRegistry.Instance;
        registry.EnsureSpawnRoot();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            registry.RebuildIndexFromScene();
#endif


        CreateMerchant("merchant_weaponsgear", typeof(WeaponsGearMerchant), new Vector3(-10,0,10));
        CreateMerchant("merchant_consumables", typeof(ConsumablesMerchant), new Vector3(0,0,10));
        CreateMerchant("merchant_skilling", typeof(SkillingSuppliesMerchant), new Vector3(10,0,10));
        CreateMerchant("merchant_workshop", typeof(WorkshopMerchant), new Vector3(20,0,10));

        CreateInteractable("interactable_forge", typeof(Abyssbound.WorldInteraction.ForgeInteractable), new Vector3(20,0,12));
        CreateInteractable("interactable_smithingstand", typeof(SmithingStandInteractable), new Vector3(22,0,10));
        CreateInteractable("interactable_workshop", typeof(WorkshopInteractable), new Vector3(20,0,8));
        CreateInteractable("interactable_bonfire", typeof(Abyssbound.WorldInteraction.BonfireInteractable), new Vector3(18,0,10));
    }

    [ContextMenu("Nuke Town Spawns")]
    public void NukeTownSpawns()
    {
        TownRegistry.Instance.DestroyAllRegistered();
        Debug.Log("[AutoTownSetup] All registered town spawns destroyed via TownRegistry.", this);
    }

    private void CreateMerchant(string key, System.Type script, Vector3 pos)
    {
        if (TownRegistry.Instance.TryGet(key, out var existing))
        {
            Debug.Log($"[AutoTownSetup] Merchant '{key}' already exists, skipping spawn.", this);
            return;
        }
        var go = new GameObject(key);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().isTrigger = true;
        go.AddComponent(script);
        TownRegistry.Instance.RegisterOrKeep(key, go);
    }

    private void CreateInteractable(string key, System.Type script, Vector3 pos)
    {
        if (TownRegistry.Instance.TryGet(key, out var existing))
        {
            Debug.Log($"[AutoTownSetup] Interactable '{key}' already exists, skipping spawn.", this);
            return;
        }
        var go = new GameObject(key);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().isTrigger = true;
        go.AddComponent(script);
        TownRegistry.Instance.RegisterOrKeep(key, go);
    }
#endif
}
