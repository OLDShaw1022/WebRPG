using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 呈現商店餘額、商品及操作結果，並轉送按鈕事件。
    [DisallowMultipleComponent]
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _balance;
        [SerializeField] private TextMeshProUGUI _details;
        [SerializeField] private UnityEngine.UI.Button _next;
        [SerializeField] private TextMeshProUGUI _nextLabel;
        [SerializeField] private UnityEngine.UI.Button _exchange;
        [SerializeField] private TextMeshProUGUI _exchangeLabel;
        [SerializeField] private UnityEngine.UI.Button _tab;
        [SerializeField] private TextMeshProUGUI _tabLabel;
        [SerializeField] private UnityEngine.UI.Button _close;

        public event Action NextRequested;
        public event Action ExchangeRequested;
        public event Action TabRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            _next.onClick.AddListener(HandleNext);
            _exchange.onClick.AddListener(HandleExchange);
            _tab.onClick.AddListener(HandleTab);
            _close.onClick.AddListener(HandleClose);
        }

        public void Render(string balance, string details, string next, string exchange, string tab, bool busy)
        {
            _balance.text = balance;
            _details.text = details;
            _nextLabel.text = next;
            _exchangeLabel.text = exchange;
            _tabLabel.text = tab;
            _next.gameObject.SetActive(!string.IsNullOrEmpty(next));
            _exchange.gameObject.SetActive(!string.IsNullOrEmpty(exchange));
            _next.interactable = !busy;
            _exchange.interactable = !busy;
            _tab.interactable = !busy;
            _close.interactable = !busy;
        }

        private void HandleNext() => NextRequested?.Invoke();
        private void HandleExchange() => ExchangeRequested?.Invoke();
        private void HandleTab() => TabRequested?.Invoke();
        private void HandleClose() => CloseRequested?.Invoke();
    }
}
