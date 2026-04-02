using AutoRipDVD.Models;
using Microsoft.Data.Sqlite;

namespace AutoRipDVD.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task SaveSettingsAsync();
    Task LoadSettingsAsync();
}

public class SettingsService : ISettingsService
{
    private readonly IDatabase _database;
    
    public AppSettings Settings { get; private set; }

    public SettingsService(IDatabase database)
    {
        _database = database;
        Settings = new AppSettings();
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            using var connection = await _database.GetConnectionAsync();
            Settings = new AppSettings();

            // Load all settings from database
            var query = "SELECT Key, Value FROM Settings";
            using var cmd = new SqliteCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);

                switch (key)
                {
                    case "MakeMkvPath":
                        Settings.MakeMkvPath = value;
                        break;
                    case "HandBrakePath":
                        Settings.HandBrakePath = value;
                        break;
                    case "OutputPath":
                        Settings.OutputPath = value;
                        break;
                    case "TempPath":
                        Settings.TempPath = value;
                        break;
                    case "OmdbApiKey":
                        Settings.OmdbApiKey = value;
                        break;
                    case "TvdbApiKey":
                        Settings.TvdbApiKey = value;
                        break;
                    case "TmdbApiKey":
                        Settings.TmdbApiKey = value;
                        break;
                    case "HandBrakePreset":
                        Settings.HandBrakePreset = value;
                        break;
                    case "NotificationWebhook":
                        Settings.NotificationWebhook = value;
                        break;
                    case "AutoRip":
                        Settings.AutoRip = bool.Parse(value);
                        break;
                    case "RipMainFeatureOnly":
                        Settings.RipMainFeatureOnly = bool.Parse(value);
                        break;
                    case "EjectWhenComplete":
                        Settings.EjectWhenComplete = bool.Parse(value);
                        break;
                    case "TranscodeAfterRip":
                        Settings.TranscodeAfterRip = bool.Parse(value);
                        break;
                    case "EnableNotifications":
                        Settings.EnableNotifications = bool.Parse(value);
                        break;
                    case "MinimumTitleLengthSeconds":
                        Settings.MinimumTitleLengthSeconds = int.Parse(value);
                        break;
                    case "VideoQuality":
                        Settings.VideoQuality = int.Parse(value);
                        break;
                }
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            using var connection = await _database.GetConnectionAsync();
            var timestamp = DateTime.UtcNow.ToString("O");

            await SaveSettingAsync(connection, "MakeMkvPath", Settings.MakeMkvPath, timestamp);
            await SaveSettingAsync(connection, "HandBrakePath", Settings.HandBrakePath, timestamp);
            await SaveSettingAsync(connection, "OutputPath", Settings.OutputPath, timestamp);
            await SaveSettingAsync(connection, "TempPath", Settings.TempPath, timestamp);
            await SaveSettingAsync(connection, "OmdbApiKey", Settings.OmdbApiKey, timestamp);
            await SaveSettingAsync(connection, "TvdbApiKey", Settings.TvdbApiKey, timestamp);
            await SaveSettingAsync(connection, "TmdbApiKey", Settings.TmdbApiKey, timestamp);
            await SaveSettingAsync(connection, "HandBrakePreset", Settings.HandBrakePreset, timestamp);
            await SaveSettingAsync(connection, "NotificationWebhook", Settings.NotificationWebhook, timestamp);
            await SaveSettingAsync(connection, "AutoRip", Settings.AutoRip.ToString(), timestamp);
            await SaveSettingAsync(connection, "RipMainFeatureOnly", Settings.RipMainFeatureOnly.ToString(), timestamp);
            await SaveSettingAsync(connection, "EjectWhenComplete", Settings.EjectWhenComplete.ToString(), timestamp);
            await SaveSettingAsync(connection, "TranscodeAfterRip", Settings.TranscodeAfterRip.ToString(), timestamp);
            await SaveSettingAsync(connection, "EnableNotifications", Settings.EnableNotifications.ToString(), timestamp);
            await SaveSettingAsync(connection, "MinimumTitleLengthSeconds", Settings.MinimumTitleLengthSeconds.ToString(), timestamp);
            await SaveSettingAsync(connection, "VideoQuality", Settings.VideoQuality.ToString(), timestamp);
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private static async Task SaveSettingAsync(SqliteConnection connection, string key, string value, string timestamp)
    {
        var query = @"
            INSERT INTO Settings (Key, Value, UpdatedAt) 
            VALUES (@key, @value, @timestamp)
            ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = @timestamp";

        using var cmd = new SqliteCommand(query, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@timestamp", timestamp);
        await cmd.ExecuteNonQueryAsync();
    }
}
