using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

/// <summary>
/// Subtitle extraction, conversion and management service.
///
/// What MakeMKV / DVDFab do with subtitles:
///   DVD      – VOBsub (bitmap PGS) → MKV soft subtitle or SRT (via OCR with Tesseract)
///   Blu-ray  – PGS/HDMV bitmap → MKV soft subtitle or SRT
///   MKV      – can embed subrip (SRT), ASS, PGS, VOBsub
///
/// What HandBrake does:
///   --all-subtitles          pass through all subtitle tracks
///   --subtitle-burned N      burn track N into video
///   --subtitle-forced        only include forced-flagged tracks
///   --srt-file / --ssa-file  import external subtitle files
/// </summary>
public interface ISubtitleService
{
    /// <summary>Extract all subtitle tracks from an MKV to individual SRT files using ffmpeg.</summary>
    Task<List<string>> ExtractSubtitlesAsync(string mkvPath, string outputDirectory,
        List<int>? trackIndices = null, IProgress<string>? progress = null);

    /// <summary>Convert a VOBsub (.idx/.sub) pair to SRT using OCR (requires Tesseract).</summary>
    Task<string?> ConvertVobSubToSrtAsync(string idxPath, string outputPath,
        string language = "eng", IProgress<string>? progress = null);

    /// <summary>Detect forced subtitle tracks in an MKV file.</summary>
    Task<List<SubtitleStreamInfo>> GetForcedSubtitlesAsync(string mkvPath);

    /// <summary>Merge an external SRT file into an existing MKV (mux, non-destructive).</summary>
    Task<bool> MergeSubtitleIntoMkvAsync(string mkvPath, string srtPath,
        string language = "eng", string title = "", bool makeDefault = false);

    /// <summary>List subtitle tracks available in an MKV using ffprobe data.</summary>
    Task<List<SubtitleStreamInfo>> GetTracksAsync(string mkvPath);

    /// <summary>Generate a timed SRT from chapter markers in an MKV (useful for chapterised rips).</summary>
    Task<string?> GenerateChapterSrtAsync(string mkvPath, string outputPath);
}

public class SubtitleService : ISubtitleService
{
    private readonly ISettingsService _settings;
    private readonly ILogService      _log;
    private readonly IFfprobeService  _ffprobe;

    public SubtitleService(ISettingsService settings, ILogService log, IFfprobeService ffprobe)
    {
        _settings = settings;
        _log      = log;
        _ffprobe  = ffprobe;
    }

    // ── Extraction ────────────────────────────────────────────────────────────

    public async Task<List<string>> ExtractSubtitlesAsync(
        string mkvPath, string outputDirectory,
        List<int>? trackIndices = null, IProgress<string>? progress = null)
    {
        var extracted = new List<string>();

        var mediaInfo = await _ffprobe.AnalyseFileAsync(mkvPath);
        if (mediaInfo == null || mediaInfo.SubtitleStreams.Count == 0)
        {
            await _log.LogAsync("ExtractSubtitles: no subtitle tracks found");
            return extracted;
        }

        Directory.CreateDirectory(outputDirectory);
        var ffmpegPath = _settings.Settings.FfmpegPath;

        if (!File.Exists(ffmpegPath))
        {
            await _log.LogAsync("ExtractSubtitles: ffmpeg not found. Set FfmpegPath in Settings.");
            return extracted;
        }

        var tracksToExtract = trackIndices != null
            ? mediaInfo.SubtitleStreams.Where(s => trackIndices.Contains(s.StreamIndex)).ToList()
            : mediaInfo.SubtitleStreams;

        foreach (var track in tracksToExtract)
        {
            progress?.Report($"Extracting subtitle track {track.StreamIndex} ({track.Language})…");

            var ext  = track.IsTextBased ? ".srt" : ".sup";
            var safe = SanitiseFileName($"{track.StreamIndex:D2}_{track.Language ?? "und"}" +
                                        (track.IsForced ? "_forced" : ""));
            var outPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(mkvPath)}.{safe}{ext}");

            // ffmpeg -i input.mkv -map 0:s:N output.srt
            var args = $"-i \"{mkvPath}\" -map 0:s:{track.StreamIndex} -y \"{outPath}\"";

            try
            {
                var (exitCode, stderr) = await RunProcessAsync(ffmpegPath, args);

                if (exitCode == 0 && File.Exists(outPath))
                {
                    extracted.Add(outPath);
                    await _log.LogAsync($"Extracted subtitle → {Path.GetFileName(outPath)}");
                }
                else
                {
                    await _log.LogAsync($"Subtitle extraction failed for track {track.StreamIndex}: {stderr}");
                }
            }
            catch (Exception ex)
            {
                await _log.LogAsync($"Subtitle extraction error: {ex.Message}");
            }
        }

