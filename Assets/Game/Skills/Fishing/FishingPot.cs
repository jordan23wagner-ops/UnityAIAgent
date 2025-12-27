using Abyssbound.Skills.Gathering;
using Abyssbound.Stats;
using UnityEngine;

namespace Abyssbound.Skills.Fishing
{
    [DisallowMultipleComponent]
    public sealed class FishingPot : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private FishingSkillConfigSO config;
        [SerializeField] private int tierIndex;

        private float _placedTime;
        private bool _initialized;

        private void OnEnable()
        {
            if (_initialized) return;
            _initialized = true;
            _placedTime = Time.time;
        }

        public void Interact()
        {
            var ctrl = GatheringSkillController.GetOrAttachToPlayer();
            if (ctrl == null)
                return;

            var inv = ctrl.GetOrFindInventory();
            var stats = ctrl.GetOrFindStats();
            if (inv == null || stats == null)
                return;

            if (config == null)
            {
                ShowPopup("No Fishing config assigned.");
                return;
            }

            if (!config.TryGetTier(tierIndex, out var tier))
            {
                ShowPopup("Invalid Fishing tier.");
                return;
            }

            int stored = GetStoredCatches();
            if (stored <= 0)
            {
                ShowPopup("Nothing caught yet.");
                return;
            }

            // Award action XP per stored catch (even if inventory is full).
            int actionXp = Mathf.Max(0, tier.actionXp) * stored;
            AwardXp(stats, config.primarySkill, actionXp);
            if (tier.awardSecondaryXp)
                AwardXp(stats, tier.secondarySkill, Mathf.Max(0, tier.secondaryActionXp) * stored);

            if (string.IsNullOrWhiteSpace(tier.yieldItemId) || tier.yieldAmount <= 0)
            {
                Destroy(gameObject);
                return;
            }

            int totalYield = tier.yieldAmount * stored;
            if (!inv.HasRoomForAdd(tier.yieldItemId, totalYield))
            {
                ShowPopup("Inventory full.");
                return; // keep pot so player can retry
            }

            inv.Add(tier.yieldItemId, totalYield);

            int yieldXp = Mathf.Max(0, tier.yieldXp) * stored;
            AwardXp(stats, config.primarySkill, yieldXp);
            if (tier.awardSecondaryXp)
                AwardXp(stats, tier.secondarySkill, Mathf.Max(0, tier.secondaryYieldXp) * stored);

            Destroy(gameObject);
        }

        private int GetStoredCatches()
        {
            if (config == null) return 0;
            float per = Mathf.Max(0.1f, config.potSecondsPerCatch);
            int max = Mathf.Max(1, config.potMaxStoredCatches);

            float elapsed = Mathf.Max(0f, Time.time - _placedTime);
            int stored = Mathf.FloorToInt(elapsed / per);
            return Mathf.Clamp(stored, 0, max);
        }

        private static void AwardXp(PlayerStatsRuntime stats, Abyssbound.Loot.StatType stat, int amount)
        {
            if (stats == null || amount <= 0)
                return;

            try { stats.AddXp(stat, amount); }
            catch { }

            try
            {
                var name = Abyssbound.Loot.StatTypeCanonical.ToCanonicalPrimaryName(stat);
                var pos = stats.transform.position + Vector3.up * 2.0f;
                FloatingDamageTextManager.ShowXpGain(pos, amount, name);
            }
            catch { }
        }

        private static void ShowPopup(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            try
            {
#if UNITY_2022_2_OR_NEWER
                var popup = UnityEngine.Object.FindFirstObjectByType<SimpleInteractPopup>(FindObjectsInactive.Exclude);
#else
                var popup = UnityEngine.Object.FindObjectOfType<SimpleInteractPopup>();
#endif
                if (popup != null)
                {
                    popup.Show(message);
                    return;
                }
            }
            catch { }
        }
    }
}
