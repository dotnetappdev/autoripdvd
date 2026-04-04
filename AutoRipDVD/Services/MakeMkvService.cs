using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

public interface IMakeMkvService
{
    Task<List<TitleInfo>> ScanDiscAsync(string driveLetter, IProgress<string>? progress = null);
    Task<bool> RipTitlesAsync(string driveLetter, List<int> titleIndices, string outputPath,
        IProgress<double>? progress = null, IProgress<MakeMkvRipStats>? statsProgress = null,
        CancellationToken ct = default);
    Task<string> GetVersionAsync();
    Task<List<string>> GetDriveListAsync();
    Task<bool> BackupDiscAsync(string driveLetter, string outputPath,
        IProgress<double>? progress = null, CancellationToken ct = default);
}

public class MakeMkvService : IMakeMkvService
{
    private readonly ISettingsService _settings;
    private readonly ILogService      _logService;

    // ── MakeMKV TINFO / SINFO codes ─────────────────────────────────────────
    // Reference: https://www.makemkv.com/developers/usage.txt

    // TINFO codes
    private const int TInfoName         = 2;
    private const int TInfoChapterCount = 8;
    private const int TInfoDuration     = 9;
    private const int TInfoDiskSize     = 10;
    private const int TInfoAngleCount   = 11;
    private const int TInfoSourceId     = 14;
    private const int TInfoOutputFile   = 27;
    private const int TInfoComment      = 30;
    private const int TInfoSegmentCount = 19;
    private const int TInfoSegmentMap   = 20;
    private const int TInfoFlags        = 8192;

    // SINFO codes
    private const int SInfoType         = 1;   // 1=Video 2=Audio 3=Subtitle
    private const int SInfoCodecId      = 2;   // e.g. "V_MPEG4/ISO/AVC"
    private const int SInfoLangCode     = 3;   // ISO 639-2 "eng"
    private const int SInfoLangName     = 4;   // "English"
    private const int SInfoCodecShort   = 5;   // "H.264"
    private const int SInfoCodecLong    = 6;   // "H.264/AVC High@L4.1"
    private const int SInfoVideoFps     = 7;   // "23.976 (24000/1001)"
    private const int SInfoVideoRes     = 13;  // "1920x1080"
    private const int SInfoVideoAspect  = 17;  // "16:9"
    private const int SInfoAudioChannels = 19; // "6" or "7.1"
    private const int SInfoAudioSampleRate = 20;
    private const int SInfoBitrate      = 21;  // "25.7 Mb/s"
    private const int SInfoAudioBitDepth = 22;
    private const int SInfoStreamFlags  = 28;  // bit 4=forced, bit 8=default

    // Stream type values
    private const int StreamTypeVideo    = 1;
    private const int StreamTypeAudio    = 2;
    private const int StreamTypeSubtitle = 3;

