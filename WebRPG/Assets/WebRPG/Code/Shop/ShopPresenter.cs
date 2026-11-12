using System;
using System.Linq;
using UnityEngine;

namespace WebRPG
{
    // 協調商店商品、帳號餘額、購買確認與交易結果。
    internal sealed class ShopPresenter : IDisposable
    {
        private const string Loading = "正在讀取商店…";
        private const string NoOffers = "目前沒有可購買的商品。";
        private const string BalanceFormat = "金幣 {0:N0}　水晶 {1:N0}";
        private const string ConfirmTitle = "確認購買";
        private const string ConfirmFormat = "確定花費 {0:N0} 水晶購買 {1:N0} 金幣嗎？";
        private const string ConfirmGoldPurchaseFormat = "確定花費 {0:N0} 金幣購買空物件嗎？";
        private const string Confirm = "購買";
        private const string Cancel = "取消";
        private const string Dismiss = "關閉";
        private const string ReturnToMenu = "返回主選單";
        private const string Retry = "重試";
        private const string NoticeTitle = "商店訊息";
        private const string TopUpUnavailable = "目前沒有可購買的儲值方案。";
        private const string NoOrders = "目前沒有儲值訂單。";
        private const string OrderTitle = "訂單明細";
        private const string TopUpTitle = "儲值水晶";
        private const string Checkout = "前往 PayPal";
        private const string CreateOrder = "建立訂單";
        private const string SyncOrder = "同步訂單";
        private const string ExchangedWithDebt = "已取得 {0:N0} 金幣，另有 {1:N0} 金幣用於抵扣欠額。";
        private const string ConfirmTopUpFormat = "以 {0} {1:0.##} 購買 {2:N0} 水晶？";
        private const string OrderFormat = "{0}\n{1} {2:0.##}／{3:N0} 水晶\n訂單：{4}";
        private const string CheckoutOpened = "付款後請回到遊戲，開啟歷史訂單查看進度。";
        private const string Exchanged = "購買成功，已獲得 {0:N0} 金幣。";
        private const string GoldPurchaseSucceeded = "購買成功，已扣除 {0:N0} 金幣。";
        private const string Changed = "商品已更新，請重新選擇並確認購買。";
        private const string Insufficient = "水晶餘額不足，還需要 {0:N0} 水晶。";
        private const string InsufficientGold = "金幣餘額不足，還需要 {0:N0} 金幣。";
        private const string Limit = "帳號金幣已達餘額上限。";
        private const string Failed = "無法完成操作，請重試。";
        private const string RetryRequired = "尚未取得這筆購買的結果。請重試同一筆交易。";

        private readonly ShopModel _model;
        private readonly MainMenuView _menu;
        private readonly ShopView _view;
        private ShopSnapshot _snapshot;
        private int _sessionVersion;
        private bool _busy;
        private Guid _requestId;
        private ShopOffer _selectedOffer;
        private ShopOffer _pendingOffer;
        private ShopTopUpOffer _selectedTopUp;
        private ShopTopUpOrder _selectedOrder;
        private Guid _topUpRequestId;
        private Guid _checkoutOrderId;
        private ShopPage _page;
        private string _notice = string.Empty;

        public ShopPresenter(ShopModel model, MainMenuView menu)
        {
            _model = model;
            _menu = menu;
            _view = menu.Shop;
            _view.RefreshRequested += Refresh;
            _view.OfferRequested += SelectOffer;
            _view.ConfirmRequested += ConfirmPurchase;
            _view.DismissRequested += DismissDialog;
            _view.TopUpRequested += ShowTopUp;
            _view.HistoryRequested += ShowHistory;
            _view.TopUpOfferRequested += SelectTopUp;
            _view.TopUpOrderRequested += SelectOrder;
            _view.CloseRequested += Close;
        }

        public void Open()
        {
            _menu.ShowShop(true);
            if (_pendingOffer == null)
            {
                Refresh();
                return;
            }

            _notice = RetryRequired;
            Render();
        }

        public void Reset()
        {
            _sessionVersion++;
            _busy = false;
            _selectedOffer = null;
            _pendingOffer = null;
            _snapshot = null;
            _selectedTopUp = null;
            _selectedOrder = null;
            _page = ShopPage.Exchange;
            _checkoutOrderId = Guid.Empty;
            _notice = string.Empty;
            _menu.ShowShop(false);
        }

