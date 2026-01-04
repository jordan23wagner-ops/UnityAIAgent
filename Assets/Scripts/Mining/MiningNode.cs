using System.Collections;
using UnityEngine;
using Abyssbound.WorldInteraction;
using UnityEngine.Serialization;
using Game.Systems;
using Abyssbound.Skilling;
using Abyssbound.Skills;
using System;

namespace Abyssbound.Mining
{
    public sealed class MiningNode : WorldInteractable
    {
        private const string BasicPickaxeId = "pickaxe_basic";
        private const string BronzePickaxeId = "tool_bronze_pickaxe";

        [Header("Mining")]
        [FormerlySerializedAs("mineSeconds")]
        [SerializeField] private float mineDuration = 1.25f;

        [FormerlySerializedAs("cooldownSeconds")]
        [SerializeField] private float cooldownDuration = 3f;

        [SerializeField] private int oreAmountMin = 1;
        [SerializeField] private int oreAmountMax = 3;

        private bool isMining;
        private float nextReadyTime;

        private void Reset()
        {
            SetDisplayName("Copper Rock");
        }

        private void OnValidate()
        {
            // Keep tooltip text stable for designers.
            if (string.IsNullOrWhiteSpace(DisplayName) || string.Equals(DisplayName, "Interactable", StringComparison.OrdinalIgnoreCase))
                SetDisplayName("Copper Rock");
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            if (!base.CanInteract(interactor, out reason))
            {
                WorldInteractionFeedback.LogBlocked(reason, $"mine {DisplayName}", this);
                return false;
            }

            if (isMining)
            {
                reason = "Already mining";
                WorldInteractionFeedback.LogBlocked(reason, $"mine {DisplayName}", this);
                return false;
            }

            if (Time.time < nextReadyTime)
            {
                float remaining = Mathf.Max(0f, nextReadyTime - Time.time);
                reason = $"Depleted ({remaining:0.0}s)";
                WorldInteractionFeedback.LogBlocked(reason, $"mine {DisplayName}", this);
                return false;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                reason = "No inventory";
                WorldInteractionFeedback.LogBlocked(reason, $"mine {DisplayName}", this);
                return false;
            }

            if (!HasBasicPickaxe(inv))
            {
                reason = "missing Pickaxe";
                WorldInteractionFeedback.LogBlocked(reason, $"mine {DisplayName}", this);
                return false;
            }

            reason = null;
            return true;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            if (!gameObject.activeInHierarchy)
                return;

            Debug.Log($"[Mining] Started mining {DisplayName}");

            StartCoroutine(MineRoutine(interactor));
        }

        private IEnumerator MineRoutine(GameObject interactor)
        {
            isMining = true;

            yield return new WaitForSeconds(mineDuration);

            int min = Mathf.Max(0, oreAmountMin);
            int max = Mathf.Max(min, oreAmountMax);
            int amount = UnityEngine.Random.Range(min, max + 1);

            if (amount > 0)
            {
                var inv = PlayerInventoryResolver.GetOrFind();
                if (inv != null)
                {
                    inv.Add(SkillingItemIds.CopperOre, amount);
                }
                else
                {
                    Debug.LogWarning("[Mining] No PlayerInventory found");
                }

                Debug.Log($"[Mining] Gained {amount}x Copper Ore");

                var skills = PlayerSkills.FindOrCreateOnPlayer();
                if (skills != null)
                {
                    int xp = amount * 8;
                    skills.AddXp(SkillType.Mining, xp, source: "Mining");
                }
            }

            nextReadyTime = Time.time + cooldownDuration;
            isMining = false;
        }

        private static bool HasBasicPickaxe(PlayerInventory inv)
        {
            if (inv == null) return false;

            // Fast path: known IDs.
            try
            {
                if (inv.Has(BasicPickaxeId, 1))
                    return true;
            }
            catch { }

            try
            {
                if (inv.Has(BronzePickaxeId, 1))
                    return true;
            }
            catch { }

            // Safe fallback: accept any inventory ID that looks like a pickaxe.
            // (Avoid loosening to other tools.)
            try
            {
                var snap = inv.GetAllItemsSnapshot();
                if (snap != null)
                {
                    foreach (var kv in snap)
                    {
                        if (kv.Value <= 0) continue;
                        var id = kv.Key;
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        var lower = id.ToLowerInvariant();
                        if (lower.Contains("pickaxe") && (lower.StartsWith("pickaxe_") || lower.StartsWith("tool_") || lower.EndsWith("_pickaxe")))
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public override string GetHoverText()
        {
            // Hover tooltip should identify the resource type.
            return DisplayName;
        }
    }
}
