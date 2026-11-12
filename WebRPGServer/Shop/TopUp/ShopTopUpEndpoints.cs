using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace WebRPGServer;

// 提供帳號儲值入口與獨立的 PayPal 通知、付款回傳端點。
internal static class ShopTopUpEndpoints
{
    private const int WebhookMaxBytes = 64 * 1024;

    internal static void MapPlayer(RouteGroupBuilder group, Func<HttpContext, string> readToken)
    {
        group.MapPost("/shop/top-ups", async (ShopTopUpRequest request, HttpContext context) =>
        {
            PostgresAccountStore accounts = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountIdentity? identity = await accounts.AuthenticateAsync(readToken(context), context.RequestAborted);
            if (identity is null) return Results.Unauthorized();
            PayPalSettings settings = context.RequestServices.GetRequiredService<PayPalSettings>();
            if (!settings.Enabled || !settings.Configured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            if (request.RequestId == Guid.Empty || request.Offer is null || !ShopTopUpCatalog.IsValid(request.Offer))
                return Results.BadRequest();

            ShopTopUpCatalog catalog = context.RequestServices.GetRequiredService<ShopTopUpCatalog>();
            PayPalTopUpService service = context.RequestServices.GetRequiredService<PayPalTopUpService>();
            try
            {
                ShopTopUpOrder? order = await service.CreateAsync(Guid.Parse(identity.AccountId), request,
                    catalog.Find(request.Offer.Id), context.RequestAborted);
                if (order is null || order.Offer != request.Offer || order.Sandbox != settings.Sandbox)
                    return Results.Ok(new { offerChanged = true });
                return Results.Ok(new { order = order.Receipt });
            }
            catch (HttpRequestException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        });

        group.MapPost("/shop/top-ups/{orderId:guid}/sync", async (Guid orderId, HttpContext context) =>
        {
            PostgresAccountStore accounts = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountIdentity? identity = await accounts.AuthenticateAsync(readToken(context), context.RequestAborted);
            if (identity is null) return Results.Unauthorized();
            PayPalSettings settings = context.RequestServices.GetRequiredService<PayPalSettings>();
            PostgresTopUpStore store = context.RequestServices.GetRequiredService<PostgresTopUpStore>();
            ShopTopUpOrder? order = await store.FindAsync(orderId, settings.Sandbox, context.RequestAborted);
            if (order is null || order.AccountId != Guid.Parse(identity.AccountId)) return Results.NotFound();
            if (!settings.Configured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            PayPalTopUpService service = context.RequestServices.GetRequiredService<PayPalTopUpService>();
            try
            {
                await service.ReconcileAsync(order, true, false, null, context.RequestAborted);
                return Results.NoContent();
            }
            catch (HttpRequestException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        });
    }

    internal static void MapPayPal(WebApplication app)
    {
        app.MapPost("/api/payments/paypal/webhook", async (HttpContext context, PayPalSettings settings) =>
        {
            PayPalTopUpService? service = context.RequestServices.GetService<PayPalTopUpService>();
            if (!settings.Configured || service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            try
            {
                using JsonDocument notification = await JsonDocument.ParseAsync(context.Request.Body,
                    cancellationToken: context.RequestAborted);
                return await service.NotifyAsync(context.Request.Headers, notification.RootElement, context.RequestAborted)
                    ? Results.Ok() : Results.BadRequest();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
            catch (HttpRequestException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }).WithMetadata(new RequestSizeLimitAttribute(WebhookMaxBytes));

        app.MapGet("/api/payments/paypal/return", async (Guid order, string? token, bool? cancel, HttpContext context,
            PayPalSettings settings) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            PostgresTopUpStore? store = context.RequestServices.GetService<PostgresTopUpStore>();
            PayPalTopUpService? service = context.RequestServices.GetService<PayPalTopUpService>();
            if (!settings.Configured || store is null || service is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            ShopTopUpOrder? record = await store.FindAsync(order, settings.Sandbox, context.RequestAborted);
            if (record is null || !PayPalResponse.IsId(token) || record.PayPalOrderId != token)
                return Results.BadRequest();
            try
            {
                await service.ReconcileAsync(record, cancel != true, cancel == true, null, context.RequestAborted);
            }
            catch (HttpRequestException)
            {
                // PayPal 仍可透過通知重送處理，玩家回遊戲查詢最終結果。
            }

            string gameUrl = HtmlEncoder.Default.Encode(settings.GameUrl!);
            return Results.Content($"""
                <!doctype html><html lang="zh-Hant"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
                <title>WebRPG 儲值</title><main><h1>返回 WebRPG</h1><p>請回原本的遊戲視窗，重新整理商店查看訂單和水晶餘額。</p>
                <a href="{gameUrl}">開啟 WebRPG 網頁版</a></main></html>
                """, "text/html; charset=utf-8");
        });
    }
}
