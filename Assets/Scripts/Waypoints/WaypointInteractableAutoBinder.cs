using UnityEngine;
using UnityEngine.SceneManagement;
using Abyssbound.WorldInteraction;

namespace Abyss.Waypoints
{
    /// <summary>
    /// Ensures waypoint scene objects participate in the WorldInteraction hover+click pipeline.
    /// Adds a WaypointWorldInteractable to any WaypointComponent root that doesn't already have a WorldInteractable.
    /// Ensures the root is on the "WorldInteractable" layer and has a trigger collider so raycast hits can resolve.
    /// </summary>
    public sealed class WaypointInteractableAutoBinder : MonoBehaviour
    {
        private static WaypointInteractableAutoBinder _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<WaypointInteractableAutoBinder>();
            if (existing != null)
            {
                _instance = existing;
                _instance.TryBindAll();
                return;
            }

            var go = new GameObject("WaypointInteractableAutoBinder");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            _instance = go.AddComponent<WaypointInteractableAutoBinder>();
            _instance.TryBindAll();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindAll();
        }

        private void TryBindAll()
        {
            try
            {
                var wps = FindObjectsByType<WaypointComponent>(FindObjectsSortMode.None);
                if (wps == null || wps.Length == 0)
                    return;

                int wiLayer = LayerMask.NameToLayer("WorldInteractable");

                for (int i = 0; i < wps.Length; i++)
                {
                    var wp = wps[i];
                    if (wp == null) continue;

                    // Ensure layer so the WorldInteractionRaycaster mask can hit it.
                    if (wiLayer >= 0)
                    {
                        try { wp.gameObject.layer = wiLayer; } catch { }
                    }

                    // Ensure a trigger collider on the same object the raycast will hit.
                    // (Do not add box colliders.)
                    try
                    {
                        var col = wp.GetComponent<Collider>();
                        if (col == null)
                        {
                            var sphere = wp.gameObject.AddComponent<SphereCollider>();
                            sphere.isTrigger = true;
                            sphere.radius = 1.25f;
                            sphere.center = new Vector3(0f, 0.6f, 0f);
                        }
                        else
                        {
                            col.isTrigger = true;
                        }
                    }
                    catch { }

                    // If someone already attached a world interactable, don't duplicate.
                    if (wp.GetComponent<WorldInteractable>() != null)
                        continue;

                    wp.gameObject.AddComponent<WaypointWorldInteractable>();
                }
            }
            catch
            {
                // Swallow errors during scene churn
            }
        }
    }
}
