namespace WebRPGServer;

// 分離沙盒與正式 PayPal 應用程式，付款網址由部署設定提供。
internal sealed class PayPalSettings(IConfiguration configuration)
{
    internal bool Sandbox { get; } = !configuration.GetValue<bool>("Shop:PayPal:Live");
    internal bool Enabled { get; } = configuration.GetValue<bool>("Shop:PayPal:Enabled");
    internal string? ClientId { get; } = configuration["Shop:PayPal:ClientId"];
    internal string? ClientSecret { get; } = configuration["Shop:PayPal:ClientSecret"];
    internal string? WebhookId { get; } = configuration["Shop:PayPal:WebhookId"];
    internal string? CallbackBase { get; } = configuration["Shop:PayPal:CallbackUrl"];
    internal string? GameUrl { get; } = configuration["Shop:PayPal:GameUrl"];

    internal bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(WebhookId) && IsHttps(CallbackBase) && IsHttps(GameUrl);

    private static bool IsHttps(string? url) => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
