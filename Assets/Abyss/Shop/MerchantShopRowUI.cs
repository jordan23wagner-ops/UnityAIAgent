using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Shop
{
    public class MerchantShopRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button button;

        private Action _onClick;

        public void Bind(string itemName, int price, Action onClick)
        {
            if (nameText != null) nameText.text = itemName;
            if (priceText != null) priceText.text = price.ToString();
            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
            }
        }

        public void ButtonSelect()
        {
            if (button != null)
                button.Select();
        }

        public Button Button => button;
    }
}
