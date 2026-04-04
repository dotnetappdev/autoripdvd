using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoRipDVD.Services;

/// <summary>
/// Wraps ffprobe to extract deep media stream information from ripped MKV/video files.
/// Returns structured VideoStreamInfo, AudioStreamInfo, SubtitleStreamInfo, and ChapterInfo
/// without relying on any 3rd-party library – just spawning the ffprobe process and parsing
/// its JSON output (ffprobe -v quiet -print_format json -show_streams -show_chapters -show_format).
/// </summary>
public interface IFfprobeService
{
    Task<MediaStreamInfo?> AnalyseFileAsync(string filePath);
    Task<string> GetFfprobeVersionAsync();
    bool IsAvailable { get; }
}

public class FfprobeService : IFfprobeService
{
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    private bool? _available;

    public bool IsAvailable => _available ??= File.Exists(_settings.Settings.FfprobePath);

    public FfprobeService(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log      = log;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<MediaStreamInfo?> AnalyseFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            await _log.LogAsync($"ffprobe: file not found: {filePath}");
            return null;
        }

        if (!IsAvailable)
        {
            await _log.LogAsync("ffprobe not found – skipping stream analysis. Set FfprobePath in Settings.");
            return null;
        }

        try
        {
            var json = await RunFfprobeAsync(filePath);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var result = ParseFfprobeJson(json, filePath);
            await _log.LogAsync($"ffprobe: {Path.GetFileName(filePath)} → " +
                $"{result.VideoStreams.Count}V {result.AudioStreams.Count}A {result.SubtitleStreams.Count}S " +
                $"{result.ChapterCount} chapters, {result.PrimaryVideo?.ResolutionLabel ?? "?"}");
            return result;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"ffprobe error for {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GetFfprobeVersionAsync()
    {
        if (!IsAvailable) return "ffprobe not found";
        try
        {
            var output = await RunProcessAsync(_settings.Settings.FfprobePath, "-version");
            var firstLine = output.Split('\n').FirstOrDefault() ?? string.Empty;
            return firstLine.Trim();
        }
        catch { return "unknown"; }
    }

    // ── Process runner ────────────────────────────────────────────────────────

    private async Task<string> RunFfprobeAsync(string filePath)
    {
        // -v quiet           – suppress banner
        // -print_format json – structured output
        // -show_streams      – all stream details
        // -show_chapters     – chapter markers
        // -show_format       – container info (duration, bitrate, size)
        var args = $"-v quiet -print_format json -show_streams -show_chapters -show_format \"{filePath}\"";
        return await RunProcessAsync(_settings.Settings.FfprobePath, args);
    }

    private static async Task<string> RunProcessAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = exe,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return stdout;
    }

    // ── JSON parser ───────────────────────────────────────────────────────────

    private static MediaStreamInfo ParseFfprobeJson(string json, string filePath)
    {
        var result = new MediaStreamInfo { FilePath = filePath };

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // ── Format (container) ────────────────────────────────────────────────
        if (root.TryGetProperty("format", out var fmt))
        {
            result.ContainerFormat = fmt.GetStringOrDefault("format_name") ?? string.Empty;
            result.FileSizeBytes   = fmt.GetLongOrDefault("size");
            result.TotalBitRate    = fmt.GetLongOrDefault("bit_rate");

            if (fmt.TryGetProperty("duration", out var durProp)
                && double.TryParse(durProp.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var durSec))
            {
                result.Duration = TimeSpan.FromSeconds(durSec);
            }
        }

        // ── Streams ───────────────────────────────────────────────────────────
        if (root.TryGetProperty("streams", out var streams))
        {
            int vstreamIdx = 0, astreamIdx = 0, sstreamIdx = 0;

            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.GetStringOrDefault("codec_type") ?? string.Empty;

                switch (codecType)
                {
                    case "video":
                        result.VideoStreams.Add(ParseVideoStream(stream, vstreamIdx++));
                        break;
                    case "audio":
                        result.AudioStreams.Add(ParseAudioStream(stream, astreamIdx++));
                        break;
                    case "subtitle":
                        result.SubtitleStreams.Add(ParseSubtitleStream(stream, sstreamIdx++));
                        break;
                }
            }
        }

