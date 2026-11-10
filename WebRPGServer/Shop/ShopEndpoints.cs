namespace WebRPGServer;

// 使用既有帳號 API 驗證提供商品、餘額及兌換操作。
public static class ShopEndpoints
{
    public static void Map(RouteGroupBuilder group, Func<HttpContext, string> readToken)
    {
        group.MapPost("/shop", async (HttpContext context) =>
        {
            PostgresAccountStore accounts = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountIdentity? identity = await accounts.AuthenticateAsync(readToken(context), context.RequestAborted);
            if (identity is null) return Results.Unauthorized();
            PostgresShopStore shop = context.RequestServices.GetRequiredService<PostgresShopStore>();
            ShopCatalog catalog = context.RequestServices.GetRequiredService<ShopCatalog>();

            ShopBalance balance = await shop.GetBalanceAsync(Guid.Parse(identity.AccountId), context.RequestAborted);
            return Results.Ok(new { balance, offers = catalog.Offers });
        });

        group.MapPost("/shop/exchange", async (ShopExchangeRequest request, HttpContext context) =>
        {
            PostgresAccountStore accounts = context.RequestServices.GetRequiredService<PostgresAccountStore>();
            AccountIdentity? identity = await accounts.AuthenticateAsync(readToken(context), context.RequestAborted);
            if (identity is null) return Results.Unauthorized();
            PostgresShopStore shop = context.RequestServices.GetRequiredService<PostgresShopStore>();
            ShopCatalog catalog = context.RequestServices.GetRequiredService<ShopCatalog>();
            if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.OfferId) ||
                request.OfferId.Length > ShopCatalog.MaximumOfferIdLength || request.CrystalCost <= 0 || request.GoldAmount <= 0)
                return Results.BadRequest();

            ShopExchangeResult result = await shop.ExchangeAsync(Guid.Parse(identity.AccountId), request,
                catalog.Find(request.OfferId), context.RequestAborted);
            return Results.Ok(result);
        });
    }
}
