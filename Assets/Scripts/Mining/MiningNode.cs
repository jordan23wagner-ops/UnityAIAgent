using System;
using System.Collections;
using UnityEngine;
using Abyssbound.WorldInteraction;

namespace Abyssbound.Mining
{
    public sealed class MiningNode : WorldInteractable
    {
        [Header("Mining")]
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private float mineSeconds = 2.0f;
        [SerializeField] private float cooldownSeconds = 8.0f;
        [SerializeField] private string oreItemId = "ore_copper";

        private bool isMining;
        private float nextReadyTime;

        private void Reset()
        {
            SetDisplayName("Copper Rock");
        }

        public override bool CanInteract(Vector3 interactorPos)
        {
            if (isMining)
                return false;

            if (Time.time < nextReadyTime)
                return false;

            float d = Vector3.Distance(transform.position, interactorPos);
            return d <= interactRange;
        }

        public override void Interact(GameObject interactor)
        {
            var pos = interactor != null ? interactor.transform.position : transform.position;
            if (!CanInteract(pos))
                return;

            if (!gameObject.activeInHierarchy)
                return;

            StartCoroutine(MineRoutine(interactor));
        }

        private IEnumerator MineRoutine(GameObject interactor)
        {
            isMining = true;

            yield return new WaitForSeconds(mineSeconds);

            bool added = TryAddToInventory(interactor, oreItemId, 1);
            if (!added)
            {
                Debug.Log($"[Abyssbound] Mined ore: {oreItemId} x1");
            }

            nextReadyTime = Time.time + cooldownSeconds;
            isMining = false;
        }

        private static bool TryAddToInventory(GameObject interactor, string itemId, int amount)
        {
            var inventoryType = FindTypeByName("PlayerInventory");
            if (inventoryType == null)
                return false;

            var method = inventoryType.GetMethod("AddItem", new[] { typeof(string), typeof(int) });
            if (method == null)
                return false;

            object inventoryInstance = null;

            if (interactor != null)
            {
                try
                {
                    inventoryInstance = interactor.GetComponentInParent(inventoryType);
                }
                catch
                {
                    // ignore
                }
            }

#pragma warning disable CS0618
            if (inventoryInstance == null)
                inventoryInstance = UnityEngine.Object.FindObjectOfType(inventoryType);
#pragma warning restore CS0618

            if (inventoryInstance == null)
                return false;

            try
            {
                method.Invoke(inventoryInstance, new object[] { itemId, amount });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t != null && t.Name == typeName)
                        return t;
                }
            }

            return null;
        }
    }
}
