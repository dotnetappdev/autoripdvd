using AutoRipDVD.Models;

namespace AutoRipDVD.Services;

/// <summary>
/// Parses DVD IFO (Information) files directly from a disc or ripped VIDEO_TS folder.
/// This mirrors what MakeMKV does under the hood: reads the binary IFO structures
/// to enumerate titles, chapters, audio tracks, subtitles, and protection flags
/// without needing any external tool.
///
/// DVD-Video IFO structure reference:
///   VIDEO_TS.IFO  – Video Manager (VMG): disc-level title table, region codes, VMG attributes
///   VTS_XX_0.IFO  – Video Title Set (VTS): per-title chapters, A/V attributes, cell timestamps
/// </summary>
public interface IIfoParserService
{
    /// <summary>Parse the complete disc structure from VIDEO_TS on the given drive or folder.</summary>
    Task<DvdDiscStructure?> ParseDiscAsync(string videoTsPath);

    /// <summary>Parse a single VTS IFO file and return its title info.</summary>
    Task<DvdTitleSetInfo?> ParseVtsIfoAsync(string vtsIfoPath, int vtsNumber);

    /// <summary>Check whether a VIDEO_TS directory looks like a valid DVD.</summary>
    bool IsValidDvd(string videoTsPath);
}

public class IfoParserService : IIfoParserService
{
    private readonly ILogService _log;

    // ── IFO magic signatures ─────────────────────────────────────────────────
    private const string VmgMagic = "DVDVIDEO-VMG";
    private const string VtsMagic = "DVDVIDEO-VTS";

    // ── VMG offsets (all big-endian in the spec) ──────────────────────────────
    private const int VmgOffsetVersionNumber      = 0x000C; // 2 bytes
    private const int VmgOffsetRegionInfo         = 0x0023; // 1 byte  (inverted bitmask)
    private const int VmgOffsetNumberOfTitles      = 0x003E; // 2 bytes
    private const int VmgOffsetNumVts             = 0x003F; // 1 byte  (actually 2 at 0x3E but only low byte)
    private const int VmgOffsetTitleTablePointer   = 0x00C4; // 4 bytes → sector number × 2048
    private const int VmgTitleEntrySize            = 12;     // bytes per title table entry

    // ── VTS attribute offsets (VTS_XX_0.IFO) ─────────────────────────────────
    private const int VtsOffsetVtsmVbsi          = 0x0100; // VTS Management Table, Video Attrs
    private const int VtsOffsetNumberOfAudioSteams = 0x0204; // 2 bytes
    private const int VtsOffsetAudioAttribBase   = 0x0206; // 8 bytes × stream count
    private const int VtsOffsetNumberOfSubpicture = 0x0254; // 2 bytes
    private const int VtsOffsetSubpicAttribBase  = 0x0256; // 6 bytes × stream count
    private const int VtsOffsetVtsiMat           = 0x0000; // VTS Info Management Area Table starts here

    public IfoParserService(ILogService log) => _log = log;

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsValidDvd(string videoTsPath)
    {
        if (!Directory.Exists(videoTsPath)) return false;
        var vmgIfo = Path.Combine(videoTsPath, "VIDEO_TS.IFO");
        if (!File.Exists(vmgIfo)) return false;
        try
        {
            using var fs = File.OpenRead(vmgIfo);
            var sig = new byte[12];
            return fs.Read(sig, 0, 12) == 12 && System.Text.Encoding.ASCII.GetString(sig) == VmgMagic;
        }
        catch { return false; }
    }

