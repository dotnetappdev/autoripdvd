using AutoRipDVD.Models;
using System.Diagnostics;

namespace AutoRipDVD.Services;

/// <summary>
/// Detects copy protection on optical discs.
///
/// DVD protection landscape (like AnyDVD/DVDFab handles):
///   CSS         – Content Scramble System: encrypted VOB titles. libdvdcss bypasses this.
///   ARccOS      – Sony extended protection: intentionally bad sectors that trip up most rippers.
///   RCE         – Region Code Enhancement: refuse playback outside intended region.
///   APS/Macrovision – Analogue protection (irrelevant for digital ripping).
///   UOPs        – User Operation Prohibitions (skip-protection, menu lock).
///
/// Blu-ray protection landscape:
///   AACS        – Advanced Access Content System: title/unit key encryption + revocation.
///   BD+         – Java/BDJ virtual machine that validates player cert; generates a valid key table.
///   BDROM Mark  – Physical ROM mark verified by drive hardware (BD-ROM only).
///   Cinavia     – Audio watermark in film audio track.
///
/// We detect these without bypassing them – detection only, no circumvention.
/// </summary>
public interface ICopyProtectionService
{
    Task<CopyProtectionInfo> AnalyseDiscAsync(DiscInfo disc, IProgress<string>? progress = null);
    Task<CopyProtectionInfo> AnalyseMkvOutputAsync(string makeMkvScanOutput, DiscInfo disc);
}

public class CopyProtectionService : ICopyProtectionService
{
    private readonly ILogService _log;
    private readonly IIfoParserService _ifoParser;

    // ── ARccOS sector signatures ──────────────────────────────────────────────
    // ARccOS plants intentionally corrupted navigation packs at predictable
    // locations. We look for known "dummy VOB" patterns that lack valid
    // PACK_START_CODE headers in otherwise occupied sectors.
    private static readonly byte[] PackStartCode = { 0x00, 0x00, 0x01, 0xBA };

    public CopyProtectionService(ILogService log, IIfoParserService ifoParser)
    {
        _log       = log;
        _ifoParser = ifoParser;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<CopyProtectionInfo> AnalyseDiscAsync(DiscInfo disc, IProgress<string>? progress = null)
    {
        var info = new CopyProtectionInfo();

        try
        {
            await _log.LogAsync($"Analysing copy protection on {disc.DriveLetter} ({disc.DiscType})");
            progress?.Report("Analysing copy protection…");

            if (disc.DiscType == DiscType.DVD)
                await AnalyseDvdAsync(disc, info, progress);
            else if (disc.DiscType == DiscType.BluRay)
                await AnalyseBluRayAsync(disc, info, progress);
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"CopyProtection analysis error: {ex.Message}");
            info.Details.Add($"Analysis error: {ex.Message}");
        }

        await _log.LogAsync($"Protection detected: {info.ProtectionSummary} | Regions: {info.RegionSummary}");
        return info;
    }

    /// <summary>
    /// Parse protection hints directly from MakeMKV's -r info output, which
    /// already probes the disc and logs protection-related messages.
    /// </summary>
    public Task<CopyProtectionInfo> AnalyseMkvOutputAsync(string makeMkvOutput, DiscInfo disc)
    {
        var info = new CopyProtectionInfo();

        var lower = makeMkvOutput.ToLowerInvariant();

        // MakeMKV emits MSG lines:  MSG:code,flags,count,"english text","format string",param...
        // We scan the English text for known protection keywords.

        if (lower.Contains("css") || lower.Contains("encrypted"))
        {
            info.Flags |= CopyProtectionFlags.CSS;
            info.Details.Add("CSS encryption detected (MakeMKV)");
        }

        if (lower.Contains("arccOS") || lower.Contains("bad sector") || lower.Contains("damaged"))
        {
            info.Flags |= CopyProtectionFlags.ARccOS;
            info.Details.Add("ARccOS bad-sector protection (MakeMKV)");
        }

        if (lower.Contains("region") && (lower.Contains("code") || lower.Contains("mismatch")))
        {
            info.Flags |= CopyProtectionFlags.RCE;
            info.Details.Add("Region code mismatch (MakeMKV)");
        }

        if (lower.Contains("aacs"))
        {
            info.Flags |= CopyProtectionFlags.AACS;
            info.Details.Add("AACS encryption detected (MakeMKV)");
        }

        if (lower.Contains("bd+") || lower.Contains("bdplus"))
        {
            info.Flags |= CopyProtectionFlags.BDPlus;
            info.Details.Add("BD+ protection detected (MakeMKV)");
        }

        if (lower.Contains("cinavia"))
        {
            info.Flags |= CopyProtectionFlags.Cinavia;
            info.Details.Add("Cinavia watermark detected (MakeMKV)");
        }

        // Parse region info from MakeMKV TINFO:0,28,… or MSG lines
        ParseRegionFromMakeMkv(makeMkvOutput, info);

        return Task.FromResult(info);
    }

