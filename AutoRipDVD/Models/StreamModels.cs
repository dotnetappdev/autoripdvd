using System.Text.Json.Serialization;

namespace AutoRipDVD.Models;

// ── Output format ─────────────────────────────────────────────────────────────

public enum OutputFormat
{
    MKV,   // Matroska – best compatibility, all tracks preserved
    MP4,   // MPEG-4 – widest device support
    WebM,  // WebM – open web format (VP9/AV1 + Opus/Vorbis only)
    M4V    // iTunes-compatible container
}

// ── Audio channel mix ─────────────────────────────────────────────────────────

public enum AudioMixdown
{
    Auto,           // Let HandBrake decide
    Mono,           // 1.0
    Stereo,         // 2.0
    DPL1,           // Dolby Pro Logic I  2.0
    DPL2,           // Dolby Pro Logic II 2.0
    Surround_5_1,   // 5.1
    Surround_6_1,   // 6.1
    Surround_7_1,   // 7.1
    Passthrough     // Keep original layout
}

// ── Subtitle handling ─────────────────────────────────────────────────────────

public enum SubtitleDisposition
{
    PassThrough,   // Embed as soft subtitle in container
    BurnIn,        // Render subtitles permanently into video
    ExtractSrt,    // Extract as separate .srt file
    Disabled       // Ignore this track
}

// ── HDR/Color transfer ────────────────────────────────────────────────────────

public enum HdrMode
{
    Passthrough,   // Keep HDR as-is
    ToneMapToSDR   // Convert HDR10/HLG → SDR
}

// ── Copy protection types ─────────────────────────────────────────────────────

[Flags]
public enum CopyProtectionFlags
{
    None          = 0,
    CSS           = 1 << 0,   // DVD Content Scramble System
    CPPM          = 1 << 1,   // Content Protection for Prerecorded Media (DVD-Audio)
    CPRM          = 1 << 2,   // Content Protection for Recordable Media
    ARccOS        = 1 << 3,   // Sony ARccOS bad-sector protection
    RCE           = 1 << 4,   // Region Code Enhancement
    APS           = 1 << 5,   // Analog Protection System (Macrovision)
    AACS          = 1 << 6,   // Blu-ray Advanced Access Content System
    BDPlus        = 1 << 7,   // Blu-ray BD+
    HDCP          = 1 << 8,   // High-bandwidth Digital Content Protection
    Cinavia       = 1 << 9,   // Cinavia audio watermark
    UOPs          = 1 << 10   // User Operation Prohibitions (skipping disabled)
}

// ── Region codes ──────────────────────────────────────────────────────────────

[Flags]
public enum DiscRegions
{
    None      = 0,
    Region1   = 1 << 0,  // USA, Canada
    Region2   = 1 << 1,  // Europe, Japan, Middle East
    Region3   = 1 << 2,  // SE Asia
    Region4   = 1 << 3,  // Latin America, Oceania
    Region5   = 1 << 4,  // Russia, India, Africa
    Region6   = 1 << 5,  // China
    Region7   = 1 << 6,  // Reserved
    Region8   = 1 << 7,  // International venues
    RegionFree = Region1 | Region2 | Region3 | Region4 | Region5 | Region6 | Region7 | Region8
}

// ── Stream information models ─────────────────────────────────────────────────

