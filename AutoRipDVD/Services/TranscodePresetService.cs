using AutoRipDVD.Models;
using System.Text.Json;

namespace AutoRipDVD.Services;

/// <summary>
/// Manages a full HandBrake-style preset library.
///
/// Built-in presets mirror HandBrake's categories:
///   General  – everyday ripping (480p, 720p, 1080p)
///   HQ       – high quality 720p / 1080p
///   Super HQ – archival quality
///   Web      – streaming-compatible (H.264/AAC MP4)
///   Devices  – Apple TV, Chromecast, Roku targets
///   Matroska – MKV-specific presets preserving lossless audio
///   4K       – 2160p HEVC / AV1 targets
///   Custom   – user-defined presets stored in settings
/// </summary>
public interface ITranscodePresetService
{
    IReadOnlyList<TranscodePreset>           AllPresets   { get; }
    IReadOnlyList<string>                    Categories   { get; }
    IReadOnlyList<TranscodePreset>           GetByCategory(string category);
    TranscodePreset?                         GetById(string id);
    TranscodePreset?                         GetByName(string name);
    TranscodePreset                          GetDefault();
    Task                                     SaveCustomPresetAsync(TranscodePreset preset);
    Task                                     DeleteCustomPresetAsync(string id);
    Task                                     LoadCustomPresetsAsync();
    string                                   BuildHandBrakeArgs(TranscodePreset preset, string input, string output);
}

public class TranscodePresetService : ITranscodePresetService
{
    private readonly ISettingsService _settings;
    private readonly ILogService      _log;

    private readonly List<TranscodePreset> _builtIn;
    private readonly List<TranscodePreset> _custom = new();

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // ── Static encoder maps (mirrors HandBrakeService) ────────────────────────
    private static readonly Dictionary<VideoEncoderType, string> EncMap = new()
    {
        [VideoEncoderType.x264]           = "x264",
        [VideoEncoderType.x265]           = "x265",
        [VideoEncoderType.x265_10bit]     = "x265_10bit",
        [VideoEncoderType.VP9]            = "VP9",
        [VideoEncoderType.AV1]            = "svt_av1",
        [VideoEncoderType.NVENC_H264]     = "nvenc_h264",
        [VideoEncoderType.NVENC_H265]     = "nvenc_h265",
        [VideoEncoderType.QuickSync_H264] = "qsv_h264",
        [VideoEncoderType.QuickSync_H265] = "qsv_h265",
        [VideoEncoderType.VCE_H264]       = "vce_h264",
        [VideoEncoderType.VCE_H265]       = "vce_h265",
    };

    private static readonly Dictionary<AudioEncoderType, string> AudMap = new()
    {
        [AudioEncoderType.AAC]         = "av_aac",
        [AudioEncoderType.AC3]         = "ac3",
        [AudioEncoderType.EAC3]        = "eac3",
        [AudioEncoderType.MP3]         = "mp3",
        [AudioEncoderType.Opus]        = "opus",
        [AudioEncoderType.FLAC]        = "flac16",
        [AudioEncoderType.Vorbis]      = "vorbis",
        [AudioEncoderType.Passthrough] = "copy",
        [AudioEncoderType.Auto]        = "av_aac",
    };

    private static readonly Dictionary<AudioMixdown, string> MixMap = new()
    {
        [AudioMixdown.Mono]         = "mono",
        [AudioMixdown.Stereo]       = "stereo",
        [AudioMixdown.DPL1]         = "dpl1",
        [AudioMixdown.DPL2]         = "dpl2",
        [AudioMixdown.Surround_5_1] = "5point1",
        [AudioMixdown.Surround_6_1] = "6point1",
        [AudioMixdown.Surround_7_1] = "7point1",
        [AudioMixdown.Passthrough]  = "none",
        [AudioMixdown.Auto]         = "dpl2",
    };

    public IReadOnlyList<TranscodePreset> AllPresets
        => _builtIn.Concat(_custom).ToList().AsReadOnly();

    public IReadOnlyList<string> Categories
        => AllPresets.Select(p => p.Category).Distinct().OrderBy(c => c).ToList().AsReadOnly();

