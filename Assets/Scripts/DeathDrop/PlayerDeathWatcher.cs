using System;
using Game.Systems;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathWatcher : MonoBehaviour
    {
        private PlayerHealth _health;
        private PlayerInventory _inventory;

        private bool _wasDead;

        private void OnEnable()
        {
            TryResolve();
        }

        private void Update()
        {
            if (_health == null)
                TryResolve();

            if (_health == null)
                return;

            bool isDead;
            try { isDead = _health.IsDead; }
            catch { isDead = false; }

            if (isDead)
            {
                if (_wasDead)
                    return;

                _wasDead = true;

                var mgr = DeathDropManager.Instance;
                if (mgr == null)
                {
                    try
                    {
#if UNITY_2022_2_OR_NEWER
                        mgr = UnityEngine.Object.FindFirstObjectByType<DeathDropManager>(FindObjectsInactive.Exclude);
#else
                        mgr = UnityEngine.Object.FindObjectOfType<DeathDropManager>();
#endif
                    }
                    catch { mgr = null; }
                }

                if (mgr != null)
                    mgr.HandlePlayerDeath(transform, _health, _inventory);

                return;
            }

            // Reset latch when revived.
            if (_wasDead)
                _wasDead = false;
        }

        private void TryResolve()
        {
            try { _health = GetComponent<PlayerHealth>(); } catch { _health = null; }
            if (_health == null)
            {
                try { _health = GetComponentInChildren<PlayerHealth>(true); } catch { _health = null; }
            }

            try { _inventory = GetComponent<PlayerInventory>(); } catch { _inventory = null; }
            if (_inventory == null)
            {
                try { _inventory = GetComponentInChildren<PlayerInventory>(true); } catch { _inventory = null; }
            }

            if (_inventory == null)
            {
                try { _inventory = PlayerInventoryResolver.GetOrFind(); } catch { _inventory = null; }
            }
        }
    }
}
