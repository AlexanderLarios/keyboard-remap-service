using Microsoft.Data.Sqlite;

namespace KeyRemap.Api.Data;

public static class DatabaseInitializer
{
    private const int ApexProGen3Id = 1;

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS keyboards (
            id    INTEGER PRIMARY KEY,
            name  TEXT NOT NULL UNIQUE COLLATE NOCASE
        );

        CREATE TABLE IF NOT EXISTS keyboard_keys (
            keyboard_id  INTEGER NOT NULL REFERENCES keyboards(id),
            hid_code     INTEGER NOT NULL CHECK (hid_code BETWEEN 0 AND 255),
            name         TEXT NOT NULL,
            PRIMARY KEY (keyboard_id, hid_code)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS key_mappings (
            keyboard_id  INTEGER NOT NULL,
            source_code  INTEGER NOT NULL,
            target_code  INTEGER NOT NULL,
            PRIMARY KEY (keyboard_id, source_code),
            FOREIGN KEY (keyboard_id, source_code)
                REFERENCES keyboard_keys (keyboard_id, hid_code),
            FOREIGN KEY (keyboard_id, target_code)
                REFERENCES keyboard_keys (keyboard_id, hid_code)
        ) WITHOUT ROWID;
        """;

    public static void Initialize(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = Schema;
            create.ExecuteNonQuery();
        }

        using var transaction = connection.BeginTransaction();

        using (var keyboard = connection.CreateCommand())
        {
            keyboard.CommandText = "INSERT OR IGNORE INTO keyboards (id, name) VALUES (@id, @name)";
            keyboard.Parameters.AddWithValue("@id", ApexProGen3Id);
            keyboard.Parameters.AddWithValue("@name", ApexProGen3Catalog.Name);
            keyboard.ExecuteNonQuery();
        }

        using var key = connection.CreateCommand();
        key.CommandText =
            "INSERT OR IGNORE INTO keyboard_keys (keyboard_id, hid_code, name) VALUES (@keyboardId, @code, @name)";
        key.Parameters.AddWithValue("@keyboardId", ApexProGen3Id);
        var code = key.Parameters.Add("@code", SqliteType.Integer);
        var name = key.Parameters.Add("@name", SqliteType.Text);
        foreach (var hidKey in ApexProGen3Catalog.Keys)
        {
            code.Value = hidKey.Code;
            name.Value = hidKey.Name;
            key.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
