using Microsoft.Data.Sqlite;
using System.Data;

namespace AutoRipDVD.Services;

public interface IDatabase
{
    Task InitializeAsync();
    Task<SqliteConnection> GetConnectionAsync();
}

public class DatabaseService : IDatabase
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "AutoRipDVD");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "autorip.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createSettingsTable = @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";

        var createJobsTable = @"
            CREATE TABLE IF NOT EXISTS Jobs (
                Id TEXT PRIMARY KEY,
                DiscLabel TEXT,
                MediaTitle TEXT,
                Status TEXT,
                Progress REAL,
                CreatedAt TEXT,
                CompletedAt TEXT,
                ErrorMessage TEXT
            )";

        var createMatchHistoryTable = @"
            CREATE TABLE IF NOT EXISTS MatchHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OriginalTitle TEXT NOT NULL,
                MatchedTitle TEXT NOT NULL,
                MatchedId TEXT NOT NULL,
                Source TEXT NOT NULL,
                MediaType TEXT NOT NULL,
                MatchedAt TEXT NOT NULL
            )";

        using var cmd1 = new SqliteCommand(createSettingsTable, connection);
        await cmd1.ExecuteNonQueryAsync();

        using var cmd2 = new SqliteCommand(createJobsTable, connection);
        await cmd2.ExecuteNonQueryAsync();

        using var cmd3 = new SqliteCommand(createMatchHistoryTable, connection);
        await cmd3.ExecuteNonQueryAsync();
    }

    public async Task<SqliteConnection> GetConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
