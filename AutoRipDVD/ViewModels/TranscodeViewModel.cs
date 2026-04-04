using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

/// <summary>
/// HandBrake-style per-job transcoding view model.
/// Allows the user to configure every aspect of a transcode before it begins –
/// preset selection, video encoder / quality, audio track picker, subtitle options,
/// picture settings (crop/scale/filters), and output format.
/// </summary>
public partial class TranscodeViewModel : ObservableObject
{
    private readonly IHandBrakeService       _handBrake;
    private readonly ITranscodePresetService _presets;
    private readonly IFfprobeService         _ffprobe;
    private readonly ISettingsService        _settings;
    private readonly ILogService             _log;

    // ── Embedded sub-ViewModels ───────────────────────────────────────────────
    // These are exposed so the UI can bind them into embedded panels without
    // needing separate page navigation.
    public TrackSelectorViewModel         TrackSelector { get; }
    public SubtitleLanguagePickerViewModel LanguagePicker { get; }

    // ── Presets ────────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<string> _presetCategories = new();
    [ObservableProperty] private ObservableCollection<TranscodePreset> _availablePresets = new();
    [ObservableProperty] private string _selectedCategory = "General";
    [ObservableProperty] private TranscodePreset? _selectedPreset;

    // ── Source ────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _sourceFile   = string.Empty;
    [ObservableProperty] private string _outputFile   = string.Empty;
    [ObservableProperty] private MediaStreamInfo? _sourceInfo;

    // ── Video ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EncoderDisplayName))]
    private VideoEncoderType _videoEncoder = VideoEncoderType.x264;

    [ObservableProperty] private int     _videoQuality    = 22;
    [ObservableProperty] private bool    _useBitrateMode  = false;
    [ObservableProperty] private int     _videoBitrate    = 4000;
    [ObservableProperty] private bool    _twoPass         = false;
    [ObservableProperty] private bool    _turboFirstPass  = true;
    [ObservableProperty] private string  _encoderPreset   = "medium";
    [ObservableProperty] private string  _encoderProfile  = "auto";
    [ObservableProperty] private string  _encoderTune     = "none";

    // Resolution
    [ObservableProperty] private int?    _maxWidth;
    [ObservableProperty] private int?    _maxHeight;
    [ObservableProperty] private bool    _keepAspectRatio = true;

    // ── Filters ───────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _enableDeinterlace    = false;
    [ObservableProperty] private string _deinterlacePreset    = "default";
    [ObservableProperty] private bool   _enableDetelecine     = false;
    [ObservableProperty] private bool   _enableDenoise        = false;
    [ObservableProperty] private string _denoisePreset        = "medium";
    [ObservableProperty] private string _denoiseTune          = "none";
    [ObservableProperty] private bool   _enableSharpen        = false;
    [ObservableProperty] private string _sharpenPreset        = "medium";
    [ObservableProperty] private bool   _enableDeblock        = false;
    [ObservableProperty] private bool   _grayscale            = false;
    [ObservableProperty] private HdrMode _hdrHandling         = HdrMode.Passthrough;

    // Cropping
    [ObservableProperty] private bool   _autoCrop     = true;
    [ObservableProperty] private int    _cropTop      = 0;
    [ObservableProperty] private int    _cropBottom   = 0;
    [ObservableProperty] private int    _cropLeft     = 0;
    [ObservableProperty] private int    _cropRight    = 0;

    // ── Audio ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private AudioEncoderType _audioEncoder    = AudioEncoderType.AAC;
    [ObservableProperty] private int              _audioBitrate    = 160;
    [ObservableProperty] private AudioMixdown     _audioMixdown    = AudioMixdown.Auto;
    [ObservableProperty] private bool             _includeAllAudio = true;
    [ObservableProperty] private bool             _passthroughDts    = true;
    [ObservableProperty] private bool             _passthroughTrueHd = true;
    [ObservableProperty] private bool             _passthroughAc3    = false;
    [ObservableProperty] private double           _audioGain       = 0.0;

    [ObservableProperty]
    private ObservableCollection<AudioTrackSelection> _audioTracks = new();

    // ── Subtitles ─────────────────────────────────────────────────────────────

    [ObservableProperty] private bool  _includeSubtitles   = true;
    [ObservableProperty] private bool  _burnFirstSubtitle  = false;
    [ObservableProperty] private bool  _forcedSubsOnly     = false;

