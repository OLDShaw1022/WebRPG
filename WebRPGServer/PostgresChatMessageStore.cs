using Npgsql;
using WebRPGShared;

namespace WebRPGServer;

// 使用 PostgreSQL 建立聊天資料表、儲存訊息並查詢近期紀錄。
public sealed class PostgresChatMessageStore : IChatMessageStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresChatMessageStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            CREATE TABLE IF NOT EXISTS chat_messages (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                username VARCHAR(24) NOT NULL,
                message TEXT NOT NULL,
                connection_id VARCHAR(32) NOT NULL,
                timestamp_utc TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_chat_messages_timestamp_utc
                ON chat_messages (timestamp_utc DESC, id DESC);
            """);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveMessageAsync(
        ChatPacket message,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            INSERT INTO chat_messages (username, message, connection_id, timestamp_utc)
            VALUES ($1, $2, $3, $4);
            """);
        command.Parameters.AddWithValue(message.Username);
        command.Parameters.AddWithValue(message.Text);
        command.Parameters.AddWithValue(message.ConnectionId);
        command.Parameters.AddWithValue(message.TimestampUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatPacket>> GetRecentMessagesAsync(
        int count,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(
            """
            SELECT username, message, connection_id, timestamp_utc
            FROM chat_messages
            ORDER BY timestamp_utc DESC, id DESC
            LIMIT $1;
            """);
        command.Parameters.AddWithValue(count);

        List<ChatPacket> messages = new();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new ChatPacket
            {
                Type = ChatPacketType.Chat,
                Username = reader.GetString(0),
                Text = reader.GetString(1),
                ConnectionId = reader.GetString(2),
                TimestampUtc = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero)
            });
        }

        messages.Reverse();
        return messages;
    }
}