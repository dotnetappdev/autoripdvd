using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

public interface IHandBrakeService
{
    Task<bool> TranscodeAsync(string inputPath, string outputPath, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<bool> TranscodeAsync(string inputPath, string outputPath, string preset, int quality, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<List<string>> GetPresetsAsync();
    string BuildOutputFileName(string inputPath, string outputBaseName, MediaType mediaType);
}

public class HandBrakeService : IHandBrakeService
{
    private readonly ISettingsService _settings;
    private readonly ILogService _logService;

    // HandBrake encoder name map
    private static readonly Dictionary<VideoEncoderType, string> EncoderMap = new()
    {
        [VideoEncoderType.x264]            = "x264",
        [VideoEncoderType.x265]            = "x265",
        [VideoEncoderType.x265_10bit]      = "x265_10bit",
        [VideoEncoderType.VP9]             = "VP9",
        [VideoEncoderType.AV1]             = "svt_av1",
        [VideoEncoderType.NVENC_H264]      = "nvenc_h264",
        [VideoEncoderType.NVENC_H265]      = "nvenc_h265",
        [VideoEncoderType.QuickSync_H264]  = "qsv_h264",
        [VideoEncoderType.QuickSync_H265]  = "qsv_h265",
        [VideoEncoderType.VCE_H264]        = "vce_h264",
        [VideoEncoderType.VCE_H265]        = "vce_h265",
    };

    private static readonly Dictionary<AudioEncoderType, string> AudioEncoderMap = new()
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

    public HandBrakeService(ISettingsService settings, ILogService logService)
    {
        _settings = settings;
        _logService = logService;
    }

    public Task<bool> TranscodeAsync(string inputPath, string outputPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var s = _settings.Settings;
        return TranscodeAsync(inputPath, outputPath, s.HandBrakePreset, s.VideoQuality, progress, ct);
    }

    public async Task<bool> TranscodeAsync(string inputPath, string outputPath, string preset, int quality, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await _logService.LogAsync($"Transcoding: {Path.GetFileName(inputPath)} → {Path.GetFileName(outputPath)}");

            var args = BuildArguments(inputPath, outputPath, preset, quality);
            await _logService.LogAsync($"HandBrakeCLI args: {args}");

            var psi = new ProcessStartInfo
            {
                FileName = _settings.Settings.HandBrakePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };

            // HandBrake writes progress to stderr
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                _ = _logService.LogAsync($"[HB] {e.Data}");

                // "Encoding: task 1 of 1, 45.67 % (123.45 fps, avg 110.23 fps, ETA 00h01m23s)"
                var m = Regex.Match(e.Data, @"(\d+\.\d+)\s*%");
                if (m.Success && double.TryParse(m.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    progress?.Report(pct);
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            await process.WaitForExitAsync(ct);

            if (ct.IsCancellationRequested && !process.HasExited)
                process.Kill(entireProcessTree: true);

            var success = process.ExitCode == 0;
            await _logService.LogAsync(success
                ? $"Transcode complete: {Path.GetFileName(outputPath)}"
                : $"Transcode failed (exit {process.ExitCode}): {Path.GetFileName(inputPath)}");

            return success;
        }
        catch (OperationCanceledException)
        {
            await _logService.LogAsync("Transcode cancelled");
            return false;
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Transcode error: {ex.Message}");
            return false;
        }
    }

    private string BuildArguments(string inputPath, string outputPath, string preset, int quality)
    {
        var s = _settings.Settings;
        var sb = new StringBuilder();

        // Input / Output
        sb.Append($"--input \"{inputPath}\" ");
        sb.Append($"--output \"{outputPath}\" ");

        // Preset (base configuration) - skip if empty so we can build manually
        if (!string.IsNullOrWhiteSpace(preset))
            sb.Append($"--preset \"{preset}\" ");

        // Video encoder
        var encoderName = EncoderMap.TryGetValue(s.VideoEncoder, out var enc) ? enc : "x264";
        sb.Append($"--encoder {encoderName} ");
        sb.Append($"--quality {quality} ");

        // x264/x265 encoder options
        if (s.VideoEncoder is VideoEncoderType.x264 or VideoEncoderType.NVENC_H264 or VideoEncoderType.QuickSync_H264)
        {
            sb.Append($"--encoder-preset {s.x264Preset} ");
            if (s.x264Tune != "none")
                sb.Append($"--encoder-tune {s.x264Tune} ");
            if (s.x264Profile != "auto")
                sb.Append($"--encoder-profile {s.x264Profile} ");
        }
        else if (s.VideoEncoder is VideoEncoderType.x265 or VideoEncoderType.x265_10bit or VideoEncoderType.NVENC_H265 or VideoEncoderType.QuickSync_H265)
        {
            sb.Append($"--encoder-preset {s.x265Preset} ");
        }

        if (!string.IsNullOrWhiteSpace(s.CustomEncoderOptions))
            sb.Append($"--encopts \"{s.CustomEncoderOptions}\" ");

        // Two-pass encoding
        if (s.EnableTwoPassEncoding)
        {
            sb.Append("--two-pass ");
            if (s.EnableTurboFirstPass)
                sb.Append("--turbo ");
        }

        // Audio
        var audioEnc = AudioEncoderMap.TryGetValue(s.AudioEncoder, out var ae) ? ae : "av_aac";
        sb.Append($"--aencoder {audioEnc} ");
        if (s.AudioEncoder != AudioEncoderType.Passthrough && s.AudioEncoder != AudioEncoderType.FLAC)
            sb.Append($"--ab {s.AudioBitrate} ");

        // Filters
        if (s.EnableDeinterlacing)
            sb.Append("--decomb ");

        if (s.EnableDenoise)
            sb.Append($"--denoise=\"{s.DenoisePreset}\" ");

        if (s.EnableSharpen)
            sb.Append("--lapsharp ");

        if (s.EnableDeblock)
            sb.Append("--deblock ");

        // Format: always MKV for ripped content (preserves subtitles/chapters)
        sb.Append("--format av_mkv ");

        // Chapter markers
        sb.Append("--markers ");

        // Subtitle pass-through (scan for forced subs)
        sb.Append("--subtitle-lang-list eng --all-subtitles ");

        // Audio: all tracks, try to preserve surround
        sb.Append("--all-audio ");

        // Logging
        var verbosity = s.LogVerbosity switch
        {
            LogVerbosity.Minimal => 0,
            LogVerbosity.Verbose => 2,
            LogVerbosity.Debug   => 3,
            _                   => 1
        };
        sb.Append($"--verbose={verbosity} ");

        // Number of preview frames to scan
        sb.Append($"--previews {s.NumberOfPreviewsToScan} ");

        return sb.ToString().TrimEnd();
    }

    public async Task<List<string>> GetPresetsAsync()
    {
        var presets = new List<string>
        {
            // Sensible built-in defaults always available
            "Fast 1080p30",
            "Fast 720p30",
            "HQ 1080p30 Surround",
            "HQ 720p30 Surround",
            "Super HQ 1080p30 Surround",
            "Production Max",
            "Production Standard"
        };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _settings.Settings.HandBrakePath,
                Arguments = "--preset-list",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Each preset line starts with whitespace then the name
            var custom = output.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('>') && !l.StartsWith('<') && !l.EndsWith(':'))
                .Distinct()
                .ToList();

            if (custom.Count > 0)
                return custom;
        }
        catch
        {
            // Fall back to built-in list
        }

        return presets;
    }

    /// <summary>Returns the recommended output file name for a transcoded file.</summary>
    public string BuildOutputFileName(string inputPath, string outputBaseName, MediaType mediaType)
    {
        var ext = ".mkv";
        var sanitised = SanitiseName(outputBaseName);
        return $"{sanitised}{ext}";
    }

    private static string SanitiseName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
