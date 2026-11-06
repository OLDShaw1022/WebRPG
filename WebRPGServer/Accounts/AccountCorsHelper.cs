namespace WebRPGServer;

// 僅允許明確設定的遊戲來源以 Cookie 呼叫跨來源帳號 API。
public static class AccountCorsHelper
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        string[] origins = configuration.GetSection("Accounts:AllowedOrigins").Get<string[]>() ??
            new[] { AccountCookieHelper.ProductionOrigin };
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods("POST")
            .WithHeaders("Content-Type", "Authorization", AccountCookieHelper.RequestHeader)
            .AllowCredentials()));
    }
}
