namespace AutoRipDVD.Models;

public enum DiscType
{
    Unknown,
    DVD,
    BluRay,
    CD,
    DataDisc
}

// Compatibility aliases for older property names used across the codebase
public partial class AppSettings
{
    public int Quality
    {
        get => VideoQuality;
        set => VideoQuality = value;
    }

    public bool AutoTranscode
    {
        get => TranscodeAfterRip;
        set => TranscodeAfterRip = value;
    }

    public string OutputBasePath
    {
        get => OutputPath;
        set => OutputPath = value;
    }
}

public enum MediaType
{
    Unknown,
    Movie,
    TVShow,
    Audio,
    Data
}

public enum RipStatus
{
    Pending,
    Detecting,
    FetchingMetadata,
    Ripping,
    Transcoding,
    Completed,
    Failed,
    Cancelled
}

public enum MakeMKVQuality
{
    Original,    // No compression, full quality
    High,        // Minimal compression
    Medium,      // Balanced quality/size
    Low          // Maximum compression
}

public enum VideoEncoderType
{
    x264,        // H.264/AVC (most compatible)
    x265,        // H.265/HEVC (better compression)
    x265_10bit,  // 10-bit HEVC
    VP9,         // Google VP9
    AV1,         // AV1 (best compression, slow)
    NVENC_H264,  // NVIDIA hardware H.264
    NVENC_H265,  // NVIDIA hardware H.265
    QuickSync_H264,  // Intel QuickSync H.264
    QuickSync_H265,  // Intel QuickSync H.265
    VCE_H264,    // AMD VCE H.264
    VCE_H265     // AMD VCE H.265
}

public enum AudioEncoderType
{
    AAC,         // Advanced Audio Coding (most compatible)
    AC3,         // Dolby Digital
    EAC3,        // Dolby Digital Plus
    MP3,         // MPEG-1 Audio Layer 3
    Opus,        // Opus (best quality at low bitrates)
    FLAC,        // Free Lossless Audio Codec
    Vorbis,      // Ogg Vorbis
    Passthrough, // Copy original audio
    Auto         // Let HandBrake choose
}

public enum ProcessPriorityLevel
{
    Low,
    BelowNormal,
    Normal,
    AboveNormal,
    High
}

public enum LogVerbosity
{
    Minimal,     // Errors only
    Standard,    // Info + Errors
    Verbose,     // Detailed operation logs
    Debug        // Everything including debug info
}

