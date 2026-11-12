using Npgsql;

namespace WebRPGServer;

// 在 PostgreSQL 保存帳號、唯一 Email 與可撤銷的登入工作階段。
public sealed partial class PostgresAccountStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _dummyHash = AccountPasswordHelper.Hash(Guid.NewGuid().ToString());

    public PostgresAccountStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS game_accounts (
                account_id UUID PRIMARY KEY,
                normalized_email VARCHAR(254) NOT NULL UNIQUE,
                username VARCHAR(24) NOT NULL,
                password_hash TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS game_account_remembered_sessions (
                remembered_session_id UUID PRIMARY KEY,
                token_hash TEXT NOT NULL UNIQUE,
                account_id UUID NOT NULL REFERENCES game_accounts(account_id) ON DELETE CASCADE,
                expires_at TIMESTAMPTZ NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_game_account_remembered_sessions_expires_at
                ON game_account_remembered_sessions(expires_at);
            CREATE TABLE IF NOT EXISTS game_account_sessions (
                token_hash TEXT PRIMARY KEY,
                account_id UUID NOT NULL REFERENCES game_accounts(account_id) ON DELETE CASCADE,
                expires_at TIMESTAMPTZ NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_game_account_sessions_expires_at ON game_account_sessions(expires_at);
            ALTER TABLE game_account_sessions ADD COLUMN IF NOT EXISTS remembered_session_id UUID
                REFERENCES game_account_remembered_sessions(remembered_session_id) ON DELETE CASCADE;
            CREATE INDEX IF NOT EXISTS ix_game_account_sessions_remembered_session_id
                ON game_account_sessions(remembered_session_id);
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RegisterAsync(AccountRegistration registration, CancellationToken cancellationToken)
    {
        AccountRegistration normalized = AccountValidation.Normalize(registration);
        string hash = AccountPasswordHelper.Hash(normalized.Password);
        await using NpgsqlCommand command = _dataSource.CreateCommand("""
            INSERT INTO game_accounts(account_id, normalized_email, username, password_hash)
            VALUES ($1, $2, $3, $4) ON CONFLICT (normalized_email) DO NOTHING;
            """);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(normalized.Email);
        command.Parameters.AddWithValue(normalized.Username);
        command.Parameters.AddWithValue(hash);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new ArgumentException("此 Email 無法註冊，請使用其他 Email 或嘗試登入。");
    }

    public async Task<AccountSession?> LoginAsync(AccountLogin login, CancellationToken cancellationToken)
    {
        if (login.Password is null || login.Password.Length > AccountValidation.MaximumPasswordLength) return null;
        string email;
        try { email = AccountValidation.NormalizeEmail(login.Email); }
        catch (ArgumentException) { return null; }
        Guid accountId = Guid.Empty;
        string username = string.Empty;
        string hash = _dummyHash;
        await using (NpgsqlCommand command = _dataSource.CreateCommand(
            "SELECT account_id, username, password_hash FROM game_accounts WHERE normalized_email = $1;"))
        {
            command.Parameters.AddWithValue(email);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                accountId = reader.GetGuid(0);
                username = reader.GetString(1);
                hash = reader.GetString(2);
            }
        }
        bool valid = AccountPasswordHelper.Verify(hash, login.Password);
        if (!valid || accountId == Guid.Empty) return null;
        string token = AccountTokenHelper.Create();
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddHours(AccountTokenHelper.SessionLifetimeHours);
        await using (NpgsqlCommand cleanup = _dataSource.CreateCommand(
            "DELETE FROM game_account_remembered_sessions WHERE expires_at <= CURRENT_TIMESTAMP; DELETE FROM " +
                "game_account_sessions WHERE expires_at <= CURRENT_TIMESTAMP;"))
        {
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }
        await using NpgsqlCommand insert = _dataSource.CreateCommand("""
            INSERT INTO game_account_sessions(token_hash, account_id, expires_at) VALUES ($1, $2, $3);
            """);
        insert.Parameters.AddWithValue(AccountTokenHelper.Hash(token));
        insert.Parameters.AddWithValue(accountId);
        insert.Parameters.AddWithValue(expires);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return new AccountSession(token, accountId.ToString("N"), username, expires);
    }

    public async Task<AccountIdentity?> AuthenticateAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > AccountTokenHelper.MaximumTokenLength) return null;
        await using NpgsqlCommand command = _dataSource.CreateCommand("""
            SELECT a.account_id, a.username FROM game_account_sessions s
            JOIN game_accounts a ON a.account_id = s.account_id
            WHERE s.token_hash = $1 AND s.expires_at > CURRENT_TIMESTAMP;
            """);
        command.Parameters.AddWithValue(AccountTokenHelper.Hash(token));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AccountIdentity(reader.GetGuid(0).ToString("N"), reader.GetString(1)) : null;
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            WITH revoked AS (
                DELETE FROM game_account_remembered_sessions WHERE remembered_session_id IN (
                    SELECT remembered_session_id FROM game_account_sessions WHERE token_hash = $1
                )
            )
            DELETE FROM game_account_sessions WHERE token_hash = $1;
            """);
        command.Parameters.AddWithValue(AccountTokenHelper.Hash(token));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

}
