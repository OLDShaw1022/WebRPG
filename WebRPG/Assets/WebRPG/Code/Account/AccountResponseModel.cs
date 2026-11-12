using System;

namespace WebRPG
{
    // 接收帳號服務傳回的登入憑證與身分資料。
    public sealed class AccountResponseModel
    {
        public string AccessToken;
        public string AccountId;
        public string Username;
        public DateTimeOffset ExpiresAt;
    }
}
