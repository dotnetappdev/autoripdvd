using AutoRipDVD.Models;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoRipDVD.Services;

// IsoCreationMode is defined in AutoRipDVD.Models.StreamModels so AppSettings can reference it.

// ── ISO job result ────────────────────────────────────────────────────────────

public class IsoResult
{
    public bool    Success       { get; set; }
    public string  IsoPath       { get; set; } = string.Empty;
    public long    FileSizeBytes { get; set; }
    public TimeSpan Duration     { get; set; }
    public string  ErrorMessage  { get; set; } = string.Empty;
    public IsoCreationMode Mode  { get; set; }
    public string  FormattedSize
    {
        get
        {
            if (FileSizeBytes >= 1_073_741_824) return $"{FileSizeBytes / 1_073_741_824.0:F2} GB";
            if (FileSizeBytes >= 1_048_576)     return $"{FileSizeBytes / 1_048_576.0:F1} MB";
            return $"{FileSizeBytes / 1024.0:F1} KB";
        }
    }
}

// ── Service interface ─────────────────────────────────────────────────────────

public interface IIsoCreatorService
{
    /// <summary>
    /// Create an ISO image from the optical disc in <paramref name="driveLetter"/>.
    /// The output file is placed at <paramref name="outputPath"/>.
    /// </summary>
    Task<IsoResult> CreateIsoAsync(
        DiscInfo disc,
        string outputPath,
        IsoCreationMode mode,
        IProgress<IsoProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>List available creation modes for the current system configuration.</summary>
    Task<List<IsoCreationMode>> GetAvailableModesAsync();
}

// ── Progress payload ──────────────────────────────────────────────────────────

public record IsoProgress(
    double PercentComplete,
    long   BytesWritten,
    long   TotalBytes,
    double SpeedMBps,
    string StatusMessage);

// ── Implementation ────────────────────────────────────────────────────────────

public class IsoCreatorService : IIsoCreatorService
{
    private readonly ISettingsService _settings;
    private readonly ILogService      _log;

    // ── Win32 P/Invoke declarations ───────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint   dwDesiredAccess,
        uint   dwShareMode,
        IntPtr lpSecurityAttributes,
        uint   dwCreationDisposition,
        uint   dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint   dwIoControlCode,
        IntPtr lpInBuffer,
        uint   nInBufferSize,
        IntPtr lpOutBuffer,
        uint   nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint GENERIC_READ          = 0x80000000;
    private const uint FILE_SHARE_READ       = 0x00000001;
    private const uint FILE_SHARE_WRITE      = 0x00000002;
    private const uint OPEN_EXISTING         = 3;
    private const uint FILE_FLAG_NO_BUFFERING      = 0x20000000;
    private const uint FILE_FLAG_SEQUENTIAL_SCAN   = 0x08000000;

    // IOCTL_CDROM_GET_LAST_SESSION / IOCTL_DISK_GET_DRIVE_GEOMETRY_EX
    private const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
    private const uint IOCTL_STORAGE_GET_MEDIA_TYPES_EX  = 0x002D0C04;

    // Optical disc sector sizes
    private const int SectorSize    = 2048;   // Mode-1 CD/DVD/BD sector
    private const int SectorBufCount = 64;    // sectors to read per I/O call (128 KB)
    private const int ReadBufSize   = SectorSize * SectorBufCount;

    public IsoCreatorService(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log      = log;
    }

    // ── Available modes ───────────────────────────────────────────────────────

    public async Task<List<IsoCreationMode>> GetAvailableModesAsync()
    {
        var modes = new List<IsoCreationMode> { IsoCreationMode.RawSectorCopy };

        if (File.Exists(_settings.Settings.MakeMkvPath))
        {
            var mkisofs = await FindMkisofsAsync();
            if (mkisofs != null)
                modes.Add(IsoCreationMode.MakeMkvDecrypted);
        }

        if (File.Exists(_settings.Settings.ImgBurnPath))
            modes.Add(IsoCreationMode.ImgBurn);

        return modes;
    }

    // ── Main entry point ──────────────────────────────────────────────────────