    public MakeMkvService(ISettingsService settings, ILogService logService)
    {
        _settings   = settings;
        _logService = logService;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public async Task<List<TitleInfo>> ScanDiscAsync(string driveLetter, IProgress<string>? progress = null)
    {
        progress?.Report("Scanning disc with MakeMKV…");
        await _logService.LogAsync($"Scanning disc in {driveLetter}");

        try
        {
            var driveIndex = GetDriveIndex(driveLetter);
            var minLen     = _settings.Settings.MinimumTitleLengthSeconds;
            var args       = $"-r --cache=1 --minlength={minLen} info disc:{driveIndex}";

            var output = await RunMakeMkvAsync(args);
            var titles = ParseFullOutput(output);

            await _logService.LogAsync($"Found {titles.Count} titles on disc");
            return titles;
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error scanning disc: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> RipTitlesAsync(
        string driveLetter, List<int> titleIndices, string outputPath,
        IProgress<double>? progress = null, IProgress<MakeMkvRipStats>? statsProgress = null,
        CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(outputPath);
            var driveIndex = GetDriveIndex(driveLetter);
            var s          = _settings.Settings;

            // One stats tracker for the whole batch
            var tracker = new MakeMkvStatsTracker(outputPath);

            foreach (var titleIndex in titleIndices)
            {
                if (ct.IsCancellationRequested) return false;

                await _logService.LogAsync($"Ripping title {titleIndex} from {driveLetter}");

                tracker.ResetTitleTimer();
                var args = BuildRipArguments(driveIndex, titleIndex, outputPath, s);
                await RunMakeMkvWithProgressAsync(args, progress, statsProgress, tracker, ct);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            await _logService.LogAsync("Rip cancelled");
            return false;
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error ripping titles: {ex.Message}");
            return false;
        }
    }

    /// <summary>Full disc backup (like MakeMKV "Backup" mode) – copies all VTS to folder.</summary>
    public async Task<bool> BackupDiscAsync(
        string driveLetter, string outputPath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(outputPath);
            var driveIndex = GetDriveIndex(driveLetter);
            var args       = $"-r backup disc:{driveIndex} \"{outputPath}\"";

            await _logService.LogAsync($"Backing up disc {driveLetter} to {outputPath}");
            await RunMakeMkvWithProgressAsync(args, progress, null, new MakeMkvStatsTracker(outputPath), ct);
            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Backup error: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GetVersionAsync()
    {
        try
        {
            var output = await RunMakeMkvAsync("-r --version");
            return output.Split('\n').FirstOrDefault(l => l.StartsWith("MSG:5010"))
                         ?.Split('"').Skip(1).FirstOrDefault() ?? output.Trim();
        }
        catch { return "unknown"; }
    }

    public async Task<List<string>> GetDriveListAsync()
    {
        var drives = new List<string>();
        try
        {
            // makemkvcon -r info disc:9999 lists all available drives
            var output = await RunMakeMkvAsync("-r info disc:9999");
            foreach (var line in output.Split('\n'))
            {
                // DRV:index,visible,enabled,flags,"driveName","discLabel","drivePath"
                if (!line.StartsWith("DRV:")) continue;
                var parts = line.Substring(4).Split(',');
                if (parts.Length < 6) continue;
                var label = parts[5].Trim('"');
                var path  = parts.Length > 6 ? parts[6].Trim('"') : string.Empty;
                if (!string.IsNullOrWhiteSpace(label))
                    drives.Add($"{path} [{label}]");
            }
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"GetDriveList error: {ex.Message}");
        }
        return drives;
    }

    // ── Argument builder ──────────────────────────────────────────────────────

    private string BuildRipArguments(int driveIndex, int titleIndex, string outputPath, AppSettings s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("-r ");

        // Minimum length filter
        sb.Append($"--minlength={s.MinimumTitleLengthSeconds} ");

        // Expert mode
        if (s.ExpertMode) sb.Append("--expert ");

        // No internet (offline mode)
        if (!s.EnableInternetAccess) sb.Append("--noscan ");

        // Disc profile: leave MakeMKV to auto-detect (default)

        sb.Append($"mkv disc:{driveIndex} {titleIndex} \"{outputPath}\"");
        return sb.ToString();
    }

    // ── Process runners ───────────────────────────────────────────────────────

    private async Task<string> RunMakeMkvAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = _settings.Settings.MakeMkvPath,
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    private async Task RunMakeMkvWithProgressAsync(
        string arguments,
        IProgress<double>? progress,
        IProgress<MakeMkvRipStats>? statsProgress,
        MakeMkvStatsTracker tracker,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = _settings.Settings.MakeMkvPath,
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            _ = _logService.LogAsync(e.Data);
            ParseRipOutputLine(e.Data, progress, statsProgress, tracker);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _ = _logService.LogAsync($"[MKV] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (ct.IsCancellationRequested && !process.HasExited)
            process.Kill(entireProcessTree: true);
    }

    private static void ParseRipOutputLine(
        string line,
        IProgress<double>? progress,
        IProgress<MakeMkvRipStats>? statsProgress,
        MakeMkvStatsTracker tracker)
    {
        if (line.StartsWith("PRGV:"))
        {
            // PRGV:current,total,max
            // current = per-title progress, total = overall progress, max = scale
            var m = Regex.Match(line, @"PRGV:(\d+),(\d+),(\d+)");
            if (m.Success
                && long.TryParse(m.Groups[1].Value, out var cur)
                && long.TryParse(m.Groups[2].Value, out var tot)
                && long.TryParse(m.Groups[3].Value, out var max)
                && max > 0)
            {
                tracker.TitleProgressRaw   = cur;
                tracker.OverallProgressRaw = tot;
                tracker.ProgressMax        = max;
                progress?.Report((double)tot / max * 100.0);
                statsProgress?.Report(tracker.ToStats());
            }
        }
        else if (line.StartsWith("PRGC:"))
        {
            // PRGC:code,id,"message"  – current (per-title) bar label
            var msg = ExtractLastQuotedValue(line);
            if (!string.IsNullOrEmpty(msg))
            {
                tracker.TitleProgressLabel = msg;
                tracker.ResetTitleTimer();
                statsProgress?.Report(tracker.ToStats());
            }
        }
        else if (line.StartsWith("PRGT:"))
        {
            // PRGT:code,id,"message"  – total (overall) bar label
            var msg = ExtractLastQuotedValue(line);
            if (!string.IsNullOrEmpty(msg))
            {
                tracker.OverallProgressLabel = msg;
                statsProgress?.Report(tracker.ToStats());
            }
        }
        else if (line.StartsWith("MSG:"))
        {
            // MSG:code,flags,count,"human message","format","param0",...
            // The 4th field (index 3 after splitting) is the human-readable message.
            var humanMsg = ExtractFirstQuotedValue(line);
            if (string.IsNullOrEmpty(humanMsg)) return;

            // ── Classify the message ──────────────────────────────────────
            // Title skipped / added – add to messages list
            if (humanMsg.Contains("was added") || humanMsg.Contains("was therefore skipped")
                || humanMsg.Contains("Operation successfully completed")
                || humanMsg.Contains("Saving") && humanMsg.Contains("titles into directory"))
            {
                tracker.Messages.Add(humanMsg);
                statsProgress?.Report(tracker.ToStats());
                return;
            }

            // Read-rate line: "6.3 M/s [4.7X]" appears somewhere in a MSG
            var rateMatch = Regex.Match(humanMsg, @"\d+\.\d+\s*M/s\s*\[\d+\.?\d*X\]");
            if (rateMatch.Success)
            {
                tracker.ReadRate = rateMatch.Value;
                statsProgress?.Report(tracker.ToStats());
                return;
            }

            // Source file (current VOB/IFO being read)
            var srcMatch = Regex.Match(humanMsg,
                @"([A-Za-z]:[/\\][^""]+\.(?:VOB|IFO|BUP|SSIF)|/[^""]+\.(?:VOB|IFO|BUP|SSIF))",
                RegexOptions.IgnoreCase);
            if (srcMatch.Success)
            {
                tracker.SourceFile = srcMatch.Value;
                statsProgress?.Report(tracker.ToStats());
                return;
            }

            // Output file (current MKV being saved)
            var outMatch = Regex.Match(humanMsg,
                @"([A-Za-z]:[/\\][^""]+\.mkv|/[^""]+\.mkv)",
                RegexOptions.IgnoreCase);
            if (outMatch.Success)
            {
                tracker.OutputFile = outMatch.Value;
                // Attempt to read output size + free space from the filesystem
                tracker.RefreshOutputSizeAndFreeSpace(outMatch.Value);
                statsProgress?.Report(tracker.ToStats());
            }
        }
        else if (line.StartsWith("CINFO:"))
        {
            // CINFO:code,flags,"value"
            var parts = line.Substring("CINFO:".Length).Split(',');
            if (parts.Length < 2) return;
            if (!int.TryParse(parts[0], out var code)) return;
            var value = parts.Length >= 3
                ? parts[2].Trim('"')
                : parts[1].Trim('"');

            switch (code)
            {
                case 1:   // disc type / label
                    if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(tracker.Source))
                        tracker.Source = value;
                    break;
                case 2:   // disc name
                    if (!string.IsNullOrEmpty(value))
                        tracker.Source = value + (string.IsNullOrEmpty(tracker.Source)
                                         ? string.Empty : $" ({tracker.Source})");
                    break;
                case 11:  // disc size in bytes
                    if (long.TryParse(value, out var bytes) && bytes > 0)
                        tracker.SourceSize = FormatBytes(bytes);
                    break;
                case 30:  // drive comment (sometimes contains drive model)
                    if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(tracker.Source))
                        tracker.Source = value;
                    break;
            }
            statsProgress?.Report(tracker.ToStats());
        }
    }

    // ── String extraction helpers ─────────────────────────────────────────────

    /// <summary>Extract the human-readable message – the first quoted token in a MSG: line.</summary>
    private static string ExtractFirstQuotedValue(string line)
    {
        // Skip past "MSG:code,flags,count," then grab the first "..."
        var start = line.IndexOf('"');
        if (start < 0) return string.Empty;
        var end = line.IndexOf('"', start + 1);
        if (end < 0) return string.Empty;
        return line.Substring(start + 1, end - start - 1);
    }

    /// <summary>Extract the last quoted token (used for PRGC/PRGT labels).</summary>
    private static string ExtractLastQuotedValue(string line)
    {
        var end = line.LastIndexOf('"');
        if (end < 1) return string.Empty;
        var start = line.LastIndexOf('"', end - 1);
        if (start < 0) return string.Empty;
        return line.Substring(start + 1, end - start - 1);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824L)
            return $"{bytes / 1_073_741_824.0:F1} G";
        if (bytes >= 1_048_576L)
            return $"{bytes / 1_048_576.0:F1} M";
        return $"{bytes / 1_024.0:F1} K";
    }

    // ── Full output parser (TINFO + SINFO) ────────────────────────────────────

    /// <summary>
    /// Parse all TINFO and SINFO lines from a -r info disc:N run.
    ///
    /// TINFO:titleIdx,code,value  → per-title attributes
    /// SINFO:titleIdx,streamIdx,code,value → per-stream attributes
    /// CINFO:code,value           → disc attributes
    ///
    /// This is exactly what MakeMKV exposes via its robot mode (-r flag).
    /// </summary>
    private List<TitleInfo> ParseFullOutput(string output)
    {
        var titles = new List<TitleInfo>();

        // Temporary per-stream data: titleIndex → list of stream records
        var streamData = new Dictionary<int, List<StreamRecord>>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("TINFO:"))
                ParseTInfoLine(line, titles);
            else if (line.StartsWith("SINFO:"))
                ParseSInfoLine(line, streamData);
        }

