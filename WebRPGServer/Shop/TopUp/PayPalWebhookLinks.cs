using System.Text.Json;

namespace WebRPGServer;

// 由付款通知擷取交易識別。通知中的網址只用來讀取識別。
internal static class PayPalWebhookLinks
{
    internal static string? OrderId(JsonElement resource) =>
        RelatedId(resource, "order_id") ?? LinkId(resource, "/v2/checkout/orders/");

    internal static string? CaptureId(JsonElement resource, bool refund) =>
        RelatedId(resource, "capture_id") ?? (refund ? LinkId(resource, "/v2/payments/captures/") : PayPalResponse.Text(resource, "id"));

    private static string? RelatedId(JsonElement resource, string name) =>
        resource.TryGetProperty("supplementary_data", out JsonElement data) && data.ValueKind == JsonValueKind.Object &&
        data.TryGetProperty("related_ids", out JsonElement ids) ? PayPalResponse.Text(ids, name) : null;

    private static string? LinkId(JsonElement resource, string prefix)
    {
        if (!resource.TryGetProperty("links", out JsonElement links) || links.ValueKind != JsonValueKind.Array) return null;
        foreach (JsonElement link in links.EnumerateArray())
        {
            if (PayPalResponse.Text(link, "rel") != "up" ||
                !Uri.TryCreate(PayPalResponse.Text(link, "href"), UriKind.Absolute, out Uri? uri) ||
                !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string id = uri.AbsolutePath[prefix.Length..];
            if (PayPalResponse.IsId(id)) return id;
        }
        return null;
    }
}
