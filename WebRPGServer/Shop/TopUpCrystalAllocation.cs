using Npgsql;

namespace WebRPGServer;

// 優先使用無儲值來源的水晶，再依入帳順序記錄付費水晶的使用。
internal static class TopUpCrystalAllocation
{
    internal static async Task RecordAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        Guid requestId, long previousBalance, long crystalCost, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new("""
            WITH eligible AS (
                SELECT order_id, remaining,
                    SUM(remaining) OVER () AS total,
                    COALESCE(SUM(remaining) OVER (ORDER BY created_at, order_id
                        ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS earlier
                FROM game_shop_top_ups WHERE account_id = $1 AND remaining > 0
            ), allocation AS (
                SELECT order_id, LEAST(remaining, GREATEST($4 - GREATEST($3 - total, 0) - earlier, 0))::bigint AS used
                FROM eligible
            ), recorded AS (
                INSERT INTO game_shop_top_up_allocations(account_id, request_id, order_id, crystals)
                    SELECT $1, $2, order_id, used FROM allocation WHERE used > 0
                RETURNING order_id, crystals
            )
            UPDATE game_shop_top_ups t SET remaining = t.remaining - r.crystals FROM recorded r WHERE t.order_id = r.order_id;
            """, connection, transaction);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(requestId);
        command.Parameters.AddWithValue(previousBalance);
        command.Parameters.AddWithValue(crystalCost);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
