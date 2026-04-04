using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

/// <summary>
/// Extracts preview frames and filmstrip thumbnails from:
///   • Ripped MKV / MP4 files (via ffmpeg -ss seek)
///   • Directly from disc using ffmpeg's dvd:// or MakeMKV scan output
///
/// Mirrors the DVD Shrink / MakeMKV preview panel experience:
///   – Single frame at a configurable timestamp
///   – Filmstrip (N evenly-spaced thumbnails across the title duration)
///   – Subtitle overlay preview (burns a chosen subtitle stream into the frame)
/// </summary>
public interface IDiscPreviewService
{
    /// <summary>
    /// Extract a single JPEG preview frame from <paramref name="videoPath"/>
    /// at position <paramref name="seekTime"/>.
    /// Returns the path to the written JPEG, or null on failure.
    /// </summary>
    Task<string?> ExtractFrameAsync(string videoPath, TimeSpan seekTime,
        int width = 640, int height = 360,
        string? outputPath = null);

    /// <summary>
    /// Extract a filmstrip of <paramref name="frameCount"/> evenly-spaced frames.
    /// Returns an ordered list of JPEG paths.
    /// </summary>
    Task<List<string>> ExtractFilmstripAsync(string videoPath, int frameCount = 8,
        int thumbWidth = 320, int thumbHeight = 180,
        IProgress<double>? progress = null);

    /// <summary>
    /// Extract a frame with a specific subtitle stream burned in,
    /// so the user can preview subtitle rendering / language.
    /// </summary>
    Task<string?> ExtractFrameWithSubtitleAsync(string videoPath, TimeSpan seekTime,
        int subtitleStreamIndex, int width = 640, int height = 360,
        string? outputPath = null);

    /// <summary>
    /// Generate an animated GIF / WebP preview clip (a few seconds) for the UI.
    /// </summary>
    Task<string?> ExtractClipPreviewAsync(string videoPath, TimeSpan seekTime,
        TimeSpan duration, int width = 480, string? outputPath = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the duration of a media file (used to calculate filmstrip seek points).
    /// </summary>
    Task<TimeSpan?> GetDurationAsync(string videoPath);

    bool IsAvailable { get; }
}

public class DiscPreviewService : IDiscPreviewService
{
    private readonly ISettingsService _settings;
    private readonly ILogService      _log;

    // Preview frames live in a temp subfolder so they can be cleaned up easily
    private string PreviewCacheDir
        => Path.Combine(_settings.Settings.TempPath, "previews");

    public bool IsAvailable => File.Exists(_settings.Settings.FfmpegPath);

    public DiscPreviewService(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log      = log;
    }

    // ── Single frame extraction ───────────────────────────────────────────────

