using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    [DisallowMultipleComponent]
    public sealed class DeathPile : MonoBehaviour
    {
        [SerializeField] private List<DroppedItem> items = new();
        [SerializeField] private bool isLooted;

        private int _remainingPickups;
        private DeathDropManager _manager;

        public bool IsLooted => isLooted;
        public IReadOnlyList<DroppedItem> Items => items;

        public void Initialize(DeathDropManager manager, List<DroppedItem> dropped)
        {
            _manager = manager;
            items = dropped != null ? new List<DroppedItem>(dropped) : new List<DroppedItem>();
            isLooted = false;

            SpawnPickups();
        }

        private void SpawnPickups()
        {
            _remainingPickups = 0;

            if (items == null || items.Count == 0)
            {
                MarkLooted();
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.IsValid())
                    continue;

                var pickup = _manager != null
                    ? _manager.SpawnPickupForItem(transform, it)
                    : DeathDropManager.SpawnFallbackPickup(transform, it);

                if (pickup == null)
                    continue;

                _remainingPickups++;
            }

            if (_remainingPickups <= 0)
                MarkLooted();
        }

        internal void NotifyPickupCollected(DeathPilePickup pickup)
        {
            if (isLooted)
                return;

            _remainingPickups = Mathf.Max(0, _remainingPickups - 1);
            if (_remainingPickups <= 0)
                MarkLooted();
        }

        private void MarkLooted()
        {
            if (isLooted)
                return;

            isLooted = true;

            try { _manager?.NotifyPileLooted(this); } catch { }

            // Clean up after looting. Keep it slightly delayed so the last pickup can finish its destroy.
            try { Destroy(gameObject, 0.05f); } catch { }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isLooted ? new Color(0.4f, 0.4f, 0.4f, 0.7f) : new Color(0.9f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.35f);
        }
#endif
    }
}
