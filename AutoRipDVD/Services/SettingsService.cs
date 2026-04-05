using AutoRipDVD.Models;
using AutoRipDVD.Database.Repositories;

namespace AutoRipDVD.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task SaveSettingsAsync();
    Task LoadSettingsAsync();
}

public class SettingsService : ISettingsService
{
    private readonly SettingsRepository _repository;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService(IDatabase database)
    {
        _repository = database.Settings;
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            var data = await _repository.LoadAllAsync();
            var s = new AppSettings();

            string Get(string key, string def) =>
                data.TryGetValue(key, out var v) ? v : def;

            bool GetBool(string key, bool def) =>
                data.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def;

            int GetInt(string key, int def) =>
                data.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;

            T GetEnum<T>(string key, T def) where T : struct, Enum =>
                data.TryGetValue(key, out var v) && Enum.TryParse<T>(v, out var e) ? e : def;

            // Paths
            s.MakeMkvPath   = Get("MakeMkvPath",   s.MakeMkvPath);
            s.HandBrakePath = Get("HandBrakePath",  s.HandBrakePath);
            s.OutputPath    = Get("OutputPath",     s.OutputPath);
            s.DashboardOutputPath = Get("DashboardOutputPath", string.Empty);
            s.TempPath      = Get("TempPath",       s.TempPath);
            s.MakeMkvDataDirectory = Get("MakeMkvDataDirectory", s.MakeMkvDataDirectory);
            s.LogPath       = Get("LogPath",        s.LogPath);

            // API Keys
            s.OmdbApiKey  = Get("OmdbApiKey",  string.Empty);
            s.TmdbApiKey  = Get("TmdbApiKey",  string.Empty);
            s.TvdbApiKey  = Get("TvdbApiKey",  string.Empty);
            s.AnidbApiKey = Get("AnidbApiKey", string.Empty);

            // General ripping
            s.AutoRip                   = GetBool("AutoRip",                   false);
            s.RipMainFeatureOnly        = GetBool("RipMainFeatureOnly",        true);
            s.EjectWhenComplete         = GetBool("EjectWhenComplete",         true);
            s.TranscodeAfterRip         = GetBool("TranscodeAfterRip",        true);
            s.MinimumTitleLengthSeconds = GetInt("MinimumTitleLengthSeconds",  120);
            s.AutoMatchMetadata         = GetBool("AutoMatchMetadata",         true);
            s.CreatePlexFolderStructure = GetBool("CreatePlexFolderStructure", true);

            // MakeMKV
            s.MakeMkvQualityPreset  = GetEnum("MakeMkvQualityPreset",  MakeMKVQuality.Original);
            s.EnableInternetAccess  = GetBool("EnableInternetAccess",  true);
            s.LogDebugMessages      = GetBool("LogDebugMessages",      false);
            s.ExpertMode            = GetBool("ExpertMode",            false);
            s.PreserveDTS           = GetBool("PreserveDTS",           true);
            s.PreserveTrueHD        = GetBool("PreserveTrueHD",        true);
            s.IncludeAllAudioTracks = GetBool("IncludeAllAudioTracks", false);
            s.IncludeAllSubtitles   = GetBool("IncludeAllSubtitles",   true);
            s.PreserveChapterMarkers = GetBool("PreserveChapterMarkers", true);

            // HandBrake general
            s.HandBrakePreset          = Get("HandBrakePreset",           "Fast 1080p30");
            s.VideoQuality             = GetInt("VideoQuality",            22);
            s.VideoEncoder             = GetEnum("VideoEncoder",           VideoEncoderType.x264);
            s.AudioEncoder             = GetEnum("AudioEncoder",           AudioEncoderType.AAC);
            s.AudioBitrate             = GetInt("AudioBitrate",            160);
            s.UseHardwareAcceleration  = GetBool("UseHardwareAcceleration", true);
            s.AutoDetectHardwareEncoder = GetBool("AutoDetectHardwareEncoder", true);

            // HandBrake advanced
            s.EnableTwoPassEncoding    = GetBool("EnableTwoPassEncoding",  false);
            s.EnableTurboFirstPass     = GetBool("EnableTurboFirstPass",   true);
            s.x264Preset               = Get("x264Preset",                "medium");
            s.x264Tune                 = Get("x264Tune",                  "none");
            s.x264Profile              = Get("x264Profile",               "auto");
            s.x265Preset               = Get("x265Preset",               "medium");
            s.CustomEncoderOptions     = Get("CustomEncoderOptions",       string.Empty);
            s.NumberOfPreviewsToScan   = GetInt("NumberOfPreviewsToScan", 10);
            s.ProcessPriority          = GetEnum("ProcessPriority",       ProcessPriorityLevel.Normal);

            // HandBrake filters
            s.EnableDeinterlacing = GetBool("EnableDeinterlacing", false);
            s.EnableDenoise       = GetBool("EnableDenoise",       false);
            s.DenoisePreset       = Get("DenoisePreset",           "medium");
            s.EnableSharpen       = GetBool("EnableSharpen",       false);
            s.EnableDeblock       = GetBool("EnableDeblock",       false);

            // HandBrake logging
            s.LogVerbosity              = GetEnum("LogVerbosity",              LogVerbosity.Standard);
            s.ClearLogsOlderThan30Days  = GetBool("ClearLogsOlderThan30Days", true);
            s.CustomLogLocation         = Get("CustomLogLocation",             string.Empty);

            // Notifications
            s.EnableNotifications = GetBool("EnableNotifications", true);
            s.NotificationWebhook = Get("NotificationWebhook",    string.Empty);
            s.NotifyOnCompletion  = GetBool("NotifyOnCompletion",  true);
            s.NotifyOnError       = GetBool("NotifyOnError",       true);
            s.SlackWebhook        = Get("SlackWebhook",            string.Empty);
            s.DiscordWebhook      = Get("DiscordWebhook",          string.Empty);

            // Sound
            s.EnableSounds           = GetBool("EnableSounds",           true);
            s.PlaySoundOnCompletion  = GetBool("PlaySoundOnCompletion",  true);
            s.PlaySoundOnEjection    = GetBool("PlaySoundOnEjection",    true);
            s.PlaySoundOnError       = GetBool("PlaySoundOnError",       true);
            s.PlaySoundOnRipStart    = GetBool("PlaySoundOnRipStart",    false);
            s.CompletionSoundAlias   = Get("CompletionSoundAlias",   "SystemAsterisk");
            s.EjectionSoundAlias     = Get("EjectionSoundAlias",     "SystemNotification");
            s.ErrorSoundAlias        = Get("ErrorSoundAlias",        "SystemHand");
            s.RipStartSoundAlias     = Get("RipStartSoundAlias",     "SystemExclamation");
            s.CompletionSoundPath    = Get("CompletionSoundPath",    string.Empty);
            s.EjectionSoundPath      = Get("EjectionSoundPath",      string.Empty);
            s.ErrorSoundPath         = Get("ErrorSoundPath",         string.Empty);
            s.RipStartSoundPath      = Get("RipStartSoundPath",      string.Empty);

            // Per-media output folders
            s.MoviesOutputPath       = Get("MoviesOutputPath",       string.Empty);
            s.TvOutputPath           = Get("TvOutputPath",           string.Empty);
            s.MusicOutputPath        = Get("MusicOutputPath",        string.Empty);

            // Recent dashboard output folders (stored as pipe-separated string)
            var recent = Get("RecentOutputFolders", string.Empty);
            s.RecentOutputFolders = string.IsNullOrWhiteSpace(recent)
                ? new List<string>()
                : recent.Split('|').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            

            // UI
            s.Theme             = Get("Theme",             "System");
            s.ShowAdvancedOptions = GetBool("ShowAdvancedOptions", false);
            s.MinimizeToSystemTray = GetBool("MinimizeToSystemTray", false);
            s.StartMinimized    = GetBool("StartMinimized",    false);

            Settings = s;
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public async Task SaveSettingsAsync()
    {
        var s = Settings;
        var data = new Dictionary<string, string>
        {
            // Paths
            ["MakeMkvPath"]          = s.MakeMkvPath,
            ["HandBrakePath"]        = s.HandBrakePath,
            ["OutputPath"]           = s.OutputPath,
            ["TempPath"]             = s.TempPath,
            ["MakeMkvDataDirectory"] = s.MakeMkvDataDirectory,
            ["LogPath"]              = s.LogPath,

            // API Keys
            ["OmdbApiKey"]           = s.OmdbApiKey,
            ["TmdbApiKey"]           = s.TmdbApiKey,
            ["TvdbApiKey"]           = s.TvdbApiKey,
            ["AnidbApiKey"]          = s.AnidbApiKey,

            // General ripping
            ["AutoRip"]                   = s.AutoRip.ToString(),
            ["RipMainFeatureOnly"]        = s.RipMainFeatureOnly.ToString(),
            ["EjectWhenComplete"]         = s.EjectWhenComplete.ToString(),
            ["TranscodeAfterRip"]         = s.TranscodeAfterRip.ToString(),
            ["MinimumTitleLengthSeconds"] = s.MinimumTitleLengthSeconds.ToString(),
            ["AutoMatchMetadata"]         = s.AutoMatchMetadata.ToString(),
            ["CreatePlexFolderStructure"] = s.CreatePlexFolderStructure.ToString(),

            // MakeMKV
            ["MakeMkvQualityPreset"]  = s.MakeMkvQualityPreset.ToString(),
            ["EnableInternetAccess"]  = s.EnableInternetAccess.ToString(),
            ["LogDebugMessages"]      = s.LogDebugMessages.ToString(),
            ["ExpertMode"]            = s.ExpertMode.ToString(),
            ["PreserveDTS"]           = s.PreserveDTS.ToString(),
            ["PreserveTrueHD"]        = s.PreserveTrueHD.ToString(),
            ["IncludeAllAudioTracks"] = s.IncludeAllAudioTracks.ToString(),
            ["IncludeAllSubtitles"]   = s.IncludeAllSubtitles.ToString(),
            ["PreserveChapterMarkers"] = s.PreserveChapterMarkers.ToString(),

            // HandBrake general
            ["HandBrakePreset"]          = s.HandBrakePreset,
            ["VideoQuality"]             = s.VideoQuality.ToString(),
            ["VideoEncoder"]             = s.VideoEncoder.ToString(),
            ["AudioEncoder"]             = s.AudioEncoder.ToString(),
            ["AudioBitrate"]             = s.AudioBitrate.ToString(),
            ["UseHardwareAcceleration"]  = s.UseHardwareAcceleration.ToString(),
            ["AutoDetectHardwareEncoder"] = s.AutoDetectHardwareEncoder.ToString(),

            // HandBrake advanced
            ["EnableTwoPassEncoding"]  = s.EnableTwoPassEncoding.ToString(),
            ["EnableTurboFirstPass"]   = s.EnableTurboFirstPass.ToString(),
            ["x264Preset"]             = s.x264Preset,
            ["x264Tune"]               = s.x264Tune,
            ["x264Profile"]            = s.x264Profile,
            ["x265Preset"]             = s.x265Preset,
            ["CustomEncoderOptions"]   = s.CustomEncoderOptions,
            ["NumberOfPreviewsToScan"] = s.NumberOfPreviewsToScan.ToString(),
            ["ProcessPriority"]        = s.ProcessPriority.ToString(),

            // Filters
            ["EnableDeinterlacing"] = s.EnableDeinterlacing.ToString(),
            ["EnableDenoise"]       = s.EnableDenoise.ToString(),
            ["DenoisePreset"]       = s.DenoisePreset,
            ["EnableSharpen"]       = s.EnableSharpen.ToString(),
            ["EnableDeblock"]       = s.EnableDeblock.ToString(),

            // Logging
            ["LogVerbosity"]             = s.LogVerbosity.ToString(),
            ["ClearLogsOlderThan30Days"] = s.ClearLogsOlderThan30Days.ToString(),
            ["CustomLogLocation"]        = s.CustomLogLocation,

            // Notifications
            ["EnableNotifications"] = s.EnableNotifications.ToString(),
            ["NotificationWebhook"] = s.NotificationWebhook,
            ["NotifyOnCompletion"]  = s.NotifyOnCompletion.ToString(),
            ["NotifyOnError"]       = s.NotifyOnError.ToString(),
            ["SlackWebhook"]        = s.SlackWebhook,
            ["DiscordWebhook"]      = s.DiscordWebhook,

            // Sound
            ["EnableSounds"]          = s.EnableSounds.ToString(),
            ["PlaySoundOnCompletion"] = s.PlaySoundOnCompletion.ToString(),
            ["PlaySoundOnEjection"]   = s.PlaySoundOnEjection.ToString(),
            ["PlaySoundOnError"]      = s.PlaySoundOnError.ToString(),
            ["PlaySoundOnRipStart"]   = s.PlaySoundOnRipStart.ToString(),
            ["CompletionSoundAlias"]  = s.CompletionSoundAlias,
            ["EjectionSoundAlias"]    = s.EjectionSoundAlias,
            ["ErrorSoundAlias"]       = s.ErrorSoundAlias,
            ["RipStartSoundAlias"]    = s.RipStartSoundAlias,
            ["CompletionSoundPath"]   = s.CompletionSoundPath,
            ["EjectionSoundPath"]     = s.EjectionSoundPath,
            ["ErrorSoundPath"]        = s.ErrorSoundPath,
            ["RipStartSoundPath"]     = s.RipStartSoundPath,

            // Per-media output folders
            ["MoviesOutputPath"]      = s.MoviesOutputPath,
            ["TvOutputPath"]          = s.TvOutputPath,
            ["MusicOutputPath"]       = s.MusicOutputPath,
            ["DashboardOutputPath"]   = s.DashboardOutputPath,
            ["RecentOutputFolders"]   = string.Join('|', s.RecentOutputFolders ?? new List<string>()),

            // UI
            ["Theme"]               = s.Theme,
            ["ShowAdvancedOptions"] = s.ShowAdvancedOptions.ToString(),
            ["MinimizeToSystemTray"] = s.MinimizeToSystemTray.ToString(),
            ["StartMinimized"]      = s.StartMinimized.ToString(),
        };

        await _repository.SaveManyAsync(data);
    }
}
