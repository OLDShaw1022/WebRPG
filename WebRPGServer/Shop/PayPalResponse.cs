using System.Globalization;
using System.Text.Json;

namespace WebRPGServer;

// 核對 PayPal 訂單、商品條件及請款明細。
internal static class PayPalResponse
{
    internal static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    internal static bool IsId(string? value) => value is { Length: > 0 and <= 64 } && value.All(char.IsAsciiLetterOrDigit);

    internal static bool MatchesAmount(JsonElement parent, ShopTopUpOffer offer) =>
        parent.TryGetProperty("amount", out JsonElement amount) &&
        Text(amount, "currency_code") == offer.Currency &&
        decimal.TryParse(Text(amount, "value"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal total) &&
        total == offer.Amount;

    internal static bool TryMatchOrder(JsonElement root, ShopTopUpOrder order, out JsonElement unit)
    {
        unit = default;
        if (Text(root, "id") != order.PayPalOrderId || Text(root, "intent") != "CAPTURE" ||
            !root.TryGetProperty("purchase_units", out JsonElement units) || units.ValueKind != JsonValueKind.Array ||
            units.GetArrayLength() != 1)
            return false;
        unit = units[0];
        return Text(unit, "custom_id") == order.OrderId.ToString("N") && MatchesAmount(unit, order.Offer);
    }
}