        // ── Chapters ──────────────────────────────────────────────────────────
        if (root.TryGetProperty("chapters", out var chapters))
        {
            int chapNum = 1;
            foreach (var ch in chapters.EnumerateArray())
            {
                var chapter = new ChapterInfo
                {
                    Number = chapNum++,
                    Name   = ch.GetTagOrDefault("title") ?? $"Chapter {chapNum - 1}"
                };

                if (ch.TryGetProperty("start_time", out var st)
                    && double.TryParse(st.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var startSec))
                    chapter.StartTime = TimeSpan.FromSeconds(startSec);

                if (ch.TryGetProperty("end_time", out var et)
                    && double.TryParse(et.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var endSec))
                    chapter.EndTime = TimeSpan.FromSeconds(endSec);

                result.Chapters.Add(chapter);
            }
        }

        return result;
    }

    private static VideoStreamInfo ParseVideoStream(JsonElement s, int idx)
    {
        var v = new VideoStreamInfo { StreamIndex = idx };

        v.Codec       = s.GetStringOrDefault("codec_name")       ?? string.Empty;
        v.CodecLong   = s.GetStringOrDefault("codec_long_name")  ?? string.Empty;
        v.Width       = s.GetIntOrDefault("width");
        v.Height      = s.GetIntOrDefault("height");
        v.PixelFormat = s.GetStringOrDefault("pix_fmt")          ?? string.Empty;
        v.Profile     = s.GetStringOrDefault("profile")          ?? string.Empty;
        v.Level       = (s.GetIntOrDefault("level") / 10.0).ToString("F1");

        // Aspect ratio
        if (s.TryGetProperty("display_aspect_ratio", out var dar))
            v.AspectRatio = dar.GetString() ?? string.Empty;

        // Bit depth from pixel format
        if (v.PixelFormat.Contains("10le") || v.PixelFormat.Contains("10be"))
            v.BitDepth = 10;
        else if (v.PixelFormat.Contains("12"))
            v.BitDepth = 12;

        // Frame rate – prefer avg_frame_rate, fall back to r_frame_rate
        var fpsStr = s.GetStringOrDefault("avg_frame_rate") ?? s.GetStringOrDefault("r_frame_rate") ?? "0/1";
        v.FrameRate = ParseFraction(fpsStr);

        // Bit rate
        if (s.TryGetProperty("bit_rate", out var br)
            && long.TryParse(br.GetString(), out var brl))
            v.BitRate = brl;

        // Color metadata
        v.ColorSpace     = s.GetStringOrDefault("color_space")    ?? string.Empty;
        v.ColorTransfer  = s.GetStringOrDefault("color_transfer") ?? string.Empty;
        v.ColorPrimaries = s.GetStringOrDefault("color_primaries") ?? string.Empty;

        return v;
    }

    private static AudioStreamInfo ParseAudioStream(JsonElement s, int idx)
    {
        var a = new AudioStreamInfo { StreamIndex = idx };

        a.Codec         = s.GetStringOrDefault("codec_name")      ?? string.Empty;
        a.CodecLong     = s.GetStringOrDefault("codec_long_name") ?? string.Empty;
        a.Channels      = s.GetIntOrDefault("channels");
        a.ChannelLayout = s.GetStringOrDefault("channel_layout")  ?? string.Empty;
        a.SampleRate    = s.GetIntOrDefault("sample_rate");

        if (s.TryGetProperty("bit_rate", out var br)
            && long.TryParse(br.GetString(), out var brl))
            a.BitRate = brl;

        if (s.TryGetProperty("bits_per_raw_sample", out var bps)
            && int.TryParse(bps.GetString(), out var bpsI))
            a.BitDepth = bpsI;

        // Tags
        a.Language     = s.GetTagOrDefault("language") ?? string.Empty;
        a.LanguageCode = a.Language;
        a.Title        = s.GetTagOrDefault("title")    ?? string.Empty;
        a.Language     = Iso639ToEnglish(a.Language);

        // Disposition
        if (s.TryGetProperty("disposition", out var disp))
        {
            a.IsDefault = disp.GetIntOrDefault("default") == 1;
            a.IsForced  = disp.GetIntOrDefault("forced")  == 1;
        }

        // Atmos / DTS:X detection from codec name or profile
        var profile = s.GetStringOrDefault("profile") ?? string.Empty;
        a.IsAtmos = a.Codec == "truehd" && profile.Contains("Atmos", StringComparison.OrdinalIgnoreCase);
        a.IsDtsx  = a.Codec.StartsWith("dts")       && profile.Contains("DTS:X",  StringComparison.OrdinalIgnoreCase);

        return a;
    }

    private static SubtitleStreamInfo ParseSubtitleStream(JsonElement s, int idx)
    {
        var sub = new SubtitleStreamInfo { StreamIndex = idx };

        sub.Codec        = s.GetStringOrDefault("codec_name")   ?? string.Empty;
        sub.Language     = s.GetTagOrDefault("language")        ?? string.Empty;
        sub.LanguageCode = sub.Language;
        sub.Language     = Iso639ToEnglish(sub.Language);
        sub.Title        = s.GetTagOrDefault("title")           ?? string.Empty;

        if (s.TryGetProperty("disposition", out var disp))
        {
            sub.IsDefault = disp.GetIntOrDefault("default") == 1;
            sub.IsForced  = disp.GetIntOrDefault("forced")  == 1;
        }

        // Infer forced if title contains "forced" keyword
        if (!sub.IsForced && sub.Title.Contains("forced", StringComparison.OrdinalIgnoreCase))
            sub.IsForced = true;

        return sub;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double ParseFraction(string frac)
    {
        var parts = frac.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var num)
            && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var den)
            && den > 0)
            return Math.Round(num / den, 3);
        return 0;
    }

    private static readonly Dictionary<string, string> Iso639 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "English", ["fra"] = "French",  ["fre"] = "French",  ["deu"] = "German",
        ["ger"] = "German",  ["spa"] = "Spanish", ["ita"] = "Italian", ["jpn"] = "Japanese",
        ["kor"] = "Korean",  ["zho"] = "Chinese", ["chi"] = "Chinese", ["por"] = "Portuguese",
        ["rus"] = "Russian", ["nld"] = "Dutch",   ["pol"] = "Polish",  ["swe"] = "Swedish",
        ["nor"] = "Norwegian", ["dan"] = "Danish", ["fin"] = "Finnish", ["ces"] = "Czech",
        ["cze"] = "Czech",   ["hun"] = "Hungarian", ["ell"] = "Greek", ["tur"] = "Turkish",
        ["ara"] = "Arabic",  ["heb"] = "Hebrew",  ["tha"] = "Thai",   ["vie"] = "Vietnamese",
        ["und"] = "Undetermined"
    };

    private static string Iso639ToEnglish(string code)
        => !string.IsNullOrEmpty(code) && Iso639.TryGetValue(code, out var name) ? name : code;
}

// ── JsonElement extension helpers ─────────────────────────────────────────────

internal static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    public static int GetIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        return v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : int.TryParse(v.GetString(), out var i) ? i : 0;
    }

    public static long GetLongOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        return v.ValueKind == JsonValueKind.Number
            ? v.GetInt64()
            : long.TryParse(v.GetString(), out var l) ? l : 0;
    }

    public static string? GetTagOrDefault(this JsonElement el, string tag)
    {
        if (!el.TryGetProperty("tags", out var tags)) return null;
        // Tags are case-insensitive in practice; check both cases
        if (tags.TryGetProperty(tag,                    out var v1)) return v1.GetString();
        if (tags.TryGetProperty(tag.ToUpperInvariant(), out var v2)) return v2.GetString();
        if (tags.TryGetProperty(tag.ToLowerInvariant(), out var v3)) return v3.GetString();
        return null;
    }
}