    public async Task<string?> ExtractFrameAsync(
        string videoPath, TimeSpan seekTime,
        int width = 640, int height = 360,
        string? outputPath = null)
    {
        if (!IsAvailable || !File.Exists(videoPath)) return null;

        outputPath ??= GetCachePath($"{HashPath(videoPath)}_{(int)seekTime.TotalSeconds}_{width}x{height}.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // Return cached frame if it exists
        if (File.Exists(outputPath)) return outputPath;

        var seek = FormatTime(seekTime);
        // -ss before -i = fast keyframe seek; -ss after -i = accurate seek
        // We use keyframe seek for speed (preview only needs approximate position)
        var args = $"-ss {seek} -i \"{videoPath}\" " +
                   $"-vf \"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                   $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black\" " +
                   $"-vframes 1 -q:v 3 -y \"{outputPath}\"";

        var success = await RunFfmpegAsync(args);
        return success && File.Exists(outputPath) ? outputPath : null;
    }

    // ── Filmstrip (N evenly-spaced thumbs) ────────────────────────────────────

    public async Task<List<string>> ExtractFilmstripAsync(
        string videoPath, int frameCount = 8,
        int thumbWidth = 320, int thumbHeight = 180,
        IProgress<double>? progress = null)
    {
        var frames = new List<string>();
        if (!IsAvailable || !File.Exists(videoPath)) return frames;

        var duration = await GetDurationAsync(videoPath);
        if (duration == null || duration.Value.TotalSeconds < 1) return frames;

        // Skip first 5 % and last 5 % (avoids black frames / credits)
        var totalSec = duration.Value.TotalSeconds;
        var startSec = totalSec * 0.05;
        var endSec   = totalSec * 0.95;
        var step     = (endSec - startSec) / Math.Max(1, frameCount - 1);

        for (int i = 0; i < frameCount; i++)
        {
            var seekSec = startSec + step * i;
            var outPath = await ExtractFrameAsync(
                videoPath, TimeSpan.FromSeconds(seekSec),
                thumbWidth, thumbHeight);

            if (outPath != null) frames.Add(outPath);
            progress?.Report((i + 1.0) / frameCount * 100.0);
        }

        return frames;
    }

    // ── Frame with subtitle overlay ───────────────────────────────────────────

    public async Task<string?> ExtractFrameWithSubtitleAsync(
        string videoPath, TimeSpan seekTime,
        int subtitleStreamIndex,
        int width = 640, int height = 360,
        string? outputPath = null)
    {
        if (!IsAvailable || !File.Exists(videoPath)) return null;

        outputPath ??= GetCachePath(
            $"{HashPath(videoPath)}_{(int)seekTime.TotalSeconds}_sub{subtitleStreamIndex}_{width}x{height}.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (File.Exists(outputPath)) return outputPath;

        var seek = FormatTime(seekTime);

        // Attempt subtitle burn-in:
        //   For text-based (SRT/ASS): -vf "subtitles=input.mkv:si=N,scale=..."
        //   For bitmap (PGS/VOBsub):  -filter_complex "[0:v][0:s:N]overlay,scale=..."
        // We try bitmap filter first (handles both PGS and bitmap subs properly).
        var filterComplex =
            $"[0:v][0:s:{subtitleStreamIndex}]overlay," +
            $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
            $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black";

        var args = $"-ss {seek} -i \"{videoPath}\" " +
                   $"-filter_complex \"{filterComplex}\" " +
                   $"-vframes 1 -q:v 3 -y \"{outputPath}\"";

        var success = await RunFfmpegAsync(args);

        if (!success || !File.Exists(outputPath))
        {
            // Fall back to text subtitle filter
            var textFilter =
                $"subtitles='{EscapeFilterPath(videoPath)}':si={subtitleStreamIndex}," +
                $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black";

            var argsText = $"-ss {seek} -i \"{videoPath}\" " +
                           $"-vf \"{textFilter}\" " +
                           $"-vframes 1 -q:v 3 -y \"{outputPath}\"";

            success = await RunFfmpegAsync(argsText);
        }

        return success && File.Exists(outputPath) ? outputPath : null;
    }

    // ── Animated clip preview (GIF / WebP) ───────────────────────────────────

    public async Task<string?> ExtractClipPreviewAsync(
        string videoPath, TimeSpan seekTime,
        TimeSpan duration, int width = 480,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable || !File.Exists(videoPath)) return null;

        var ext = ".webp";  // WebP animated = better than GIF
        outputPath ??= GetCachePath(
            $"{HashPath(videoPath)}_{(int)seekTime.TotalSeconds}_clip{width}{ext}");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (File.Exists(outputPath)) return outputPath;

        var seek = FormatTime(seekTime);
        var dur  = $"{duration.TotalSeconds:F1}";

        // -loop 0 = infinite loop (WebP)
        // fps=8 keeps the file small while still being useful as a preview
        var args = $"-ss {seek} -t {dur} -i \"{videoPath}\" " +
                   $"-vf \"fps=8,scale={width}:-1:flags=lanczos\" " +
                   $"-loop 0 -quality 70 -y \"{outputPath}\"";

        var success = await RunFfmpegAsync(args, ct);
        return success && File.Exists(outputPath) ? outputPath : null;
    }

    // ── Duration probe ────────────────────────────────────────────────────────

    public async Task<TimeSpan?> GetDurationAsync(string videoPath)
    {
        if (!IsAvailable || !File.Exists(videoPath)) return null;

        // ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1
        var ffprobe = _settings.Settings.FfprobePath;
        if (!File.Exists(ffprobe)) return null;

        var args = $"-v error -show_entries format=duration " +
                   $"-of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = ffprobe,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true
            };
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (double.TryParse(output.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var sec))
                return TimeSpan.FromSeconds(sec);
        }
        catch { }

        return null;
    }

    // ── Cache management ──────────────────────────────────────────────────────

    /// <summary>Clears all cached preview frames for a given source file.</summary>
    public void ClearCacheForFile(string videoPath)
    {
        var prefix = HashPath(videoPath);
        if (!Directory.Exists(PreviewCacheDir)) return;
        foreach (var f in Directory.GetFiles(PreviewCacheDir, $"{prefix}_*"))
        {
            try { File.Delete(f); } catch { }
        }
    }

    /// <summary>Clears all preview frames older than <paramref name="maxAge"/>.</summary>
    public void PruneCache(TimeSpan maxAge)
    {
        if (!Directory.Exists(PreviewCacheDir)) return;
        var cutoff = DateTime.Now - maxAge;
        foreach (var f in Directory.GetFiles(PreviewCacheDir))
        {
            try
            {
                if (File.GetLastWriteTime(f) < cutoff)
                    File.Delete(f);
            }
            catch { }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> RunFfmpegAsync(string args, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _settings.Settings.FfmpegPath,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"ffmpeg preview error: {ex.Message}");
            return false;
        }
    }

    private string GetCachePath(string filename)
        => Path.Combine(PreviewCacheDir, filename);

    private static string HashPath(string path)
    {
        // Short stable hash of the file path for cache key names
        var hash = 0;
        foreach (var c in path.ToUpperInvariant()) hash = hash * 31 + c;
        return Math.Abs(hash).ToString("X8");
    }

    private static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";

    // ffmpeg subtitle filter needs forward slashes and escaped colons on Windows
    private static string EscapeFilterPath(string path)
        => path.Replace("\\", "/").Replace(":", "\\:");
}