        public void Dispose()
        {
            _sessionVersion++;
            _view.RefreshRequested -= Refresh;
            _view.OfferRequested -= SelectOffer;
            _view.ConfirmRequested -= ConfirmPurchase;
            _view.DismissRequested -= DismissDialog;
            _view.TopUpRequested -= ShowTopUp;
            _view.HistoryRequested -= ShowHistory;
            _view.TopUpOfferRequested -= SelectTopUp;
            _view.TopUpOrderRequested -= SelectOrder;
            _view.CloseRequested -= Close;
        }

        private void Refresh()
        {
            if (!_busy && _pendingOffer == null) LoadAsync(_checkoutOrderId).Forget();
        }

        private async Awaitable LoadAsync(Guid syncOrderId)
        {
            _busy = true;
            _notice = string.Empty;
            int version = _sessionVersion;
            Render();
            try
            {
                if (syncOrderId != Guid.Empty) await _model.SyncTopUpAsync(syncOrderId);
                if (version != _sessionVersion) return;
                ShopSnapshot snapshot = await _model.LoadAsync();
                if (version == _sessionVersion)
                {
                    _snapshot = snapshot;
                    _checkoutOrderId = Guid.Empty;
                }
            }
            catch (Exception)
            {
                if (version == _sessionVersion) _notice = Failed;
            }
            finally
            {
                if (version == _sessionVersion)
                {
                    _busy = false;
                    Render();
                }
            }
        }

        private void SelectOffer(ShopOffer offer)
        {
            if (_busy || _selectedOffer != null || _pendingOffer != null || _notice.Length > 0) return;
            _selectedOffer = offer;
            Render();
        }

        private void ConfirmPurchase()
        {
            if (_busy) return;
            if (_selectedOrder != null)
            {
                if (_selectedOrder.CanOpenCheckout)
                {
                    _checkoutOrderId = _selectedOrder.OrderId;
                    Application.OpenURL(_selectedOrder.CheckoutUrl);
                    _selectedOrder = null;
                    _notice = CheckoutOpened;
                    Render();
                }
                else if (_selectedOrder.Status != ShopTopUpStatus.Reversed)
                {
                    Guid orderId = _selectedOrder.OrderId;
                    _selectedOrder = null;
                    LoadAsync(orderId).Forget();
                }
                return;
            }
            if (_selectedTopUp != null)
            {
                CreateTopUpAsync().Forget();
                return;
            }
            if (_pendingOffer == null)
            {
                if (_selectedOffer == null) return;
                _pendingOffer = _selectedOffer;
                _requestId = Guid.NewGuid();
            }

            ExchangeAsync().Forget();
        }

        private async Awaitable ExchangeAsync()
        {
            _busy = true;
            int version = _sessionVersion;
            ShopOffer offer = _pendingOffer;
            Render();
            try
            {
                ShopExchangeResult result = await _model.ExchangeAsync(_requestId, offer);
                if (version != _sessionVersion) return;
                _pendingOffer = null;
                _selectedOffer = null;
                _snapshot.Balance = result.Balance;
                _notice = result.Status switch
                {
                    ShopExchangeStatus.Exchanged when result.GoldDebtPaid > 0 =>
                        string.Format(ExchangedWithDebt, offer.GoldAmount - result.GoldDebtPaid, result.GoldDebtPaid),
                    ShopExchangeStatus.Exchanged => offer.IsGoldPurchase
                        ? string.Format(GoldPurchaseSucceeded, offer.GoldCost)
                        : string.Format(Exchanged, offer.GoldAmount),
                    ShopExchangeStatus.OfferChanged => Changed,
                    ShopExchangeStatus.InsufficientCrystals =>
                        string.Format(Insufficient, offer.CrystalCost - result.Balance.Crystals),
                    ShopExchangeStatus.InsufficientGold =>
                        string.Format(InsufficientGold, offer.GoldCost - result.Balance.Gold),
                    ShopExchangeStatus.BalanceLimit => Limit,
                    _ => Failed
                };

                if (result.Status == ShopExchangeStatus.OfferChanged)
                {
                    _snapshot.Offers = result.Offer == null
                        ? _snapshot.Offers.Where(item => item != offer).ToArray()
                        : _snapshot.Offers.Select(item => item == offer ? result.Offer : item).ToArray();
                }
            }
            catch (Exception)
            {
                if (version != _sessionVersion) return;
                _selectedOffer = null;
                _notice = RetryRequired;
            }
            finally
            {
                if (version == _sessionVersion)
                {
                    _busy = false;
                    Render();
                }
            }
        }

        private void DismissDialog()
        {
            if (_busy) return;
            if (_pendingOffer != null)
            {
                Close();
                return;
            }

            _selectedOffer = null;
            _selectedTopUp = null;
            _selectedOrder = null;
            _notice = string.Empty;
            Render();
        }

