using System.Threading.RateLimiting;

namespace WebRPGServer;

// 提供註冊、登入與撤銷登入工作階段的 HTTP API。
public static class AccountEndpoints
{
    private const int AuthRequestsPerMinute = 10;
    private const int MaximumRequestBytes = 4096;
    private const int RateWindowMinutes = 1;
    private const string RatePolicy = "accounts";
    private const string BearerPrefix = "Bearer ";

    public static void AddServices(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AuthRequestsPerMinute, Window = TimeSpan.FromMinutes(RateWindowMinutes),
                        QueueLimit = 0
                }));
        });
    }

    public static void Map(WebApplication application)
    {
        application.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/accounts"))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (!AccountCookieHelper.IsTrustedRequest(context))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                var size = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (size is { IsReadOnly: false }) size.MaxRequestBodySize = MaximumRequestBytes;
                if (context.Request.ContentLength > MaximumRequestBytes)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
                if (!context.Request.IsHttps && context.Connection.RemoteIpAddress is { } remote
                    && !System.Net.IPAddress.IsLoopback(remote))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
            }
            await next(context);
        });
        RouteGroupBuilder group = application.MapGroup("/api/accounts").RequireRateLimiting(RatePolicy);
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (context.HttpContext.RequestServices.GetService<PostgresAccountStore>() is null)
                return Results.Json(new { error = "帳號服務尚未設定。" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            return await next(context);
        });
        ShopEndpoints.Map(group, ReadToken);
        group.MapPost("/register", async (AccountRegistration registration, HttpContext context) =>
        {
            PostgresAccountStore store = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            try
            {
                await store.RegisterAsync(registration, context.RequestAborted);
                return Results.Ok(new { message = "註冊成功，請登入。" });
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        });
        group.MapPost("/login", async (AccountLogin login, HttpContext context) =>
        {
            PostgresAccountStore store = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountSession? session = await store.LoginAsync(login, context.RequestAborted);
            if (session is null)
                return Results.Json(new { error = "Email 或密碼錯誤。" }, statusCode: StatusCodes.Status401Unauthorized);
            await store.RevokeRememberedAsync(AccountCookieHelper.Read(context), context.RequestAborted);
            if (login.RememberMe)
                AccountCookieHelper.Write(context, await store.RememberAsync(session, context.RequestAborted));
            else
                AccountCookieHelper.Clear(context);
            return Results.Ok(session);
        });
        group.MapPost("/refresh", async (HttpContext context) =>
        {
            if (!AccountCookieHelper.HasRequestHeader(context)) return Results.StatusCode(
                StatusCodes.Status403Forbidden);
            PostgresAccountStore store = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            string accessToken = ReadToken(context);
            RememberedAccountSession? session = await store.RefreshAsync(
                AccountCookieHelper.Read(context), context.RequestAborted, accessToken);
            if (session is null)
            {
                if (string.IsNullOrEmpty(accessToken)) AccountCookieHelper.Clear(context);
                return Results.Unauthorized();
            }
            AccountCookieHelper.Write(context, session);
            return Results.Ok(session.Session);
        });
        group.MapPost("/logout", async (HttpContext context) =>
        {
            string token = ReadToken(context);
            string? remembered = AccountCookieHelper.Read(context);
            if (!string.IsNullOrEmpty(remembered) && !AccountCookieHelper.HasRequestHeader(context))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(remembered)) return Results.Unauthorized();
            PostgresAccountStore store = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            await store.RevokeRememberedAsync(remembered, context.RequestAborted);
            if (!string.IsNullOrEmpty(token)) await store.LogoutAsync(token, context.RequestAborted);
            AccountCookieHelper.Clear(context);
            return Results.NoContent();
        });
    }

    private static string ReadToken(HttpContext context)
    {
        string header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ? header[BearerPrefix.Length..]
            : string.Empty;
    }
}
