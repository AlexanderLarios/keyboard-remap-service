using KeyRemap.Api.Domain;
using Microsoft.Data.Sqlite;

namespace KeyRemap.Api.Data;

public sealed class SqliteKeyboardRepository(string connectionString) : IKeyboardRepository
{
    private const string ReadMappingsSql = """
        SELECT k.hid_code, k.name,
               t.hid_code AS target_code, t.name AS target_name,
               t.hid_code <> k.hid_code AS remapped
        FROM keyboard_keys k
        LEFT JOIN key_mappings m
               ON m.keyboard_id = k.keyboard_id AND m.source_code = k.hid_code
        JOIN keyboard_keys t
               ON t.keyboard_id = k.keyboard_id
              AND t.hid_code = COALESCE(m.target_code, k.hid_code)
        WHERE k.keyboard_id = @keyboardId
        ORDER BY k.hid_code;
        """;

    private const string UpsertMappingSql = """
        INSERT INTO key_mappings (keyboard_id, source_code, target_code)
        VALUES (@keyboardId, @source, @target)
        ON CONFLICT (keyboard_id, source_code)
        DO UPDATE SET target_code = excluded.target_code;
        """;

    public async Task<Keyboard?> FindKeyboardAsync(string name, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);

        int id;
        string storedName;
        await using (var find = connection.CreateCommand())
        {
            find.CommandText = "SELECT id, name FROM keyboards WHERE name = @name";
            find.Parameters.AddWithValue("@name", name);
            await using var reader = await find.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            id = reader.GetInt32(0);
            storedName = reader.GetString(1);
        }

        await using var keys = connection.CreateCommand();
        keys.CommandText = "SELECT hid_code, name FROM keyboard_keys WHERE keyboard_id = @keyboardId ORDER BY hid_code";
        keys.Parameters.AddWithValue("@keyboardId", id);
        await using var keyReader = await keys.ExecuteReaderAsync(ct);
        var hidKeys = new List<HidKey>();
        while (await keyReader.ReadAsync(ct))
        {
            hidKeys.Add(new HidKey(keyReader.GetByte(0), keyReader.GetString(1)));
        }

        return new Keyboard(id, storedName, hidKeys);
    }

    public async Task<IReadOnlyList<KeyMapping>> GetMappingsAsync(int keyboardId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var read = connection.CreateCommand();
        read.CommandText = ReadMappingsSql;
        read.Parameters.AddWithValue("@keyboardId", keyboardId);

        await using var reader = await read.ExecuteReaderAsync(ct);
        var mappings = new List<KeyMapping>();
        while (await reader.ReadAsync(ct))
        {
            mappings.Add(new KeyMapping(
                new HidKey(reader.GetByte(0), reader.GetString(1)),
                new HidKey(reader.GetByte(2), reader.GetString(3))));
        }

        return mappings;
    }

    public async Task UpsertMappingsAsync(int keyboardId, IReadOnlyList<KeyMapping> mappings, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = UpsertMappingSql;
        upsert.Parameters.AddWithValue("@keyboardId", keyboardId);
        var source = upsert.Parameters.Add("@source", SqliteType.Integer);
        var target = upsert.Parameters.Add("@target", SqliteType.Integer);

        foreach (var mapping in mappings)
        {
            source.Value = mapping.Source.Code;
            target.Value = mapping.Target.Code;
            await upsert.ExecuteNonQueryAsync(ct);
        }

        // Disposing an uncommitted transaction rolls it back, so a failed insert leaves the previous set.
        await transaction.CommitAsync(ct);
    }

    public async Task ClearMappingsAsync(int keyboardId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var clear = connection.CreateCommand();
        clear.CommandText = "DELETE FROM key_mappings WHERE keyboard_id = @keyboardId";
        clear.Parameters.AddWithValue("@keyboardId", keyboardId);
        await clear.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
