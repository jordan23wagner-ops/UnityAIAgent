using System;
using UnityEngine;
using Abyssbound.WorldInteraction;

namespace Abyss.Shop
{
    /// <summary>
    /// WorldInteraction tooltip provider for merchants.
    /// Tooltip text comes from MerchantShop.MerchantName (fallback: cleaned GameObject name).
    /// Interaction is intentionally disabled so merchant clicks continue to be handled by the shop system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MerchantTooltipWorldInteractable : WorldInteractable
    {
        private MerchantShop _shop;

        private void Awake()
        {
            _shop = GetComponentInParent<MerchantShop>();

            // Ensure WorldInteraction doesn't treat this as a gameplay interaction target.
            try { SetRequiresRange(false); } catch { }

            // Keep highlight renderers empty; merchant highlight is handled elsewhere.
            try { SetHighlightRenderers(null); } catch { }
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            reason = "Merchant interaction handled by MerchantShop.";
            return false;
        }

        public override string GetHoverText()
        {
            if (_shop == null)
            {
                try { _shop = GetComponentInParent<MerchantShop>(); }
                catch { _shop = null; }
            }

            string name = null;
            try { name = _shop != null ? _shop.MerchantName : null; }
            catch { name = null; }

            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    var goName = gameObject != null ? gameObject.name : "Merchant";
                    name = CleanFallbackName(goName);
                }
                catch
                {
                    name = "Merchant";
                }
            }

            return name;
        }

        private static string CleanFallbackName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Merchant";

            string s = raw.Replace('_', ' ');
            s = s.Replace("Door", "");
            s = s.Replace("Interactable", "");
            s = s.Replace("Shop", "");
            s = s.Replace("Merchant", "");

            s = s.Trim();
            while (s.Contains("  ", StringComparison.Ordinal))
                s = s.Replace("  ", " ");

            return string.IsNullOrWhiteSpace(s) ? "Merchant" : s;
        }
    }
}
