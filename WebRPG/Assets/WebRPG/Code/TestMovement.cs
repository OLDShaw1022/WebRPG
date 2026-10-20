using UnityEngine;
using UnityEngine.InputSystem;
using WebRPGShared;
using NativeWebSocket;

namespace WebRPG
{
    public class TestMovement : MonoBehaviour
    {
        [SerializeField]
        private float _speed;

        private Vector2 _direction;

        private WebSocket _webSocket;

        private async void Start()
        {
            Debug.Log("A");
            Class1 class1 = new Class1();
            class1.Test();
            Debug.Log("B");

            _webSocket = new WebSocket("wss://ws.oldshaw-webrpg.com/ws");

            _webSocket.OnOpen += () => Debug.Log("Connection open!");
            _webSocket.OnError += (e) => Debug.Log("Error! " + e);
            _webSocket.OnClose += (code) => Debug.Log("Connection closed!");

            _webSocket.OnMessage += (bytes) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log("Received: " + message);
            };

            InvokeRepeating("SendWebSocketMessage", 0.0f, 0.3f);

            await _webSocket.Connect();
        }

        async void SendWebSocketMessage()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.Send(new byte[] { 10, 20, 30 });
                await _webSocket.SendText("plain text message");
            }
        }

        private async void OnApplicationQuit()
        {
            await _webSocket.Close();
        }

        private void Update()
        {
            _webSocket?.DispatchMessageQueue();

            _direction = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
                _direction.y = 1;
            if (Keyboard.current.sKey.isPressed)
                _direction.y = -1;
            if (Keyboard.current.dKey.isPressed)
                _direction.x = 1;
            if (Keyboard.current.aKey.isPressed)
                _direction.x = -1;

            transform.Translate(_direction.normalized * (_speed * Time.deltaTime));
        }
    }
}
