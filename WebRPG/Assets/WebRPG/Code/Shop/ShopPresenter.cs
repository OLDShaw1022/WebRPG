using System;
using UnityEngine;

namespace WebRPG
{
    // 協調商店導覽、條件確認及交易重試。
    internal sealed class ShopPresenter : IDisposable
    {
        private const string Loading = "正在讀取商店…";
        private const string NoOffers = "目前沒有可兌換的商品。";
        private const string BalanceFormat = "金幣 {0}　水晶 {1}";
        private const string OfferFormat = "組合 {0}/{1}　{2} 水晶兌換 {3} 金幣";
        private const string MissingFormat = "還需要 {0} 水晶。";
        private const string Confirm = "確認兌換";
        private const string Exchange = "兌換";
        private const string Retry = "重試";
        private const string Next = "下一個組合";
        private const string Refresh = "重新整理";
        private const string TopUp = "儲值水晶";
        private const string ExchangeTab = "水晶兌換金幣";
        private const string TopUpUnavailable = "水晶儲值尚未開放。";
        private const string Exchanged = "已完成兌換。";
        private const string Changed = "商品條件已變更，請重新確認。";
        private const string Insufficient = "水晶餘額不足。";
        private const string Limit = "帳號金幣已達餘額上限。";
        private const string Failed = "無法完成操作，請重試。";

        private readonly ShopModel _model;
        private readonly MainMenuView _menu;
        private readonly ShopView _view;
        private ShopSnapshot _snapshot;
        private int _selected;
        private int _sessionVersion;
        private bool _busy;
        private bool _confirming;
        private bool _topUp;
        private Guid _requestId;
        private ShopOffer _pendingOffer;
        private string _message = string.Empty;

        public ShopPresenter(ShopModel model, MainMenuView menu)
        {
            _model = model;
            _menu = menu;
            _view = menu.Shop;
            _view.NextRequested += NextOffer;
            _view.ExchangeRequested += RequestExchange;
            _view.TabRequested += SwitchTab;
            _view.CloseRequested += Close;
        }

        public void Open()
        {
            _menu.ShowShop(true);
            if (_pendingOffer == null) LoadAsync().Forget();
            else Render();
        }

        public void Reset()
        {
            _sessionVersion++;
            _busy = false;
            _confirming = false;
            _topUp = false;
            _pendingOffer = null;
            _snapshot = null;
            _selected = 0;
            _message = string.Empty;
            _menu.ShowShop(false);
        }

        public void Dispose()
        {
            _sessionVersion++;
            _view.NextRequested -= NextOffer;
            _view.ExchangeRequested -= RequestExchange;
            _view.TabRequested -= SwitchTab;
            _view.CloseRequested -= Close;
        }

        private async Awaitable LoadAsync()
        {
            _busy = true;
            _snapshot = null;
            _message = Loading;
            int version = _sessionVersion;
            Render();
            try
            {
                ShopSnapshot snapshot = await _model.LoadAsync();
                if (version != _sessionVersion) return;
                _snapshot = snapshot;
                _selected = 0;
                _message = string.Empty;
            }
            catch (Exception)
            {
                if (version == _sessionVersion) _message = Failed;
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

        private void NextOffer()
        {
            if (_snapshot == null || _snapshot.Offers.Length <= 1)
            {
                LoadAsync().Forget();
                return;
            }

            _selected = (_selected + 1) % _snapshot.Offers.Length;
            _confirming = false;
            _message = string.Empty;
            Render();
        }

        private void RequestExchange()
        {
            if (_pendingOffer == null)
            {
                if (!_confirming)
                {
                    _confirming = true;
                    Render();
                    return;
                }

                _pendingOffer = _snapshot.Offers[_selected];
                _requestId = Guid.NewGuid();
            }

            ExchangeAsync().Forget();
        }

        private async Awaitable ExchangeAsync()
        {
            _busy = true;
            int version = _sessionVersion;
            Render();
            try
            {
                ShopExchangeResult result = await _model.ExchangeAsync(_requestId, _pendingOffer);
                if (version != _sessionVersion) return;
                _pendingOffer = null;
                _confirming = false;
                _snapshot.Balance = result.Balance;
                _message = result.Status switch
                {
                    ShopExchangeStatus.Exchanged => Exchanged,
                    ShopExchangeStatus.OfferChanged => Changed,
                    ShopExchangeStatus.InsufficientCrystals => Insufficient,
                    ShopExchangeStatus.BalanceLimit => Limit,
                    _ => Failed
                };
                if (result.Status == ShopExchangeStatus.OfferChanged)
                {
                    if (result.Offer != null) _snapshot.Offers[_selected] = result.Offer;
                    else
                    {
                        var offers = new System.Collections.Generic.List<ShopOffer>(_snapshot.Offers);
                        offers.RemoveAt(_selected);
                        _snapshot.Offers = offers.ToArray();
                        _selected = 0;
                    }
                }
            }
            catch (Exception)
            {
                if (version == _sessionVersion) _message = Failed;
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

        private void SwitchTab()
        {
            _topUp = !_topUp;
            _confirming = false;
            Render();
        }

        private void Close()
        {
            _confirming = false;
            _topUp = false;
            _menu.ShowShop(false);
        }

        private void Render()
        {
            ShopBalance balance = _snapshot?.Balance;
            string balanceText = balance == null ? string.Empty : string.Format(BalanceFormat, balance.Gold, balance.Crystals);
            if (_topUp)
            {
                _view.Render(balanceText, TopUpUnavailable, string.Empty, string.Empty, ExchangeTab, _busy);
                return;
            }

            ShopOffer[] offers = _snapshot?.Offers;
            ShopOffer offer = offers != null && offers.Length > 0 ? offers[_selected] : null;
            string details = offer == null
                ? (_snapshot == null ? string.Empty : NoOffers)
                : string.Format(OfferFormat, _selected + 1, offers.Length, offer.CrystalCost, offer.GoldAmount);
            string action = string.Empty;
            if (_pendingOffer != null) action = Retry;
            else if (offer != null)
            {
                if (balance.Crystals >= offer.CrystalCost) action = _confirming ? Confirm : Exchange;
                else details += Environment.NewLine + string.Format(MissingFormat, offer.CrystalCost - balance.Crystals);
            }

            if (!string.IsNullOrEmpty(_message))
                details = string.IsNullOrEmpty(details) ? _message : details + Environment.NewLine + _message;
            string next = _pendingOffer != null ? string.Empty : offers?.Length > 1 ? Next : Refresh;
            _view.Render(balanceText, details, next, action, TopUp, _busy);
        }
    }
}
