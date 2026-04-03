using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Database.Repositories;

public record JobRecord(
    string Id,
    string DiscLabel,
    string DiscType,
    string DriveLetter,
    string? MediaTitle,
    string? MediaType,
    int? Season,
    int? Episode,
    int? Year,
    string? ImdbId,
    string Status,
    double Progress,
    string? OutputPath,
    string? ErrorMessage,
    int TitleCount,
    int SelectedTitleCount,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

/// <summary>
/// Persists rip job history to SQLite.
/// </summary>
public class JobRepository
{
    private readonly string _connectionString;

    public JobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task UpsertAsync(JobRecord job)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO Jobs (
                Id, DiscLabel, DiscType, DriveLetter, MediaTitle, MediaType,
                Season, Episode, Year, ImdbId, Status, Progress, OutputPath,
                ErrorMessage, TitleCount, SelectedTitleCount, CreatedAt, CompletedAt
            ) VALUES (
                @Id, @DiscLabel, @DiscType, @DriveLetter, @MediaTitle, @MediaType,
                @Season, @Episode, @Year, @ImdbId, @Status, @Progress, @OutputPath,
                @ErrorMessage, @TitleCount, @SelectedTitleCount, @CreatedAt, @CompletedAt
            )
            ON CONFLICT(Id) DO UPDATE SET
                Status = @Status,
                Progress = @Progress,
                OutputPath = @OutputPath,
                ErrorMessage = @ErrorMessage,
                MediaTitle = @MediaTitle,
                MediaType = @MediaType,
                Season = @Season,
                Episode = @Episode,
                Year = @Year,
                ImdbId = @ImdbId,
                SelectedTitleCount = @SelectedTitleCount,
                CompletedAt = @CompletedAt";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", job.Id);
        cmd.Parameters.AddWithValue("@DiscLabel", job.DiscLabel);
        cmd.Parameters.AddWithValue("@DiscType", job.DiscType);
        cmd.Parameters.AddWithValue("@DriveLetter", job.DriveLetter);
        cmd.Parameters.AddWithValue("@MediaTitle", (object?)job.MediaTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MediaType", (object?)job.MediaType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Season", (object?)job.Season ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Episode", (object?)job.Episode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Year", (object?)job.Year ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ImdbId", (object?)job.ImdbId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", job.Status);
        cmd.Parameters.AddWithValue("@Progress", job.Progress);
        cmd.Parameters.AddWithValue("@OutputPath", (object?)job.OutputPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)job.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TitleCount", job.TitleCount);
        cmd.Parameters.AddWithValue("@SelectedTitleCount", job.SelectedTitleCount);
        cmd.Parameters.AddWithValue("@CreatedAt", job.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedAt",
            job.CompletedAt.HasValue ? (object)job.CompletedAt.Value.ToString("O") : DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<JobRecord>> GetRecentAsync(int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT Id, DiscLabel, DiscType, DriveLetter, MediaTitle, MediaType,
                   Season, Episode, Year, ImdbId, Status, Progress, OutputPath,
                   ErrorMessage, TitleCount, SelectedTitleCount, CreatedAt, CompletedAt
            FROM Jobs
            ORDER BY CreatedAt DESC
            LIMIT @limit";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@limit", limit);

        var jobs = new List<JobRecord>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            jobs.Add(ReadRecord(reader));

        return jobs;
    }

    public async Task<List<JobRecord>> GetByStatusAsync(string status)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT Id, DiscLabel, DiscType, DriveLetter, MediaTitle, MediaType,
                   Season, Episode, Year, ImdbId, Status, Progress, OutputPath,
                   ErrorMessage, TitleCount, SelectedTitleCount, CreatedAt, CompletedAt
            FROM Jobs WHERE Status = @status ORDER BY CreatedAt DESC";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@status", status);

        var jobs = new List<JobRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            jobs.Add(ReadRecord(reader));

        return jobs;
    }

    private static JobRecord ReadRecord(SqliteDataReader r)
    {
        return new JobRecord(
            Id: r.GetString(0),
            DiscLabel: r.GetString(1),
            DiscType: r.GetString(2),
            DriveLetter: r.GetString(3),
            MediaTitle: r.IsDBNull(4) ? null : r.GetString(4),
            MediaType: r.IsDBNull(5) ? null : r.GetString(5),
            Season: r.IsDBNull(6) ? null : r.GetInt32(6),
            Episode: r.IsDBNull(7) ? null : r.GetInt32(7),
            Year: r.IsDBNull(8) ? null : r.GetInt32(8),
            ImdbId: r.IsDBNull(9) ? null : r.GetString(9),
            Status: r.GetString(10),
            Progress: r.GetDouble(11),
            OutputPath: r.IsDBNull(12) ? null : r.GetString(12),
            ErrorMessage: r.IsDBNull(13) ? null : r.GetString(13),
            TitleCount: r.GetInt32(14),
            SelectedTitleCount: r.GetInt32(15),
            CreatedAt: DateTime.Parse(r.GetString(16)),
            CompletedAt: r.IsDBNull(17) ? null : DateTime.Parse(r.GetString(17))
        );
    }
}
