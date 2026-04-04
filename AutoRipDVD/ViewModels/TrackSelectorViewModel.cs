using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

/// <summary>
/// DVD Shrink–style combined track selector + live video preview.
///
/// Displays:
///   Left panel  – disc / title structure (title, duration, size)
///   Right panel – Video section      (resolution, codec, bitrate – read-only)
///               – Audio section      (numbered, codec, channels, language,
///                                     estimated size, checkbox to include)
///               – Subpicture section (numbered, language, forced flag,
///                                     estimated size, checkbox + burn option)
///   Bottom       – Video preview pane (filmstrip + live seeking)
///                  Updates when a subtitle is selected to show rendered text.
/// </summary>
public partial class TrackSelectorViewModel : ObservableObject
{
    private readonly IFfprobeService      _ffprobe;
    private readonly IDiscPreviewService  _preview;
    private readonly ISettingsService     _settings;
    private readonly ILogService          _log;

    // ── Source info ───────────────────────────────────────────────────────────

    [ObservableProperty] private string        _sourcePath   = string.Empty;
    [ObservableProperty] private MediaStreamInfo? _mediaInfo;
    [ObservableProperty] private bool          _hasSource    = false;
    [ObservableProperty] private string        _sourceTitle  = string.Empty;
    [ObservableProperty] private TimeSpan      _totalDuration;
    [ObservableProperty] private long          _totalSourceBytes;

    // ── Track lists ───────────────────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<SelectableAudioTrack> _audioTracks = new();

    [ObservableProperty]
    private ObservableCollection<SelectableSubtitleTrack> _subtitleTracks = new();

    // ── Video info (read-only display) ────────────────────────────────────────

    [ObservableProperty] private string _videoCodec      = string.Empty;
    [ObservableProperty] private string _videoResolution = string.Empty;
    [ObservableProperty] private string _videoFrameRate  = string.Empty;
    [ObservableProperty] private string _videoBitrate    = string.Empty;
    [ObservableProperty] private string _videoHdr        = string.Empty;
    [ObservableProperty] private string _videoAspect     = string.Empty;

    // ── Size estimates ────────────────────────────────────────────────────────

    [ObservableProperty] private string _selectedSizeEstimate = "—";
    [ObservableProperty] private string _totalSizeLabel       = "—";
    [ObservableProperty] private double _compressionRatio     = 1.0;

    // ── Preview panel ─────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<FilmstripFrame> _filmstripFrames = new();
    [ObservableProperty] private FilmstripFrame?   _selectedFrame;
    [ObservableProperty] private string?           _previewImagePath;
    [ObservableProperty] private bool              _isLoadingPreview = false;
    [ObservableProperty] private bool              _showSubtitleOverlay = false;
    [ObservableProperty] private SelectableSubtitleTrack? _previewSubtitleTrack;
    [ObservableProperty] private double            _seekPosition    = 0;  // 0–100 % slider
    [ObservableProperty] private string            _seekTimeLabel   = "00:00:00";
    [ObservableProperty] private bool              _filmstripLoaded = false;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _statusMessage  = string.Empty;
    [ObservableProperty] private bool   _isBusy         = false;

    private CancellationTokenSource? _previewCts;

    // ── Constructor ───────────────────────────────────────────────────────────

    public TrackSelectorViewModel(
        IFfprobeService     ffprobe,
        IDiscPreviewService preview,
        ISettingsService    settings,
        ILogService         log)
    {
        _ffprobe  = ffprobe;
        _preview  = preview;
        _settings = settings;
        _log      = log;
    }

    // ── Loading a source file ─────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadSourceAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        IsBusy        = true;
        SourcePath    = filePath;
        SourceTitle   = Path.GetFileNameWithoutExtension(filePath);
        StatusMessage = "Analysing tracks…";