    public async Task<IsoResult> CreateIsoAsync(
        DiscInfo disc,
        string outputPath,
        IsoCreationMode mode,
        IProgress<IsoProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await _log.LogAsync($"Creating ISO from {disc.DriveLetter} ({disc.DiscType}) → {outputPath} [{mode}]");

        var started = DateTime.Now;
        IsoResult result;

        try
        {
            result = mode switch
            {
                IsoCreationMode.RawSectorCopy     => await RawSectorCopyAsync(disc, outputPath, progress, ct),
                IsoCreationMode.MakeMkvDecrypted  => await MakeMkvDecryptedIsoAsync(disc, outputPath, progress, ct),
                IsoCreationMode.ImgBurn           => await ImgBurnIsoAsync(disc, outputPath, progress, ct),
                _                                 => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }
        catch (OperationCanceledException)
        {
            await _log.LogAsync("ISO creation cancelled");
            // Remove partial file
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new IsoResult { Success = false, ErrorMessage = "Cancelled", Mode = mode };
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"ISO creation error: {ex.Message}");
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new IsoResult { Success = false, ErrorMessage = ex.Message, Mode = mode };
        }

        result.Duration = DateTime.Now - started;
        if (result.Success && File.Exists(outputPath))
            result.FileSizeBytes = new FileInfo(outputPath).Length;

        await _log.LogAsync(result.Success
            ? $"ISO created: {result.FormattedSize} in {result.Duration.TotalSeconds:F0}s → {outputPath}"
            : $"ISO creation failed: {result.ErrorMessage}");

        return result;
    }

    // ── Method 1: Raw sector copy (Win32 CreateFile) ──────────────────────────
    //
    // Opens \\.\D: as a raw device and reads every sector in 128 KB chunks.
    // This is identical to `dd if=\\.\D: of=output.iso bs=2048` on Windows.
    // Works for any optical disc; for CSS/AACS-encrypted discs the sectors are
    // readable but the content is encrypted (still valid for archival ISO).

    private async Task<IsoResult> RawSectorCopyAsync(
        DiscInfo disc,
        string outputPath,
        IProgress<IsoProgress>? progress,
        CancellationToken ct)
    {
        var drivePath = $@"\\.\{disc.DriveLetter.TrimEnd('\\', ':')}:";

        var handle = CreateFile(
            drivePath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_NO_BUFFERING | FILE_FLAG_SEQUENTIAL_SCAN,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Cannot open drive {disc.DriveLetter} for raw read (Win32 error {err}). " +
                $"Run as Administrator for raw disc access.");
        }

        long totalBytes = disc.SizeBytes > 0 ? disc.SizeBytes : EstimateDiscSize(disc.DiscType);
        long written    = 0;
        var  startTime  = DateTime.Now;

        await _log.LogAsync($"Raw ISO copy: {totalBytes / 1_073_741_824.0:F2} GB from {drivePath}");

        using (handle)
        using (var fs = new FileStream(handle, FileAccess.Read, ReadBufSize, isAsync: true))
        using (var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write,
                                          FileShare.None, ReadBufSize, useAsync: true))
        {
            var buf = new byte[ReadBufSize];

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int bytesRead = await fs.ReadAsync(buf.AsMemory(0, ReadBufSize), ct);
                if (bytesRead == 0) break;

                await outFs.WriteAsync(buf.AsMemory(0, bytesRead), ct);
                written += bytesRead;

                // Report progress every ~4 MB
                if (written % (ReadBufSize * 32) == 0 || bytesRead < ReadBufSize)
                {
                    var elapsed  = (DateTime.Now - startTime).TotalSeconds;
                    var speedMBs = elapsed > 0 ? written / 1_048_576.0 / elapsed : 0;
                    var pct      = totalBytes > 0 ? Math.Min(100, written * 100.0 / totalBytes) : 0;
                    progress?.Report(new IsoProgress(
                        pct, written, totalBytes, speedMBs,
                        $"Reading sectors… {written / 1_048_576.0:F0} MB / {totalBytes / 1_073_741_824.0:F1} GB ({speedMBs:F1} MB/s)"));
                }
            }

