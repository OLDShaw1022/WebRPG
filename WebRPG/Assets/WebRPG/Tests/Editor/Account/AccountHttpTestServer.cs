using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WebRPG.Tests
{
    // 提供 Unity 帳號用戶端測試使用的本機 HTTP 回應。
    internal sealed class AccountHttpTestServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        public string SocketUrl { get; }
        public string LastPath { get; private set; }
        public string LastAuthorization { get; private set; }
        public string LastRequestHeader { get; private set; }
        public string LastBody { get; private set; }
        public string LastCookie { get; private set; }

        public AccountHttpTestServer()
        {
            TcpListener reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            SocketUrl = $"ws://localhost:{port}/ws?channel=lobby";
        }

        public async Task RespondAsync(int status, string body, string setCookie = null)
        {
            HttpListenerContext context = await _listener.GetContextAsync();
            LastPath = context.Request.Url.AbsolutePath;
            LastAuthorization = context.Request.Headers["Authorization"];
            LastRequestHeader = context.Request.Headers["X-WebRPG-Request"];
            LastCookie = context.Request.Headers["Cookie"];
            if (setCookie != null) context.Response.Headers.Add("Set-Cookie", setCookie);
            using (System.IO.StreamReader reader = new System.IO.StreamReader(context.Request.InputStream, Encoding.UTF8))
                LastBody = await reader.ReadToEndAsync();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        public void Dispose() => _listener.Close();
    }
}