        try
        {
            var info = await _ffprobe.AnalyseFileAsync(filePath);
            MediaInfo = info;
            HasSource = info != null;

            if (info != null)
            {
                PopulateVideoInfo(info);
                PopulateAudioTracks(info);
                PopulateSubtitleTracks(info);
                TotalDuration   = info.Duration;
                TotalSourceBytes = info.FileSizeBytes;
                UpdateSizeEstimate();
                StatusMessage = $"Ready – {info.AudioTrackCount} audio, {info.SubtitleTrackCount} subtitle track(s)";
            }
            else
            {
                StatusMessage = "Could not analyse file (ffprobe not configured)";
            }

            // Auto-load filmstrip in background
            _ = LoadFilmstripAsync(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await _log.LogAsync($"TrackSelector load error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Filmstrip ─────────────────────────────────────────────────────────────

    private async Task LoadFilmstripAsync(string filePath)
    {
        FilmstripLoaded = false;
        FilmstripFrames.Clear();
        StatusMessage = "Generating preview thumbnails…";

        var frameCount  = _settings.Settings.FilmstripFrameCount;
        var thumbWidth  = _settings.Settings.PreviewThumbnailWidth;
        var thumbHeight = _settings.Settings.PreviewThumbnailHeight;

        var prog = new Progress<double>(p => StatusMessage = $"Loading filmstrip… {p:F0}%");
        var paths = await _preview.ExtractFilmstripAsync(filePath, frameCount, thumbWidth, thumbHeight, prog);

        FilmstripFrames.Clear();
        var duration = TotalDuration.TotalSeconds;
        for (int i = 0; i < paths.Count; i++)
        {
            // Approximate seek time for each frame
            var pct     = paths.Count > 1 ? (double)i / (paths.Count - 1) : 0;
            var seekSec = duration * 0.05 + (duration * 0.90 * pct);
            FilmstripFrames.Add(new FilmstripFrame
            {
                ImagePath = paths[i],
                SeekTime  = TimeSpan.FromSeconds(seekSec),
                Index     = i
            });
        }

        FilmstripLoaded = FilmstripFrames.Count > 0;
        if (FilmstripLoaded)
        {
            // Auto-select middle frame
            var mid = FilmstripFrames[FilmstripFrames.Count / 2];
            await SelectFrameAsync(mid);
        }

        StatusMessage = FilmstripLoaded ? "Preview ready" : "Preview unavailable (configure ffmpeg path in Settings)";
    }

    // ── Frame selection ───────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SelectFrameAsync(FilmstripFrame frame)
    {
        SelectedFrame   = frame;
        SeekTimeLabel   = frame.SeekTime.ToString(@"hh\:mm\:ss");
        SeekPosition    = TotalDuration.TotalSeconds > 0
            ? frame.SeekTime.TotalSeconds / TotalDuration.TotalSeconds * 100.0 : 0;

        await RefreshPreviewImageAsync(frame.SeekTime);
    }

    /// <summary>Called when the user drags the seek slider.</summary>
    [RelayCommand]
    public async Task SeekAsync(double positionPercent)
    {
        SeekPosition = positionPercent;
        var seekTime = TimeSpan.FromSeconds(TotalDuration.TotalSeconds * positionPercent / 100.0);
        SeekTimeLabel = seekTime.ToString(@"hh\:mm\:ss");
        await RefreshPreviewImageAsync(seekTime);
    }

    // ── Subtitle overlay toggle ───────────────────────────────────────────────

    partial void OnShowSubtitleOverlayChanged(bool value)
    {
        if (SelectedFrame != null)
            _ = RefreshPreviewImageAsync(SelectedFrame.SeekTime);
    }

    partial void OnPreviewSubtitleTrackChanged(SelectableSubtitleTrack? value)
    {
        if (SelectedFrame != null && ShowSubtitleOverlay)
            _ = RefreshPreviewImageAsync(SelectedFrame.SeekTime);
    }

    private async Task RefreshPreviewImageAsync(TimeSpan seekTime)
    {
        if (!HasSource || string.IsNullOrEmpty(SourcePath)) return;

        // Cancel any in-flight preview request
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        IsLoadingPreview = true;
        try
        {
            string? imagePath;

            if (ShowSubtitleOverlay && PreviewSubtitleTrack != null)
            {
                // Render selected subtitle stream burned into the frame
                imagePath = await _preview.ExtractFrameWithSubtitleAsync(
                    SourcePath, seekTime,
                    PreviewSubtitleTrack.Track.StreamIndex,
                    _settings.Settings.PreviewWidth,
                    _settings.Settings.PreviewHeight);
            }
            else
            {
                imagePath = await _preview.ExtractFrameAsync(
                    SourcePath, seekTime,
                    _settings.Settings.PreviewWidth,
                    _settings.Settings.PreviewHeight);
            }

            if (!ct.IsCancellationRequested)
                PreviewImagePath = imagePath;
        }
        catch (OperationCanceledException) { }
        finally { IsLoadingPreview = false; }
    }

    // ── Track selection helpers ───────────────────────────────────────────────

    [RelayCommand]
    public void SelectAllAudio()
    {
        foreach (var t in AudioTracks) t.IsIncluded = true;
        UpdateSizeEstimate();
    }

    [RelayCommand]
    public void SelectNoAudio()
    {
        foreach (var t in AudioTracks) t.IsIncluded = false;
        UpdateSizeEstimate();
    }

    [RelayCommand]
    public void SelectAllSubtitles()
    {
        foreach (var t in SubtitleTracks) t.IsIncluded = true;
        UpdateSizeEstimate();
    }

    [RelayCommand]
    public void SelectNoSubtitles()
    {
        foreach (var t in SubtitleTracks) t.IsIncluded = false;
        UpdateSizeEstimate();
    }

    [RelayCommand]
    public void ApplyPreferredLanguages()
    {
        var audLangs = ParseLangSet(_settings.Settings.PreferredAudioLanguages);
        var subLangs = ParseLangSet(_settings.Settings.PreferredSubtitleLanguages);

        foreach (var t in AudioTracks)
            t.IsIncluded = audLangs.Count == 0 || audLangs.Contains(t.Track.LanguageCode);

        foreach (var t in SubtitleTracks)
            t.IsIncluded = subLangs.Count == 0 || subLangs.Contains(t.Track.LanguageCode);

        // Always ensure at least one audio track is included
        if (AudioTracks.Any() && !AudioTracks.Any(t => t.IsIncluded))
            AudioTracks[0].IsIncluded = true;

        UpdateSizeEstimate();
        StatusMessage = "Preferred languages applied";
    }

    /// <summary>Set a subtitle track as the burn-in target (only one at a time).</summary>
    [RelayCommand]
    public void SetSubtitleBurnIn(SelectableSubtitleTrack target)
    {
        foreach (var t in SubtitleTracks)
            t.IsBurnedIn = false;
        target.IsBurnedIn = true;
        target.IsIncluded = true;
        PreviewSubtitleTrack  = target;
        ShowSubtitleOverlay   = true;
    }

    [RelayCommand]
    public void ClearBurnIn()
    {
        foreach (var t in SubtitleTracks)
            t.IsBurnedIn = false;
        ShowSubtitleOverlay  = false;
        PreviewSubtitleTrack = null;
        _ = RefreshPreviewImageAsync(SelectedFrame?.SeekTime ?? TimeSpan.FromMinutes(5));
    }

    // ── Build TranscodeJobSettings from current selections ────────────────────

    public TranscodeJobSettings BuildJobSettings()
    {
        var js = new TranscodeJobSettings { UseGlobalSettings = false };

        js.AudioTrackIndices = AudioTracks
            .Where(t => t.IsIncluded)
            .Select(t => t.Track.StreamIndex)
            .ToList();

        js.SubtitleTrackIndices = SubtitleTracks
            .Where(t => t.IsIncluded)
            .Select(t => t.Track.StreamIndex)
            .ToList();

        foreach (var sub in SubtitleTracks.Where(t => t.IsIncluded))
        {
            js.SubtitleDispositions[sub.Track.StreamIndex] =
                sub.IsBurnedIn ? SubtitleDisposition.BurnIn : SubtitleDisposition.PassThrough;
        }

        return js;
    }

    // ── Populate helpers ──────────────────────────────────────────────────────

    private void PopulateVideoInfo(MediaStreamInfo info)
    {
        var v = info.PrimaryVideo;
        if (v == null) return;

        VideoCodec      = $"{v.Codec.ToUpperInvariant()} – {v.Profile}".TrimEnd(' ', '–');
        VideoResolution = $"{v.Width}×{v.Height} ({v.ResolutionLabel})";
        VideoFrameRate  = $"{v.FrameRate:F3} fps";
        VideoBitrate    = v.BitRate > 0 ? $"{v.BitRate / 1_000_000.0:F1} Mb/s" : "—";
        VideoHdr        = v.IsHdr ? v.HdrType : "SDR";
        VideoAspect     = v.AspectRatio;
    }

    private void PopulateAudioTracks(MediaStreamInfo info)
    {
        var audLangs = ParseLangSet(_settings.Settings.PreferredAudioLanguages);
        AudioTracks.Clear();

        foreach (var a in info.AudioStreams)
        {
            var track = new SelectableAudioTrack(a)
            {
                IsIncluded = audLangs.Count == 0 || audLangs.Contains(a.LanguageCode)
            };
            track.PropertyChanged += (_, _) => UpdateSizeEstimate();
            AudioTracks.Add(track);
        }

        // If preferred language filter excluded everything, include at least track 1
        if (AudioTracks.Any() && !AudioTracks.Any(t => t.IsIncluded))
            AudioTracks[0].IsIncluded = true;
    }

    private void PopulateSubtitleTracks(MediaStreamInfo info)
    {
        var subLangs = ParseLangSet(_settings.Settings.PreferredSubtitleLanguages);
        SubtitleTracks.Clear();

        foreach (var s in info.SubtitleStreams)
        {
            var track = new SelectableSubtitleTrack(s)
            {
                IsIncluded = subLangs.Count == 0 || subLangs.Contains(s.LanguageCode)
            };
            track.PropertyChanged += (_, _) => UpdateSizeEstimate();
            SubtitleTracks.Add(track);
        }
    }

    private void UpdateSizeEstimate()
    {
        if (TotalSourceBytes <= 0) return;

        // Rough estimate: video ≈ 75 % of disc size; each audio track ≈ proportional to bitrate
        var videoBytes = TotalSourceBytes * 0.75;
        var totalAudioBytes = TotalSourceBytes * 0.24;
        var totalSubBytes   = TotalSourceBytes * 0.01;

        var audioTrackBytes   = AudioTracks.Count > 0 ? totalAudioBytes / AudioTracks.Count : 0;
        var subTrackBytes     = SubtitleTracks.Count > 0 ? totalSubBytes / SubtitleTracks.Count : 0;

        var selectedAudio  = AudioTracks.Count(t => t.IsIncluded);
        var selectedSubs   = SubtitleTracks.Count(t => t.IsIncluded);

        // Update per-track size labels
        foreach (var a in AudioTracks)
            a.EstimatedSizeLabel = FormatBytes((long)audioTrackBytes);
        foreach (var s in SubtitleTracks)
            s.EstimatedSizeLabel = FormatBytes((long)subTrackBytes);

        var total = (long)(videoBytes + selectedAudio * audioTrackBytes + selectedSubs * subTrackBytes);
        SelectedSizeEstimate = FormatBytes(total);
        TotalSizeLabel       = FormatBytes(TotalSourceBytes);
        CompressionRatio     = TotalSourceBytes > 0 ? (double)total / TotalSourceBytes : 1.0;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F2} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        return $"{bytes / 1024.0:F1} KB";
    }

    private static HashSet<string> ParseLangSet(string csv)
        => new(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
               StringComparer.OrdinalIgnoreCase);
}

// ── Selectable track items ────────────────────────────────────────────────────

public partial class SelectableAudioTrack : ObservableObject
{
    public AudioStreamInfo Track { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackground))]
    private bool _isIncluded = true;