    // ── DVD analysis ──────────────────────────────────────────────────────────

    private async Task AnalyseDvdAsync(DiscInfo disc, CopyProtectionInfo info, IProgress<string>? progress)
    {
        var videoTs = Path.Combine(disc.DriveLetter + "\\", "VIDEO_TS");

        // 1. Parse IFO for region codes and UOPs
        progress?.Report("Parsing IFO files…");
        if (_ifoParser.IsValidDvd(videoTs))
        {
            var structure = await _ifoParser.ParseDiscAsync(videoTs);
            if (structure != null)
            {
                info.RegionCodes = structure.RegionCodes;

                if (structure.Protection.HasFlag(CopyProtectionFlags.UOPs))
                {
                    info.Flags |= CopyProtectionFlags.UOPs;
                    info.Details.Add("User Operation Prohibitions set in IFO (skip/menu locks)");
                }
                if (structure.Protection.HasFlag(CopyProtectionFlags.RCE))
                {
                    info.Flags |= CopyProtectionFlags.RCE;
                    info.Details.Add($"Region-locked disc: {info.RegionSummary}");
                }
            }
        }

        // 2. CSS detection: try to read a small portion of the first encrypted VOB.
        //    libdvdcss (used by MakeMKV/VLC) is the bypass – we only detect.
        progress?.Report("Checking for CSS encryption…");
        await DetectCssAsync(disc, info, videoTs);

        // 3. ARccOS detection: scan for the signature corrupted-navigation-pack sectors
        progress?.Report("Checking for ARccOS bad sectors…");
        await DetectArccOsAsync(disc, info, videoTs);

        // 4. Macrovision/APS – check IFO permission bits
        await DetectApsAsync(info, videoTs);
    }

    private async Task DetectCssAsync(DiscInfo disc, CopyProtectionInfo info, string videoTs)
    {
        try
        {
            // The simplest CSS heuristic: attempt direct binary read of the first VTS VOB.
            // If reading succeeds without CRC/read errors the blocks are still encrypted (CSS
            // encrypts them but the sectors themselves are readable; only the MPEG stream is
            // garbled). We look for the *absence* of valid MPEG-2 pack start codes in the first
            // few sectors, which indicates the stream is CSS-encrypted.

            var vobs = Directory.GetFiles(videoTs, "VTS_*_1.VOB");
            if (vobs.Length == 0)
            {
                // No VOBs – possibly empty/data disc
                return;
            }

            // Read the first 4 sectors (4 × 2048 bytes) of the first VOB
            var firstVob = vobs.OrderBy(f => f).First();
            const int sectorSize = 2048;
            const int sectorsToRead = 4;
            var buf = new byte[sectorSize * sectorsToRead];

            using (var fs = new FileStream(firstVob, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int read = await fs.ReadAsync(buf.AsMemory(0, buf.Length));
                if (read < sectorSize) return;
            }

            // Check for MPEG-2 pack start codes in the buffer
            bool hasValidPacks = false;
            for (int i = 0; i <= buf.Length - 4; i++)
            {
                if (buf[i] == 0x00 && buf[i + 1] == 0x00 && buf[i + 2] == 0x01 && buf[i + 3] == 0xBA)
                {
                    hasValidPacks = true;
                    break;
                }
            }

            if (!hasValidPacks)
            {
                info.Flags |= CopyProtectionFlags.CSS;
                info.Details.Add("CSS encryption detected (no valid MPEG-2 pack headers in VOB data)");
            }
            else
            {
                info.Details.Add("No CSS encryption detected (clear VOB)");
            }
        }
        catch (UnauthorizedAccessException)
        {
            // On a CSS disc the OS may deny raw sector reads
            info.Flags |= CopyProtectionFlags.CSS;
            info.Details.Add("CSS encryption likely (access denied on VOB read)");
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"CSS detection error: {ex.Message}");
        }
    }

