using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WebRPGServer;

// 透過固定的 PayPal REST API 建立及請款訂單，並驗證通知。
internal sealed class PayPalCheckout(HttpClient client, PayPalSettings settings)
{
    internal async Task<(string Id, string Url)> CreateAsync(ShopTopUpOrder order, CancellationToken cancellationToken)
    {
        string orderKey = order.OrderId.ToString("N");
        string returnUrl = QueryHelpers.AddQueryString(settings.CallbackBase!, "order", orderKey);
        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[] { new
            {
                reference_id = orderKey,
                custom_id = orderKey,
                invoice_id = orderKey,
                description = "WebRPG crystals",
                amount = new { currency_code = order.Offer.Currency, value = order.Offer.Amount.ToString("0.##", CultureInfo.InvariantCulture) }
            } },
            payment_source = new { paypal = new { experience_context = new
            {
                shipping_preference = "NO_SHIPPING",
                user_action = "PAY_NOW",
                return_url = returnUrl,
                cancel_url = QueryHelpers.AddQueryString(returnUrl, "cancel", "true")
            } } }
        };
        using JsonDocument result = await SendAsync(HttpMethod.Post, "v2/checkout/orders", body, orderKey, cancellationToken);
        JsonElement root = result.RootElement;
        string? id = PayPalResponse.Text(root, "id");
        if (!PayPalResponse.IsId(id)) throw new HttpRequestException("PayPal did not return an order ID.");
        if (root.TryGetProperty("links", out JsonElement links))
        {
            foreach (JsonElement link in links.EnumerateArray())
            {
                if (PayPalResponse.Text(link, "rel") is not ("approve" or "payer-action")) continue;
                string? url = PayPalResponse.Text(link, "href");
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps &&
                    uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
                    uri.Host == (settings.Sandbox ? "www.sandbox.paypal.com" : "www.paypal.com"))
                    return (id!, url!);
            }
        }
        throw new HttpRequestException("PayPal did not return a trusted approval URL.");
    }

    internal async Task<JsonDocument> GetOrderAsync(string orderId, CancellationToken cancellationToken) =>
        await SendAsync(HttpMethod.Get, "v2/checkout/orders/" + orderId, null, null, cancellationToken);

    internal async Task<bool> CaptureAsync(ShopTopUpOrder order, CancellationToken cancellationToken)
    {
        string path = "v2/checkout/orders/" + order.PayPalOrderId + "/capture";
        using JsonDocument result = await SendAsync(HttpMethod.Post, path, new { },
            order.OrderId.ToString("D"), cancellationToken, allowUnprocessable: true);
        JsonElement root = result.RootElement;
        if (!root.TryGetProperty("details", out JsonElement details) || details.ValueKind != JsonValueKind.Array)
            return true;
        foreach (JsonElement detail in details.EnumerateArray())
        {
            string? issue = PayPalResponse.Text(detail, "issue");
            if (issue is "ORDER_ALREADY_CAPTURED" or "ORDER_NOT_APPROVED") return true;
            if (issue is "INSTRUMENT_DECLINED" or "PAYER_CANNOT_PAY" or "TRANSACTION_REFUSED") return false;
        }
        throw new HttpRequestException("PayPal did not accept capture.");
    }

    internal async Task<bool> VerifyAsync(IHeaderDictionary headers, JsonElement notification, CancellationToken cancellationToken)
    {
        string transmissionId = headers["PAYPAL-TRANSMISSION-ID"].ToString();
        string transmissionTime = headers["PAYPAL-TRANSMISSION-TIME"].ToString();
        string certUrl = headers["PAYPAL-CERT-URL"].ToString();
        string algorithm = headers["PAYPAL-AUTH-ALGO"].ToString();
        string signature = headers["PAYPAL-TRANSMISSION-SIG"].ToString();
        if (new[] { transmissionId, transmissionTime, certUrl, algorithm, signature }.Any(string.IsNullOrWhiteSpace))
            return false;
        var body = new
        {
            transmission_id = transmissionId, transmission_time = transmissionTime, cert_url = certUrl,
            auth_algo = algorithm, transmission_sig = signature, webhook_id = settings.WebhookId,
            webhook_event = notification
        };
        using JsonDocument result = await SendAsync(HttpMethod.Post, "v1/notifications/verify-webhook-signature",
            body, null, cancellationToken);
        return PayPalResponse.Text(result.RootElement, "verification_status") == "SUCCESS";
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, string? requestId,
        CancellationToken cancellationToken, bool allowUnprocessable = false)
    {
        using HttpRequestMessage tokenRequest = new(HttpMethod.Post, "v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        tokenRequest.Headers.Authorization = new("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.ClientId + ":" + settings.ClientSecret)));
        using HttpResponseMessage tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        using JsonDocument token = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PayPalResponse.Text(token.RootElement, "access_token"));
        if (body is not null) request.Content = JsonContent.Create(body);
        if (requestId is not null) request.Headers.Add("PayPal-Request-Id", requestId);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!allowUnprocessable || response.StatusCode != HttpStatusCode.UnprocessableEntity)
            response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
    }
}
