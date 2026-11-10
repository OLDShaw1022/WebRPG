using System.Text.Json;
using Npgsql;

namespace WebRPGServer;

// 保存帳號錢包及兌換流水，並序列化同一帳號的兌換。
public sealed class PostgresShopStore(NpgsqlDataSource source)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = source.CreateCommand("""
            CREATE TABLE IF NOT EXISTS game_wallets (
                account_id UUID PRIMARY KEY REFERENCES game_accounts(account_id) ON DELETE CASCADE,
                gold BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0),
                crystals BIGINT NOT NULL DEFAULT 0 CHECK (crystals >= 0)
            );
            CREATE TABLE IF NOT EXISTS game_shop_exchanges (
                account_id UUID NOT NULL REFERENCES game_wallets(account_id) ON DELETE CASCADE,
                request_id UUID NOT NULL,
                offer_id TEXT NOT NULL,
                crystal_cost BIGINT NOT NULL,
                gold_amount BIGINT NOT NULL,
                result JSONB NOT NULL,
                PRIMARY KEY (account_id, request_id)
            );
            CREATE TABLE IF NOT EXISTS game_wallet_entries (
                account_id UUID NOT NULL REFERENCES game_wallets(account_id) ON DELETE CASCADE,
                source TEXT NOT NULL,
                reference_id UUID NOT NULL,
                currency TEXT NOT NULL CHECK (currency IN ('gold', 'crystals')),
                delta BIGINT NOT NULL CHECK (delta <> 0),
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (account_id, source, reference_id, currency)
            );
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShopBalance> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = source.CreateCommand("SELECT gold, crystals FROM game_wallets WHERE account_id = $1;");
        command.Parameters.AddWithValue(accountId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ShopBalance(reader.GetInt64(0), reader.GetInt64(1))
            : new ShopBalance(0, 0);
    }

    public async Task<ShopExchangeResult> ExchangeAsync(Guid accountId, ShopExchangeRequest request, ShopOffer? offer,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (NpgsqlCommand initialize = new("""
            INSERT INTO game_wallets (account_id) VALUES ($1) ON CONFLICT DO NOTHING;
            """, connection, transaction))
        {
            initialize.Parameters.AddWithValue(accountId);
            await initialize.ExecuteNonQueryAsync(cancellationToken);
        }

        ShopBalance balance;
        await using (NpgsqlCommand wallet = new("""
            SELECT gold, crystals FROM game_wallets WHERE account_id = $1 FOR UPDATE;
            """, connection, transaction))
        {
            wallet.Parameters.AddWithValue(accountId);
            await using NpgsqlDataReader reader = await wallet.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            balance = new ShopBalance(reader.GetInt64(0), reader.GetInt64(1));
        }

        await using (NpgsqlCommand previous = new("""
            SELECT offer_id, crystal_cost, gold_amount, result
            FROM game_shop_exchanges WHERE account_id = $1 AND request_id = $2;
            """, connection, transaction))
        {
            previous.Parameters.AddWithValue(accountId);
            previous.Parameters.AddWithValue(request.RequestId);
            await using NpgsqlDataReader reader = await previous.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                ShopOffer original = new(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2));
                if (original != new ShopOffer(request.OfferId, request.CrystalCost, request.GoldAmount))
                    return new ShopExchangeResult(ShopExchangeStatus.RequestConflict, balance, offer);
                return JsonSerializer.Deserialize<ShopExchangeResult>(reader.GetString(3))!;
            }
        }

        ShopExchangeStatus status = ShopExchangeStatus.Exchanged;
        if (offer != new ShopOffer(request.OfferId, request.CrystalCost, request.GoldAmount))
            status = ShopExchangeStatus.OfferChanged;
        else if (balance.Crystals < request.CrystalCost)
            status = ShopExchangeStatus.InsufficientCrystals;
        else if (balance.Gold > long.MaxValue - request.GoldAmount)
            status = ShopExchangeStatus.BalanceLimit;

        bool exchanged = status == ShopExchangeStatus.Exchanged;
        ShopBalance updated = exchanged
            ? new ShopBalance(balance.Gold + request.GoldAmount, balance.Crystals - request.CrystalCost)
            : balance;
        ShopExchangeResult result = new(status, updated, offer);
        await using NpgsqlCommand exchange = new("""
            WITH updated_wallet AS (
                UPDATE game_wallets SET gold = $6, crystals = $7 WHERE account_id = $1 AND $8 RETURNING account_id
            ), receipt AS (
                INSERT INTO game_shop_exchanges (account_id, request_id, offer_id, crystal_cost, gold_amount, result)
                VALUES ($1, $2, $3, $4, $5, CAST($9 AS JSONB))
            )
            INSERT INTO game_wallet_entries (account_id, source, reference_id, currency, delta)
            SELECT account_id, 'exchange', $2, movement.currency, movement.delta
            FROM updated_wallet CROSS JOIN (VALUES ('crystals', -CAST($4 AS BIGINT)), ('gold', $5)) AS movement(currency, delta);
            """, connection, transaction);
        exchange.Parameters.AddWithValue(accountId);
        exchange.Parameters.AddWithValue(request.RequestId);
        exchange.Parameters.AddWithValue(request.OfferId);
        exchange.Parameters.AddWithValue(request.CrystalCost);
        exchange.Parameters.AddWithValue(request.GoldAmount);
        exchange.Parameters.AddWithValue(updated.Gold);
        exchange.Parameters.AddWithValue(updated.Crystals);
        exchange.Parameters.AddWithValue(exchanged);
        exchange.Parameters.AddWithValue(JsonSerializer.Serialize(result));
        await exchange.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