            await outFs.FlushAsync(ct);
        }

        progress?.Report(new IsoProgress(100, written, written, 0, "ISO image complete"));
        return new IsoResult { Success = true, IsoPath = outputPath, Mode = IsoCreationMode.RawSectorCopy };
    }

    // ── Method 2: MakeMKV backup → mkisofs ISO ────────────────────────────────
    //
    // Phase A – makemkvcon backup --decrypt disc:N tempDir
    //           (decrypts CSS/AACS, produces clean VIDEO_TS or BDMV folder)
    // Phase B – mkisofs / genisoimage to wrap the folder into a proper ISO
    //           with correct UDF/ISO-9660 filesystem (like the original disc).

    private async Task<IsoResult> MakeMkvDecryptedIsoAsync(
        DiscInfo disc,
        string outputPath,
        IProgress<IsoProgress>? progress,
        CancellationToken ct)
    {
        var tempBackupDir = Path.Combine(
            _settings.Settings.TempPath,
            $"iso_backup_{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempBackupDir);

        try
        {
            var driveIndex = GetDriveIndex(disc.DriveLetter);

            // ── Phase A: MakeMKV backup ──────────────────────────────────────
            progress?.Report(new IsoProgress(0, 0, 0, 0, "MakeMKV: decrypting disc backup…"));
            await _log.LogAsync("ISO Phase A: MakeMKV backup (decrypt)");

            var backupArgs = $"-r --decrypt backup disc:{driveIndex} \"{tempBackupDir}\"";
            var backupOk = await RunMakeMkvWithProgressAsync(
                backupArgs,
                p => progress?.Report(new IsoProgress(
                    p * 0.70, 0, 0, 0,
                    $"MakeMKV backup: {p:F0}%")),
                ct);

            if (!backupOk)
                return new IsoResult { Success = false, ErrorMessage = "MakeMKV backup failed", Mode = IsoCreationMode.MakeMkvDecrypted };

            ct.ThrowIfCancellationRequested();

            // ── Phase B: mkisofs / genisoimage ───────────────────────────────
            progress?.Report(new IsoProgress(70, 0, 0, 0, "Building ISO filesystem…"));
            await _log.LogAsync("ISO Phase B: mkisofs → ISO");

            var mkisofs = await FindMkisofsAsync()
                ?? throw new InvalidOperationException(
                    "mkisofs / genisoimage not found. Install via: winget install oscdimg OR download genisoimage.");

            var volId  = SanitiseVolId(disc.VolumeLabel.IfEmpty("DISC"));
            bool isBd  = disc.DiscType == DiscType.BluRay;

            string mkisofsArgs = isBd
                ? BuildBdIsoArgs(volId, tempBackupDir, outputPath)
                : BuildDvdIsoArgs(volId, tempBackupDir, outputPath);

            await _log.LogAsync($"mkisofs args: {mkisofsArgs}");

            var (exitCode, stderr) = await RunProcessAsync(mkisofs, mkisofsArgs,
                line =>
                {
                    // mkisofs outputs "XX.XX%" to stderr
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"([\d.]+)%");
                    if (m.Success && double.TryParse(m.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pct))
                    {
                        progress?.Report(new IsoProgress(
                            70 + pct * 0.30, 0, 0, 0,
                            $"Building ISO: {pct:F0}%"));
                    }
                }, ct);

            if (exitCode != 0)
                return new IsoResult
                {
                    Success = false,
                    ErrorMessage = $"mkisofs failed (exit {exitCode}): {stderr}",
                    Mode = IsoCreationMode.MakeMkvDecrypted
                };

            progress?.Report(new IsoProgress(100, 0, 0, 0, "Decrypted ISO complete"));
            return new IsoResult { Success = true, IsoPath = outputPath, Mode = IsoCreationMode.MakeMkvDecrypted };
        }
        finally
        {
            // Clean up temp backup dir
            try { Directory.Delete(tempBackupDir, true); }
            catch { /* ignore */ }
        }
    }

    // ── Method 3: ImgBurn CLI ─────────────────────────────────────────────────
    //
    // ImgBurn /MODE READ handles bad-sector skipping (ARccOS) and creates a
    // sector-accurate ISO.  Requires ImgBurn to be installed.

    private async Task<IsoResult> ImgBurnIsoAsync(
        DiscInfo disc,
        string outputPath,
        IProgress<IsoProgress>? progress,
        CancellationToken ct)
    {
        var imgBurnPath = _settings.Settings.ImgBurnPath;
        if (!File.Exists(imgBurnPath))
            throw new InvalidOperationException(
                $"ImgBurn not found at '{imgBurnPath}'. Set ImgBurnPath in Settings.");

        // ImgBurn CLI reference:
        // /MODE READ          – create image from disc
        // /SRC <drive>        – source drive letter
        // /DEST <file>        – destination .iso path
        // /START              – start immediately
        // /CLOSE              – close after finish
        // /NOIMAGEDETAILS     – suppress per-sector detail popup
        // /OVERWRITE YES      – overwrite existing file
        // /TESTMODE NO        – no test mode
        var args = $"/MODE READ /SRC \"{disc.DriveLetter.TrimEnd('\\')}\" " +
                   $"/DEST \"{outputPath}\" " +
                   $"/START /CLOSE /NOIMAGEDETAILS /OVERWRITE YES /TESTMODE NO";

        await _log.LogAsync($"ImgBurn ISO: {args}");
        progress?.Report(new IsoProgress(0, 0, 0, 0, "ImgBurn: reading disc…"));

        var (exitCode, stderr) = await RunProcessAsync(imgBurnPath, args,
            line =>
            {
                // ImgBurn outputs "x%" to stdout
                var m = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)%");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var pct))
                    progress?.Report(new IsoProgress(pct, 0, 0, 0, $"ImgBurn: {pct}%"));
            }, ct);

        if (exitCode != 0)
            return new IsoResult
            {
                Success = false,
                ErrorMessage = $"ImgBurn failed (exit {exitCode})",
                Mode = IsoCreationMode.ImgBurn
            };

        progress?.Report(new IsoProgress(100, 0, 0, 0, "ISO created via ImgBurn"));
        return new IsoResult { Success = true, IsoPath = outputPath, Mode = IsoCreationMode.ImgBurn };
    }

    // ── mkisofs argument builders ─────────────────────────────────────────────

    private static string BuildDvdIsoArgs(string volId, string sourceDir, string outputPath)
    {
        // -dvd-video  – generate DVD-Video–compliant UDF/ISO-9660 bridge
        // -udf        – include UDF filesystem (required for DVD players)
        // -iso-level 1 – conservative ISO-9660 level for compatibility
        // -V          – volume ID (max 32 chars)
        // -follow-links – follow symlinks in source
        return $"-dvd-video -udf -iso-level 1 " +
               $"-V \"{volId}\" " +
               $"-follow-links " +
               $"-o \"{outputPath}\" " +
               $"\"{sourceDir}\"";
    }

    private static string BuildBdIsoArgs(string volId, string sourceDir, string outputPath)
    {
        // For Blu-ray: UDF 2.6 bridge disc
        // genisoimage doesn't natively produce BD-correct UDF 2.6;
        // a better tool is bd_info + UDF 2.6 aware writers.
        // As a practical workaround we use -udf and UDF 2.01.
        return $"-udf -udf-version 2.01 " +
               $"-V \"{volId}\" " +
               $"-follow-links " +
               $"-allow-lowercase " +
               $"-o \"{outputPath}\" " +
               $"\"{sourceDir}\"";
    }

    // ── mkisofs / genisoimage locator ─────────────────────────────────────────

    private async Task<string?> FindMkisofsAsync()
    {
        // Check user-configured path first
        var configured = _settings.Settings.MkisofsPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        // Common Windows installation locations
        var candidates = new[]
        {
            @"C:\Program Files\cdrtools\mkisofs.exe",
            @"C:\Program Files (x86)\cdrtools\mkisofs.exe",
            @"C:\tools\mkisofs.exe",
            @"C:\cdrtools\mkisofs.exe",
            @"C:\Program Files\genisoimage\genisoimage.exe",
            // Also check %PATH% via where.exe
        };

        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        // Try to find via where.exe
        try
        {
            foreach (var name in new[] { "mkisofs", "genisoimage", "oscdimg" })
            {
                var (exit, output) = await RunProcessAsync("where.exe", name, null, CancellationToken.None);
                if (exit == 0)
                {
                    var found = output.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
                    if (!string.IsNullOrEmpty(found) && File.Exists(found))
                        return found;
                }
            }
        }
        catch { /* ignore */ }

        return null;
    }

    // ── MakeMKV process runner ────────────────────────────────────────────────

    private async Task<bool> RunMakeMkvWithProgressAsync(
        string arguments,
        Action<double>? onProgress,
        CancellationToken ct)
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
            _ = _log.LogAsync(e.Data);

            var m = System.Text.RegularExpressions.Regex.Match(e.Data, @"PRGV:(\d+),(\d+),(\d+)");
            if (m.Success
                && int.TryParse(m.Groups[1].Value, out var cur)
                && int.TryParse(m.Groups[3].Value, out var max)
                && max > 0)
            {
                onProgress?.Invoke((double)cur / max * 100.0);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        if (ct.IsCancellationRequested && !process.HasExited)
            process.Kill(entireProcessTree: true);

        return process.ExitCode == 0;
    }

    // ── Generic process runner ────────────────────────────────────────────────

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string exe, string args,
        Action<string>? onLine,
        CancellationToken ct)
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
        var outputLines = new List<string>();

        proc.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputLines.Add(e.Data);
                onLine?.Invoke(e.Data);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputLines.Add(e.Data);
                onLine?.Invoke(e.Data);
            }
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);

        if (ct.IsCancellationRequested && !proc.HasExited)
            proc.Kill(entireProcessTree: true);

        return (proc.ExitCode, string.Join("\n", outputLines));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int GetDriveIndex(string driveLetter)
    {
        var letter = driveLetter.TrimEnd(':', '\\').ToUpper()[0];
        return letter - 'A';
    }

    private static long EstimateDiscSize(DiscType type)
        => type switch
        {
            DiscType.BluRay => 50L * 1024 * 1024 * 1024,  // 50 GB dual-layer BD
            DiscType.DVD    =>  9L * 1024 * 1024 * 1024,  //  9 GB dual-layer DVD
            DiscType.CD     =>  700L * 1024 * 1024,        // 700 MB CD
            _               =>  9L * 1024 * 1024 * 1024
        };

    private static string SanitiseVolId(string label)
    {
        // ISO-9660 volume identifier: max 32 uppercase ASCII chars
        var clean = new string(label
            .ToUpperInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_')
            .ToArray());
        return clean.Length > 32 ? clean[..32] : clean;
    }
}
