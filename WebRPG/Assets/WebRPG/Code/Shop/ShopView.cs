using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 呈現帳號餘額、商品格與商店彈窗，並轉送玩家操作。
    [DisallowMultipleComponent]
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _balance;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private UnityEngine.UI.ScrollRect _offersScroll;
        [SerializeField] private CanvasGroup _offersGroup;
        [SerializeField] private ShopOfferCardView _offerTemplate;
        [SerializeField] private UnityEngine.UI.Button _refresh;
        [SerializeField] private UnityEngine.UI.Button _topUp;
        [SerializeField] private TextMeshProUGUI _topUpLabel;
        [SerializeField] private UnityEngine.UI.Button _history;
        [SerializeField] private UnityEngine.UI.Button _close;
        [SerializeField] private GameObject _dialog;
        [SerializeField] private TextMeshProUGUI _dialogTitle;
        [SerializeField] private TextMeshProUGUI _dialogMessage;
        [SerializeField] private UnityEngine.UI.Button _confirm;
        [SerializeField] private TextMeshProUGUI _confirmLabel;
        [SerializeField] private UnityEngine.UI.Button _dismiss;
        [SerializeField] private TextMeshProUGUI _dismissLabel;

        private readonly List<ShopOfferCardView> _cards = new List<ShopOfferCardView>();
        private const string ShopTitle = "商店";
        private const string TopUpLabel = "儲值水晶";
        private const string HistoryTitle = "歷史訂單";
        private const string ReturnToTopUp = "返回儲值";
        private const string ExchangeLabel = "兌換商品";
        private const string CrystalFormat = "{0:N0} 水晶";
        private const string PriceFormat = "{0} {1:0.##}";
        private const string ReceiptFormat = "訂單 {0:MM/dd HH:mm}";
        private const string Waiting = "等待付款";
        private const string Processing = "處理中";
        private const string Credited = "已入帳";
        private const string Failed = "失敗";
        private const string Canceled = "已取消";
        private const string PartiallyRefunded = "部分退款";
        private const string Reversed = "已沖銷";
        private const string Unknown = "狀態未確認";
        private ShopOffer[] _offers;
        private ShopTopUpOffer[] _topUpOffers;
        private ShopTopUpOrder[] _orders;
        private ShopPage _page;

        internal event Action<ShopOffer> OfferRequested;
        internal event Action RefreshRequested;
        internal event Action TopUpRequested;
        internal event Action HistoryRequested;
        internal event Action<ShopTopUpOffer> TopUpOfferRequested;
        internal event Action<ShopTopUpOrder> TopUpOrderRequested;
        internal event Action CloseRequested;
        internal event Action ConfirmRequested;
        internal event Action DismissRequested;

        private void Awake()
        {
            _refresh.onClick.AddListener(() => RefreshRequested?.Invoke());
            _topUp.onClick.AddListener(() => TopUpRequested?.Invoke());
            _history.onClick.AddListener(() => HistoryRequested?.Invoke());
            _close.onClick.AddListener(() => CloseRequested?.Invoke());
            _confirm.onClick.AddListener(() => ConfirmRequested?.Invoke());
            _dismiss.onClick.AddListener(() => DismissRequested?.Invoke());
        }

        internal void Render(string balance, ShopSnapshot snapshot, ShopPage page, string status, bool interactable)
        {
            _title.text = page switch
            {
                ShopPage.TopUp => TopUpLabel,
                ShopPage.History => HistoryTitle,
                _ => ShopTitle
            };
            _balance.text = balance;
            _status.text = status;
            _offersGroup.interactable = interactable;
            _offersGroup.blocksRaycasts = interactable;
            _refresh.interactable = interactable;
            _topUp.interactable = interactable;
            _topUpLabel.text = page switch
            {
                ShopPage.TopUp => ExchangeLabel,
                ShopPage.History => ReturnToTopUp,
                _ => TopUpLabel
            };
            _history.gameObject.SetActive(page == ShopPage.TopUp);
            _history.interactable = interactable;
            _close.interactable = interactable;

            ShopOffer[] offers = page == ShopPage.Exchange ? snapshot?.Offers : null;
            ShopTopUpOffer[] topUps = page == ShopPage.TopUp ? snapshot?.TopUpOffers : null;
            ShopTopUpOrder[] orders = page == ShopPage.History ? snapshot?.TopUpOrders : null;
            if (_offers == offers && _topUpOffers == topUps && _orders == orders && _page == page) return;
            _offers = offers;
            _topUpOffers = topUps;
            _orders = orders;
            _page = page;
            foreach (ShopOfferCardView card in _cards)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }

            _cards.Clear();
            foreach (ShopOffer offer in offers ?? Array.Empty<ShopOffer>())
                AddCard(card => card.Bind(offer, selected => OfferRequested?.Invoke(selected)));
            foreach (ShopTopUpOffer offer in topUps ?? Array.Empty<ShopTopUpOffer>())
                AddCard(card => card.Bind(string.Format(CrystalFormat, offer.Crystals),
                    string.Format(PriceFormat, offer.Currency, offer.Amount), () => TopUpOfferRequested?.Invoke(offer)));
            foreach (ShopTopUpOrder order in orders ?? Array.Empty<ShopTopUpOrder>())
                AddCard(card => card.Bind(string.Format(ReceiptFormat, order.CreatedAt.ToLocalTime()),
                    DescribeStatus(order.Status), () => TopUpOrderRequested?.Invoke(order)));

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_offersScroll.content);
            _offersScroll.StopMovement();
            _offersScroll.verticalNormalizedPosition = 1f;
        }

        private void AddCard(Action<ShopOfferCardView> bind)
        {
            ShopOfferCardView card = Instantiate(_offerTemplate, _offersScroll.content);
            bind(card);
            card.gameObject.SetActive(true);
            _cards.Add(card);
        }

        internal static string DescribeStatus(ShopTopUpStatus status) => status switch
        {
            ShopTopUpStatus.AwaitingPayment => Waiting,
            ShopTopUpStatus.Processing => Processing,
            ShopTopUpStatus.Credited => Credited,
            ShopTopUpStatus.Failed => Failed,
            ShopTopUpStatus.Canceled => Canceled,
            ShopTopUpStatus.PartiallyRefunded => PartiallyRefunded,
            ShopTopUpStatus.Reversed => Reversed,
            _ => Unknown
        };

        internal void ShowDialog(string title, string message, string confirm, string dismiss, bool busy)
        {
            _dialogTitle.text = title;
            _dialogMessage.text = message;
            _confirmLabel.text = confirm;
            _dismissLabel.text = dismiss;
            _confirm.gameObject.SetActive(confirm.Length > 0);
            _confirm.interactable = !busy;
            _dismiss.interactable = !busy;
            _dialog.SetActive(true);
        }

        internal void HideDialog() => _dialog.SetActive(false);
    }
}