public class VideoStreamInfo
{
    public int StreamIndex { get; set; }
    public string Codec { get; set; } = string.Empty;         // h264, hevc, mpeg2video, …
    public string CodecLong { get; set; } = string.Empty;     // "H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10"
    public int Width { get; set; }
    public int Height { get; set; }
    public string AspectRatio { get; set; } = string.Empty;   // "16:9", "4:3"
    public double FrameRate { get; set; }                     // e.g. 23.976, 29.97, 25.0
    public string FrameRateMode { get; set; } = string.Empty; // "cfr" or "vfr"
    public long BitRate { get; set; }                         // bps
    public string PixelFormat { get; set; } = string.Empty;   // yuv420p, yuv420p10le, …
    public int BitDepth { get; set; } = 8;
    public string ColorSpace { get; set; } = string.Empty;    // bt709, bt2020nc, …
    public string ColorTransfer { get; set; } = string.Empty; // bt709, smpte2084 (HDR10), arib-std-b67 (HLG)
    public string ColorPrimaries { get; set; } = string.Empty;
    public bool IsHdr => ColorTransfer is "smpte2084" or "arib-std-b67";
    public bool IsHdr10 => ColorTransfer == "smpte2084";
    public bool IsHlg  => ColorTransfer == "arib-std-b67";
    public string HdrType => IsHdr10 ? "HDR10" : IsHlg ? "HLG" : "SDR";
    public string Profile { get; set; } = string.Empty;       // High, Main, Baseline
    public string Level { get; set; } = string.Empty;         // "4.1", "5.0"
    public string ResolutionLabel                             // "1080p", "720p", "4K UHD"
        => (Width, Height) switch
        {
            (>= 3840, _) => "4K UHD",
            (>= 2560, _) => "2K QHD",
            (>= 1920, _) => "1080p",
            (>= 1280, _) => "720p",
            (>= 720,  _) => "480p",
            _            => $"{Height}p"
        };
}

