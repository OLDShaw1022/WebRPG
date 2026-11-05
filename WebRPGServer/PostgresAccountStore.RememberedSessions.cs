using Npgsql;

namespace WebRPGServer;

// 以交易管理可跨瀏覽器重啟的登入憑證及其關聯工作階段。
public sealed partial class PostgresAccountStore
{
    public async Task<RememberedAccountSession> RememberAsync(AccountSession session, CancellationToken cancellationToken)
    {
        string token = AccountTokenHelper.Create();
        Guid family = Guid.NewGuid();
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddDays(AccountTokenHelper.RememberedLifetimeDays);
        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (NpgsqlCommand insert = new("""
            INSERT INTO game_account_remembered_sessions(remembered_session_id, token_hash, account_id, expires_at)
            VALUES ($1, $2, $3, $4);
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(family);
            insert.Parameters.AddWithValue(AccountTokenHelper.Hash(token));
            insert.Parameters.AddWithValue(Guid.Parse(session.AccountId));
            insert.Parameters.AddWithValue(expires);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (NpgsqlCommand attach = new("UPDATE game_account_sessions SET remembered_session_id = $1 WHERE token_hash = $2;", connection, transaction))
        {
            attach.Parameters.AddWithValue(family);
            attach.Parameters.AddWithValue(AccountTokenHelper.Hash(session.AccessToken));
            if (await attach.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Login session is no longer available.");
        }
        await transaction.CommitAsync(cancellationToken);
        return new RememberedAccountSession(session, token, expires);
    }

    public async Task<RememberedAccountSession?> RefreshAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > AccountTokenHelper.MaximumTokenLength) return null;
        string rotatedToken = AccountTokenHelper.Create();
        Guid family;
        Guid accountId;
        DateTimeOffset refreshExpires;
        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        // 原子更新並鎖定該工作階段，確保同一憑證只會成功換發一次。
        await using (NpgsqlCommand rotate = new("""
            UPDATE game_account_remembered_sessions SET token_hash = $2
            WHERE token_hash = $1 AND expires_at > CURRENT_TIMESTAMP
            RETURNING remembered_session_id, account_id, expires_at;
            """, connection, transaction))
        {
            rotate.Parameters.AddWithValue(AccountTokenHelper.Hash(token));
            rotate.Parameters.AddWithValue(AccountTokenHelper.Hash(rotatedToken));
            await using NpgsqlDataReader reader = await rotate.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            family = reader.GetGuid(0);
            accountId = reader.GetGuid(1);
            refreshExpires = reader.GetFieldValue<DateTimeOffset>(2);
        }
        string username;
        await using (NpgsqlCommand account = new("SELECT username FROM game_accounts WHERE account_id = $1;", connection, transaction))
        {
            account.Parameters.AddWithValue(accountId);
            username = (string)(await account.ExecuteScalarAsync(cancellationToken))!;
        }
        await using (NpgsqlCommand remove = new("DELETE FROM game_account_sessions WHERE remembered_session_id = $1;", connection, transaction))
        {
            remove.Parameters.AddWithValue(family);
            await remove.ExecuteNonQueryAsync(cancellationToken);
        }
        string accessToken = AccountTokenHelper.Create();
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddHours(AccountTokenHelper.SessionLifetimeHours);
        if (expires > refreshExpires) expires = refreshExpires;
        await using (NpgsqlCommand insert = new("""
            INSERT INTO game_account_sessions(token_hash, account_id, expires_at, remembered_session_id)
            VALUES ($1, $2, $3, $4);
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(AccountTokenHelper.Hash(accessToken));
            insert.Parameters.AddWithValue(accountId);
            insert.Parameters.AddWithValue(expires);
            insert.Parameters.AddWithValue(family);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new RememberedAccountSession(new AccountSession(accessToken, accountId.ToString("N"), username, expires), rotatedToken, refreshExpires);
    }

    public async Task RevokeRememberedAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > AccountTokenHelper.MaximumTokenLength) return;
        await using NpgsqlCommand command = _dataSource.CreateCommand("DELETE FROM game_account_remembered_sessions WHERE token_hash = $1;");
        command.Parameters.AddWithValue(AccountTokenHelper.Hash(token));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

// 將長效憑證保留於伺服器內部，僅由端點寫入 HttpOnly Cookie。
public sealed record RememberedAccountSession(AccountSession Session, string RefreshToken, DateTimeOffset RefreshExpiresAt);
