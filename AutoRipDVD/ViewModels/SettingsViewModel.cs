using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AutoRipDVD.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHandBrakeService _handBrakeService;
    private AppSettings _originalSettings = new();

    // ── Paths ─────────────────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _makeMkvPath = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _handBrakePath = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _outputBasePath = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _tempPath = string.Empty;

    // ── API Keys ──────────────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _omdbApiKey = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _tmdbApiKey = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _tvdbApiKey = string.Empty;

    // ── Ripping Options ───────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _autoRip;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _ripMainFeatureOnly;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _ejectWhenComplete;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _autoTranscode;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _autoMatchMetadata;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _createPlexFolderStructure;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private int _minimumTitleLengthSeconds;

    // ── MakeMKV Options ───────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _makeMKVQuality = "Original";

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _preserveDTS;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _preserveTrueHD;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _includeAllAudioTracks;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _includeAllSubtitles;

    // ── HandBrake Settings ────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _handBrakePreset = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _videoEncoder = "x264 (H.264)";

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private int _quality = 22;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _audioEncoder = "AAC";

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private int _audioBitrate = 160;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _enableTwoPassEncoding;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _x264Preset = "medium";

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _x264Tune = "none";

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _enableDeinterlacing;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _enableDenoise;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _useHardwareAcceleration;

    // ── Notifications ─────────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _enableNotifications;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _notificationWebhook = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _discordWebhook = string.Empty;

    // ── Appearance ────────────────────────────────────────────────────────────

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))]
    private ElementTheme _selectedTheme;

    // ── Available options (populated from HandBrake) ──────────────────────────

    [ObservableProperty]
    private List<string> _availablePresets = new();

    public List<string> VideoEncoderOptions { get; } = new()
    {
        "x264 (H.264)", "x265 (H.265/HEVC)", "x265 10-bit", "VP9", "AV1",
        "NVENC H.264", "NVENC H.265", "QuickSync H.264", "QuickSync H.265",
        "VCE H.264", "VCE H.265"
    };

    public List<string> AudioEncoderOptions { get; } = new()
    {
        "AAC", "AC3 (Dolby Digital)", "E-AC3 (Dolby Digital Plus)", "MP3",
        "Opus", "FLAC (Lossless)", "Passthrough (Copy)"
    };

    public List<string> MakeMKVQualityOptions { get; } = new()
    {
        "Original", "High", "Medium", "Low"
    };

    public List<string> x264PresetOptions { get; } = new()
    {
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow"
    };

    public List<string> x264TuneOptions { get; } = new()
    {
        "none", "film", "animation", "grain", "stillimage", "fastdecode"
    };

    // ── Change detection ──────────────────────────────────────────────────────

    public bool HasChanges =>
        MakeMkvPath != _originalSettings.MakeMkvPath ||
        HandBrakePath != _originalSettings.HandBrakePath ||
        OutputBasePath != _originalSettings.OutputPath ||
        TempPath != _originalSettings.TempPath ||
        OmdbApiKey != _originalSettings.OmdbApiKey ||
        TmdbApiKey != _originalSettings.TmdbApiKey ||
        TvdbApiKey != _originalSettings.TvdbApiKey ||
        AutoRip != _originalSettings.AutoRip ||
        RipMainFeatureOnly != _originalSettings.RipMainFeatureOnly ||
        EjectWhenComplete != _originalSettings.EjectWhenComplete ||
        AutoTranscode != _originalSettings.TranscodeAfterRip ||
        AutoMatchMetadata != _originalSettings.AutoMatchMetadata ||
        CreatePlexFolderStructure != _originalSettings.CreatePlexFolderStructure ||
        MinimumTitleLengthSeconds != _originalSettings.MinimumTitleLengthSeconds ||
        MakeMKVQuality != _originalSettings.MakeMkvQualityPreset.ToString() ||
        PreserveDTS != _originalSettings.PreserveDTS ||
        PreserveTrueHD != _originalSettings.PreserveTrueHD ||
        IncludeAllAudioTracks != _originalSettings.IncludeAllAudioTracks ||
        IncludeAllSubtitles != _originalSettings.IncludeAllSubtitles ||
        HandBrakePreset != _originalSettings.HandBrakePreset ||
        VideoEncoder != VideoEncoderToString(_originalSettings.VideoEncoder) ||
        Quality != _originalSettings.VideoQuality ||
        AudioEncoder != AudioEncoderToString(_originalSettings.AudioEncoder) ||
        AudioBitrate != _originalSettings.AudioBitrate ||
        EnableTwoPassEncoding != _originalSettings.EnableTwoPassEncoding ||
        X264Preset != _originalSettings.x264Preset ||
        X264Tune != _originalSettings.x264Tune ||
        EnableDeinterlacing != _originalSettings.EnableDeinterlacing ||
        EnableDenoise != _originalSettings.EnableDenoise ||
        UseHardwareAcceleration != _originalSettings.UseHardwareAcceleration ||
        EnableNotifications != _originalSettings.EnableNotifications ||
        NotificationWebhook != _originalSettings.NotificationWebhook ||
        DiscordWebhook != _originalSettings.DiscordWebhook ||
        GetThemeString() != _originalSettings.Theme;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(ISettingsService settingsService, IHandBrakeService handBrakeService)
    {
        _settingsService = settingsService;
        _handBrakeService = handBrakeService;
        LoadSettings();
        _ = LoadPresetsAsync();
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        var s = _settingsService.Settings;
        _originalSettings = CloneSettings(s);

        MakeMkvPath            = s.MakeMkvPath;
        HandBrakePath          = s.HandBrakePath;
        OutputBasePath         = s.OutputPath;
        TempPath               = s.TempPath;
        OmdbApiKey             = s.OmdbApiKey;
        TmdbApiKey             = s.TmdbApiKey;
        TvdbApiKey             = s.TvdbApiKey;
        AutoRip                = s.AutoRip;
        RipMainFeatureOnly     = s.RipMainFeatureOnly;
        EjectWhenComplete      = s.EjectWhenComplete;
        AutoTranscode          = s.TranscodeAfterRip;
        AutoMatchMetadata      = s.AutoMatchMetadata;
        CreatePlexFolderStructure = s.CreatePlexFolderStructure;
        MinimumTitleLengthSeconds = s.MinimumTitleLengthSeconds;
        MakeMKVQuality         = s.MakeMkvQualityPreset.ToString();
        PreserveDTS            = s.PreserveDTS;
        PreserveTrueHD         = s.PreserveTrueHD;
        IncludeAllAudioTracks  = s.IncludeAllAudioTracks;
        IncludeAllSubtitles    = s.IncludeAllSubtitles;
        HandBrakePreset        = s.HandBrakePreset;
        VideoEncoder           = VideoEncoderToString(s.VideoEncoder);
        Quality                = s.VideoQuality;
        AudioEncoder           = AudioEncoderToString(s.AudioEncoder);
        AudioBitrate           = s.AudioBitrate;
        EnableTwoPassEncoding  = s.EnableTwoPassEncoding;
        X264Preset             = s.x264Preset;
        X264Tune               = s.x264Tune;
        EnableDeinterlacing    = s.EnableDeinterlacing;
        EnableDenoise          = s.EnableDenoise;
        UseHardwareAcceleration = s.UseHardwareAcceleration;
        EnableNotifications    = s.EnableNotifications;
        NotificationWebhook    = s.NotificationWebhook;
        DiscordWebhook         = s.DiscordWebhook;
        SelectedTheme          = s.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark"  => ElementTheme.Dark,
            _       => ElementTheme.Default
        };
    }

    private async Task LoadPresetsAsync()
    {
        try { AvailablePresets = await _handBrakeService.GetPresetsAsync(); }
        catch { /* keep defaults */ }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var s = _settingsService.Settings;

        s.MakeMkvPath             = MakeMkvPath;
        s.HandBrakePath           = HandBrakePath;
        s.OutputPath              = OutputBasePath;
        s.TempPath                = TempPath;
        s.OmdbApiKey              = OmdbApiKey;
        s.TmdbApiKey              = TmdbApiKey;
        s.TvdbApiKey              = TvdbApiKey;
        s.AutoRip                 = AutoRip;
        s.RipMainFeatureOnly      = RipMainFeatureOnly;
        s.EjectWhenComplete       = EjectWhenComplete;
        s.TranscodeAfterRip       = AutoTranscode;
        s.AutoMatchMetadata       = AutoMatchMetadata;
        s.CreatePlexFolderStructure = CreatePlexFolderStructure;
        s.MinimumTitleLengthSeconds = MinimumTitleLengthSeconds;
        s.MakeMkvQualityPreset    = Enum.TryParse<MakeMKVQuality>(MakeMKVQuality, out var q) ? q : Models.MakeMKVQuality.Original;
        s.PreserveDTS             = PreserveDTS;
        s.PreserveTrueHD          = PreserveTrueHD;
        s.IncludeAllAudioTracks   = IncludeAllAudioTracks;
        s.IncludeAllSubtitles     = IncludeAllSubtitles;
        s.HandBrakePreset         = HandBrakePreset;
        s.VideoEncoder            = VideoEncoderFromString(VideoEncoder);
        s.VideoQuality            = Quality;
        s.AudioEncoder            = AudioEncoderFromString(AudioEncoder);
        s.AudioBitrate            = AudioBitrate;
        s.EnableTwoPassEncoding   = EnableTwoPassEncoding;
        s.x264Preset              = X264Preset;
        s.x264Tune                = X264Tune;
        s.EnableDeinterlacing     = EnableDeinterlacing;
        s.EnableDenoise           = EnableDenoise;
        s.UseHardwareAcceleration = UseHardwareAcceleration;
        s.EnableNotifications     = EnableNotifications;
        s.NotificationWebhook     = NotificationWebhook;
        s.DiscordWebhook          = DiscordWebhook;
        s.Theme                   = GetThemeString();

        await _settingsService.SaveSettingsAsync();
        LoadSettings(); // refresh originals
    }

    [RelayCommand]
    private void Cancel() => LoadSettings();

    public async Task<bool> ConfirmDiscardChangesAsync(XamlRoot xamlRoot)
    {
        if (!HasChanges) return true;

        var dialog = new ContentDialog
        {
            Title            = "Unsaved Changes",
            Content          = "You have unsaved changes. Do you want to save them before closing?",
            PrimaryButtonText   = "Save",
            SecondaryButtonText = "Discard",
            CloseButtonText     = "Cancel",
            DefaultButton    = ContentDialogButton.Primary,
            XamlRoot         = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)  { await ApplyAsync(); return true; }
        if (result == ContentDialogResult.Secondary) { Cancel();           return true; }
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetThemeString() => SelectedTheme switch
    {
        ElementTheme.Light => "Light",
        ElementTheme.Dark  => "Dark",
        _                  => "System"
    };

    private static AppSettings CloneSettings(AppSettings s) => new()
    {
        MakeMkvPath             = s.MakeMkvPath,
        HandBrakePath           = s.HandBrakePath,
        OutputPath              = s.OutputPath,
        TempPath                = s.TempPath,
        OmdbApiKey              = s.OmdbApiKey,
        TmdbApiKey              = s.TmdbApiKey,
        TvdbApiKey              = s.TvdbApiKey,
        AutoRip                 = s.AutoRip,
        RipMainFeatureOnly      = s.RipMainFeatureOnly,
        EjectWhenComplete       = s.EjectWhenComplete,
        TranscodeAfterRip       = s.TranscodeAfterRip,
        AutoMatchMetadata       = s.AutoMatchMetadata,
        CreatePlexFolderStructure = s.CreatePlexFolderStructure,
        MinimumTitleLengthSeconds = s.MinimumTitleLengthSeconds,
        MakeMkvQualityPreset    = s.MakeMkvQualityPreset,
        PreserveDTS             = s.PreserveDTS,
        PreserveTrueHD          = s.PreserveTrueHD,
        IncludeAllAudioTracks   = s.IncludeAllAudioTracks,
        IncludeAllSubtitles     = s.IncludeAllSubtitles,
        HandBrakePreset         = s.HandBrakePreset,
        VideoEncoder            = s.VideoEncoder,
        VideoQuality            = s.VideoQuality,
        AudioEncoder            = s.AudioEncoder,
        AudioBitrate            = s.AudioBitrate,
        EnableTwoPassEncoding   = s.EnableTwoPassEncoding,
        x264Preset              = s.x264Preset,
        x264Tune                = s.x264Tune,
        EnableDeinterlacing     = s.EnableDeinterlacing,
        EnableDenoise           = s.EnableDenoise,
        UseHardwareAcceleration = s.UseHardwareAcceleration,
        EnableNotifications     = s.EnableNotifications,
        NotificationWebhook     = s.NotificationWebhook,
        DiscordWebhook          = s.DiscordWebhook,
        Theme                   = s.Theme
    };

    private static string VideoEncoderToString(VideoEncoderType e) => e switch
    {
        VideoEncoderType.x265          => "x265 (H.265/HEVC)",
        VideoEncoderType.x265_10bit    => "x265 10-bit",
        VideoEncoderType.VP9           => "VP9",
        VideoEncoderType.AV1           => "AV1",
        VideoEncoderType.NVENC_H264    => "NVENC H.264",
        VideoEncoderType.NVENC_H265    => "NVENC H.265",
        VideoEncoderType.QuickSync_H264 => "QuickSync H.264",
        VideoEncoderType.QuickSync_H265 => "QuickSync H.265",
        VideoEncoderType.VCE_H264      => "VCE H.264",
        VideoEncoderType.VCE_H265      => "VCE H.265",
        _                              => "x264 (H.264)"
    };

    private static VideoEncoderType VideoEncoderFromString(string s) => s switch
    {
        "x265 (H.265/HEVC)" => VideoEncoderType.x265,
        "x265 10-bit"       => VideoEncoderType.x265_10bit,
        "VP9"               => VideoEncoderType.VP9,
        "AV1"               => VideoEncoderType.AV1,
        "NVENC H.264"       => VideoEncoderType.NVENC_H264,
        "NVENC H.265"       => VideoEncoderType.NVENC_H265,
        "QuickSync H.264"   => VideoEncoderType.QuickSync_H264,
        "QuickSync H.265"   => VideoEncoderType.QuickSync_H265,
        "VCE H.264"         => VideoEncoderType.VCE_H264,
        "VCE H.265"         => VideoEncoderType.VCE_H265,
        _                   => VideoEncoderType.x264
    };

    private static string AudioEncoderToString(AudioEncoderType e) => e switch
    {
        AudioEncoderType.AC3         => "AC3 (Dolby Digital)",
        AudioEncoderType.EAC3        => "E-AC3 (Dolby Digital Plus)",
        AudioEncoderType.MP3         => "MP3",
        AudioEncoderType.Opus        => "Opus",
        AudioEncoderType.FLAC        => "FLAC (Lossless)",
        AudioEncoderType.Passthrough => "Passthrough (Copy)",
        _                            => "AAC"
    };

    private static AudioEncoderType AudioEncoderFromString(string s) => s switch
    {
        "AC3 (Dolby Digital)"        => AudioEncoderType.AC3,
        "E-AC3 (Dolby Digital Plus)" => AudioEncoderType.EAC3,
        "MP3"                        => AudioEncoderType.MP3,
        "Opus"                       => AudioEncoderType.Opus,
        "FLAC (Lossless)"            => AudioEncoderType.FLAC,
        "Passthrough (Copy)"         => AudioEncoderType.Passthrough,
        _                            => AudioEncoderType.AAC
    };

    [RelayCommand] private void BrowseMakeMkv()  { }
    [RelayCommand] private void BrowseHandBrake() { }
    [RelayCommand] private void BrowseOutput()    { }
}