    public async Task<DvdDiscStructure?> ParseDiscAsync(string videoTsPath)
    {
        try
        {
            if (!IsValidDvd(videoTsPath))
            {
                await _log.LogAsync($"IFO Parser: Not a valid DVD structure at {videoTsPath}");
                return null;
            }

            var vmgPath = Path.Combine(videoTsPath, "VIDEO_TS.IFO");
            var vmgData = await File.ReadAllBytesAsync(vmgPath);

            var structure = new DvdDiscStructure();

            // Volume name from the directory or its parent
            structure.VolumeName = Path.GetFileName(Path.GetDirectoryName(videoTsPath) ?? videoTsPath);

            // Region codes (byte at 0x0023 is an inverted bitmask where bit N → Region N+1 allowed)
            if (vmgData.Length > VmgOffsetRegionInfo)
            {
                byte regionByte = vmgData[VmgOffsetRegionInfo];
                structure.RegionCodes = ParseRegionByte(regionByte);
            }

            // Total number of VTS
            int numVts = vmgData.Length > 0x3E ? vmgData[0x3E] : 0;

            // Title table
            if (vmgData.Length > VmgOffsetTitleTablePointer + 3)
            {
                uint titleTableSector = ReadBE32(vmgData, VmgOffsetTitleTablePointer);
                int titleTableOffset  = (int)(titleTableSector * 2048);

                if (titleTableOffset + 8 < vmgData.Length)
                {
                    int numTitles = ReadBE16(vmgData, titleTableOffset + 8);
                    structure.TotalTitles = numTitles;
                    await _log.LogAsync($"IFO Parser: Found {numTitles} titles across {numVts} VTS");

                    var vmgTitles = ParseVmgTitleTable(vmgData, titleTableOffset + 12, numTitles);
                    structure.AllTitles.AddRange(vmgTitles);
                }
            }

            // Parse each VTS IFO
            for (int v = 1; v <= numVts; v++)
            {
                var vtsPath = Path.Combine(videoTsPath, $"VTS_{v:D2}_0.IFO");
                if (!File.Exists(vtsPath)) continue;

                var vtsInfo = await ParseVtsIfoAsync(vtsPath, v);
                if (vtsInfo != null)
                {
                    structure.TitleSets.Add(vtsInfo);

                    // Merge VTS-level track info into AllTitles
                    foreach (var title in structure.AllTitles.Where(t => t.VtsNumber == v))
                    {
                        var vtsTitle = vtsInfo.Titles.FirstOrDefault(vt => vt.TitleNumber == title.TitleNumber);
                        if (vtsTitle != null)
                        {
                            title.AudioTracks  = vtsTitle.AudioTracks;
                            title.Subpictures  = vtsTitle.Subpictures;
                            title.ChapterTimes = vtsTitle.ChapterTimes;
                            title.Duration     = vtsTitle.Duration;
                            title.FrameRate    = vtsTitle.FrameRate;
                            title.Width        = vtsTitle.Width;
                            title.Height       = vtsTitle.Height;
                            title.AspectRatio  = vtsTitle.AspectRatio;
                        }
                    }
                }
            }

            // Basic protection heuristics on the IFO data
            structure.Protection = DetectProtectionFromIfo(vmgData);

            await _log.LogAsync($"IFO Parser: Disc '{structure.VolumeName}' – {structure.TotalTitles} titles, region {structure.RegionCodes}");
            return structure;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"IFO Parser error: {ex.Message}");
            return null;
        }
    }

    public async Task<DvdTitleSetInfo?> ParseVtsIfoAsync(string vtsIfoPath, int vtsNumber)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(vtsIfoPath);
            if (data.Length < 0x100) return null;

            // Validate VTS magic
            var magic = System.Text.Encoding.ASCII.GetString(data, 0, 12);
            if (magic != VtsMagic) return null;

            var info = new DvdTitleSetInfo { VtsNumber = vtsNumber };

            // ── Video attributes (VTS_VOBS video stream) ──────────────────────
            // Offset 0x0200: 2 bytes video attrs
            int vWidth = 720, vHeight = 480;
            double fps = 29.97;
            string aspectRatio = "4:3";

            if (data.Length > 0x0201)
            {
                byte va0 = data[0x0200];
                byte va1 = data[0x0201];

                // Bits 12-13 of video attribute: 00=4:3, 11=16:9
                bool isWide = ((va0 >> 2) & 0x03) == 3;
                aspectRatio = isWide ? "16:9" : "4:3";

                // Bits 0-3: video standard. 0=NTSC, 1=PAL
                bool isPal = (va0 & 0x03) == 1;
                fps    = isPal ? 25.0 : 29.97;
                vHeight = isPal ? 576 : 480;

                // Resolution bits 3-4 of va1
                int resBits = (va1 >> 3) & 0x03;
                vWidth = resBits switch { 0 => 720, 1 => 704, 2 => 352, _ => 352 };
            }

            // ── Audio attributes ──────────────────────────────────────────────
            var audioTracks = new List<DvdAudioInfo>();
            if (data.Length > VtsOffsetNumberOfAudioSteams + 1)
            {
                int numAudio = ReadBE16(data, VtsOffsetNumberOfAudioSteams);
                numAudio = Math.Min(numAudio, 8); // DVD spec: max 8 audio streams

                for (int i = 0; i < numAudio; i++)
                {
                    int offset = VtsOffsetAudioAttribBase + i * 8;
                    if (offset + 7 >= data.Length) break;

                    var audio = ParseAudioAttribute(data, offset, i + 1);
                    audioTracks.Add(audio);
                }
            }

            // ── Subpicture (subtitle) attributes ─────────────────────────────
            var subTracks = new List<DvdSubpictureInfo>();
            if (data.Length > VtsOffsetNumberOfSubpicture + 1)
            {
                int numSub = ReadBE16(data, VtsOffsetNumberOfSubpicture);
                numSub = Math.Min(numSub, 32); // DVD spec: max 32 sub streams

                for (int i = 0; i < numSub; i++)
                {
                    int offset = VtsOffsetSubpicAttribBase + i * 6;
                    if (offset + 5 >= data.Length) break;

                    var sub = ParseSubpictureAttribute(data, offset, i + 1);
                    subTracks.Add(sub);
                }
            }

            // ── VTS_PGCI – program chain info (chapters / cells) ──────────────
            // This gives us actual title durations and chapter timestamps
            var titles = new List<DvdTitleInfo>();
            if (data.Length > 0x00CC + 3)
            {
                uint pgciSector  = ReadBE32(data, 0x00CC);
                int  pgciOffset  = (int)(pgciSector * 2048);
                var  titleChapters = ParsePgciTable(data, pgciOffset, vtsNumber, vWidth, vHeight, fps, aspectRatio);
                titles.AddRange(titleChapters);
            }

            // If PGCI parse failed, create a minimal entry per the VTS
            if (titles.Count == 0)
            {
                titles.Add(new DvdTitleInfo
                {
                    TitleNumber = 1,
                    VtsNumber   = vtsNumber,
                    Width       = vWidth,
                    Height      = vHeight,
                    FrameRate   = fps,
                    AspectRatio = aspectRatio,
                    AudioTracks = audioTracks,
                    Subpictures = subTracks
                });
            }
            else
            {
                // Attach track lists to all titles in this VTS
                foreach (var t in titles)
                {
                    if (t.AudioTracks.Count == 0) t.AudioTracks = audioTracks;
                    if (t.Subpictures.Count  == 0) t.Subpictures  = subTracks;
                }
            }

            info.Titles     = titles;
            info.TitleCount = titles.Count;
            return info;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"IFO Parser VTS {vtsNumber} error: {ex.Message}");
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static List<DvdTitleInfo> ParseVmgTitleTable(byte[] data, int offset, int count)
    {
        var list = new List<DvdTitleInfo>();
        for (int i = 0; i < count; i++)
        {
            int o = offset + i * VmgTitleEntrySize;
            if (o + VmgTitleEntrySize > data.Length) break;

            // byte  0: title type flags
            // byte  2: number of angles
            // byte  3: number of chapters (PTTs)
            // bytes 4-5: parental management mask
            // byte  6: VTS number
            // byte  7: title number within VTS
            // bytes 8-11: start sector of VTS_C_xx_0.VOB

            byte angles   = data[o + 2];
            byte chapters = data[o + 3];
            int  vtsNum   = data[o + 6];
            int  titleNum = data[o + 7];

            list.Add(new DvdTitleInfo
            {
                TitleNumber  = i + 1,
                VtsNumber    = vtsNum,
                AngleCount   = Math.Max(1, angles),
                ChapterCount = Math.Max(1, chapters)
            });
        }
        return list;
    }

    private static List<DvdTitleInfo> ParsePgciTable(byte[] data, int pgciOffset,
        int vtsNumber, int width, int height, double fps, string aspectRatio)
    {
        var list = new List<DvdTitleInfo>();
        if (pgciOffset + 8 > data.Length) return list;

        try
        {
            int numPgcs = ReadBE16(data, pgciOffset);
            numPgcs = Math.Min(numPgcs, 99);

            for (int i = 0; i < numPgcs; i++)
            {
                // Each PGC search pointer is 8 bytes: 2 title, 2 category, 4 relative offset
                int ptrOffset  = pgciOffset + 8 + i * 8;
                if (ptrOffset + 7 >= data.Length) break;

                uint pgcRelOffset = ReadBE32(data, ptrOffset + 4);
                int  pgcOffset    = pgciOffset + (int)pgcRelOffset;
                if (pgcOffset + 0x9C > data.Length) continue;

                // PGC header
                // offset  0: reserved
                // offset  2: number of programs (cells that begin a chapter)
                // offset  3: number of cells
                // offset  4: playback time BCD  (hh:mm:ss:ff where ff=frames)
                byte programs  = data[pgcOffset + 2];
                byte cells     = data[pgcOffset + 3];

                byte h  = BcdToByte(data[pgcOffset + 4]);
                byte m  = BcdToByte(data[pgcOffset + 5]);
                byte s  = BcdToByte(data[pgcOffset + 6]);
                byte fr = (byte)(data[pgcOffset + 7] & 0x3F); // frame bits

                var duration = new TimeSpan(0, h, m, s, (int)(fr / fps * 1000));

                // Chapter times from Cell Playback Information Table
                // Cell playback table offset is at PGC header+0xE8 (4 bytes relative to pgcOffset)
                var chapterTimes = new List<TimeSpan>();
                if (pgcOffset + 0xEC < data.Length)
                {
                    uint cellPbOffset = ReadBE32(data, pgcOffset + 0xE8);
                    int  cellPbAbs    = pgcOffset + (int)cellPbOffset;

                    for (int c = 0; c < cells && c < 99; c++)
                    {
                        int cellOff = cellPbAbs + c * 24;
                        if (cellOff + 8 > data.Length) break;

                        byte ch = BcdToByte(data[cellOff + 4]);
                        byte cm = BcdToByte(data[cellOff + 5]);
                        byte cs = BcdToByte(data[cellOff + 6]);
                        chapterTimes.Add(new TimeSpan(0, ch, cm, cs));
                    }
                }

                list.Add(new DvdTitleInfo
                {
                    TitleNumber  = i + 1,
                    VtsNumber    = vtsNumber,
                    ChapterCount = programs,
                    Duration     = duration,
                    FrameRate    = fps,
                    Width        = width,
                    Height       = height,
                    AspectRatio  = aspectRatio,
                    ChapterTimes = chapterTimes
                });
            }
        }
        catch { /* fall through, return partial */ }

        return list;
    }

    private static DvdAudioInfo ParseAudioAttribute(byte[] data, int offset, int trackNum)
    {
        // Byte 0-1: coding mode (bits 5-7), multichannel extension (bit 4), application mode (bits 2-3)
        // Byte 2: quantization/DRC
        // Byte 3: sample rate / quantization
        // Byte 4-5: language code (2 ASCII chars if language type set)
        // Byte 6: language code extension
        // Byte 7: code extension
        byte b0 = data[offset];
        byte b1 = data[offset + 1];
        byte b3 = data[offset + 3];

        int codingMode = (b0 >> 5) & 0x07;
        int channels   = (b1 & 0x07) + 1;  // bits 0-2 of byte 1 = channels – 1

        string codec = codingMode switch
        {
            0 => "AC3",
            2 => "MPEG1",
            3 => "MPEG2ext",
            4 => "LPCM",
            6 => "DTS",
            _ => "Unknown"
        };

        int sampleRate = ((b3 >> 4) & 0x01) == 0 ? 48000 : 96000;
        int bitDepth   = codingMode == 4 ? ((b3 >> 6) == 0 ? 16 : 20) : 0;

        // Language code: 2-byte ISO 639 at offset+4 if audio lang type ≠ 0
        string langCode = string.Empty;
        string lang     = string.Empty;
        if (data[offset] >> 2 != 0 && offset + 5 < data.Length)
        {
            langCode = $"{(char)data[offset + 4]}{(char)data[offset + 5]}";
            lang     = Iso639ToEnglish(langCode);
        }

        return new DvdAudioInfo
        {
            TrackNumber  = trackNum,
            CodingMode   = codec,
            Channels     = channels,
            Language     = lang,
            LanguageCode = langCode,
            SampleRate   = sampleRate,
            BitDepth     = bitDepth
        };
    }

    private static DvdSubpictureInfo ParseSubpictureAttribute(byte[] data, int offset, int trackNum)
    {
        // Bytes 0-1: coding mode / type
        // Bytes 2-3: language code
        // Byte  4: language code extension
        // Byte  5: code extension
        string langCode = string.Empty;
        string lang     = string.Empty;

        if (offset + 3 < data.Length)
        {
            langCode = $"{(char)data[offset + 2]}{(char)data[offset + 3]}";
            lang     = Iso639ToEnglish(langCode);
        }

        bool forced = offset < data.Length && (data[offset] & 0x01) != 0;

        return new DvdSubpictureInfo
        {
            TrackNumber  = trackNum,
            Language     = lang,
            LanguageCode = langCode,
            IsForced     = forced
        };
    }

    private static DiscRegions ParseRegionByte(byte regionByte)
    {
        // The byte is an inverted bitmask: bit N clear → Region N+1 is allowed
        var regions = DiscRegions.None;
        for (int i = 0; i < 8; i++)
        {
            if ((regionByte & (1 << i)) == 0)          // bit clear = region allowed
                regions |= (DiscRegions)(1 << i);
        }
        return regions == DiscRegions.None ? DiscRegions.RegionFree : regions;
    }

    private static CopyProtectionFlags DetectProtectionFromIfo(byte[] vmgData)
    {
        var flags = CopyProtectionFlags.None;

        // User Operation Prohibitions at offset 0x018C (4 bytes) in VMG header
        // If any bits are set, at least some operations are restricted
        if (vmgData.Length > 0x018F)
        {
            uint uop = ReadBE32(vmgData, 0x018C);
            if (uop != 0) flags |= CopyProtectionFlags.UOPs;
        }

        // Region information – inverted mask ≠ 0xFF means region-locked
        if (vmgData.Length > VmgOffsetRegionInfo)
        {
            byte regionByte = vmgData[VmgOffsetRegionInfo];
            if (regionByte != 0x00 && regionByte != 0xFF)
                flags |= CopyProtectionFlags.RCE;
        }

        return flags;
    }

    // ── Binary helpers ────────────────────────────────────────────────────────

    private static ushort ReadBE16(byte[] data, int offset)
        => (ushort)((data[offset] << 8) | data[offset + 1]);

    private static uint ReadBE32(byte[] data, int offset)
        => ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
         | ((uint)data[offset + 2] << 8)  |  data[offset + 3];

    private static byte BcdToByte(byte bcd)
        => (byte)((bcd >> 4) * 10 + (bcd & 0x0F));

    // ── Language lookup ───────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> Iso639Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English", ["fr"] = "French",  ["de"] = "German",   ["es"] = "Spanish",
        ["it"] = "Italian", ["ja"] = "Japanese", ["ko"] = "Korean",   ["zh"] = "Chinese",
        ["pt"] = "Portuguese", ["ru"] = "Russian", ["nl"] = "Dutch",  ["pl"] = "Polish",
        ["sv"] = "Swedish", ["no"] = "Norwegian", ["da"] = "Danish",  ["fi"] = "Finnish",
        ["cs"] = "Czech",   ["hu"] = "Hungarian", ["el"] = "Greek",   ["tr"] = "Turkish",
        ["ar"] = "Arabic",  ["he"] = "Hebrew",    ["th"] = "Thai",    ["vi"] = "Vietnamese"
    };

    private static string Iso639ToEnglish(string code)
        => code.Length >= 2 && Iso639Map.TryGetValue(code[..2], out var name) ? name : code;
}
