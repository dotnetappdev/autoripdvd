using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Database.Repositories;

/// <summary>
/// Persists application settings as key-value pairs in SQLite.
/// </summary>
public class SettingsRepository
{
    private readonly string _connectionString;

    public SettingsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Dictionary<string, string>> LoadAllAsync()
    {
        var result = new Dictionary<string, string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new SqliteCommand("SELECT Key, Value FROM Settings", connection);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetString(1);

        return result;
    }

    public async Task SaveAsync(string key, string value)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO Settings (Key, Value, UpdatedAt)
            VALUES (@key, @value, @ts)
            ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = @ts";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveManyAsync(IEnumerable<KeyValuePair<string, string>> settings)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        var ts = DateTime.UtcNow.ToString("O");

        const string sql = @"
            INSERT INTO Settings (Key, Value, UpdatedAt)
            VALUES (@key, @value, @ts)
            ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = @ts";

        foreach (var kv in settings)
        {
            using var cmd = new SqliteCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@key", kv.Key);
            cmd.Parameters.AddWithValue("@value", kv.Value);
            cmd.Parameters.AddWithValue("@ts", ts);
            await cmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }
}