public class AudioStreamInfo
{
    public int StreamIndex { get; set; }
    public string Codec { get; set; } = string.Empty;         // aac, ac3, eac3, dts, truehd, flac, …
    public string CodecLong { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;      // "English"
    public string LanguageCode { get; set; } = string.Empty;  // ISO 639-2: "eng", "fra", "jpn"
    public int Channels { get; set; }                         // 2, 6, 8
    public string ChannelLayout { get; set; } = string.Empty; // "stereo", "5.1", "7.1"
    public long BitRate { get; set; }                         // bps (0 if lossless)
    public int SampleRate { get; set; }                       // Hz: 48000, 96000
    public int BitDepth { get; set; }                         // 16, 24 (for lossless)
    public bool IsDefault { get; set; }
    public bool IsForced { get; set; }
    public bool IsLossless => Codec is "truehd" or "flac" or "pcm_s16le" or "pcm_s24le" or "mlp";
    public bool IsAtmos { get; set; }
    public bool IsDtsx  { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FormattedChannels
        => Channels switch { 1 => "1.0 Mono", 2 => "2.0 Stereo", 6 => "5.1", 7 => "6.1", 8 => "7.1", _ => $"{Channels}ch" };
}

public class SubtitleStreamInfo
{
    public int StreamIndex { get; set; }
    public string Codec { get; set; } = string.Empty;         // dvd_subtitle, hdmv_pgs_subtitle, subrip, ass, …
    public string Language { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsForced { get; set; }
    public bool IsBitmapBased => Codec is "dvd_subtitle" or "hdmv_pgs_subtitle" or "dvb_subtitle";
    public bool IsTextBased   => Codec is "subrip" or "ass" or "ssa" or "webvtt" or "mov_text";
    public string Title { get; set; } = string.Empty;
    public SubtitleDisposition Disposition { get; set; } = SubtitleDisposition.PassThrough;
}

// ── Chapter information ───────────────────────────────────────────────────────

public class ChapterInfo
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;   // Named chapters (e.g. from Blu-ray)
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime   { get; set; }
    public TimeSpan Duration  => EndTime - StartTime;
}

// ── Full media stream info (from ffprobe/MakeMKV) ─────────────────────────────

public class MediaStreamInfo
{
    public string FilePath  { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long TotalBitRate { get; set; }           // bps
    public long FileSizeBytes { get; set; }
    public string ContainerFormat { get; set; } = string.Empty;  // "matroska,webm", "mov,mp4,m4a,3gp,3g2,mj2"

    public List<VideoStreamInfo>    VideoStreams    { get; set; } = new();
    public List<AudioStreamInfo>    AudioStreams    { get; set; } = new();
    public List<SubtitleStreamInfo> SubtitleStreams { get; set; } = new();
    public List<ChapterInfo>        Chapters        { get; set; } = new();

    public VideoStreamInfo? PrimaryVideo    => VideoStreams.FirstOrDefault();
    public AudioStreamInfo? PrimaryAudio    => AudioStreams.FirstOrDefault(a => a.IsDefault) ?? AudioStreams.FirstOrDefault();
    public int AudioTrackCount    => AudioStreams.Count;
    public int SubtitleTrackCount => SubtitleStreams.Count;
    public int ChapterCount       => Chapters.Count;
}

// ── Copy protection info ──────────────────────────────────────────────────────

public class CopyProtectionInfo
{
    public CopyProtectionFlags Flags   { get; set; } = CopyProtectionFlags.None;
    public DiscRegions RegionCodes     { get; set; } = DiscRegions.RegionFree;
    public bool IsProtected            => Flags != CopyProtectionFlags.None;
    public List<string> Details        { get; set; } = new();   // human-readable notes
    public string ProtectionSummary
    {
        get
        {
            if (!IsProtected) return "No protection detected";
            var flags = Enum.GetValues<CopyProtectionFlags>()
                .Where(f => f != CopyProtectionFlags.None && Flags.HasFlag(f))
                .Select(f => f.ToString());
            return string.Join(", ", flags);
        }
    }
    public string RegionSummary
    {
        get
        {
            if (RegionCodes == DiscRegions.RegionFree) return "Region Free";
            var regions = Enum.GetValues<DiscRegions>()
                .Where(r => r != DiscRegions.None && r != DiscRegions.RegionFree && RegionCodes.HasFlag(r))
                .Select(r => r.ToString().Replace("Region", "R"));
            return string.Join(", ", regions);
        }
    }
}

// ── DVD disc structure (from IFO parser) ─────────────────────────────────────

public class DvdTitleSetInfo
{
    public int VtsNumber          { get; set; }   // 1-based VTS index
    public int TitleCount         { get; set; }
    public List<DvdTitleInfo> Titles { get; set; } = new();
}

public class DvdTitleInfo
{
    public int TitleNumber        { get; set; }
    public int VtsNumber          { get; set; }
    public int AngleCount         { get; set; }
    public int ChapterCount       { get; set; }
    public TimeSpan Duration      { get; set; }
    public double FrameRate       { get; set; }
    public int Width              { get; set; }
    public int Height             { get; set; }
    public string AspectRatio     { get; set; } = string.Empty;
    public List<DvdAudioInfo>     AudioTracks    { get; set; } = new();
    public List<DvdSubpictureInfo> Subpictures   { get; set; } = new();
    public List<TimeSpan>         ChapterTimes   { get; set; } = new();
}

public class DvdAudioInfo
{
    public int TrackNumber        { get; set; }
    public string CodingMode      { get; set; } = string.Empty;  // "AC3", "DTS", "LPCM", "MPEG1", "MPEG2ext"
    public int Channels           { get; set; }
    public string Language        { get; set; } = string.Empty;
    public string LanguageCode    { get; set; } = string.Empty;
    public int SampleRate         { get; set; }
    public int BitDepth           { get; set; }
}

public class DvdSubpictureInfo
{
    public int TrackNumber        { get; set; }
    public string Language        { get; set; } = string.Empty;
    public string LanguageCode    { get; set; } = string.Empty;
    public bool IsForced          { get; set; }
}

public class DvdDiscStructure
{
    public string VolumeName       { get; set; } = string.Empty;
    public int TotalTitles         { get; set; }
    public DiscRegions RegionCodes { get; set; }
    public CopyProtectionFlags Protection { get; set; }
    public List<DvdTitleSetInfo>   TitleSets { get; set; } = new();
    public List<DvdTitleInfo>      AllTitles { get; set; } = new();
}

// ── Transcode preset ──────────────────────────────────────────────────────────

public record TranscodePreset
{
    public string Id              { get; set; } = Guid.NewGuid().ToString();
    public string Name            { get; set; } = string.Empty;
    public string Category        { get; set; } = string.Empty;  // "General", "Web", "Devices", "Matroska", "Custom"
    public string Description     { get; set; } = string.Empty;
    public bool IsBuiltIn         { get; set; } = true;

    // Output
    public OutputFormat OutputFormat { get; set; } = OutputFormat.MKV;

    // Video
    public VideoEncoderType VideoEncoder { get; set; } = VideoEncoderType.x264;
    public int VideoQuality       { get; set; } = 22;        // RF/CQ value
    public int? VideoBitrate      { get; set; }              // kbps (when not using RF)
    public bool UseBitrateMode    { get; set; } = false;
    public int? Width             { get; set; }              // null = keep source
    public int? Height            { get; set; }
    public bool KeepAspectRatio   { get; set; } = true;
    public string EncoderPreset   { get; set; } = "medium";  // ultrafast..veryslow
    public string EncoderProfile  { get; set; } = "auto";
    public string EncoderTune     { get; set; } = "none";
    public bool TwoPass           { get; set; } = false;
    public bool TurboFirstPass    { get; set; } = true;

    // Filters
    public bool Deinterlace       { get; set; } = false;
    public string DeinterlacePreset { get; set; } = "default";  // default, skip-spatial, bob
    public bool Detelecine        { get; set; } = false;        // NTSC film 3:2 pulldown
    public bool Denoise           { get; set; } = false;
    public string DenoisePreset   { get; set; } = "medium";     // ultralight, light, medium, strong, custom
    public string DenoiseTune     { get; set; } = "none";       // none, film, grain, highmotion, animation
    public bool Sharpen           { get; set; } = false;
    public string SharpenPreset   { get; set; } = "medium";
    public bool Deblock           { get; set; } = false;
    public int? DeblockStrength   { get; set; }
    public bool GrayscaleVideo    { get; set; } = false;
    public HdrMode HdrHandling    { get; set; } = HdrMode.Passthrough;

    // Cropping
    public bool AutoCrop          { get; set; } = true;
    public int CropTop            { get; set; }
    public int CropBottom         { get; set; }
    public int CropLeft           { get; set; }
    public int CropRight          { get; set; }

    // Audio
    public AudioEncoderType AudioEncoder  { get; set; } = AudioEncoderType.AAC;
    public int AudioBitrate       { get; set; } = 160;       // kbps
    public AudioMixdown AudioMixdown { get; set; } = AudioMixdown.Auto;
    public int AudioSampleRate    { get; set; } = 0;         // 0 = auto
    public double AudioGain       { get; set; } = 0;         // dB gain
    public bool PassthroughDts    { get; set; } = true;
    public bool PassthroughTrueHd { get; set; } = true;
    public bool PassthroughAc3    { get; set; } = false;
    public bool IncludeAllAudioTracks { get; set; } = true;
    public List<string> PreferredAudioLanguages { get; set; } = new() { "eng" };

    // Subtitles
    public bool IncludeSubtitles  { get; set; } = true;
    public bool BurnFirstSubtitle { get; set; } = false;
    public bool ForcedSubtitlesOnly { get; set; } = false;
    public List<string> PreferredSubtitleLanguages { get; set; } = new() { "eng" };

    // Chapters / Container
    public bool ChapterMarkers    { get; set; } = true;
    public bool OptimizeForWeb    { get; set; } = false;     // --optimize (MP4 fast-start)
    public string? CustomOptions  { get; set; }              // raw extra CLI args
}

// ── Per-job transcode override ────────────────────────────────────────────────

public class TranscodeJobSettings
{
    public string? PresetId       { get; set; }              // null = use global default
    public TranscodePreset? CustomPreset { get; set; }       // fully custom preset
    public bool UseGlobalSettings { get; set; } = true;

    // Track selection overrides
    public List<int> AudioTrackIndices    { get; set; } = new();
    public List<int> SubtitleTrackIndices { get; set; } = new();

    // Per-track subtitle disposition
    public Dictionary<int, SubtitleDisposition> SubtitleDispositions { get; set; } = new();

    // Crop override
    public bool OverrideCrop      { get; set; }
    public int CropTop            { get; set; }
    public int CropBottom         { get; set; }
    public int CropLeft           { get; set; }
    public int CropRight          { get; set; }

    public TranscodePreset GetEffectivePreset(TranscodePreset globalDefault)
        => CustomPreset ?? globalDefault;
}
