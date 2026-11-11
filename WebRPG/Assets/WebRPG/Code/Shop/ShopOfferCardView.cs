using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 綁定單一商品的名稱、花費與點擊事件。
    [DisallowMultipleComponent]
    public sealed class ShopOfferCardView : MonoBehaviour
    {
        private const string NameFormat = "{0:N0} 金幣";
        private const string EmptyItemName = "空物件";
        private const string CrystalCostFormat = "花費 {0:N0} 水晶";
        private const string GoldCostFormat = "花費 {0:N0} 金幣";

        [SerializeField] private UnityEngine.UI.Button _button;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _cost;

        internal void Bind(ShopOffer offer, Action<ShopOffer> onSelected)
        {
            _name.text = offer.IsGoldPurchase ? EmptyItemName : string.Format(NameFormat, offer.GoldAmount);
            _cost.text = offer.IsGoldPurchase
                ? string.Format(GoldCostFormat, offer.GoldCost)
                : string.Format(CrystalCostFormat, offer.CrystalCost);
            _button.onClick.AddListener(() => onSelected(offer));
        }
    }
}