        private void ShowTopUp()
        {
            _page = _page == ShopPage.TopUp ? ShopPage.Exchange : ShopPage.TopUp;
            Render();
        }

        private void ShowHistory()
        {
            _page = ShopPage.History;
            Refresh();
        }

        private void SelectTopUp(ShopTopUpOffer offer)
        {
            _selectedTopUp = offer;
            _topUpRequestId = Guid.NewGuid();
            Render();
        }

        private void SelectOrder(ShopTopUpOrder order)
        {
            _selectedOrder = order;
            Render();
        }

        private async Awaitable CreateTopUpAsync()
        {
            _busy = true;
            _notice = string.Empty;
            int version = _sessionVersion;
            Render();
            try
            {
                ShopTopUpResult result = await _model.CreateTopUpAsync(_topUpRequestId, _selectedTopUp);
                if (version != _sessionVersion) return;
                _selectedTopUp = null;
                if (result.OfferChanged)
                {
                    ShopSnapshot snapshot = await _model.LoadAsync();
                    if (version == _sessionVersion)
                    {
                        _snapshot = snapshot;
                        _notice = Changed;
                    }
                    return;
                }

                _selectedOrder = result.Order;
                _snapshot.TopUpOrders = new[] { result.Order }
                    .Concat(_snapshot.TopUpOrders.Where(order => order.OrderId != result.Order.OrderId)).ToArray();
            }
            catch (Exception)
            {
                if (version == _sessionVersion) _notice = _selectedTopUp == null ? Failed : RetryRequired;
            }
            finally
            {
                if (version == _sessionVersion)
                {
                    _busy = false;
                    Render();
                }
            }
        }

        private void Close()
        {
            _selectedOffer = null;
            _selectedTopUp = null;
            _selectedOrder = null;
            _notice = string.Empty;
            _menu.ShowShop(false);
        }

        private void Render()
        {
            ShopBalance balance = _snapshot?.Balance;
            string balanceText = balance == null
                ? string.Empty
                : string.Format(BalanceFormat, balance.Gold, balance.Crystals);
            string status = _busy && _pendingOffer == null ? Loading : string.Empty;
            if (!_busy && _snapshot != null)
            {
                status = _page switch
                {
                    ShopPage.TopUp when _snapshot.TopUpOffers.Length == 0 => TopUpUnavailable,
                    ShopPage.History when _snapshot.TopUpOrders.Length == 0 => NoOrders,
                    ShopPage.Exchange when _snapshot.Offers.Length == 0 => NoOffers,
                    _ => status
                };
            }

            bool modalOpen = _selectedOffer != null || _selectedTopUp != null || _selectedOrder != null
                || _notice.Length > 0;
            _view.Render(balanceText, _snapshot, _page, status, !_busy && !modalOpen);

            if (_selectedOrder != null)
            {
                ShopTopUpOffer offer = _selectedOrder.Offer;
                string message = string.Format(OrderFormat, ShopView.DescribeStatus(_selectedOrder.Status),
                    offer.Currency, offer.Amount, offer.Crystals, _selectedOrder.OrderId);
                string action = _selectedOrder.CanOpenCheckout ? Checkout :
                    _selectedOrder.Status == ShopTopUpStatus.Reversed ? string.Empty : SyncOrder;
                _view.ShowDialog(OrderTitle, message, action, Dismiss, _busy);
            }
            else if (_selectedTopUp != null)
            {
                string message = _notice.Length > 0 ? _notice : string.Format(ConfirmTopUpFormat,
                    _selectedTopUp.Currency, _selectedTopUp.Amount, _selectedTopUp.Crystals);
                _view.ShowDialog(TopUpTitle, message, _notice.Length > 0 ? Retry : CreateOrder, Cancel, _busy);
            }
            else if (_selectedOffer != null)
            {
                string message = _selectedOffer.IsGoldPurchase
                    ? string.Format(ConfirmGoldPurchaseFormat, _selectedOffer.GoldCost)
                    : string.Format(ConfirmFormat, _selectedOffer.CrystalCost, _selectedOffer.GoldAmount);
                _view.ShowDialog(ConfirmTitle, message, Confirm, Cancel, _busy);
            }
            else if (_notice.Length > 0)
            {
                bool retry = _pendingOffer != null;
                _view.ShowDialog(NoticeTitle, _notice, retry ? Retry : string.Empty,
                    retry ? ReturnToMenu : Dismiss, _busy);
            }
            else
            {
                _view.HideDialog();
            }
        }
    }
}
