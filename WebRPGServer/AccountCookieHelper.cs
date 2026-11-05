namespace WebRPGServer;

// 設定登入 Cookie 並驗證瀏覽器要求來源，阻止跨站偽造操作。
public static class AccountCookieHelper
{
    public const string ProductionOrigin = "https://oldshaw-webrpg.com";
    public const string CookieName = "__Secure-WebRPGRemember";
    public const string RequestHeader = "X-WebRPG-Request";
    public const string RequestHeaderValue = "1";
    private const string CookiePath = "/api/accounts";

    public static bool IsTrustedRequest(HttpContext context)
    {
        if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site") return false;
        string origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true;
        string[] origins = context.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("Accounts:AllowedOrigins").Get<string[]>() ?? new[] { ProductionOrigin };
        if (origins.Contains(origin, StringComparer.Ordinal)) return true;
        return context.Connection.RemoteIpAddress is { } remote && System.Net.IPAddress.IsLoopback(remote) &&
            Uri.TryCreate(origin, UriKind.Absolute, out Uri? address) && address.IsLoopback &&
            origin == context.Request.Scheme + "://" + context.Request.Host;
    }

    public static bool HasRequestHeader(HttpContext context) => context.Request.Headers[RequestHeader] == RequestHeaderValue;
    public static string? Read(HttpContext context) => context.Request.Cookies[CookieName];

    public static void Write(HttpContext context, RememberedAccountSession session) =>
        context.Response.Cookies.Append(CookieName, session.RefreshToken, Options(session.RefreshExpiresAt));

    public static void Clear(HttpContext context) => context.Response.Cookies.Delete(CookieName, Options(null));

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
        Path = CookiePath, Expires = expires, IsEssential = true
    };
}
