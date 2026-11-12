using Npgsql;

namespace WebRPGServer;

// 保存儲值訂單，並在同一交易內更新訂單、錢包與流水。
internal sealed class PostgresTopUpStore(NpgsqlDataSource source)
{
    private const string OrderColumns = """
        order_id, account_id, offer_id, currency, amount, crystals, status, created_at,
        paypal_order_id, approval_url, capture_id, sandbox
        """;
    private const int HistoryLimit = 30;

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = source.CreateCommand("""
            ALTER TABLE game_wallets ADD COLUMN IF NOT EXISTS gold_debt NUMERIC NOT NULL DEFAULT 0
                CHECK (gold_debt >= 0);
            CREATE TABLE IF NOT EXISTS game_shop_top_ups (
                order_id UUID PRIMARY KEY,
                account_id UUID NOT NULL REFERENCES game_accounts(account_id) ON DELETE CASCADE,
                request_id UUID NOT NULL,
                offer_id TEXT NOT NULL,
                currency TEXT NOT NULL,
                amount NUMERIC NOT NULL CHECK (amount > 0),
                crystals BIGINT NOT NULL CHECK (crystals > 0),
                remaining BIGINT NOT NULL DEFAULT 0 CHECK (remaining >= 0 AND remaining <= crystals),
                status INTEGER NOT NULL,
                sandbox BOOLEAN NOT NULL,
                paypal_order_id TEXT,
                approval_url TEXT,
                capture_id TEXT,
                credited BOOLEAN NOT NULL DEFAULT FALSE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE (account_id, request_id),
                UNIQUE (sandbox, paypal_order_id),
                UNIQUE (sandbox, capture_id)
            );
            CREATE INDEX IF NOT EXISTS game_shop_top_ups_history ON game_shop_top_ups(account_id, created_at DESC);
            CREATE TABLE IF NOT EXISTS game_shop_top_up_allocations (
                account_id UUID NOT NULL,
                request_id UUID NOT NULL,
                order_id UUID NOT NULL REFERENCES game_shop_top_ups(order_id) ON DELETE CASCADE,
                crystals BIGINT NOT NULL CHECK (crystals > 0),
                reversed BOOLEAN NOT NULL DEFAULT FALSE,
                PRIMARY KEY (account_id, request_id, order_id),
                FOREIGN KEY (account_id, request_id)
                    REFERENCES game_shop_exchanges(account_id, request_id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS game_shop_top_up_allocations_order
                ON game_shop_top_up_allocations(order_id) WHERE NOT reversed;
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task<ShopTopUpOrder?> ReserveAsync(Guid accountId, ShopTopUpRequest request,
        ShopTopUpOffer? currentOffer, bool sandbox, CancellationToken cancellationToken)
    {
        if (request.Offer == currentOffer)
        {
            await using NpgsqlCommand insert = source.CreateCommand("""
                INSERT INTO game_shop_top_ups
                    (order_id, account_id, request_id, offer_id, currency, amount, crystals, status, sandbox)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                ON CONFLICT (account_id, request_id) DO NOTHING;
                """);
            insert.Parameters.AddWithValue(Guid.NewGuid());
            insert.Parameters.AddWithValue(accountId);
            insert.Parameters.AddWithValue(request.RequestId);
            insert.Parameters.AddWithValue(request.Offer.Id);
            insert.Parameters.AddWithValue(request.Offer.Currency);
            insert.Parameters.AddWithValue(request.Offer.Amount);
            insert.Parameters.AddWithValue(request.Offer.Crystals);
            insert.Parameters.AddWithValue((int)ShopTopUpStatus.AwaitingPayment);
            insert.Parameters.AddWithValue(sandbox);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using NpgsqlCommand query = source.CreateCommand(
            $"SELECT {OrderColumns} FROM game_shop_top_ups WHERE account_id = $1 AND request_id = $2;");
        query.Parameters.AddWithValue(accountId);
        query.Parameters.AddWithValue(request.RequestId);
        return await ReadFirstAsync(query, cancellationToken);
    }

    internal async Task<ShopTopUpOrder?> FindAsync(Guid orderId, bool sandbox, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand query = source.CreateCommand(
            $"SELECT {OrderColumns} FROM game_shop_top_ups WHERE order_id = $1 AND sandbox = $2;");
        query.Parameters.AddWithValue(orderId);
        query.Parameters.AddWithValue(sandbox);
        return await ReadFirstAsync(query, cancellationToken);
    }

    internal async Task<ShopTopUpOrder?> FindPaymentAsync(string externalId, bool sandbox, bool byCapture,
        CancellationToken cancellationToken)
    {
        string column = byCapture ? "capture_id" : "paypal_order_id";
        await using NpgsqlCommand query = source.CreateCommand(
            $"SELECT {OrderColumns} FROM game_shop_top_ups WHERE {column} = $1 AND sandbox = $2;");
        query.Parameters.AddWithValue(externalId);
        query.Parameters.AddWithValue(sandbox);
        return await ReadFirstAsync(query, cancellationToken);
    }

    internal async Task<IReadOnlyList<ShopTopUpReceipt>> ListAsync(
        Guid accountId, bool sandbox, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand query = source.CreateCommand($"""
            SELECT {OrderColumns} FROM game_shop_top_ups WHERE account_id = $1 AND sandbox = $2
            ORDER BY created_at DESC, order_id DESC LIMIT $3;
            """);
        query.Parameters.AddWithValue(accountId);
        query.Parameters.AddWithValue(sandbox);
        query.Parameters.AddWithValue(HistoryLimit);
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
        List<ShopTopUpReceipt> receipts = [];
        while (await reader.ReadAsync(cancellationToken)) receipts.Add(ReadOrder(reader).Receipt);
        return receipts;
    }

    internal async Task<ShopTopUpOrder?> AttachAsync(ShopTopUpOrder order, string externalId, string url,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand update = source.CreateCommand($"""
            UPDATE game_shop_top_ups SET paypal_order_id = $2, approval_url = $3
            WHERE order_id = $1 AND (paypal_order_id IS NULL OR paypal_order_id = $2) RETURNING {OrderColumns};
            """);
        update.Parameters.AddWithValue(order.OrderId);
        update.Parameters.AddWithValue(externalId);
        update.Parameters.AddWithValue(url);
        return await ReadFirstAsync(update, cancellationToken);
    }

    internal async Task UpdatePaymentAsync(ShopTopUpOrder order, ShopTopUpStatus status, string? captureId,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (NpgsqlCommand initialize = new("""
            INSERT INTO game_wallets(account_id) VALUES ($1) ON CONFLICT DO NOTHING;
            """, connection, transaction))
        {
            initialize.Parameters.AddWithValue(order.AccountId);
            await initialize.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (NpgsqlCommand wallet = new(
            "SELECT 1 FROM game_wallets WHERE account_id = $1 FOR UPDATE;", connection, transaction))
        {
            wallet.Parameters.AddWithValue(order.AccountId);
            await wallet.ExecuteScalarAsync(cancellationToken);
        }

        ShopTopUpStatus current;
        bool credited;
        await using (NpgsqlCommand query = new("""
            SELECT status, credited, capture_id FROM game_shop_top_ups WHERE order_id = $1 FOR UPDATE;
            """, connection, transaction))
        {
            query.Parameters.AddWithValue(order.OrderId);
            await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return;
            current = (ShopTopUpStatus)reader.GetInt32(0);
            credited = reader.GetBoolean(1);
            if (captureId is not null && !reader.IsDBNull(2) && reader.GetString(2) != captureId) return;
        }

        if (current == ShopTopUpStatus.Reversed ||
            (credited && status is not (ShopTopUpStatus.PartiallyRefunded or ShopTopUpStatus.Reversed)) ||
            (current is ShopTopUpStatus.Failed or ShopTopUpStatus.Canceled && status == ShopTopUpStatus.Processing) ||
            (status == ShopTopUpStatus.Canceled && current != ShopTopUpStatus.AwaitingPayment))
            return;

        bool grant = !credited && status is ShopTopUpStatus.Credited or ShopTopUpStatus.PartiallyRefunded;
        if (grant)
        {
            await using NpgsqlCommand credit = new("""
                WITH credited_wallet AS (
                    UPDATE game_wallets SET crystals = crystals + $3 WHERE account_id = $1 RETURNING account_id
                )
                INSERT INTO game_wallet_entries(account_id, source, reference_id, currency, delta)
                SELECT account_id, 'top-up', $2, 'crystals', $3 FROM credited_wallet;
                """, connection, transaction);
            credit.Parameters.AddWithValue(order.AccountId);
            credit.Parameters.AddWithValue(order.OrderId);
            credit.Parameters.AddWithValue(order.Offer.Crystals);
            await credit.ExecuteNonQueryAsync(cancellationToken);
        }

        if (credited && status == ShopTopUpStatus.Reversed)
            await TopUpReversal.ApplyAsync(connection, transaction, order, cancellationToken);

        await using NpgsqlCommand update = new("""
            UPDATE game_shop_top_ups SET status = $2, capture_id = COALESCE($3, capture_id),
                credited = credited OR $4, remaining = CASE WHEN $4 THEN crystals WHEN $5 THEN 0 ELSE remaining END
            WHERE order_id = $1;
            """, connection, transaction);
        update.Parameters.AddWithValue(order.OrderId);
        update.Parameters.AddWithValue((int)status);
        update.Parameters.Add(
            new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)captureId
            ?? DBNull.Value });
        update.Parameters.AddWithValue(grant);
        update.Parameters.AddWithValue(status == ShopTopUpStatus.Reversed);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<ShopTopUpOrder?> ReadFirstAsync(
        NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadOrder(reader) : null;
    }

    private static ShopTopUpOrder ReadOrder(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1),
        new ShopTopUpOffer(reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetInt64(5)),
        (ShopTopUpStatus)reader.GetInt32(6), reader.GetDateTime(7),
        reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetBoolean(11));
}
