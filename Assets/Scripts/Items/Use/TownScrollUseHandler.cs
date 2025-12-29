using Game.Systems;
using UnityEngine;
using Abyssbound.DeathDrop;

namespace Abyssbound.Items.Use
{
    public static class TownScrollUseHandler
    {
        public const string TownScrollItemId = "scroll_town";

        public static bool TryUseFromInventory()
        {
            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogWarning("[TownScroll] No PlayerInventory found; cannot use scroll.");
                return false;
            }

            var player = inv.gameObject;
            if (player == null)
                return false;

            if (!inv.TryConsume(TownScrollItemId, 1))
            {
                Debug.LogWarning("[TownScroll] No scroll_town to consume.");
                return false;
            }

            if (!RespawnHelper.TryGetTownSpawn(out var pos))
            {
                Debug.LogWarning("[TownScroll] No town spawn resolved; teleporting to Vector3.zero.");
            }

            RespawnHelper.TeleportPlayerTo(player.transform, pos);
            RespawnHelper.ResetPlayerState(player);

            // Slightly longer than the default reset window to avoid immediate click-to-move.
            try { DeathDropManager.SuppressGameplayInputUntil = Time.unscaledTime + 0.35f; } catch { }

            // SUCCESS: only now notify UI listeners.
            ItemUseRouter.NotifyItemUsed(TownScrollItemId);

            return true;
        }
    }
}