    public TranscodePresetService(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log      = log;
        _builtIn  = BuildBuiltInPresets();
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    public IReadOnlyList<TranscodePreset> GetByCategory(string category)
        => AllPresets.Where(p => p.Category == category).ToList().AsReadOnly();

    public TranscodePreset? GetById(string id)
        => AllPresets.FirstOrDefault(p => p.Id == id);

    public TranscodePreset? GetByName(string name)
        => AllPresets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public TranscodePreset GetDefault()
    {
        var name = _settings.Settings.HandBrakePreset;
        return GetByName(name) ?? _builtIn.First(p => p.Name == "Fast 1080p30");
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public async Task LoadCustomPresetsAsync()
    {
        var path = CustomPresetsPath;
        if (!File.Exists(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<List<TranscodePreset>>(json, _jsonOptions);
            if (loaded != null)
            {
                _custom.Clear();
                _custom.AddRange(loaded.Select(p => p with { IsBuiltIn = false }));
                await _log.LogAsync($"Loaded {_custom.Count} custom presets");
            }
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"Error loading custom presets: {ex.Message}");
        }
    }

    public async Task SaveCustomPresetAsync(TranscodePreset preset)
    {
        preset = preset with { IsBuiltIn = false, Category = "Custom" };
        var existing = _custom.FindIndex(p => p.Id == preset.Id);
        if (existing >= 0) _custom[existing] = preset;
        else               _custom.Add(preset);

        await PersistCustomPresetsAsync();
        await _log.LogAsync($"Saved custom preset: {preset.Name}");
    }

    public async Task DeleteCustomPresetAsync(string id)
    {
        _custom.RemoveAll(p => p.Id == id);
        await PersistCustomPresetsAsync();
    }

    private async Task PersistCustomPresetsAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CustomPresetsPath)!);
        var json = JsonSerializer.Serialize(_custom, _jsonOptions);
        await File.WriteAllTextAsync(CustomPresetsPath, json);
    }

    private string CustomPresetsPath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoRipDVD",
            "custom_presets.json");

    // ── HandBrake CLI builder ─────────────────────────────────────────────────

    public string BuildHandBrakeArgs(TranscodePreset p, string input, string output)
    {
        var sb = new System.Text.StringBuilder();

        // Input / output
        sb.Append($"--input \"{input}\" --output \"{output}\" ");

        // Container format
        var fmt = p.OutputFormat switch
        {
            OutputFormat.MP4  => "av_mp4",
            OutputFormat.WebM => "av_webm",
            OutputFormat.M4V  => "av_mp4",
            _                 => "av_mkv"
        };
        sb.Append($"--format {fmt} ");
        if (p.OutputFormat == OutputFormat.MP4 && p.OptimizeForWeb) sb.Append("--optimize ");

        // Video encoder
        var enc = EncMap.TryGetValue(p.VideoEncoder, out var ev) ? ev : "x264";
        sb.Append($"--encoder {enc} ");

        // Quality / bitrate
        if (p.UseBitrateMode && p.VideoBitrate.HasValue)
        {
            sb.Append($"--vb {p.VideoBitrate.Value} ");
            if (p.TwoPass)
            {
                sb.Append("--two-pass ");
                if (p.TurboFirstPass) sb.Append("--turbo ");
            }
        }
        else
        {
            sb.Append($"--quality {p.VideoQuality} ");
        }

        // Encoder-specific presets / tunes / profiles
        if (!string.IsNullOrEmpty(p.EncoderPreset))  sb.Append($"--encoder-preset {p.EncoderPreset} ");
        if (p.EncoderTune != "none" && !string.IsNullOrEmpty(p.EncoderTune))
            sb.Append($"--encoder-tune {p.EncoderTune} ");
        if (p.EncoderProfile != "auto" && !string.IsNullOrEmpty(p.EncoderProfile))
            sb.Append($"--encoder-profile {p.EncoderProfile} ");

        // Resolution
        if (p.Width.HasValue  && p.Width  > 0) sb.Append($"--width {p.Width} ");
        if (p.Height.HasValue && p.Height > 0) sb.Append($"--height {p.Height} ");
        if (p.KeepAspectRatio) sb.Append("--keep-display-aspect ");

        // Cropping
        if (!p.AutoCrop)
            sb.Append($"--crop {p.CropTop}:{p.CropBottom}:{p.CropLeft}:{p.CropRight} ");

        // Filters
        if (p.Deinterlace)
        {
            sb.Append($"--deinterlace=\"{p.DeinterlacePreset}\" ");
        }
        if (p.Detelecine) sb.Append("--detelecine ");
        if (p.Denoise)
        {
            sb.Append($"--denoise=\"{p.DenoisePreset}\" ");
            if (p.DenoiseTune != "none") sb.Append($"--denoiser-tune \"{p.DenoiseTune}\" ");
        }
        if (p.Sharpen) sb.Append($"--lapsharp=\"{p.SharpenPreset}\" ");
        if (p.Deblock)
        {
            if (p.DeblockStrength.HasValue)
                sb.Append($"--deblock={p.DeblockStrength} ");
            else
                sb.Append("--deblock ");
        }
        if (p.GrayscaleVideo) sb.Append("--grayscale ");

        // HDR tone mapping
        if (p.HdrHandling == HdrMode.ToneMapToSDR)
            sb.Append("--hdr-opt ");

        // Audio
        if (p.IncludeAllAudioTracks) sb.Append("--all-audio ");

        var aud = AudMap.TryGetValue(p.AudioEncoder, out var av) ? av : "av_aac";

        if (p.PassthroughTrueHd || p.PassthroughDts || p.PassthroughAc3)
        {
            // Build a copy-and-fallback audio encoder string
            var passthroughs = new List<string>();
            if (p.PassthroughTrueHd) passthroughs.Add("truehd");
            if (p.PassthroughDts)    passthroughs.Add("dts,dtshd");
            if (p.PassthroughAc3)    passthroughs.Add("ac3");

            sb.Append($"--aencoder copy --audio-copy-mask {string.Join(",", passthroughs)} --audio-fallback {aud} ");
        }
        else
        {
            sb.Append($"--aencoder {aud} ");
        }

        if (p.AudioEncoder != AudioEncoderType.Passthrough && p.AudioEncoder != AudioEncoderType.FLAC)
            sb.Append($"--ab {p.AudioBitrate} ");

        var mix = MixMap.TryGetValue(p.AudioMixdown, out var mv) ? mv : "dpl2";
        if (mix != "none") sb.Append($"--mixdown {mix} ");

        if (p.AudioSampleRate > 0)   sb.Append($"--arate {p.AudioSampleRate} ");
        if (Math.Abs(p.AudioGain) > 0.01) sb.Append($"--gain {p.AudioGain:F1} ");

        if (p.PreferredAudioLanguages.Count > 0)
            sb.Append($"--audio-lang-list {string.Join(",", p.PreferredAudioLanguages)} ");

        // Subtitles
        if (p.IncludeSubtitles)
        {
            if (p.BurnFirstSubtitle)
                sb.Append("--subtitle 1 --subtitle-burned 1 ");
            else if (p.ForcedSubtitlesOnly)
                sb.Append("--subtitle-forced ");
            else
                sb.Append("--all-subtitles ");

            if (p.PreferredSubtitleLanguages.Count > 0)
                sb.Append($"--subtitle-lang-list {string.Join(",", p.PreferredSubtitleLanguages)} ");
        }

        // Chapters
        if (p.ChapterMarkers) sb.Append("--markers ");

        // Custom extra options
        if (!string.IsNullOrWhiteSpace(p.CustomOptions))
            sb.Append($"{p.CustomOptions} ");

        return sb.ToString().TrimEnd();
    }

    // ── Built-in preset library ───────────────────────────────────────────────

    private static List<TranscodePreset> BuildBuiltInPresets()
    {
        var list = new List<TranscodePreset>();

        // ── General ─────────────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "general-fast-480p30", Name = "Fast 480p30", Category = "General",
            Description = "Fast encode at 480p, good for testing or small-screen devices",
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 22, EncoderPreset = "veryfast",
            Height = 480, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 128,
            AudioMixdown = AudioMixdown.Stereo
        });

        list.Add(new TranscodePreset
        {
            Id = "general-fast-720p30", Name = "Fast 720p30", Category = "General",
            Description = "Fast encode at 720p. Good balance of speed and quality.",
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 22, EncoderPreset = "veryfast",
            Height = 720, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 160,
            AudioMixdown = AudioMixdown.DPL2
        });

        list.Add(new TranscodePreset
        {
            Id = "general-fast-1080p30", Name = "Fast 1080p30", Category = "General",
            Description = "Fast encode at 1080p. Best for most users.",
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 22, EncoderPreset = "veryfast",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 160,
            AudioMixdown = AudioMixdown.DPL2
        });

        // ── HQ ──────────────────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "hq-720p30-surround", Name = "HQ 720p30 Surround", Category = "HQ",
            Description = "High quality 720p with 5.1 surround audio",
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 20, EncoderPreset = "slow",
            EncoderProfile = "high",
            Height = 720, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 320,
            AudioMixdown = AudioMixdown.Surround_5_1, IncludeAllAudioTracks = true
        });

        list.Add(new TranscodePreset
        {
            Id = "hq-1080p30-surround", Name = "HQ 1080p30 Surround", Category = "HQ",
            Description = "High quality 1080p with 5.1 surround audio",
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 20, EncoderPreset = "slow",
            EncoderProfile = "high",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 320,
            AudioMixdown = AudioMixdown.Surround_5_1, IncludeAllAudioTracks = true,
            Denoise = false, Deinterlace = false
        });

        // ── Super HQ ─────────────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "superhq-1080p30-surround", Name = "Super HQ 1080p30 Surround", Category = "Super HQ",
            Description = "Best quality 1080p – slow encode, archival quality",
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 18, EncoderPreset = "veryslow",
            EncoderProfile = "high", EncoderTune = "film",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 320,
            AudioMixdown = AudioMixdown.Surround_5_1, IncludeAllAudioTracks = true,
            TwoPass = true, TurboFirstPass = true
        });

        list.Add(new TranscodePreset
        {
            Id = "superhq-2160p60-4k", Name = "Super HQ 2160p60 4K", Category = "Super HQ",
            Description = "Best quality 4K encode with HEVC 10-bit",
            VideoEncoder = VideoEncoderType.x265_10bit, VideoQuality = 18, EncoderPreset = "slow",
            Height = 2160, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 384,
            AudioMixdown = AudioMixdown.Surround_7_1, IncludeAllAudioTracks = true,
            PassthroughDts = true, PassthroughTrueHd = true,
            HdrHandling = HdrMode.Passthrough
        });

        // ── Matroska (MKV) ────────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "mkv-h264-1080p30", Name = "H.264 MKV 1080p30", Category = "Matroska",
            Description = "MKV container, H.264, lossless audio pass-through",
            OutputFormat = OutputFormat.MKV,
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 20, EncoderPreset = "medium",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.Auto, IncludeAllAudioTracks = true,
            PassthroughDts = true, PassthroughTrueHd = true, PassthroughAc3 = true,
            IncludeSubtitles = true, ChapterMarkers = true
        });

        list.Add(new TranscodePreset
        {
            Id = "mkv-h265-1080p30", Name = "H.265 MKV 1080p30", Category = "Matroska",
            Description = "MKV container, HEVC, smaller files than H.264",
            OutputFormat = OutputFormat.MKV,
            VideoEncoder = VideoEncoderType.x265, VideoQuality = 22, EncoderPreset = "medium",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.Auto, IncludeAllAudioTracks = true,
            PassthroughDts = true, PassthroughTrueHd = true,
            IncludeSubtitles = true, ChapterMarkers = true
        });

        list.Add(new TranscodePreset
        {
            Id = "mkv-h265-2160p", Name = "H.265 MKV 2160p (4K)", Category = "Matroska",
            Description = "MKV container, HEVC 10-bit, 4K with HDR pass-through",
            OutputFormat = OutputFormat.MKV,
            VideoEncoder = VideoEncoderType.x265_10bit, VideoQuality = 20, EncoderPreset = "slow",
            Height = 2160, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 384,
            AudioMixdown = AudioMixdown.Auto, IncludeAllAudioTracks = true,
            PassthroughDts = true, PassthroughTrueHd = true,
            HdrHandling = HdrMode.Passthrough,
            IncludeSubtitles = true, ChapterMarkers = true
        });

        // ── Web / Streaming ───────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "web-720p30", Name = "Web 720p30", Category = "Web",
            Description = "MP4 optimised for streaming / web playback",
            OutputFormat = OutputFormat.MP4, OptimizeForWeb = true,
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 23, EncoderPreset = "fast",
            EncoderProfile = "main",
            Height = 720, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 128,
            AudioMixdown = AudioMixdown.Stereo, IncludeAllAudioTracks = false,
            IncludeSubtitles = false
        });

        list.Add(new TranscodePreset
        {
            Id = "web-1080p30", Name = "Web 1080p30", Category = "Web",
            Description = "Full HD MP4 for streaming",
            OutputFormat = OutputFormat.MP4, OptimizeForWeb = true,
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 22, EncoderPreset = "fast",
            EncoderProfile = "high",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.DPL2
        });

        // ── Devices ───────────────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "device-appletv-4k", Name = "Apple TV 4K", Category = "Devices",
            Description = "Optimised for Apple TV 4K (HEVC 10-bit, AAC 5.1)",
            OutputFormat = OutputFormat.MP4, OptimizeForWeb = true,
            VideoEncoder = VideoEncoderType.x265_10bit, VideoQuality = 20, EncoderPreset = "medium",
            Height = 2160, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 384,
            AudioMixdown = AudioMixdown.Surround_5_1
        });

        list.Add(new TranscodePreset
        {
            Id = "device-chromecast-1080p", Name = "Chromecast 1080p", Category = "Devices",
            Description = "H.264 MP4 for Chromecast",
            OutputFormat = OutputFormat.MP4, OptimizeForWeb = true,
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 22, EncoderPreset = "fast",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.DPL2
        });

        list.Add(new TranscodePreset
        {
            Id = "device-android-720p", Name = "Android 720p", Category = "Devices",
            Description = "Compatible with most Android devices",
            OutputFormat = OutputFormat.MP4, OptimizeForWeb = true,
            VideoEncoder = VideoEncoderType.x264, VideoQuality = 23, EncoderPreset = "veryfast",
            EncoderProfile = "main",
            Height = 720, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 160,
            AudioMixdown = AudioMixdown.Stereo
        });

        // ── 4K specific ───────────────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "4k-h265-2160p", Name = "4K H.265 2160p", Category = "4K",
            Description = "HEVC 10-bit 4K, ideal for UHD Blu-ray rips",
            OutputFormat = OutputFormat.MKV,
            VideoEncoder = VideoEncoderType.x265_10bit, VideoQuality = 18, EncoderPreset = "slow",
            Height = 2160, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 384,
            AudioMixdown = AudioMixdown.Auto, IncludeAllAudioTracks = true,
            PassthroughDts = true, PassthroughTrueHd = true,
            HdrHandling = HdrMode.Passthrough, ChapterMarkers = true
        });

        list.Add(new TranscodePreset
        {
            Id = "4k-av1-2160p", Name = "4K AV1 2160p", Category = "4K",
            Description = "AV1 encoder for best compression – very slow",
            OutputFormat = OutputFormat.MKV,
            VideoEncoder = VideoEncoderType.AV1, VideoQuality = 28, EncoderPreset = "6",
            Height = 2160, AudioEncoder = AudioEncoderType.Opus, AudioBitrate = 384,
            AudioMixdown = AudioMixdown.Auto, HdrHandling = HdrMode.Passthrough
        });

        // ── Hardware accelerated ──────────────────────────────────────────────
        list.Add(new TranscodePreset
        {
            Id = "hw-nvenc-1080p", Name = "NVIDIA NVENC H.264 1080p", Category = "Hardware",
            Description = "GPU-accelerated H.264 via NVIDIA NVENC – very fast",
            VideoEncoder = VideoEncoderType.NVENC_H264, VideoQuality = 22, EncoderPreset = "slow",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.DPL2, OutputFormat = OutputFormat.MKV
        });

        list.Add(new TranscodePreset
        {
            Id = "hw-nvenc-hevc-1080p", Name = "NVIDIA NVENC H.265 1080p", Category = "Hardware",
            Description = "GPU-accelerated HEVC via NVIDIA NVENC",
            VideoEncoder = VideoEncoderType.NVENC_H265, VideoQuality = 22, EncoderPreset = "slow",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.DPL2, OutputFormat = OutputFormat.MKV
        });

        list.Add(new TranscodePreset
        {
            Id = "hw-qsv-1080p", Name = "Intel QuickSync H.264 1080p", Category = "Hardware",
            Description = "GPU-accelerated H.264 via Intel QuickSync",
            VideoEncoder = VideoEncoderType.QuickSync_H264, VideoQuality = 22, EncoderPreset = "balanced",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.DPL2, OutputFormat = OutputFormat.MKV
        });

        list.Add(new TranscodePreset
        {
            Id = "hw-amd-vce-1080p", Name = "AMD VCE H.264 1080p", Category = "Hardware",
            Description = "GPU-accelerated H.264 via AMD Video Coding Engine",
            VideoEncoder = VideoEncoderType.VCE_H264, VideoQuality = 22, EncoderPreset = "balanced",
            Height = 1080, AudioEncoder = AudioEncoderType.AAC, AudioBitrate = 192,
            AudioMixdown = AudioMixdown.DPL2, OutputFormat = OutputFormat.MKV
        });

        return list;
    }
}
