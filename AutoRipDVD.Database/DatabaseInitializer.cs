using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Database;

/// <summary>
/// Manages database schema creation and migrations.
/// </summary>
public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Enable WAL mode for better concurrency
        using (var pragma = new SqliteCommand("PRAGMA journal_mode=WAL;", connection))
            await pragma.ExecuteNonQueryAsync();

        using (var pragma = new SqliteCommand("PRAGMA foreign_keys=ON;", connection))
            await pragma.ExecuteNonQueryAsync();

        await CreateTablesAsync(connection);
    }

    private static async Task CreateTablesAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            // Settings key-value store
            @"CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )",

            // Full rip job history
            @"CREATE TABLE IF NOT EXISTS Jobs (
                Id TEXT PRIMARY KEY,
                DiscLabel TEXT NOT NULL,
                DiscType TEXT NOT NULL,
                DriveLetter TEXT NOT NULL,
                MediaTitle TEXT,
                MediaType TEXT,
                Season INTEGER,
                Episode INTEGER,
                Year INTEGER,
                ImdbId TEXT,
                Status TEXT NOT NULL,
                Progress REAL NOT NULL DEFAULT 0,
                OutputPath TEXT,
                ErrorMessage TEXT,
                TitleCount INTEGER NOT NULL DEFAULT 0,
                SelectedTitleCount INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT
            )",

            // Metadata match history for learning / quick re-match
            @"CREATE TABLE IF NOT EXISTS MatchHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DiscLabel TEXT NOT NULL,
                MatchedTitle TEXT NOT NULL,
                MatchedId TEXT NOT NULL,
                Source TEXT NOT NULL,
                MediaType TEXT NOT NULL,
                Year INTEGER,
                MatchedAt TEXT NOT NULL
            )",

            // Index for fast label lookups
            @"CREATE INDEX IF NOT EXISTS idx_match_history_disc ON MatchHistory(DiscLabel)",
            @"CREATE INDEX IF NOT EXISTS idx_jobs_status ON Jobs(Status)",
            @"CREATE INDEX IF NOT EXISTS idx_jobs_created ON Jobs(CreatedAt DESC)"
        };

        foreach (var sql in commands)
        {
            using var cmd = new SqliteCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public static string GetDefaultDbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "AutoRipDVD");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "autorip.db");
    }

    public static string BuildConnectionString(string dbPath)
        => $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
}
