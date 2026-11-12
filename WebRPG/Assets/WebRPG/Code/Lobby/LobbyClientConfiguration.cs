using System;
using System.Linq;

namespace WebRPG
{
    // 載入可選伺服器目錄、預設選項與大廳回應等待時間。
    [Serializable]
    public sealed class LobbyClientConfiguration
    {
        private const string SecureWebSocketScheme = "wss";
        private const string LocalWebSocketScheme = "ws";
        public string DefaultServerId;
        public LobbyServerConfiguration[] Servers;
        public float RequestTimeoutSeconds;

        public void Validate()
        {
            if (Servers == null || Servers.Length == 0 ||
                Servers.Any(server => server == null || string.IsNullOrWhiteSpace(server.Id) ||
                    string.IsNullOrWhiteSpace(server.Name) ||
                    !Uri.TryCreate(server.Url, UriKind.Absolute, out Uri address) ||
                    (address.Scheme != SecureWebSocketScheme &&
                        !(server.EditorOnly && address.Scheme == LocalWebSocketScheme))) ||
                Servers.Select(server => server.Id).Distinct(StringComparer.Ordinal).Count() != Servers.Length ||
                !Servers.Any(server => server.Id == DefaultServerId && !server.EditorOnly) ||
                float.IsNaN(RequestTimeoutSeconds) || float.IsInfinity(RequestTimeoutSeconds) ||
                RequestTimeoutSeconds <= 0f)
                throw new InvalidOperationException(nameof(LobbyClientConfiguration));
        }

        public LobbyServerConfiguration[] GetAvailableServers(bool includeEditorServers)
        {
            return Servers.Where(server => includeEditorServers || !server.EditorOnly).ToArray();
        }
    }
}
