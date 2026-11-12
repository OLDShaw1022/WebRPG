namespace WebRPG.Tests
{
    // 建立不連接正式環境的帳號用戶端測試設定。
    internal static class AccountClientConfigurationHelper
    {
        public static LobbyClientConfiguration Create(string socketUrl) => new LobbyClientConfiguration
        {
            DefaultServerId = "production", RequestTimeoutSeconds = 10,
            Servers = new[]
            {
                new LobbyServerConfiguration { Id = "production", Name = "Production",
                    Url = "wss://example.invalid/ws" },
                new LobbyServerConfiguration { Id = "test", Name = "Local", Url = socketUrl, EditorOnly = true }
            }
        };
    }
}
