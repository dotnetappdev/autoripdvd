using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Database.Repositories;

public record MatchHistoryRecord(
    string DiscLabel,
    string MatchedTitle,
    string MatchedId,
    string Source,
    string MediaType,
    int? Year,
    DateTime MatchedAt
);

/// <summary>
/// Tracks successful metadata matches so the same disc can be re-matched instantly.
/// </summary>
public class MatchHistoryRepository
{
    private readonly string _connectionString;

    public MatchHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task RecordMatchAsync(MatchHistoryRecord record)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO MatchHistory (DiscLabel, MatchedTitle, MatchedId, Source, MediaType, Year, MatchedAt)
            VALUES (@DiscLabel, @MatchedTitle, @MatchedId, @Source, @MediaType, @Year, @MatchedAt)";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@DiscLabel", record.DiscLabel);
        cmd.Parameters.AddWithValue("@MatchedTitle", record.MatchedTitle);
        cmd.Parameters.AddWithValue("@MatchedId", record.MatchedId);
        cmd.Parameters.AddWithValue("@Source", record.Source);
        cmd.Parameters.AddWithValue("@MediaType", record.MediaType);
        cmd.Parameters.AddWithValue("@Year", (object?)record.Year ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MatchedAt", record.MatchedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<MatchHistoryRecord?> FindByDiscLabelAsync(string discLabel)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT DiscLabel, MatchedTitle, MatchedId, Source, MediaType, Year, MatchedAt
            FROM MatchHistory
            WHERE DiscLabel = @label
            ORDER BY MatchedAt DESC
            LIMIT 1";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@label", discLabel);
        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return null;

        return new MatchHistoryRecord(
            DiscLabel: reader.GetString(0),
            MatchedTitle: reader.GetString(1),
            MatchedId: reader.GetString(2),
            Source: reader.GetString(3),
            MediaType: reader.GetString(4),
            Year: reader.IsDBNull(5) ? null : reader.GetInt32(5),
            MatchedAt: DateTime.Parse(reader.GetString(6))
        );
    }
}
