using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Database.Repositories;

public record FileRenameRecord(
    string Id,
    string? JobId,
    string SourcePath,
    string TargetPath,
    bool Processed,
    DateTime CreatedAt,
    DateTime? ProcessedAt
);

public class FileRenameRepository
{
    private readonly string _connectionString;

    public FileRenameRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(FileRenameRecord r)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO FileRenames (Id, JobId, SourcePath, TargetPath, Processed, CreatedAt, ProcessedAt)
            VALUES (@Id, @JobId, @SourcePath, @TargetPath, @Processed, @CreatedAt, @ProcessedAt)";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", r.Id);
        cmd.Parameters.AddWithValue("@JobId", (object?)r.JobId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourcePath", r.SourcePath);
        cmd.Parameters.AddWithValue("@TargetPath", r.TargetPath);
        cmd.Parameters.AddWithValue("@Processed", r.Processed ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", r.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ProcessedAt", (object?)r.ProcessedAt?.ToString("O") ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<FileRenameRecord>> GetPendingAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"SELECT Id, JobId, SourcePath, TargetPath, Processed, CreatedAt, ProcessedAt
                             FROM FileRenames WHERE Processed = 0 ORDER BY CreatedAt";

        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<FileRenameRecord>();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var jobId = reader.IsDBNull(1) ? null : reader.GetString(1);
            var src = reader.GetString(2);
            var tgt = reader.GetString(3);
            var processed = reader.GetInt32(4) != 0;
            var createdAt = DateTime.Parse(reader.GetString(5));
            var processedAt = reader.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(reader.GetString(6));
            list.Add(new FileRenameRecord(id, jobId, src, tgt, processed, createdAt, processedAt));
        }

        return list;
    }

    public async Task MarkProcessedAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"UPDATE FileRenames SET Processed = 1, ProcessedAt = @ts WHERE Id = @id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