    [ObservableProperty] private string _estimatedSizeLabel = "—";

    public string TrackNumber    => $"{Track.StreamIndex + 1}.";
    public string CodecLabel     => $"{Track.Codec.ToUpperInvariant()} {Track.FormattedChannels}";
    public string LanguageLabel  => string.IsNullOrEmpty(Track.Language) ? "Unknown" : Track.Language;
    public string BitrateLabel   => Track.IsLossless ? "Lossless"
                                  : Track.BitRate > 0 ? $"{Track.BitRate / 1000} kbps" : "—";
    public string QualityBadge
        => Track.IsAtmos ? "Atmos"
         : Track.IsDtsx  ? "DTS:X"
         : Track.IsLossless ? "Lossless"
         : string.Empty;

    /// <summary>Background hint for the row – matches DVD Shrink's green/white style.</summary>
    public string RowBackground => IsIncluded ? "#F0FFF0" : "Transparent";

    public string FullDescription
        => $"{Track.StreamIndex + 1}. {Track.Codec.ToUpperInvariant()} {Track.FormattedChannels} " +
           $"{LanguageLabel}" +
           (!string.IsNullOrEmpty(QualityBadge) ? $" [{QualityBadge}]" : string.Empty) +
           (Track.IsDefault ? " ★" : string.Empty);