    private async Task DetectArccOsAsync(DiscInfo disc, CopyProtectionInfo info, string videoTs)
    {
        try
        {
            // ARccOS plants VIDEO_TS/VTS_XX_0.IFO files with intentionally corrupt cell entries
            // that reference sectors outside the VTS VOB.
            // Simple heuristic: look for IFO files that contain sectors pointing well beyond
            // the end of the corresponding VOB file.

            var ifoFiles = Directory.GetFiles(videoTs, "VTS_*_0.IFO");
            foreach (var ifo in ifoFiles)
            {
                var ifoData = await File.ReadAllBytesAsync(ifo);
                if (ifoData.Length < 0x0100) continue;

                // Last sector of VTS at offset 0x001C (4 bytes BE)
                uint lastVobSector = ReadBE32(ifoData, 0x001C);

                // Menu VOB End Sector at offset 0x001C
                // VTS VOB End Sector at offset 0x0020
                uint vtsBupLastSector = ifoData.Length >= 0x0024 ? ReadBE32(ifoData, 0x0020) : 0;

                // ARccOS signature: the VTS reports sectors beyond what physically exists
                // We approximate by checking if the IFO declares more sectors than can be
                // addressed in a single-layer DVD (approx 2.2 GB = ~1.1 million sectors)
                if (lastVobSector > 0 && vtsBupLastSector > lastVobSector * 2)
                {
                    info.Flags |= CopyProtectionFlags.ARccOS;
                    info.Details.Add($"ARccOS detected in {Path.GetFileName(ifo)} (invalid sector references)");
                    break;
                }
            }

            // Secondary heuristic: look for "dummy" VOBs with zero-length content
            var smallVobs = Directory.GetFiles(videoTs, "VTS_*_0.VOB")
                .Where(f => new FileInfo(f).Length is > 0 and < 2048);
            if (smallVobs.Any())
            {
                info.Flags |= CopyProtectionFlags.ARccOS;
                info.Details.Add("ARccOS detected (abnormally small menu VOB – dummy title set)");
            }
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"ARccOS detection error: {ex.Message}");
        }
    }

    private static async Task DetectApsAsync(CopyProtectionInfo info, string videoTs)
    {
        // Macrovision / Analogue Protection System is signalled by bits in the VMG header
        var vmgIfo = Path.Combine(videoTs, "VIDEO_TS.IFO");
        if (!File.Exists(vmgIfo)) return;

        try
        {
            var data = await File.ReadAllBytesAsync(vmgIfo);
            // APS flag is in the "Disc Category and Copy Prevention" byte at offset 0x0014
            if (data.Length > 0x0014 && (data[0x0014] & 0x03) != 0)
            {
                info.Flags |= CopyProtectionFlags.APS;
                info.Details.Add("Macrovision/APS analogue protection flag set in IFO");
            }
        }
        catch { /* ignore */ }
    }

    // ── Blu-ray analysis ──────────────────────────────────────────────────────

    private async Task AnalyseBluRayAsync(DiscInfo disc, CopyProtectionInfo info, IProgress<string>? progress)
    {
        var bdmvRoot = Path.Combine(disc.DriveLetter + "\\", "BDMV");

        // AACS: Certificate directory
        progress?.Report("Checking for AACS…");
        var aacsDir = Path.Combine(bdmvRoot, "BACKUP", "CERTIFICATE");
        if (!Directory.Exists(aacsDir))
            aacsDir = Path.Combine(disc.DriveLetter + "\\", "CERTIFICATE");

        if (Directory.Exists(aacsDir) || File.Exists(Path.Combine(disc.DriveLetter + "\\", "AACS", "Unit_Key_RO.inf")))
        {
            info.Flags |= CopyProtectionFlags.AACS;
            info.Details.Add("AACS encryption detected (Certificate directory / Unit_Key_RO.inf present)");
        }

        // BD+: BDSVM directory
        progress?.Report("Checking for BD+…");
        var bdsvmDir = Path.Combine(bdmvRoot, "BDSVM");
        if (Directory.Exists(bdsvmDir))
        {
            info.Flags |= CopyProtectionFlags.BDPlus;
            info.Details.Add("BD+ detected (BDSVM virtual machine directory found)");
        }

        // BD-ROM mark detection via presence of .inf security file
        var bdRomMark = Path.Combine(disc.DriveLetter + "\\", "BD_CERT", "id.bdmv");
        if (File.Exists(bdRomMark))
        {
            info.Details.Add("BD-ROM Mark security certificate present");
        }

        // Cinavia – can only be detected in audio data; we flag it based on known title DB
        // We skip audio analysis here and leave it to MakeMKV reporting

        // Region codes from BDMV/BACKUP/id.bdmv or index.bdmv
        progress?.Report("Reading Blu-ray region info…");
        await DetectBdRegionsAsync(disc, info, bdmvRoot);

        await _log.LogAsync($"Blu-ray protection: {info.ProtectionSummary}");
    }

    private static async Task DetectBdRegionsAsync(DiscInfo disc, CopyProtectionInfo info, string bdmvRoot)
    {
        // Blu-ray region info is stored in BDMV/BDJO/*.bdjo (Java objects) or index.bdmv
        // index.bdmv has a simple region bit field at offset 0x0C (1 byte, same format as DVD)
        var indexBdmv = Path.Combine(bdmvRoot, "index.bdmv");
        if (!File.Exists(indexBdmv)) return;

        try
        {
            var data = await File.ReadAllBytesAsync(indexBdmv);
            if (data.Length <= 0x0D) return;

            byte regionByte = data[0x0C];
            // Same inverted bitmask as DVD IFO region byte
            var regions = DiscRegions.None;
            for (int i = 0; i < 8; i++)
            {
                if ((regionByte & (1 << i)) == 0)
                    regions |= (DiscRegions)(1 << i);
            }

            info.RegionCodes = regions == DiscRegions.None ? DiscRegions.RegionFree : regions;

            if (regions != DiscRegions.None && regions != DiscRegions.RegionFree)
                info.Details.Add($"Region-locked Blu-ray: {info.RegionSummary}");
        }
        catch { /* ignore */ }
    }

    // ── MakeMKV output parser ─────────────────────────────────────────────────

    private static void ParseRegionFromMakeMkv(string output, CopyProtectionInfo info)
    {
        // MakeMKV outputs TINFO:0,29,0,"..." where code 29 can contain "Regions: A,B,C"
        // or MSG lines about region mismatches.
        foreach (var line in output.Split('\n'))
        {
            // TINFO:0,29,0,"Region A"  or similar
            if (line.StartsWith("TINFO:") && line.Contains(",29,"))
            {
                var val = ExtractQuotedValue(line);
                if (!string.IsNullOrEmpty(val))
                {
                    info.Details.Add($"Disc region info: {val}");
                    // Parse A/B/C for BD or numbers for DVD
                    if (val.Contains('A') || val.Contains("ABC")) info.RegionCodes = DiscRegions.Region1 | DiscRegions.Region4;
                    else if (val.Contains('B')) info.RegionCodes = DiscRegions.Region2;
                    else if (val.Contains('C')) info.RegionCodes = DiscRegions.Region6;
                }
            }
        }
    }

    private static string ExtractQuotedValue(string line)
    {
        var start = line.LastIndexOf('"') > 0 ? line.IndexOf('"') : -1;
        if (start < 0) return string.Empty;
        var end = line.LastIndexOf('"');
        if (end <= start) return string.Empty;
        return line[(start + 1)..end];
    }

    private static uint ReadBE32(byte[] data, int offset)
        => ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
         | ((uint)data[offset + 2] << 8)  |  data[offset + 3];
}