        // Merge stream data into titles
        foreach (var (titleIdx, streams) in streamData)
        {
            while (titles.Count <= titleIdx)
                titles.Add(new TitleInfo { Index = titles.Count });

            var title = titles[titleIdx];
            ApplyStreamData(title, streams);
        }

        // Mark main feature
        if (titles.Any())
        {
            var main = titles.OrderByDescending(t => t.Duration).First();
            main.IsMainFeature = true;
        }

        return titles.Where(t => t.Duration.TotalSeconds > 0).ToList();
    }

    private static void ParseTInfoLine(string line, List<TitleInfo> titles)
    {
        // TINFO:index,code,flags,"value"
        var content = line.Substring("TINFO:".Length);
        var parts   = SplitCsvLine(content, 4);
        if (parts.Length < 3) return;

        if (!int.TryParse(parts[0], out var idx)) return;
        if (!int.TryParse(parts[1], out var code)) return;

        while (titles.Count <= idx)
            titles.Add(new TitleInfo { Index = titles.Count });

        var value = parts.Length >= 4 ? parts[3].Trim('"') : parts[2].Trim('"');
        var title  = titles[idx];

        switch (code)
        {
            case TInfoName:
                title.Name = value;
                break;
            case TInfoDuration:
                // Duration in HH:MM:SS format or total seconds
                if (TimeSpan.TryParseExact(value, @"hh\:mm\:ss", null, out var ts))
                    title.Duration = ts;
                else if (int.TryParse(value, out var secs))
                    title.Duration = TimeSpan.FromSeconds(secs);
                break;
            case TInfoDiskSize:
                if (long.TryParse(value, out var sz)) title.SizeBytes = sz;
                break;
            case TInfoChapterCount:
                if (int.TryParse(value, out var ch)) title.ChapterCount = ch;
                break;
            case TInfoSourceId:
                if (int.TryParse(value, out var sid)) title.SourceTitleId = sid;
                break;
            case TInfoOutputFile:
                title.FileName = value;
                break;
            case TInfoComment:
                title.Comment = value;
                break;
            case TInfoSegmentCount:
                if (int.TryParse(value, out var seg)) title.SegmentCount = seg;
                break;
            case TInfoSegmentMap:
                title.SegmentMap = value;
                break;
            case TInfoAngleCount:
                if (int.TryParse(value, out var ang) && ang > 1)
                    title.AngleInfo = $"{ang} angles";
                break;
        }
    }

    private static void ParseSInfoLine(string line, Dictionary<int, List<StreamRecord>> data)
    {
        // SINFO:titleIdx,streamIdx,code,flags,"value"
        var content = line.Substring("SINFO:".Length);
        var parts   = SplitCsvLine(content, 5);
        if (parts.Length < 4) return;

        if (!int.TryParse(parts[0], out var titleIdx)) return;
        if (!int.TryParse(parts[1], out var streamIdx)) return;
        if (!int.TryParse(parts[2], out var code)) return;

        var value = parts.Length >= 5 ? parts[4].Trim('"') : parts[3].Trim('"');

        if (!data.TryGetValue(titleIdx, out var streams))
        {
            streams = new List<StreamRecord>();
            data[titleIdx] = streams;
        }

        // Find or create stream record
        var stream = streams.FirstOrDefault(s => s.Index == streamIdx);
        if (stream == null)
        {
            stream = new StreamRecord { Index = streamIdx };
            streams.Add(stream);
        }

        switch (code)
        {
            case SInfoType:
                if (int.TryParse(value, out var t)) stream.Type = t;
                break;
            case SInfoCodecId:
                stream.CodecId = value;
                break;
            case SInfoCodecShort:
                stream.CodecShort = value;
                break;
            case SInfoCodecLong:
                stream.CodecLong = value;
                break;
            case SInfoLangCode:
                stream.LangCode = value;
                break;
            case SInfoLangName:
                stream.LangName = value;
                break;
            case SInfoVideoFps:
                stream.Fps = value;
                break;
            case SInfoVideoRes:
                stream.Resolution = value;
                break;
            case SInfoVideoAspect:
                stream.Aspect = value;
                break;
            case SInfoAudioChannels:
                stream.Channels = value;
                break;
            case SInfoAudioSampleRate:
                stream.SampleRate = value;
                break;
            case SInfoAudioBitDepth:
                stream.BitDepth = value;
                break;
            case SInfoBitrate:
                stream.Bitrate = value;
                break;
            case SInfoStreamFlags:
                if (int.TryParse(value, out var flags))
                {
                    stream.IsForced  = (flags & 4) != 0;
                    stream.IsDefault = (flags & 8) != 0;
                }
                break;
        }
    }

    private static void ApplyStreamData(TitleInfo title, List<StreamRecord> streams)
    {
        var videoStreams    = streams.Where(s => s.Type == StreamTypeVideo).ToList();
        var audioStreams    = streams.Where(s => s.Type == StreamTypeAudio).ToList();
        var subtitleStreams = streams.Where(s => s.Type == StreamTypeSubtitle).ToList();

        // Video attributes from first video stream
        var video = videoStreams.FirstOrDefault();
        if (video != null)
        {
            title.VideoCodec = video.CodecShort.IfEmpty(video.CodecId);
            title.Resolution = video.Resolution;
        }

        // Audio track descriptions  e.g. "AC3 5.1 English"
        title.AudioTracks = audioStreams
            .Select(a =>
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(a.CodecShort)) parts.Add(a.CodecShort);
                if (!string.IsNullOrEmpty(a.Channels))   parts.Add(a.Channels);
                if (!string.IsNullOrEmpty(a.LangName))   parts.Add(a.LangName);
                if (a.IsDefault) parts.Add("[Default]");
                if (!string.IsNullOrEmpty(a.Bitrate))    parts.Add($"({a.Bitrate})");
                return string.Join(" ", parts);
            })
            .ToList();

        // First audio codec for display
        if (audioStreams.Any())
            title.AudioCodec = audioStreams.First().CodecShort.IfEmpty(audioStreams.First().CodecId);

        // Subtitle track descriptions
        title.SubtitleTracks = subtitleStreams
            .Select(s =>
            {
                var name = string.IsNullOrEmpty(s.LangName) ? s.LangCode : s.LangName;
                if (s.IsForced) name += " [Forced]";
                if (!string.IsNullOrEmpty(s.CodecShort)) name += $" ({s.CodecShort})";
                return name;
            })
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int GetDriveIndex(string driveLetter)
    {
        var letter = driveLetter.TrimEnd(':', '\\').ToUpper()[0];
        return letter - 'A';
    }

    /// <summary>
    /// Splits a CSV line respecting quoted fields.
    /// maxParts = maximum segments to return (last segment gets the remainder).
    /// </summary>
    private static string[] SplitCsvLine(string line, int maxParts)
    {
        var parts   = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuote = !inQuote;
                current.Append(c);
            }
            else if (c == ',' && !inQuote)
            {
                parts.Add(current.ToString());
                current.Clear();

                if (parts.Count >= maxParts - 1)
                {
                    // Remainder goes as last part
                    parts.Add(line[(i + 1)..]);
                    return parts.ToArray();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        parts.Add(current.ToString());
        return parts.ToArray();
    }

    // ── Stats tracker ─────────────────────────────────────────────────────────

    /// <summary>
    /// Mutable state accumulated from MakeMKV robot-mode output lines.
    /// Call <see cref="ToStats"/> to get an immutable snapshot.
    /// </summary>
    private sealed class MakeMkvStatsTracker
    {
        private readonly string _outputPath;

        public string Source              { get; set; } = string.Empty;
        public string SourceFile          { get; set; } = string.Empty;
        public string SourceSize          { get; set; } = string.Empty;
        public string ReadRate            { get; set; } = string.Empty;
        public string OutputFile          { get; set; } = string.Empty;
        public string OutputSize          { get; set; } = string.Empty;
        public string FreeSpace           { get; set; } = string.Empty;

        public string TitleProgressLabel  { get; set; } = "Saving to MKV file";
        public string OverallProgressLabel { get; set; } = "Saving all titles to MKV files";

        public long   TitleProgressRaw    { get; set; }
        public long   OverallProgressRaw  { get; set; }
        public long   ProgressMax         { get; set; } = 65536;

        public readonly List<string> Messages = new();

        private readonly DateTime _overallStart = DateTime.UtcNow;
        private DateTime _titleStart            = DateTime.UtcNow;

        public MakeMkvStatsTracker(string outputPath)
        {
            _outputPath = outputPath;
        }

        public void ResetTitleTimer() => _titleStart = DateTime.UtcNow;

        public void RefreshOutputSizeAndFreeSpace(string filePath)
        {
            try
            {
                // Output file size
                if (File.Exists(filePath))
                    OutputSize = FormatBytesStatic(new FileInfo(filePath).Length);

                // Free space on the output drive
                var root = Path.GetPathRoot(filePath);
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                {
                    var di = new DriveInfo(root);
                    FreeSpace = FormatBytesStatic(di.AvailableFreeSpace);
                }
            }
            catch { /* non-fatal */ }
        }

        public MakeMkvRipStats ToStats()
        {
            var now            = DateTime.UtcNow;
            var overallElapsed = now - _overallStart;
            var titleElapsed   = now - _titleStart;

            double overallPct = ProgressMax > 0 ? (double)OverallProgressRaw / ProgressMax : 0;
            double titlePct   = ProgressMax > 0 ? (double)TitleProgressRaw   / ProgressMax : 0;

            TimeSpan overallRemaining = overallPct > 0.005
                ? TimeSpan.FromSeconds(overallElapsed.TotalSeconds / overallPct * (1 - overallPct))
                : TimeSpan.Zero;

            TimeSpan titleRemaining = titlePct > 0.005
                ? TimeSpan.FromSeconds(titleElapsed.TotalSeconds   / titlePct   * (1 - titlePct))
                : TimeSpan.Zero;

            return new MakeMkvRipStats
            {
                Source               = Source,
                SourceFile           = SourceFile,
                SourceSize           = SourceSize,
                ReadRate             = ReadRate,
                OutputFile           = OutputFile,
                OutputSize           = OutputSize,
                FreeSpace            = FreeSpace,
                TitleProgressLabel   = TitleProgressLabel,
                TitleProgressValue   = titlePct   * 100.0,
                TitleElapsed         = titleElapsed,
                TitleRemaining       = titleRemaining,
                OverallProgressLabel = OverallProgressLabel,
                OverallProgressValue = overallPct * 100.0,
                OverallElapsed       = overallElapsed,
                OverallRemaining     = overallRemaining,
                Messages             = Messages.ToList()
            };
        }

        private static string FormatBytesStatic(long bytes)
        {
            if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} G";
            if (bytes >= 1_048_576L)     return $"{bytes / 1_048_576.0:F1} M";
            return $"{bytes / 1_024.0:F1} K";
        }
    }

    // ── Internal stream record ─────────────────────────────────────────────────

    private class StreamRecord
    {
        public int    Index      { get; set; }
        public int    Type       { get; set; }
        public string CodecId    { get; set; } = string.Empty;
        public string CodecShort { get; set; } = string.Empty;
        public string CodecLong  { get; set; } = string.Empty;
        public string LangCode   { get; set; } = string.Empty;
        public string LangName   { get; set; } = string.Empty;
        public string Fps        { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string Aspect     { get; set; } = string.Empty;
        public string Channels   { get; set; } = string.Empty;
        public string SampleRate { get; set; } = string.Empty;
        public string BitDepth   { get; set; } = string.Empty;
        public string Bitrate    { get; set; } = string.Empty;
        public bool   IsDefault  { get; set; }
        public bool   IsForced   { get; set; }
    }
}