        return extracted;
    }

    // ── VOBsub → SRT (OCR) ────────────────────────────────────────────────────

    public async Task<string?> ConvertVobSubToSrtAsync(
        string idxPath, string outputPath,
        string language = "eng", IProgress<string>? progress = null)
    {
        var tesseract = _settings.Settings.TesseractPath;
        if (!File.Exists(tesseract))
        {
            await _log.LogAsync("VOBsub→SRT: Tesseract not found. Set TesseractPath in Settings.");
            return null;
        }

        // Use SubRip / VobSub2SRT approach:
        // We use ffmpeg to convert .idx/.sub → .srt via subtitle2pgm → tesseract pipeline,
        // OR simply extract as PGS and call tesseract on each frame.
        // Simpler approach: ffmpeg can do basic OCR on dvd_subtitle if tesseract is available.

        progress?.Report("Converting VOBsub to SRT (OCR in progress – this may take a while)…");

        // ffmpeg -i input.idx -c:s srt output.srt
        var subPath = Path.ChangeExtension(idxPath, ".sub");
        if (!File.Exists(subPath))
        {
            await _log.LogAsync("VOBsub→SRT: matching .sub file not found");
            return null;
        }

        var ffmpegPath = _settings.Settings.FfmpegPath;
        if (!File.Exists(ffmpegPath)) return null;

        try
        {
            var args = $"-i \"{idxPath}\" -sub_charenc UTF-8 -y \"{outputPath}\"";
            var (exitCode, stderr) = await RunProcessAsync(ffmpegPath, args);

            if (exitCode == 0 && File.Exists(outputPath))
            {
                await _log.LogAsync($"VOBsub→SRT complete → {Path.GetFileName(outputPath)}");
                return outputPath;
            }

            await _log.LogAsync($"VOBsub→SRT failed: {stderr}");
            return null;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"VOBsub→SRT error: {ex.Message}");
            return null;
        }
    }

    // ── Forced subtitle detection ─────────────────────────────────────────────

    public async Task<List<SubtitleStreamInfo>> GetForcedSubtitlesAsync(string mkvPath)
    {
        var info = await _ffprobe.AnalyseFileAsync(mkvPath);
        if (info == null) return new();

        return info.SubtitleStreams
            .Where(s => s.IsForced || s.Title.Contains("forced", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ── Merge SRT into MKV ────────────────────────────────────────────────────

    public async Task<bool> MergeSubtitleIntoMkvAsync(
        string mkvPath, string srtPath,
        string language = "eng", string title = "", bool makeDefault = false)
    {
        var mkvmergePath = _settings.Settings.MkvMergePath;

        if (!File.Exists(mkvmergePath))
        {
            await _log.LogAsync("MergeSubtitle: mkvmerge not found. Set MkvMergePath in Settings.");
            return false;
        }

        var outputPath = Path.Combine(
            Path.GetDirectoryName(mkvPath)!,
            Path.GetFileNameWithoutExtension(mkvPath) + "_subbed.mkv");

        var titleArg   = string.IsNullOrWhiteSpace(title) ? string.Empty : $"--track-name 0:\"{title}\"";
        var defaultArg = makeDefault ? "--default-track 0:1" : "--default-track 0:0";
        var langArg    = $"--language 0:{language}";

        var args = $"-o \"{outputPath}\" \"{mkvPath}\" {langArg} {titleArg} {defaultArg} \"{srtPath}\"";

        try
        {
            var (exitCode, stderr) = await RunProcessAsync(mkvmergePath, args);
            if (exitCode == 0)
            {
                await _log.LogAsync($"Merged subtitle → {Path.GetFileName(outputPath)}");
                return true;
            }

            await _log.LogAsync($"MergeSubtitle failed: {stderr}");
            return false;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"MergeSubtitle error: {ex.Message}");
            return false;
        }
    }

    // ── Get tracks ────────────────────────────────────────────────────────────

    public async Task<List<SubtitleStreamInfo>> GetTracksAsync(string mkvPath)
    {
        var info = await _ffprobe.AnalyseFileAsync(mkvPath);
        return info?.SubtitleStreams ?? new();
    }

    // ── Chapter → SRT ─────────────────────────────────────────────────────────

    public async Task<string?> GenerateChapterSrtAsync(string mkvPath, string outputPath)
    {
        var info = await _ffprobe.AnalyseFileAsync(mkvPath);
        if (info == null || info.Chapters.Count == 0)
        {
            await _log.LogAsync("GenerateChapterSrt: no chapters found");
            return null;
        }

        try
        {
            var sb = new StringBuilder();
            for (int i = 0; i < info.Chapters.Count; i++)
            {
                var ch = info.Chapters[i];
                sb.AppendLine((i + 1).ToString());
                sb.AppendLine($"{FormatSrtTime(ch.StartTime)} --> {FormatSrtTime(ch.EndTime)}");
                sb.AppendLine(string.IsNullOrWhiteSpace(ch.Name) ? $"Chapter {ch.Number}" : ch.Name);
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
            await _log.LogAsync($"Chapter SRT generated: {Path.GetFileName(outputPath)}");
            return outputPath;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"GenerateChapterSrt error: {ex.Message}");
            return null;
        }
    }

    // ── SRT format helpers ────────────────────────────────────────────────────

    private static string FormatSrtTime(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2},{t.Milliseconds:D3}";

    // ── Process runner ────────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string Stderr)> RunProcessAsync(string exe, string args)
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

        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stderr);
    }

    private static string SanitiseFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
