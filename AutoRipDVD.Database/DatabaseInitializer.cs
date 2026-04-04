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
            ,
            // File rename queue: store original and desired target paths and processed flag
            @"CREATE TABLE IF NOT EXISTS FileRenames (
                Id TEXT PRIMARY KEY,
                JobId TEXT,
                SourcePath TEXT NOT NULL,
                TargetPath TEXT NOT NULL,
                Processed INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                ProcessedAt TEXT
            )",
            @"CREATE INDEX IF NOT EXISTS idx_filerenames_processed ON FileRenames(Processed)"
        };

        foreach (var sql in commands)
        {
            using var cmd = new SqliteCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Resolves the database file path, respecting the install scope written by the installer.
    ///
    /// Install scope rules (mirrors standard Windows app conventions):
    ///   All-users install  → %PROGRAMDATA%\AutoRipDVD\autorip.db
    ///                         (C:\ProgramData\AutoRipDVD\autorip.db)
    ///   Per-user install   → %APPDATA%\AutoRipDVD\autorip.db
    ///   Standalone / dev   → %APPDATA%\AutoRipDVD\autorip.db  (fallback)
    ///
    /// The Inno Setup installer writes the AUTORIP_DATA_DIR environment variable
    /// (Machine scope for all-users, User scope for per-user) so the app can find
    /// the right location without hard-coding paths.
    /// </summary>
    public static string GetDefaultDbPath()
    {
        // 1. Check for installer-configured data directory.
        //    Machine scope = all-users install; User scope = per-user install.
        string? installerDir = null;

        try
        {
            installerDir =
                Environment.GetEnvironmentVariable("AUTORIP_DATA_DIR", EnvironmentVariableTarget.Machine)
                ?? Environment.GetEnvironmentVariable("AUTORIP_DATA_DIR", EnvironmentVariableTarget.User);
        }
        catch
        {
            // EnvironmentVariableTarget.Machine/User throws PlatformNotSupportedException
            // on non-Windows. Fall through to default.
        }

        // 2. Fall back to per-user AppData\Roaming (original behaviour / dev runs).
        string folder = !string.IsNullOrWhiteSpace(installerDir)
            ? installerDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoRipDVD");

        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "autorip.db");
    }

    public static string BuildConnectionString(string dbPath)
        => $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
}
