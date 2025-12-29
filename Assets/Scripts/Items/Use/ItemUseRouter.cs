using System;

namespace Abyssbound.Items.Use
{
    public static class ItemUseRouter
    {
        public static event Action<string> OnItemUsed;

        public static void NotifyItemUsed(string itemId)
        {
            try { OnItemUsed?.Invoke(itemId); } catch { }
        }
    }
}
