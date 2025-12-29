using System;
using Game.Systems;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    [DisallowMultipleComponent]
    public sealed class DeathPilePickup : MonoBehaviour
    {
        [Header("Drop")]
        [SerializeField] private string itemId;
        [SerializeField, Min(1)] private int amount = 1;

        [NonSerialized] private DeathPile _pile;

        public void Configure(DeathPile pile, string itemId, int amount)
        {
            _pile = pile;
            this.itemId = itemId;
            this.amount = Mathf.Max(1, amount);

            try
            {
                gameObject.name = $"DeathDrop_{(string.IsNullOrWhiteSpace(itemId) ? "(null)" : itemId)}";
            }
            catch { }
        }

        private void Reset()
        {
            EnsureTrigger();
        }

        private void OnValidate()
        {
            EnsureTrigger();
        }

        private void EnsureTrigger()
        {
            try
            {
                var c = GetComponent<Collider>();
                if (c != null) c.isTrigger = true;
            }
            catch { }

            try
            {
                var c2 = GetComponent<Collider2D>();
                if (c2 != null) c2.isTrigger = true;
            }
            catch { }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            TryPickup();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            TryPickup();
        }

        private static bool IsPlayer(Component c)
        {
            if (c == null) return false;

            try { if (c.CompareTag("Player")) return true; } catch { }

            try
            {
                var go = c.gameObject;
                return go != null && string.Equals(go.name, "Player_Hero", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void TryPickup()
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
                return;

            try { inv.Add(itemId, Mathf.Max(1, amount)); }
            catch { return; }

            try { _pile?.NotifyPickupCollected(this); } catch { }

            try { Destroy(gameObject); } catch { }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.15f, 0.12f);
        }
#endif
    }
}
