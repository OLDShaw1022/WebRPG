using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace WebRPG
{
    // 管理帳號 API 與記憶體內的登入憑證，不保存密碼。
    public sealed class AccountModel : IDisposable
    {
        private const string SecureScheme = "https";
        private const string LocalScheme = "http";
        private const string SecureSocketScheme = "wss";
        private const string AccountPath = "/api/accounts/";
        private const string LoginPath = "login";
        private const string RegisterPath = "register";
        private const string LogoutPath = "logout";
        private const string ContentType = "Content-Type";
        private const string JsonContentType = "application/json";
        private const string AuthorizationHeader = "Authorization";
        private const string BearerPrefix = "Bearer ";
        private const string EmailProperty = "email";
        private const string PasswordProperty = "password";
        private const string UsernameProperty = "username";
        private const string ErrorProperty = "error";
        private const string RequestFailedMessage = "無法連線至帳號服務，請稍後重試。";
        private const string InvalidResponseMessage = "帳號服務回應格式錯誤。";
        private const string MissingCredentialsMessage = "請輸入 Email 與密碼。";
        private const string RegistrationFieldsMessage = "註冊須填寫使用者名稱（1～24 字）及密碼（8～128 字）。";
        private const string RegisteredMessage = "註冊成功，請使用 Email 與密碼登入。";
        private const string LoginFailedMessage = "Email 或密碼錯誤。";
        private const string RateLimitMessage = "操作過於頻繁，請稍候一分鐘再試。";
        private const int RequestTimeoutSeconds = 15;
        private const int UnauthorizedStatus = 401;
        private const int RateLimitStatus = 429;
        private const int MinimumPasswordLength = 8;
        private const int MaximumPasswordLength = 128;
        private const int MaximumUsernameLength = 24;
        private readonly LobbyServerConfiguration[] _servers;
        private UnityWebRequest _request;
        private bool _disposed;
        public string[] ServerNames { get; }
        public int DefaultServerIndex { get; }
        public int ServerIndex { get; private set; }
        public string AccessToken { get; private set; } = string.Empty;
        public string AccountId { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken) && DateTimeOffset.UtcNow < ExpiresAt;

        public AccountModel(LobbyClientConfiguration configuration, bool includeEditorServers)
        {
            configuration.Validate();
            _servers = configuration.GetAvailableServers(includeEditorServers);
            ServerNames = _servers.Select(server => server.Name).ToArray();
            DefaultServerIndex = Array.FindIndex(_servers, server => server.Id == configuration.DefaultServerId);
        }

        public async Awaitable<string> SubmitAsync(int serverIndex, string email, string password, string username, bool register)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return MissingCredentialsMessage;
            if (register && (string.IsNullOrWhiteSpace(username) || username.Trim().Length > MaximumUsernameLength ||
                password.Length < MinimumPasswordLength || password.Length > MaximumPasswordLength)) return RegistrationFieldsMessage;
            JObject payload = new JObject { [EmailProperty] = email.Trim(), [PasswordProperty] = password };
            if (register) payload[UsernameProperty] = username.Trim();
            string response = await SendAsync(serverIndex, register ? RegisterPath : LoginPath, payload.ToString(Formatting.None), false);
            if (register) return RegisteredMessage;
            AccountResponseModel session = JsonConvert.DeserializeObject<AccountResponseModel>(response);
            if (session == null || string.IsNullOrEmpty(session.AccessToken) || string.IsNullOrEmpty(session.AccountId) ||
                string.IsNullOrEmpty(session.Username) || session.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException(InvalidResponseMessage);
            ServerIndex = serverIndex;
            AccessToken = session.AccessToken;
            AccountId = session.AccountId;
            Username = session.Username;
            ExpiresAt = session.ExpiresAt;
            return string.Empty;
        }

        public async Awaitable LogoutAsync()
        {
            if (!string.IsNullOrEmpty(AccessToken)) await SendAsync(ServerIndex, LogoutPath, string.Empty, true);
            ClearSession();
        }

        public void ClearSession()
        {
            AccessToken = string.Empty;
            AccountId = string.Empty;
            Username = string.Empty;
            ExpiresAt = default;
        }

        private async Awaitable<string> SendAsync(int serverIndex, string path, string body, bool authenticated)
        {
            if (_disposed || _request != null || serverIndex < 0 || serverIndex >= _servers.Length)
                throw new InvalidOperationException(RequestFailedMessage);
            Uri socket = new Uri(_servers[serverIndex].Url);
            UriBuilder address = new UriBuilder(socket)
            {
                Scheme = socket.Scheme == SecureSocketScheme ? SecureScheme : LocalScheme,
                Path = AccountPath + path, Query = string.Empty, Fragment = string.Empty
            };
            using UnityWebRequest request = new UnityWebRequest(address.Uri, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader(ContentType, JsonContentType);
            if (authenticated) request.SetRequestHeader(AuthorizationHeader, BearerPrefix + AccessToken);
            request.timeout = RequestTimeoutSeconds;
            _request = request;
            try
            {
                await request.SendWebRequest();
                if (_disposed) throw new OperationCanceledException();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string message = request.responseCode == UnauthorizedStatus ? LoginFailedMessage :
                        request.responseCode == RateLimitStatus ? RateLimitMessage : RequestFailedMessage;
                    try { message = JObject.Parse(request.downloadHandler.text).Value<string>(ErrorProperty) ?? message; }
                    catch (JsonException) { }
                    throw new InvalidOperationException(message);
                }
                return request.downloadHandler.text;
            }
            finally { _request = null; }
        }

        public void Dispose()
        {
            _disposed = true;
            _request?.Abort();
            ClearSession();
        }
    }

    // 接收帳號服務傳回的登入憑證與身分資料。
    public sealed class AccountResponseModel
    {
        public string AccessToken;
        public string AccountId;
        public string Username;
        public DateTimeOffset ExpiresAt;
    }
}
