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
        [SerializeField] private TextMeshProUGUI _balance;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private UnityEngine.UI.ScrollRect _offersScroll;
        [SerializeField] private CanvasGroup _offersGroup;
        [SerializeField] private ShopOfferCardView _offerTemplate;
        [SerializeField] private UnityEngine.UI.Button _refresh;
        [SerializeField] private UnityEngine.UI.Button _topUp;
        [SerializeField] private UnityEngine.UI.Button _close;
        [SerializeField] private GameObject _dialog;
        [SerializeField] private TextMeshProUGUI _dialogTitle;
        [SerializeField] private TextMeshProUGUI _dialogMessage;
        [SerializeField] private UnityEngine.UI.Button _confirm;
        [SerializeField] private TextMeshProUGUI _confirmLabel;
        [SerializeField] private UnityEngine.UI.Button _dismiss;
        [SerializeField] private TextMeshProUGUI _dismissLabel;

        private readonly List<ShopOfferCardView> _cards = new List<ShopOfferCardView>();
        private ShopOffer[] _offers;

        internal event Action<ShopOffer> OfferRequested;
        internal event Action RefreshRequested;
        internal event Action TopUpRequested;
        internal event Action CloseRequested;
        internal event Action ConfirmRequested;
        internal event Action DismissRequested;

        private void Awake()
        {
            _refresh.onClick.AddListener(() => RefreshRequested?.Invoke());
            _topUp.onClick.AddListener(() => TopUpRequested?.Invoke());
            _close.onClick.AddListener(() => CloseRequested?.Invoke());
            _confirm.onClick.AddListener(() => ConfirmRequested?.Invoke());
            _dismiss.onClick.AddListener(() => DismissRequested?.Invoke());
        }

        internal void Render(string balance, ShopOffer[] offers, string status, bool interactable)
        {
            _balance.text = balance;
            _status.text = status;
            _offersGroup.interactable = interactable;
            _offersGroup.blocksRaycasts = interactable;
            _refresh.interactable = interactable;
            _topUp.interactable = interactable;
            _close.interactable = interactable;

            if (_offers == offers) return;
            _offers = offers;
            foreach (ShopOfferCardView card in _cards)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }

            _cards.Clear();
            if (offers == null) return;

            foreach (ShopOffer offer in offers)
            {
                ShopOfferCardView card = Instantiate(_offerTemplate, _offersScroll.content);
                card.Bind(offer, selected => OfferRequested?.Invoke(selected));
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_offersScroll.content);
            _offersScroll.StopMovement();
            _offersScroll.verticalNormalizedPosition = 1f;
        }

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
