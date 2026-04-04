using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

public interface IHandBrakeService
{
    /// <summary>Transcode using the global app settings.</summary>
    Task<bool> TranscodeAsync(string inputPath, string outputPath,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Transcode with a named preset and explicit quality value.</summary>
    Task<bool> TranscodeAsync(string inputPath, string outputPath, string preset, int quality,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Transcode using a fully specified TranscodePreset (HandBrake-style).</summary>
    Task<bool> TranscodeWithPresetAsync(string inputPath, string outputPath,
        TranscodePreset preset, TranscodeJobSettings? overrides = null,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Retrieve available presets from the installed HandBrake CLI.</summary>
    Task<List<string>> GetPresetsAsync();

    /// <summary>Get HandBrake CLI version string.</summary>
    Task<string> GetVersionAsync();

    /// <summary>Scan input file and return title/chapter info via HandBrake's --scan mode.</summary>
    Task<string> ScanTitlesAsync(string inputPath, int titleNumber = 0);

    string BuildOutputFileName(string inputPath, string outputBaseName, MediaType mediaType);
}

public class HandBrakeService : IHandBrakeService
{
    private readonly ISettingsService      _settings;
    private readonly ILogService           _logService;
    private readonly ITranscodePresetService _presets;

    // ── Encoder name maps ─────────────────────────────────────────────────────
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

    private static readonly Dictionary<AudioMixdown, string> MixdownMap = new()
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

    public HandBrakeService(
        ISettingsService settings,
        ILogService logService,
        ITranscodePresetService presets)
    {
        _settings   = settings;
        _logService = logService;
        _presets    = presets;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public Task<bool> TranscodeAsync(string inputPath, string outputPath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var s = _settings.Settings;
        return TranscodeAsync(inputPath, outputPath, s.HandBrakePreset, s.VideoQuality, progress, ct);
    }

    public async Task<bool> TranscodeAsync(string inputPath, string outputPath,
        string preset, int quality,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var p = _presets.GetByName(preset) ?? _presets.GetDefault();
        var overridePreset = p with { VideoQuality = quality };
        return await TranscodeWithPresetAsync(inputPath, outputPath, overridePreset, null, progress, ct);
    }

    public async Task<bool> TranscodeWithPresetAsync(
        string inputPath, string outputPath,
        TranscodePreset preset, TranscodeJobSettings? overrides = null,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await _logService.LogAsync($"Transcoding: {Path.GetFileName(inputPath)} → {Path.GetFileName(outputPath)}");
            await _logService.LogAsync($"Preset: {preset.Name} | {preset.VideoEncoder} RF{preset.VideoQuality}");

            // Resolve effective output path (update extension from preset format)
            outputPath = ApplyOutputExtension(outputPath, preset.OutputFormat);

            var args = BuildPresetArguments(inputPath, outputPath, preset, overrides);
            await _logService.LogAsync($"HandBrakeCLI: {args}");

            var psi = new ProcessStartInfo
            {
                FileName               = _settings.Settings.HandBrakePath,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var process = new Process { StartInfo = psi };

            // Set process priority
            process.PriorityClass = _settings.Settings.ProcessPriority switch
            {
                ProcessPriorityLevel.Low          => ProcessPriorityClass.Idle,
                ProcessPriorityLevel.BelowNormal  => ProcessPriorityClass.BelowNormal,
                ProcessPriorityLevel.AboveNormal  => ProcessPriorityClass.AboveNormal,
                ProcessPriorityLevel.High         => ProcessPriorityClass.High,
                _                                 => ProcessPriorityClass.Normal
            };

            // HandBrake writes progress to stderr
            var etaPattern   = new Regex(@"ETA\s+(\d+h\d+m\d+s|\d+m\d+s)");
            var pctPattern   = new Regex(@"(\d+\.\d+)\s*%");
            var fpsPattern   = new Regex(@"([\d.]+)\s*fps");

            string? currentEta = null;
            double  currentFps = 0;

            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                _ = _logService.LogAsync($"[HB] {e.Data}");

                var mPct = pctPattern.Match(e.Data);
                if (mPct.Success && double.TryParse(mPct.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    progress?.Report(pct);
                }

                var mEta = etaPattern.Match(e.Data);
                if (mEta.Success) currentEta = mEta.Groups[1].Value;

                var mFps = fpsPattern.Match(e.Data);
                if (mFps.Success) double.TryParse(mFps.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out currentFps);
            };

            process.Start();

            // Apply priority after start
            try { process.PriorityClass = _settings.Settings.ProcessPriority switch
            {
                ProcessPriorityLevel.Low         => ProcessPriorityClass.Idle,
                ProcessPriorityLevel.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriorityLevel.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriorityLevel.High        => ProcessPriorityClass.High,
                _                                => ProcessPriorityClass.Normal
            }; } catch { /* may fail if process already exited */ }

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

    public async Task<List<string>> GetPresetsAsync()
    {
        var fallback = _presets.AllPresets.Select(p => p.Name).ToList();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _settings.Settings.HandBrakePath,
                Arguments              = "--preset-list",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var custom = output.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('>') && !l.StartsWith('<') && !l.EndsWith(':'))
                .Distinct()
                .ToList();

            return custom.Count > 0 ? custom : fallback;
        }
        catch { return fallback; }
    }

    public async Task<string> GetVersionAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _settings.Settings.HandBrakePath, Arguments = "--version",
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return output.Trim();
        }
        catch { return "unknown"; }
    }

    public async Task<string> ScanTitlesAsync(string inputPath, int titleNumber = 0)
    {
        var titleArg = titleNumber > 0 ? $"--title {titleNumber}" : "--title 0";
        var args = $"--input \"{inputPath}\" {titleArg} --scan";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _settings.Settings.HandBrakePath, Arguments = args,
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return stdout + stderr;
        }
        catch (Exception ex)
        {
            return $"Scan error: {ex.Message}";
        }
    }

    public string BuildOutputFileName(string inputPath, string outputBaseName, MediaType mediaType)
    {
        var ext     = "." + _settings.Settings.DefaultOutputFormat.ToString().ToLowerInvariant();
        var cleaned = SanitiseName(outputBaseName);
        return $"{cleaned}{ext}";
    }

    // ── Argument builder ──────────────────────────────────────────────────────

    private string BuildPresetArguments(
        string inputPath, string outputPath,
        TranscodePreset p, TranscodeJobSettings? overrides)
    {
        // If we have a matching built-in HandBrake preset name, let HandBrake load it first
        // and then layer our overrides on top. Otherwise build from scratch.
        return _presets.BuildHandBrakeArgs(p, inputPath, outputPath)
            + BuildOverrideArgs(p, overrides);
    }

    private string BuildOverrideArgs(TranscodePreset p, TranscodeJobSettings? overrides)
    {
        if (overrides == null) return string.Empty;
        var sb = new StringBuilder();

        // Crop override
        if (overrides.OverrideCrop)
            sb.Append($" --crop {overrides.CropTop}:{overrides.CropBottom}:{overrides.CropLeft}:{overrides.CropRight}");

        // Audio track selection
        if (overrides.AudioTrackIndices.Count > 0)
            sb.Append($" --audio {string.Join(",", overrides.AudioTrackIndices.Select(i => i + 1))}");

        // Subtitle track selection with per-track disposition
        if (overrides.SubtitleTrackIndices.Count > 0)
        {
            sb.Append($" --subtitle {string.Join(",", overrides.SubtitleTrackIndices.Select(i => i + 1))}");

            // Check if any track should be burned in
            var burnTrack = overrides.SubtitleDispositions
                .FirstOrDefault(kv => kv.Value == SubtitleDisposition.BurnIn);
            if (burnTrack.Key != default)
            {
                int burnIdx = overrides.SubtitleTrackIndices.IndexOf(burnTrack.Key) + 1;
                if (burnIdx > 0) sb.Append($" --subtitle-burned {burnIdx}");
            }
        }

        // Verbosity
        var verbosity = _settings.Settings.LogVerbosity switch
        {
            LogVerbosity.Minimal => 0,
            LogVerbosity.Verbose => 2,
            LogVerbosity.Debug   => 3,
            _                    => 1
        };
        sb.Append($" --verbose={verbosity}");
        sb.Append($" --previews {_settings.Settings.NumberOfPreviewsToScan}");

        return sb.ToString();
    }

    private static string ApplyOutputExtension(string outputPath, OutputFormat format)
    {
        var ext = format switch
        {
            OutputFormat.MP4  => ".mp4",
            OutputFormat.WebM => ".webm",
            OutputFormat.M4V  => ".m4v",
            _                 => ".mkv"
        };
        return Path.ChangeExtension(outputPath, ext);
    }

    private static string SanitiseName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