public class DiscInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public DiscType DiscType { get; set; }
    public string VolumeLabel { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime DetectedAt { get; set; }
    
    // Extended drive information
    public string DeviceName { get; set; } = string.Empty;
    public string CurrentProfile { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    
    // Extended disc information
    public string Protection { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public double CapacityGB { get; set; }
    public int DiscSizeMM { get; set; }
    public double MaxReadRateMbps { get; set; }
    public int NumberOfLayers { get; set; }
}

public class MediaMetadata
{
    // Keep legacy name `Type` but expose `MediaType` as expected by views/services
    public MediaType Type { get; set; }
    public MediaType MediaType
    {
        get => Type;
        set => Type = value;
    }
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string ImdbId { get; set; } = string.Empty;
    public string Plot { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public string Actors { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public double? Rating { get; set; }
    
    // TV Show specific
    // Legacy property names mapped for compatibility
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }
    public int? Season
    {
        get => SeasonNumber;
        set => SeasonNumber = value;
    }
    public int? Episode
    {
        get => EpisodeNumber;
        set => EpisodeNumber = value;
    }
    public string SeriesName { get; set; } = string.Empty;
    public string EpisodeTitle { get; set; } = string.Empty;
    
    public string GetFormattedFolderName()
    {
        if (Type == MediaType.Movie)
        {
            return Year.HasValue 
                ? $"{SanitizeFileName(Title)} ({Year})" 
                : SanitizeFileName(Title);
        }
        else if (Type == MediaType.TVShow)
        {
            var seasonEpisode = $"S{SeasonNumber:D2}E{EpisodeNumber:D2}";
            return $"{SanitizeFileName(SeriesName)}/{seasonEpisode} - {SanitizeFileName(EpisodeTitle)}";
        }
        return SanitizeFileName(Title);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}

public class RipJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DiscInfo Disc { get; set; } = new();
    public MediaMetadata? Metadata { get; set; }
    public RipStatus Status { get; set; }
    public double Progress { get; set; }
    public string CurrentOperation { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TitleInfo> Titles { get; set; } = new();
    public List<int> SelectedTitleIndices { get; set; } = new();
    
    public bool IsIndeterminate => Status == RipStatus.Detecting || Status == RipStatus.FetchingMetadata;
    public bool HasCompleted => CompletedAt.HasValue;
    public bool HasOutputPath => !string.IsNullOrEmpty(OutputPath);
}

public class TitleInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long SizeBytes { get; set; }
    public int ChapterCount { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public bool IsMainFeature { get; set; }
    
    // MakeMKV-style detailed properties
    public int SourceTitleId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int SegmentCount { get; set; }
    public string SegmentMap { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string AngleInfo { get; set; } = string.Empty;
    public int CellCount { get; set; }
    public List<string> AudioTracks { get; set; } = new();
    public List<string> SubtitleTracks { get; set; } = new();
    
    // Helper properties for display
    public string FormattedDuration => Duration.ToString(@"hh\:mm\:ss");
    public string FormattedSize
    {
        get
        {
            if (SizeBytes >= 1_073_741_824) // >= 1 GB
                return $"{SizeBytes / 1_073_741_824.0:F2} GB";
            else if (SizeBytes >= 1_048_576) // >= 1 MB
                return $"{SizeBytes / 1_048_576.0:F1} MB";
            else
                return $"{SizeBytes / 1_024.0:F1} KB";
        }
    }
    
    public string Description => 
        $"{ChapterCount} chapter(s), {FormattedSize} ({Name})";
    
    public string DetailedInfo => 
        $"Duration: {FormattedDuration} | Chapters: {ChapterCount} | Size: {FormattedSize} | Segments: {SegmentCount}";
}


public class AppSettings
{
    // Paths
    public string MakeMkvPath { get; set; } = @"C:\Program Files (x86)\MakeMKV\makemkvcon64.exe";
    public string HandBrakePath { get; set; } = @"C:\Program Files\HandBrake\HandBrakeCLI.exe";
    public string FfmpegPath { get; set; } = @"C:\Program Files\ffmpeg\bin\ffmpeg.exe";
    public string FfprobePath { get; set; } = @"C:\Program Files\ffmpeg\bin\ffprobe.exe";
    public string MkvMergePath { get; set; } = @"C:\Program Files\MKVToolNix\mkvmerge.exe";
    public string MkvExtractPath { get; set; } = @"C:\Program Files\MKVToolNix\mkvextract.exe";
    public string TesseractPath { get; set; } = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
    public string OutputPath { get; set; } = @"D:\Ripped";
    public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "AutoRipDVD");
    public string MakeMkvDataDirectory { get; set; } = @"C:\Users\{USER}\.MakeMKV";
    public string LogPath { get; set; } = "";
    
    // API Keys
    public string OmdbApiKey { get; set; } = string.Empty;
    public string TvdbApiKey { get; set; } = string.Empty;
    public string TmdbApiKey { get; set; } = string.Empty;
    public string AnidbApiKey { get; set; } = string.Empty;
    
    // General Ripping Options
    public bool AutoRip { get; set; } = false;
    public bool RipMainFeatureOnly { get; set; } = true;
    public bool EjectWhenComplete { get; set; } = true;
    public bool TranscodeAfterRip { get; set; } = true;
    public int MinimumTitleLengthSeconds { get; set; } = 120;
    public bool AutoMatchMetadata { get; set; } = true;
    public bool CreatePlexFolderStructure { get; set; } = true;
    
    // MakeMKV Quality Settings
    public MakeMKVQuality MakeMkvQualityPreset { get; set; } = MakeMKVQuality.Original;
    
    // MakeMKV Options
    public bool EnableInternetAccess { get; set; } = true;
    public string ProxyServer { get; set; } = string.Empty;
    public bool LogDebugMessages { get; set; } = false;
    public bool ExpertMode { get; set; } = false;
    public bool ShowAVSynchronization { get; set; } = false;
    public string DVDProtectionRemoval { get; set; } = "Auto"; // Auto, Always, Never
    public bool AlwaysCreateBDPlusDumps { get; set; } = false;
    public string CustomJavaLocation { get; set; } = string.Empty;
    public bool PreserveDTS { get; set; } = true;
    public bool PreserveTrueHD { get; set; } = true;
    public bool IncludeAllAudioTracks { get; set; } = false;
    public bool IncludeAllSubtitles { get; set; } = true;
    public bool PreserveChapterMarkers { get; set; } = true;
    public bool IncludeForcedSubtitlesOnly { get; set; } = false;
    
    // HandBrake Advanced Options
    public bool PreventSleepWhileEncoding { get; set; } = true;
    public bool DisableLibDVDNav { get; set; } = false;
    public bool PauseQueueOnLowDiskSpace { get; set; } = true;
    public int LowDiskSpaceThresholdGB { get; set; } = 2;
    public int NumberOfPreviewsToScan { get; set; } = 10;
    public ProcessPriorityLevel ProcessPriority { get; set; } = ProcessPriorityLevel.Normal;
    
    // HandBrake Quality Settings
    public string HandBrakePreset { get; set; } = "Fast 1080p30";
    public int VideoQuality { get; set; } = 22; // RF value (18-28, lower = better quality)
    public VideoEncoderType VideoEncoder { get; set; } = VideoEncoderType.x264;
    public double ConstantQualityFractionalGranularity { get; set; } = 0.50;
    public AudioEncoderType AudioEncoder { get; set; } = AudioEncoderType.AAC;
    public int AudioBitrate { get; set; } = 160;
    public bool UseHardwareAcceleration { get; set; } = true;
    public bool AutoDetectHardwareEncoder { get; set; } = true;

    // Output format
    public OutputFormat DefaultOutputFormat { get; set; } = OutputFormat.MKV;
    public AudioMixdown DefaultAudioMixdown { get; set; } = AudioMixdown.Auto;

    // Audio preferences
    public string PreferredAudioLanguages { get; set; } = "eng";    // comma-separated ISO 639-2
    public string PreferredSubtitleLanguages { get; set; } = "eng";
    public bool PassthroughDolbyTrueHd { get; set; } = true;
    public bool PassthroughDts { get; set; } = true;
    public bool PassthroughDolbyDigital { get; set; } = false;
    public double AudioGainDb { get; set; } = 0.0;

    // Picture settings
    public bool AutoCrop { get; set; } = true;
    public bool KeepAspectRatio { get; set; } = true;
    public int? MaxWidth { get; set; }
    public int? MaxHeight { get; set; }

    // HDR
    public HdrMode HdrHandling { get; set; } = HdrMode.Passthrough;

    // Subtitle defaults
    public bool BurnForcedSubtitles { get; set; } = false;
    public bool ExtractSubtitlesToSrt { get; set; } = false;
    public bool IncludeForcedSubsOnly { get; set; } = false;

    // Disc analysis
    public bool RunDiscAnalysisBeforeRip { get; set; } = true;
    public bool ShowCopyProtectionInfo { get; set; } = true;

    // HandBrake Advanced Video Settings
    public bool EnableTwoPassEncoding { get; set; } = false;
    public bool EnableTurboFirstPass { get; set; } = true;
    public string x264Preset { get; set; } = "medium"; // ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow
    public string x264Tune { get; set; } = "none"; // none, film, animation, grain, stillimage, fastdecode
    public string x264Profile { get; set; } = "auto"; // auto, baseline, main, high
    public string x265Preset { get; set; } = "medium";
    public string CustomEncoderOptions { get; set; } = string.Empty;

    // HandBrake Filters
    public bool EnableDeinterlacing { get; set; } = false;
    public string DeinterlacePreset { get; set; } = "default";
    public bool EnableDetelecine { get; set; } = false;
    public bool EnableDenoise { get; set; } = false;
    public string DenoisePreset { get; set; } = "medium"; // light, medium, strong
    public string DenoiseTune { get; set; } = "none";
    public bool EnableSharpen { get; set; } = false;
    public string SharpenPreset { get; set; } = "medium";
    public bool EnableDeblock { get; set; } = false;
    public bool GrayscaleVideo { get; set; } = false;
    
    // HandBrake Logging
    public LogVerbosity LogVerbosity { get; set; } = LogVerbosity.Standard;
    public bool CopyLogsToVideoLocation { get; set; } = false;
    public bool CopyLogsToSpecifiedLocation { get; set; } = false;
    public bool ClearLogsOlderThan30Days { get; set; } = true;
    public string CustomLogLocation { get; set; } = string.Empty;
    
    // Notifications
    public bool EnableNotifications { get; set; } = true;
    public string NotificationWebhook { get; set; } = string.Empty;
    public bool NotifyOnCompletion { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;
    public string SlackWebhook { get; set; } = string.Empty;
    public string DiscordWebhook { get; set; } = string.Empty;
    
    // UI
    public string Theme { get; set; } = "System";
    public bool ShowAdvancedOptions { get; set; } = false;
    public bool MinimizeToSystemTray { get; set; } = false;
    public bool StartMinimized { get; set; } = false;

    // Sound
    public bool EnableSounds { get; set; } = true;
    public bool PlaySoundOnCompletion { get; set; } = true;
    public bool PlaySoundOnEjection { get; set; } = true;
    public bool PlaySoundOnError { get; set; } = true;
    public bool PlaySoundOnRipStart { get; set; } = false;
    /// <summary>System sound alias name. One of: SystemAsterisk, SystemExclamation, SystemHand, SystemNotification, MailBeep.</summary>
    public string CompletionSoundAlias { get; set; } = "SystemAsterisk";
    public string EjectionSoundAlias   { get; set; } = "SystemNotification";
    public string ErrorSoundAlias      { get; set; } = "SystemHand";
    public string RipStartSoundAlias   { get; set; } = "SystemExclamation";
    /// <summary>Optional custom .wav file path. Takes priority over alias when set.</summary>
    public string CompletionSoundPath { get; set; } = string.Empty;
    public string EjectionSoundPath   { get; set; } = string.Empty;
    public string ErrorSoundPath      { get; set; } = string.Empty;
    public string RipStartSoundPath   { get; set; } = string.Empty;
}

