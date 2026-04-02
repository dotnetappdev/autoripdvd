using AutoRipDVD.Models;
using System.Management;

namespace AutoRipDVD.Services;

public interface IDiscDetectionService
{
    event EventHandler<DiscInfo>? DiscInserted;
    event EventHandler<string>? DiscEjected;
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
    Task<List<DiscInfo>> GetInsertedDiscsAsync();
    Task EjectDiscAsync(string driveLetter);
}

public class DiscDetectionService : IDiscDetectionService
{
    private readonly ILogService _logService;
    private ManagementEventWatcher? _insertWatcher;
    private ManagementEventWatcher? _removeWatcher;
    private readonly System.Timers.Timer _pollTimer;

    public event EventHandler<DiscInfo>? DiscInserted;
    public event EventHandler<string>? DiscEjected;

    public DiscDetectionService(ILogService logService)
    {
        _logService = logService;
        _pollTimer = new System.Timers.Timer(5000); // Poll every 5 seconds
        _pollTimer.Elapsed += async (s, e) => await CheckForDiscsAsync();
    }

    public async Task StartMonitoringAsync()
    {
        await _logService.LogAsync("Starting disc detection monitoring...");
        
        try
        {
            // WMI event watcher for drive insertions
            var insertQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            _insertWatcher = new ManagementEventWatcher(insertQuery);
            _insertWatcher.EventArrived += async (s, e) => await OnDriveInserted(e);
            _insertWatcher.Start();

            // WMI event watcher for drive removals
            var removeQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");
            _removeWatcher = new ManagementEventWatcher(removeQuery);
            _removeWatcher.EventArrived += (s, e) => OnDriveRemoved(e);
            _removeWatcher.Start();

            // Start polling timer as backup
            _pollTimer.Start();

            await _logService.LogAsync("Disc detection monitoring started");
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error starting disc detection: {ex.Message}");
        }
    }

    public async Task StopMonitoringAsync()
    {
        _insertWatcher?.Stop();
        _removeWatcher?.Stop();
        _pollTimer.Stop();
        await _logService.LogAsync("Disc detection monitoring stopped");
    }

    private async Task OnDriveInserted(EventArrivedEventArgs e)
    {
        await Task.Delay(2000); // Wait for disc to be ready
        await CheckForDiscsAsync();
    }

    private void OnDriveRemoved(EventArrivedEventArgs e)
    {
        // Handle disc removal if needed
    }

    private async Task CheckForDiscsAsync()
    {
        var discs = await GetInsertedDiscsAsync();
        foreach (var disc in discs)
        {
            DiscInserted?.Invoke(this, disc);
        }
    }

    public async Task<List<DiscInfo>> GetInsertedDiscsAsync()
    {
        var discs = new List<DiscInfo>();

        await Task.Run(() =>
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                {
                    var discType = DetermineDiscType(drive);
                    if (discType != DiscType.Unknown)
                    {
                        discs.Add(new DiscInfo
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            DiscType = discType,
                            VolumeLabel = drive.VolumeLabel ?? "Unknown",
                            SizeBytes = drive.TotalSize,
                            DetectedAt = DateTime.Now
                        });
                    }
                }
            }
        });

        return discs;
    }

    private DiscType DetermineDiscType(DriveInfo drive)
    {
        try
        {
            // Check for Blu-ray (BDMV folder)
            if (Directory.Exists(Path.Combine(drive.Name, "BDMV")))
                return DiscType.BluRay;

            // Check for DVD (VIDEO_TS folder)
            if (Directory.Exists(Path.Combine(drive.Name, "VIDEO_TS")))
                return DiscType.DVD;

            // Check for Audio CD
            var rootFiles = Directory.GetFiles(drive.Name);
            if (rootFiles.Any(f => f.EndsWith(".cda", StringComparison.OrdinalIgnoreCase)))
                return DiscType.CD;

            // Everything else is data
            if (Directory.GetFileSystemEntries(drive.Name).Any())
                return DiscType.DataDisc;
        }
        catch
        {
            // Ignore errors
        }

        return DiscType.Unknown;
    }

    public async Task EjectDiscAsync(string driveLetter)
    {
        await Task.Run(() =>
        {
            try
            {
                var query = $"SELECT * FROM Win32_CDROMDrive WHERE Drive = '{driveLetter}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject drive in searcher.Get())
                {
                    drive.InvokeMethod("Eject", null);
                    _logService.LogAsync($"Ejected disc from {driveLetter}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogAsync($"Error ejecting disc: {ex.Message}");
            }
        });
    }
}
