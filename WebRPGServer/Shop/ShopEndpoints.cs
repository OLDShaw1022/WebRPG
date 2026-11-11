namespace WebRPGServer;

// 使用既有帳號 API 驗證提供商品、餘額及兌換操作。
public static class ShopEndpoints
{
    public static void Map(RouteGroupBuilder group, Func<HttpContext, string> readToken)
    {
        ShopTopUpEndpoints.MapPlayer(group, readToken);
        group.MapPost("/shop", async (HttpContext context, PayPalSettings payPal, ShopTopUpCatalog topUps) =>
        {
            PostgresAccountStore accounts = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountIdentity? identity = await accounts.AuthenticateAsync(readToken(context), context.RequestAborted);
            if (identity is null) return Results.Unauthorized();
            PostgresShopStore shop = context.RequestServices.GetRequiredService<PostgresShopStore>();
            ShopCatalog catalog = context.RequestServices.GetRequiredService<ShopCatalog>();

            ShopBalance balance = await shop.GetBalanceAsync(Guid.Parse(identity.AccountId), context.RequestAborted);
            PostgresTopUpStore topUpStore = context.RequestServices.GetRequiredService<PostgresTopUpStore>();
            return Results.Ok(new
            {
                balance,
                offers = catalog.Offers,
                topUpOffers = payPal.Enabled && payPal.Configured ? topUps.Offers : Array.Empty<ShopTopUpOffer>(),
                topUpOrders = await topUpStore.ListAsync(Guid.Parse(identity.AccountId), payPal.Sandbox, context.RequestAborted)
            });
        });

        group.MapPost("/shop/exchange", async (ShopExchangeRequest request, HttpContext context) =>
        {
            PostgresAccountStore accounts = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountIdentity? identity = await accounts.AuthenticateAsync(readToken(context), context.RequestAborted);
            if (identity is null) return Results.Unauthorized();
            PostgresShopStore shop = context.RequestServices.GetRequiredService<PostgresShopStore>();
            ShopCatalog catalog = context.RequestServices.GetRequiredService<ShopCatalog>();
            if (request.RequestId == Guid.Empty || !request.ConfirmedOffer.IsValid)
                return Results.BadRequest();

            ShopExchangeResult result = await shop.ExchangeAsync(Guid.Parse(identity.AccountId), request,
                catalog.Find(request.OfferId), context.RequestAborted);
            return Results.Ok(result);
        });
    }
}