    [ObservableProperty]
    private ObservableCollection<SubtitleTrackSelection> _subtitleTracks = new();

    // ── Output ────────────────────────────────────────────────────────────────

    [ObservableProperty] private OutputFormat _outputFormat    = OutputFormat.MKV;
    [ObservableProperty] private bool         _chapterMarkers  = true;
    [ObservableProperty] private bool         _optimizeForWeb  = false;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty] private double  _encodeProgress = 0;
    [ObservableProperty] private bool    _isEncoding     = false;
    [ObservableProperty] private string  _statusMessage  = string.Empty;
    [ObservableProperty] private bool    _hasSourceInfo  = false;

    private CancellationTokenSource? _encodeCts;

    // ── Computed ──────────────────────────────────────────────────────────────

    public string EncoderDisplayName => VideoEncoder switch
    {
        VideoEncoderType.x264           => "H.264 (x264)",
        VideoEncoderType.x265           => "H.265 HEVC (x265)",
        VideoEncoderType.x265_10bit     => "H.265 10-bit (x265)",
        VideoEncoderType.VP9            => "VP9",
        VideoEncoderType.AV1            => "AV1 (SVT-AV1)",
        VideoEncoderType.NVENC_H264     => "H.264 NVENC (NVIDIA GPU)",
        VideoEncoderType.NVENC_H265     => "H.265 NVENC (NVIDIA GPU)",
        VideoEncoderType.QuickSync_H264 => "H.264 QuickSync (Intel GPU)",
        VideoEncoderType.QuickSync_H265 => "H.265 QuickSync (Intel GPU)",
        VideoEncoderType.VCE_H264       => "H.264 VCE (AMD GPU)",
        VideoEncoderType.VCE_H265       => "H.265 VCE (AMD GPU)",
        _                               => VideoEncoder.ToString()
    };

    public bool IsRfMode     => !UseBitrateMode;
    public bool IsBitrateMode => UseBitrateMode;
    public bool CanEncode    => !IsEncoding && !string.IsNullOrEmpty(SourceFile);
    public bool CanCancel    => IsEncoding;

    // ── Static option lists ───────────────────────────────────────────────────

    public static readonly IReadOnlyList<VideoEncoderType> VideoEncoders
        = Enum.GetValues<VideoEncoderType>().ToList().AsReadOnly();

    public static readonly IReadOnlyList<AudioEncoderType> AudioEncoders
        = Enum.GetValues<AudioEncoderType>().ToList().AsReadOnly();

    public static readonly IReadOnlyList<AudioMixdown> AudioMixdowns
        = Enum.GetValues<AudioMixdown>().ToList().AsReadOnly();

    public static readonly IReadOnlyList<OutputFormat> OutputFormats
        = Enum.GetValues<OutputFormat>().ToList().AsReadOnly();

    public static readonly IReadOnlyList<HdrMode> HdrModes
        = Enum.GetValues<HdrMode>().ToList().AsReadOnly();

    public static readonly IReadOnlyList<string> EncoderPresets = new[]
        { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" };

    public static readonly IReadOnlyList<string> x264Tunes = new[]
        { "none", "film", "animation", "grain", "stillimage", "fastdecode", "zerolatency" };

    public static readonly IReadOnlyList<string> x264Profiles = new[]
        { "auto", "baseline", "main", "high", "high10", "high422", "high444" };

    public static readonly IReadOnlyList<string> DeinterlacePresets = new[]
        { "default", "skip-spatial", "bob", "qsv" };

    public static readonly IReadOnlyList<string> DenoisePresets = new[]
        { "ultralight", "light", "medium", "strong", "custom" };

    public static readonly IReadOnlyList<string> DenoiseTunes = new[]
        { "none", "film", "grain", "highmotion", "animation", "tape", "sprite" };

    // ── Constructor ───────────────────────────────────────────────────────────

    public TranscodeViewModel(
        IHandBrakeService         handBrake,
        ITranscodePresetService   presets,
        IFfprobeService           ffprobe,
        ISettingsService          settings,
        ILogService               log,
        TrackSelectorViewModel    trackSelector,
        SubtitleLanguagePickerViewModel languagePicker)
    {
        _handBrake     = handBrake;
        _presets       = presets;
        _ffprobe       = ffprobe;
        _settings      = settings;
        _log           = log;
        TrackSelector  = trackSelector;
        LanguagePicker = languagePicker;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public async Task InitialiseAsync()
    {
        // Load presets
        await _presets.LoadCustomPresetsAsync();
        ReloadPresets();

        // Apply global defaults from settings
        LoadFromSettings(_settings.Settings);

        // Select default preset
        var defaultPreset = _presets.GetDefault();
        SelectedPreset = defaultPreset;
        if (defaultPreset != null)
            ApplyPreset(defaultPreset);
    }

    partial void OnSelectedCategoryChanged(string value) => ReloadPresets();

    partial void OnSelectedPresetChanged(TranscodePreset? value)
    {
        if (value != null) ApplyPreset(value);
    }

    partial void OnUseBitrateModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRfMode));
        OnPropertyChanged(nameof(IsBitrateMode));
    }

    // ── Source loading ────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadSourceAsync(string filePath)
    {
        SourceFile    = filePath;
        OutputFile    = string.IsNullOrEmpty(OutputFile)
            ? Path.ChangeExtension(filePath, ".out.mkv") : OutputFile;
        StatusMessage = "Analysing source file…";

        // Delegate full analysis (ffprobe + filmstrip) to TrackSelectorViewModel
        await TrackSelector.LoadSourceAsync(filePath);

        SourceInfo    = TrackSelector.MediaInfo;
        HasSourceInfo = SourceInfo != null;

        if (SourceInfo != null)
        {
            // Mirror track collections for backwards compat with existing binding paths
            AudioTracks.Clear();
            foreach (var a in SourceInfo.AudioStreams)
                AudioTracks.Add(new AudioTrackSelection(a) { IsSelected = true });

            SubtitleTracks.Clear();
            foreach (var s in SourceInfo.SubtitleStreams)
                SubtitleTracks.Add(new SubtitleTrackSelection(s) { IsSelected = true });

            StatusMessage = $"Source: {SourceInfo.PrimaryVideo?.ResolutionLabel ?? "?"} | " +
                            $"{SourceInfo.AudioTrackCount} audio | " +
                            $"{SourceInfo.SubtitleTrackCount} subtitle track(s)";
        }
        else
        {
            StatusMessage = "Could not analyse source (configure ffprobe path in Settings)";
        }

        OnPropertyChanged(nameof(CanEncode));
    }

    // ── Encode / Cancel ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanEncode))]
    public async Task StartEncodeAsync()
    {
        if (string.IsNullOrEmpty(SourceFile) || string.IsNullOrEmpty(OutputFile)) return;

        _encodeCts  = new CancellationTokenSource();
        IsEncoding  = true;
        EncodeProgress = 0;
        StatusMessage = "Encoding…";

        OnPropertyChanged(nameof(CanEncode));
        OnPropertyChanged(nameof(CanCancel));

        try
        {
            var preset   = BuildCurrentPreset();
            var jobSettings = BuildJobSettings();
            var prog     = new Progress<double>(p => { EncodeProgress = p; });

            var success = await _handBrake.TranscodeWithPresetAsync(
                SourceFile, OutputFile, preset, jobSettings, prog, _encodeCts.Token);

            StatusMessage = success ? "Encode complete!" : "Encode failed – check logs";
            EncodeProgress = success ? 100 : 0;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Encode cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await _log.LogAsync($"TranscodeViewModel encode error: {ex.Message}");
        }
        finally
        {
            IsEncoding = false;
            OnPropertyChanged(nameof(CanEncode));
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void CancelEncode()
    {
        _encodeCts?.Cancel();
        StatusMessage = "Cancelling…";
    }

    [RelayCommand]
    public async Task SaveCustomPresetAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var preset = BuildCurrentPreset() with { Name = name, Category = "Custom", IsBuiltIn = false };
        await _presets.SaveCustomPresetAsync(preset);
        ReloadPresets();
        StatusMessage = $"Preset '{name}' saved";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ReloadPresets()
    {
        PresetCategories.Clear();
        foreach (var cat in _presets.Categories)
            PresetCategories.Add(cat);

        AvailablePresets.Clear();
        var filtered = string.IsNullOrEmpty(SelectedCategory)
            ? _presets.AllPresets
            : _presets.GetByCategory(SelectedCategory);
        foreach (var p in filtered)
            AvailablePresets.Add(p);
    }

    private void ApplyPreset(TranscodePreset p)
    {
        VideoEncoder    = p.VideoEncoder;
        VideoQuality    = p.VideoQuality;
        UseBitrateMode  = p.UseBitrateMode;
        VideoBitrate    = p.VideoBitrate ?? 4000;
        TwoPass         = p.TwoPass;
        TurboFirstPass  = p.TurboFirstPass;
        EncoderPreset   = p.EncoderPreset;
        EncoderProfile  = p.EncoderProfile;
        EncoderTune     = p.EncoderTune;

        MaxWidth        = p.Width;
        MaxHeight       = p.Height;
        KeepAspectRatio = p.KeepAspectRatio;

        EnableDeinterlace  = p.Deinterlace;
        DeinterlacePreset  = p.DeinterlacePreset;
        EnableDetelecine   = p.Detelecine;
        EnableDenoise      = p.Denoise;
        DenoisePreset      = p.DenoisePreset;
        DenoiseTune        = p.DenoiseTune;
        EnableSharpen      = p.Sharpen;
        SharpenPreset      = p.SharpenPreset;
        EnableDeblock      = p.Deblock;
        Grayscale          = p.GrayscaleVideo;
        HdrHandling        = p.HdrHandling;

        AutoCrop        = p.AutoCrop;
        CropTop         = p.CropTop;
        CropBottom      = p.CropBottom;
        CropLeft        = p.CropLeft;
        CropRight       = p.CropRight;

        AudioEncoder     = p.AudioEncoder;
        AudioBitrate     = p.AudioBitrate;
        AudioMixdown     = p.AudioMixdown;
        IncludeAllAudio  = p.IncludeAllAudioTracks;
        PassthroughDts   = p.PassthroughDts;
        PassthroughTrueHd = p.PassthroughTrueHd;
        PassthroughAc3   = p.PassthroughAc3;

        IncludeSubtitles  = p.IncludeSubtitles;
        BurnFirstSubtitle = p.BurnFirstSubtitle;
        ForcedSubsOnly    = p.ForcedSubtitlesOnly;

        OutputFormat    = p.OutputFormat;
        ChapterMarkers  = p.ChapterMarkers;
        OptimizeForWeb  = p.OptimizeForWeb;
    }

    private void LoadFromSettings(AppSettings s)
    {
        VideoEncoder    = s.VideoEncoder;
        VideoQuality    = s.VideoQuality;
        AudioEncoder    = s.AudioEncoder;
        AudioBitrate    = s.AudioBitrate;
        AudioMixdown    = s.DefaultAudioMixdown;
        OutputFormat    = s.DefaultOutputFormat;
        TwoPass         = s.EnableTwoPassEncoding;
        TurboFirstPass  = s.EnableTurboFirstPass;
        EncoderPreset   = s.x264Preset;
        EncoderTune     = s.x264Tune;
        EncoderProfile  = s.x264Profile;
        EnableDeinterlace = s.EnableDeinterlacing;
        DeinterlacePreset = s.DeinterlacePreset;
        EnableDetelecine = s.EnableDetelecine;
        EnableDenoise   = s.EnableDenoise;
        DenoisePreset   = s.DenoisePreset;
        DenoiseTune     = s.DenoiseTune;
        EnableSharpen   = s.EnableSharpen;
        SharpenPreset   = s.SharpenPreset;
        EnableDeblock   = s.EnableDeblock;
        Grayscale       = s.GrayscaleVideo;
        AutoCrop        = s.AutoCrop;
        KeepAspectRatio = s.KeepAspectRatio;
        PassthroughDts  = s.PassthroughDts;
        PassthroughTrueHd = s.PassthroughDolbyTrueHd;
        PassthroughAc3  = s.PassthroughDolbyDigital;
        HdrHandling     = s.HdrHandling;
        BurnFirstSubtitle = s.BurnForcedSubtitles;
        ForcedSubsOnly  = s.IncludeForcedSubsOnly;
        AudioGain       = s.AudioGainDb;
    }

    private TranscodePreset BuildCurrentPreset()
        => new()
        {
            Name         = SelectedPreset?.Name ?? "Custom",
            Category     = SelectedPreset?.Category ?? "Custom",
            IsBuiltIn    = false,
            OutputFormat = OutputFormat,
            VideoEncoder = VideoEncoder,
            VideoQuality = VideoQuality,
            VideoBitrate = UseBitrateMode ? VideoBitrate : null,
            UseBitrateMode = UseBitrateMode,
            Width        = MaxWidth,
            Height       = MaxHeight,
            KeepAspectRatio = KeepAspectRatio,
            EncoderPreset = EncoderPreset,
            EncoderProfile = EncoderProfile,
            EncoderTune  = EncoderTune,
            TwoPass      = TwoPass,
            TurboFirstPass = TurboFirstPass,
            Deinterlace  = EnableDeinterlace,
            DeinterlacePreset = DeinterlacePreset,
            Detelecine   = EnableDetelecine,
            Denoise      = EnableDenoise,
            DenoisePreset = DenoisePreset,
            DenoiseTune  = DenoiseTune,
            Sharpen      = EnableSharpen,
            SharpenPreset = SharpenPreset,
            Deblock      = EnableDeblock,
            GrayscaleVideo = Grayscale,
            HdrHandling  = HdrHandling,
            AutoCrop     = AutoCrop,
            CropTop      = CropTop,
            CropBottom   = CropBottom,
            CropLeft     = CropLeft,
            CropRight    = CropRight,
            AudioEncoder = AudioEncoder,
            AudioBitrate = AudioBitrate,
            AudioMixdown = AudioMixdown,
            AudioGain    = AudioGain,
            IncludeAllAudioTracks = IncludeAllAudio,
            PassthroughDts = PassthroughDts,
            PassthroughTrueHd = PassthroughTrueHd,
            PassthroughAc3 = PassthroughAc3,
            IncludeSubtitles = IncludeSubtitles,
            BurnFirstSubtitle = BurnFirstSubtitle,
            ForcedSubtitlesOnly = ForcedSubsOnly,
            ChapterMarkers = ChapterMarkers,
            OptimizeForWeb = OptimizeForWeb
        };

    private TranscodeJobSettings BuildJobSettings()
    {
        // If the TrackSelectorViewModel has a loaded source, its selections are
        // the authoritative per-track choices (the user may have used the DVD Shrink
        // panel to toggle individual tracks and set burn-in targets).
        if (TrackSelector.HasSource)
            return TrackSelector.BuildJobSettings();

        // Fallback: use the simple track lists in this ViewModel
        var js = new TranscodeJobSettings { UseGlobalSettings = false };

        js.AudioTrackIndices = AudioTracks
            .Where(t => t.IsSelected)
            .Select(t => t.Stream.StreamIndex)
            .ToList();

        js.SubtitleTrackIndices = SubtitleTracks
            .Where(t => t.IsSelected)
            .Select(t => t.Stream.StreamIndex)
            .ToList();

        foreach (var sub in SubtitleTracks.Where(t => t.IsSelected))
            js.SubtitleDispositions[sub.Stream.StreamIndex] = sub.Disposition;

        js.OverrideCrop = !AutoCrop;
        js.CropTop      = CropTop;
        js.CropBottom   = CropBottom;
        js.CropLeft     = CropLeft;
        js.CropRight    = CropRight;

        return js;
    }
}

// ── Track selection helpers ───────────────────────────────────────────────────

public partial class AudioTrackSelection : ObservableObject
{
    public AudioStreamInfo Stream { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private AudioEncoderType _encoder = AudioEncoderType.Auto;
    [ObservableProperty] private AudioMixdown _mixdown = AudioMixdown.Auto;
    [ObservableProperty] private int _bitrate = 160;

    public string DisplayName
        => $"Track {Stream.StreamIndex + 1}: {Stream.Language} – {Stream.FormattedChannels} {Stream.Codec.ToUpperInvariant()}"
           + (Stream.IsDefault ? " [Default]" : string.Empty)
           + (Stream.IsAtmos ? " Atmos" : string.Empty)
           + (Stream.IsDtsx ? " DTS:X" : string.Empty);

    public AudioTrackSelection(AudioStreamInfo stream) => Stream = stream;
}

public partial class SubtitleTrackSelection : ObservableObject
{
    public SubtitleStreamInfo Stream { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private SubtitleDisposition _disposition = SubtitleDisposition.PassThrough;

    public string DisplayName
        => $"Track {Stream.StreamIndex + 1}: {Stream.Language} ({Stream.Codec})"
           + (Stream.IsForced ? " [Forced]" : string.Empty)
           + (Stream.IsDefault ? " [Default]" : string.Empty);

    public SubtitleTrackSelection(SubtitleStreamInfo stream)
    {
        Stream      = stream;
        IsSelected  = true;
        Disposition = stream.IsForced ? SubtitleDisposition.BurnIn : SubtitleDisposition.PassThrough;
    }
}
