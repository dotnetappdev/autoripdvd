using AutoRipDVD.Models;

namespace AutoRipDVD.Services;

/// <summary>
/// Orchestrates a deep disc analysis pass before ripping begins.
///
/// Combines:
///   1. IFO parser  – DVD structure, titles, chapters, audio/subtitle tracks
///   2. Copy protection detection (CSS, AACS, BD+, ARccOS, region)
///   3. MakeMKV scan output parsing for supplementary stream details
///
/// This is the equivalent of what DVDFab "Disc Info" and MakeMKV's title scanner
/// surface in their UIs before the actual rip begins.
/// </summary>
public interface IDiscAnalyzerService
{
    Task<DiscAnalysisResult> AnalyseDiscAsync(DiscInfo disc, string? makeMkvScanOutput = null,
        IProgress<string>? progress = null);
}

/// <summary>Rich disc analysis result combining all available metadata.</summary>
public class DiscAnalysisResult
{
    public DiscInfo Disc                    { get; set; } = new();
    public CopyProtectionInfo Protection    { get; set; } = new();
    public DvdDiscStructure?  DvdStructure  { get; set; }
    public List<TitleInfo>    Titles        { get; set; } = new();
    public int                TitleCount    => Titles.Count;
    public TimeSpan           LongestTitle  => Titles.MaxBy(t => t.Duration)?.Duration ?? TimeSpan.Zero;
    public string             Summary       { get; set; } = string.Empty;
    public List<string>       Warnings      { get; set; } = new();
    public DateTime           AnalysedAt    { get; set; } = DateTime.Now;
}

public class DiscAnalyzerService : IDiscAnalyzerService
{
    private readonly IIfoParserService       _ifoParser;
    private readonly ICopyProtectionService  _protection;
    private readonly ILogService             _log;

    public DiscAnalyzerService(
        IIfoParserService       ifoParser,
        ICopyProtectionService  protection,
        ILogService             log)
    {
        _ifoParser  = ifoParser;
        _protection = protection;
        _log        = log;
    }

    public async Task<DiscAnalysisResult> AnalyseDiscAsync(
        DiscInfo disc,
        string?  makeMkvScanOutput = null,
        IProgress<string>? progress = null)
    {
        var result = new DiscAnalysisResult { Disc = disc };

        await _log.LogAsync($"Analysing disc: {disc.VolumeLabel} on {disc.DriveLetter} ({disc.DiscType})");

        // ── Step 1: Copy protection ───────────────────────────────────────────
        progress?.Report("Detecting copy protection…");
        if (!string.IsNullOrEmpty(makeMkvScanOutput))
        {
            result.Protection = await _protection.AnalyseMkvOutputAsync(makeMkvScanOutput, disc);
        }
        else
        {
            result.Protection = await _protection.AnalyseDiscAsync(disc, progress);
        }

        // ── Step 2: DVD IFO structure parse ───────────────────────────────────
        if (disc.DiscType == DiscType.DVD)
        {
            progress?.Report("Parsing DVD structure (IFO files)…");
            var videoTs = Path.Combine(disc.DriveLetter.TrimEnd('\\') + "\\", "VIDEO_TS");

            if (_ifoParser.IsValidDvd(videoTs))
            {
                result.DvdStructure = await _ifoParser.ParseDiscAsync(videoTs);

                if (result.DvdStructure != null)
                {
                    // Enrich TitleInfo list with IFO-sourced data
                    foreach (var dvdTitle in result.DvdStructure.AllTitles)
                    {
                        var title = new TitleInfo
                        {
                            Index        = dvdTitle.TitleNumber - 1,
                            Name         = $"Title {dvdTitle.TitleNumber}",
                            Duration     = dvdTitle.Duration,
                            ChapterCount = dvdTitle.ChapterCount,
                            Resolution   = $"{dvdTitle.Width}x{dvdTitle.Height}",
                            VideoCodec   = "MPEG-2",
                            AngleInfo    = dvdTitle.AngleCount > 1 ? $"{dvdTitle.AngleCount} angles" : string.Empty,
                            AudioTracks  = dvdTitle.AudioTracks
                                .Select(a => $"{a.CodingMode} {a.FormattedChannels} ({a.Language})")
                                .ToList(),
                            SubtitleTracks = dvdTitle.Subpictures
                                .Select(s => s.Language + (s.IsForced ? " [Forced]" : string.Empty))
                                .ToList()
                        };
                        result.Titles.Add(title);
                    }
                }
            }
            else
            {
                result.Warnings.Add("VIDEO_TS directory not found or invalid IFO structure");
            }
        }
        else if (disc.DiscType == DiscType.BluRay)
        {
            progress?.Report("Scanning Blu-ray structure…");
            EnrichBluRayInfo(disc, result);
        }

        // ── Step 3: Mark main feature ─────────────────────────────────────────
        if (result.Titles.Any())
        {
            var main = result.Titles.OrderByDescending(t => t.Duration).First();
            main.IsMainFeature = true;
        }

        // ── Step 4: Build human-readable summary ──────────────────────────────
        result.Summary = BuildSummary(result);
        progress?.Report("Analysis complete");

        await _log.LogAsync($"Analysis complete: {result.Summary}");
        return result;
    }

    // ── Blu-ray structural info ───────────────────────────────────────────────

    private static void EnrichBluRayInfo(DiscInfo disc, DiscAnalysisResult result)
    {
        // Blu-ray uses BDMV/BDJO/*.bdjo and BDMV/MOVIEOBJ.bdmv for title structure.
        // Full BD-J parsing is out of scope here; we report what we can from the directory.
        var bdmv = Path.Combine(disc.DriveLetter.TrimEnd('\\') + "\\", "BDMV");

        if (!Directory.Exists(bdmv))
        {
            result.Warnings.Add("BDMV directory not found");
            return;
        }

        // STREAM/*.m2ts files are the actual video clips
        var streamDir = Path.Combine(bdmv, "STREAM");
        if (Directory.Exists(streamDir))
        {
            var m2ts = Directory.GetFiles(streamDir, "*.m2ts")
                                .OrderByDescending(f => new FileInfo(f).Length)
                                .ToList();

            for (int i = 0; i < m2ts.Count; i++)
            {
                var fi = new FileInfo(m2ts[i]);
                result.Titles.Add(new TitleInfo
                {
                    Index      = i,
                    Name       = Path.GetFileNameWithoutExtension(fi.Name),
                    SizeBytes  = fi.Length,
                    VideoCodec = "MPEG-4 AVC / HEVC",
                    FileName   = fi.Name
                });
            }
        }

        result.Warnings.Add("Full Blu-ray chapter/title enumeration requires MakeMKV scan");
    }

    private static string BuildSummary(DiscAnalysisResult r)
    {
        var parts = new List<string>();

        parts.Add($"{r.TitleCount} title(s)");

        if (r.LongestTitle > TimeSpan.Zero)
            parts.Add($"main feature {r.LongestTitle:hh\\:mm\\:ss}");

        parts.Add(r.Protection.IsProtected
            ? $"Protected: {r.Protection.ProtectionSummary}"
            : "No protection");

        parts.Add($"Region: {r.Protection.RegionSummary}");

        return string.Join(" | ", parts);
    }
}

// ── Extension helpers ─────────────────────────────────────────────────────────

internal static class DvdAudioInfoExtensions
{
    public static string FormattedChannels(this DvdAudioInfo a)
        => a.Channels switch { 1 => "1.0", 2 => "2.0", 6 => "5.1", 8 => "7.1", _ => $"{a.Channels}ch" };
}
