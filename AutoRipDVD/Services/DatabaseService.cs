using AutoRipDVD.Database;
using AutoRipDVD.Database.Repositories;
using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Services;

public interface IDatabase
{
    Task InitializeAsync();
    Task<SqliteConnection> GetConnectionAsync();
    SettingsRepository Settings { get; }
    JobRepository Jobs { get; }
    MatchHistoryRepository MatchHistory { get; }
    FileRenameRepository FileRenames { get; }
    string DatabasePath { get; }
}

public class DatabaseService : IDatabase
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public SettingsRepository   Settings     { get; }
    public JobRepository        Jobs         { get; }
    public MatchHistoryRepository MatchHistory { get; }
    public FileRenameRepository FileRenames { get; }

    public DatabaseService()
    {
        var dbPath = DatabaseInitializer.GetDefaultDbPath();
        _dbPath = dbPath;
        _connectionString = DatabaseInitializer.BuildConnectionString(dbPath);

        Settings     = new SettingsRepository(_connectionString);
        Jobs         = new JobRepository(_connectionString);
        MatchHistory = new MatchHistoryRepository(_connectionString);
        FileRenames  = new FileRenameRepository(_connectionString);
    }

    public async Task InitializeAsync()
    {
        var initializer = new DatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();
    }

    public async Task<SqliteConnection> GetConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>Path to the SQLite file used by this instance.</summary>
    public string DatabasePath => _dbPath;
}
