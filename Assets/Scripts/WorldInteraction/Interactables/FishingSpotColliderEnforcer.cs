using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    [DisallowMultipleComponent]
    public sealed class FishingSpotColliderEnforcer : MonoBehaviour
    {
        private const bool DEBUG_FISHING_COLLIDERS = false;

        private void Awake()
        {
            EnsureColliderState(logThisEnable: false);
        }

        private void OnEnable()
        {
            EnsureColliderState(logThisEnable: true);
        }

        private void Start()
        {
            EnsureColliderState(logThisEnable: false);
        }

        private void OnValidate()
        {
            EnsureColliderState(logThisEnable: false);
        }

        private void EnsureColliderState(bool logThisEnable)
        {
            SphereCollider sphere = null;
            try { sphere = GetComponent<SphereCollider>(); }
            catch { sphere = null; }

            if (sphere == null)
            {
                try { sphere = gameObject.AddComponent<SphereCollider>(); }
                catch { sphere = null; }
            }

            if (sphere != null)
            {
                try { sphere.enabled = true; } catch { }
                try { sphere.isTrigger = true; } catch { }
            }

            BoxCollider box = null;
            try { box = GetComponent<BoxCollider>(); }
            catch { box = null; }

            if (box != null)
            {
                try { box.enabled = false; } catch { }
            }

            if (DEBUG_FISHING_COLLIDERS && logThisEnable)
            {
                bool sphereEnabled = false;
                bool boxEnabled = false;
                try { sphereEnabled = sphere != null && sphere.enabled; } catch { sphereEnabled = false; }
                try { boxEnabled = box != null && box.enabled; } catch { boxEnabled = false; }
                Debug.Log($"[FishingCollider] {name} sphere.enabled={sphereEnabled} box.enabled={boxEnabled}", this);
            }
        }
    }
}
