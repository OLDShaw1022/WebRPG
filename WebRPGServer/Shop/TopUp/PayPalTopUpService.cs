using System.Text.Json;

namespace WebRPGServer;

// 根據 PayPal 查詢結果處理付款，並讓通知與付款頁回傳共用入帳流程。
internal sealed class PayPalTopUpService(PayPalCheckout checkout, PayPalSettings settings, PostgresTopUpStore store)
{
    internal async Task<ShopTopUpOrder?> CreateAsync(Guid accountId, ShopTopUpRequest request, ShopTopUpOffer? offer,
        CancellationToken cancellationToken)
    {
        ShopTopUpOrder? order = await store.ReserveAsync(accountId, request, offer, settings.Sandbox, cancellationToken);
        if (order is null || order.Offer != request.Offer || order.Sandbox != settings.Sandbox || order.ApprovalUrl is not null)
            return order;
        (string id, string url) = await checkout.CreateAsync(order, cancellationToken);
        return await store.AttachAsync(order, id, url, cancellationToken) ??
            throw new HttpRequestException("PayPal order could not be attached.");
    }

    internal async Task ReconcileAsync(ShopTopUpOrder order, bool capture, bool cancel,
        string? reversedCaptureId, CancellationToken cancellationToken)
    {
        if (order.PayPalOrderId is null || order.Status == ShopTopUpStatus.Reversed) return;
        using JsonDocument document = await checkout.GetOrderAsync(order.PayPalOrderId, cancellationToken);
        JsonElement root = document.RootElement;
        if (!PayPalResponse.TryMatchOrder(root, order, out JsonElement unit))
            throw new HttpRequestException("PayPal order does not match the recorded purchase.");
        string? orderStatus = PayPalResponse.Text(root, "status");

        if (orderStatus == "APPROVED" && capture)
        {
            if (!await checkout.CaptureAsync(order, cancellationToken))
            {
                await store.UpdatePaymentAsync(order, ShopTopUpStatus.Failed, null, cancellationToken);
                return;
            }
            await ReconcileAsync(order, false, false, reversedCaptureId, cancellationToken);
            return;
        }

        if (unit.TryGetProperty("payments", out JsonElement payments) && payments.ValueKind == JsonValueKind.Object &&
            payments.TryGetProperty("captures", out JsonElement captures) && captures.ValueKind == JsonValueKind.Array &&
            captures.GetArrayLength() == 1)
        {
            JsonElement payment = captures[0];
            string? captureId = PayPalResponse.Text(payment, "id");
            if (!PayPalResponse.IsId(captureId) || !PayPalResponse.MatchesAmount(payment, order.Offer))
                throw new HttpRequestException("PayPal capture does not match the recorded purchase.");
            ShopTopUpStatus? status = captureId == reversedCaptureId ? ShopTopUpStatus.Reversed :
                PayPalResponse.Text(payment, "status") switch
                {
                    "COMPLETED" => ShopTopUpStatus.Credited,
                    "PENDING" => ShopTopUpStatus.Processing,
                    "DECLINED" or "FAILED" => ShopTopUpStatus.Failed,
                    "PARTIALLY_REFUNDED" => ShopTopUpStatus.PartiallyRefunded,
                    "REFUNDED" => ShopTopUpStatus.Reversed,
                    _ => null
                };
            if (status is not null)
                await store.UpdatePaymentAsync(order, status.Value, captureId, cancellationToken);
            return;
        }

        if (orderStatus == "APPROVED")
            await store.UpdatePaymentAsync(order, ShopTopUpStatus.Processing, null, cancellationToken);
        else if (orderStatus == "VOIDED" || (cancel && orderStatus is "CREATED" or "PAYER_ACTION_REQUIRED" or "SAVED"))
            await store.UpdatePaymentAsync(order, ShopTopUpStatus.Canceled, null, cancellationToken);
    }

    internal async Task<bool> NotifyAsync(IHeaderDictionary headers, JsonElement notification, CancellationToken cancellationToken)
    {
        if (!await checkout.VerifyAsync(headers, notification, cancellationToken)) return false;
        string? type = PayPalResponse.Text(notification, "event_type");
        if (type is not ("CHECKOUT.ORDER.APPROVED" or "CHECKOUT.PAYMENT-APPROVAL.REVERSED" or
            "PAYMENT.CAPTURE.COMPLETED" or "PAYMENT.CAPTURE.PENDING" or
            "PAYMENT.CAPTURE.DENIED" or "PAYMENT.CAPTURE.DECLINED" or "PAYMENT.CAPTURE.REFUNDED" or "PAYMENT.CAPTURE.REVERSED") ||
            !notification.TryGetProperty("resource", out JsonElement resource) || resource.ValueKind != JsonValueKind.Object)
            return true;

        bool approval = type == "CHECKOUT.ORDER.APPROVED";
        bool approvalReversed = type == "CHECKOUT.PAYMENT-APPROVAL.REVERSED";
        string? externalId = approval || approvalReversed
            ? PayPalWebhookLinks.OrderId(resource) ?? PayPalResponse.Text(resource, "id")
            : PayPalWebhookLinks.OrderId(resource);
        string? captureId = approval ? null : PayPalWebhookLinks.CaptureId(resource, type == "PAYMENT.CAPTURE.REFUNDED");
        ShopTopUpOrder? order = PayPalResponse.IsId(externalId)
            ? await store.FindPaymentAsync(externalId!, settings.Sandbox, false, cancellationToken) : null;
        if (order is null && PayPalResponse.IsId(captureId))
            order = await store.FindPaymentAsync(captureId!, settings.Sandbox, true, cancellationToken);
        if (order is not null)
        {
            if (approvalReversed)
                await store.UpdatePaymentAsync(order, ShopTopUpStatus.Failed, null, cancellationToken);
            await ReconcileAsync(order, approval, false,
                type == "PAYMENT.CAPTURE.REVERSED" ? captureId : null, cancellationToken);
            if (type == "PAYMENT.CAPTURE.REFUNDED" &&
                (await store.FindAsync(order.OrderId, order.Sandbox, cancellationToken))?.Status
                    is not (ShopTopUpStatus.PartiallyRefunded or ShopTopUpStatus.Reversed))
                throw new HttpRequestException("PayPal refund is not reflected in the capture yet.");
        }
        return true;
    }
}
