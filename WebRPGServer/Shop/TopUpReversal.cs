using Npgsql;

namespace WebRPGServer;

// 撤銷用過該筆儲值的兌換，退回其他來源的水晶，並回收金幣。
internal static class TopUpReversal
{
    internal static async Task ApplyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ShopTopUpOrder order, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new("""
            WITH reversed_shop_exchanges AS (
                SELECT e.request_id, e.crystal_cost, e.gold_amount
                FROM game_shop_exchanges e JOIN game_shop_top_up_allocations a USING (account_id, request_id)
                WHERE e.account_id = $1 AND a.order_id = $2 AND NOT a.reversed
            ), restored AS (
                SELECT a.order_id, SUM(a.crystals) AS crystals
                FROM game_shop_top_up_allocations a JOIN reversed_shop_exchanges e USING (request_id)
                WHERE a.account_id = $1 AND a.order_id <> $2 AND NOT a.reversed GROUP BY a.order_id
            ), restored_orders AS (
                UPDATE game_shop_top_ups t SET remaining = t.remaining + r.crystals
                FROM restored r WHERE t.order_id = r.order_id
            ), totals AS (
                SELECT COALESCE(SUM(e.gold_amount), 0) AS gold,
                       COALESCE(SUM(e.crystal_cost - a.crystals), 0) AS restored_crystals
                FROM reversed_shop_exchanges e JOIN game_shop_top_up_allocations a USING (request_id)
                WHERE a.account_id = $1 AND a.order_id = $2 AND NOT a.reversed
            ), movements AS (
                SELECT LEAST(w.gold, totals.gold)::bigint AS gold,
                    (totals.restored_crystals - t.remaining)::bigint AS crystals,
                    GREATEST(totals.gold - w.gold, 0) AS debt
                FROM game_wallets w CROSS JOIN totals CROSS JOIN game_shop_top_ups t
                WHERE w.account_id = $1 AND t.order_id = $2
            ), updated AS (
                UPDATE game_wallets w SET gold = w.gold - m.gold, crystals = w.crystals + m.crystals,
                    gold_debt = w.gold_debt + m.debt
                FROM movements m WHERE w.account_id = $1 RETURNING m.gold, m.crystals
            ), recorded AS (
                INSERT INTO game_wallet_entries(account_id, source, reference_id, currency, delta)
                    SELECT $1, 'top-up-reversal', $2, movement.currency, movement.delta
                    FROM updated CROSS JOIN LATERAL (VALUES ('gold', -updated.gold), ('crystals', updated.crystals)) movement(currency, delta)
                    WHERE movement.delta <> 0
            )
            UPDATE game_shop_top_up_allocations a SET reversed = TRUE
                FROM reversed_shop_exchanges e WHERE a.account_id = $1 AND a.request_id = e.request_id AND NOT a.reversed;
            """, connection, transaction);
        command.Parameters.AddWithValue(order.AccountId);
        command.Parameters.AddWithValue(order.OrderId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