    public SelectableAudioTrack(AudioStreamInfo track) => Track = track;
}

public partial class SelectableSubtitleTrack : ObservableObject
{
    public SubtitleStreamInfo Track { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackground))]
    private bool _isIncluded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackground))]
    [NotifyPropertyChangedFor(nameof(BurnInLabel))]
    private bool _isBurnedIn = false;

    [ObservableProperty] private string _estimatedSizeLabel = "—";

    public string TrackNumber   => $"{Track.StreamIndex + 1}.";
    public string LanguageLabel => string.IsNullOrEmpty(Track.Language) ? "Unknown" : Track.Language;
    public string CodecLabel    => Track.IsBitmapBased ? "Bitmap (PGS/VOBsub)" : "Text (SRT/ASS)";
    public string ForcedLabel   => Track.IsForced  ? "[Forced]"  : string.Empty;
    public string DefaultLabel  => Track.IsDefault ? "[Default]" : string.Empty;
    public string BurnInLabel   => IsBurnedIn ? "🔥 Burn-in" : string.Empty;

    public string RowBackground
        => IsBurnedIn   ? "#FFF0F0"
         : IsIncluded   ? "#F0FFF0"
         : "Transparent";

    public string FullDescription
        => $"{Track.StreamIndex + 1}. {LanguageLabel}" +
           (Track.IsForced  ? " [Forced]"  : string.Empty) +
           (Track.IsDefault ? " [Default]" : string.Empty) +
           (IsBurnedIn      ? " 🔥"         : string.Empty);

    public SelectableSubtitleTrack(SubtitleStreamInfo track) => Track = track;
}

// ── Filmstrip frame ───────────────────────────────────────────────────────────

public class FilmstripFrame
{
    public string    ImagePath { get; init; } = string.Empty;
    public TimeSpan  SeekTime  { get; init; }
    public int       Index     { get; init; }
    public string    TimeLabel => SeekTime.ToString(@"hh\:mm\:ss");
}
