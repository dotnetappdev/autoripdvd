using AutoRipDVD.Models;
using System.Collections.Concurrent;

namespace AutoRipDVD.Services;

public interface IRipJobQueue
{
    event EventHandler<RipJob>? JobAdded;
    event EventHandler<RipJob>? JobUpdated;
    event EventHandler<RipJob>? JobCompleted;
    
    Task<Guid> AddJobAsync(RipJob job);
    Task<Guid> AddManualJobAsync(DiscInfo disc, MediaMetadata? metadata, List<TitleInfo> selectedTitles);
    Task UpdateJobAsync(RipJob job);
    Task<RipJob?> GetJobAsync(Guid id);
    Task<List<RipJob>> GetAllJobsAsync();
    Task<List<RipJob>> GetActiveJobsAsync();
    Task ProcessQueueAsync();
}

public class RipJobQueue : IRipJobQueue
{
    private readonly ConcurrentDictionary<Guid, RipJob> _jobs = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IMakeMkvService _makeMkvService;
    private readonly IHandBrakeService _handBrakeService;
    private readonly IMetadataService _metadataService;
    private readonly ITitleFilterService _titleFilter;
    private readonly IDiscDetectionService _discDetection;
    private readonly ISettingsService _settings;
    private readonly ILogService _logService;
    private readonly INotificationService _notificationService;

    public event EventHandler<RipJob>? JobAdded;
    public event EventHandler<RipJob>? JobUpdated;
    public event EventHandler<RipJob>? JobCompleted;

    public RipJobQueue(
        IMakeMkvService makeMkvService,
        IHandBrakeService handBrakeService,
        IMetadataService metadataService,
        ITitleFilterService titleFilter,
        IDiscDetectionService discDetection,
        ISettingsService settings,
        ILogService logService,
        INotificationService notificationService)
    {
        _makeMkvService = makeMkvService;
        _handBrakeService = handBrakeService;
        _metadataService = metadataService;
        _titleFilter = titleFilter;
        _discDetection = discDetection;
        _settings = settings;
        _logService = logService;
        _notificationService = notificationService;

        // Subscribe to disc insertion events
        _discDetection.DiscInserted += OnDiscInserted;
    }

    private async void OnDiscInserted(object? sender, DiscInfo disc)
    {
        if (disc.DiscType == DiscType.DVD || disc.DiscType == DiscType.BluRay)
        {
            await _logService.LogAsync($"Detected {disc.DiscType} disc in {disc.DriveLetter}: {disc.VolumeLabel}");

            if (_settings.Settings.AutoRip)
            {
                var job = new RipJob
                {
                    Disc = disc,
                    Status = RipStatus.Pending
                };

                await AddJobAsync(job);
                _ = ProcessJobAsync(job);
            }
        }
    }

    private async Task ProcessManualJobAsync(RipJob job)
    {
        await _semaphore.WaitAsync();
        
        try
        {
            // Manual job already has titles and metadata selected
            // Skip directly to ripping
            
            // Step 1: Rip titles
            job.Status = RipStatus.Ripping;
            job.CurrentOperation = "Ripping selected titles...";
            await UpdateJobAsync(job);

            var tempPath = Path.Combine(_settings.Settings.TempPath, job.Id.ToString());
            Directory.CreateDirectory(tempPath);

            var ripProgress = new Progress<double>(p =>
            {
                job.Progress = p * 0.7; // Ripping is 70% of total progress
                _ = UpdateJobAsync(job);
            });

            var ripSuccess = await _makeMkvService.RipTitlesAsync(
                job.Disc.DriveLetter,
                job.SelectedTitleIndices,
                tempPath,
                ripProgress);

            if (!ripSuccess)
            {
                job.Status = RipStatus.Failed;
                job.ErrorMessage = "Failed to rip disc";
                await UpdateJobAsync(job);
                await _notificationService.SendAsync("Rip Failed", $"Failed to rip {job.Metadata?.Title ?? job.Disc.VolumeLabel}");
                return;
            }

            // Step 2: Eject disc
            if (_settings.Settings.EjectWhenComplete)
            {
                await _discDetection.EjectDiscAsync(job.Disc.DriveLetter);
                await _logService.LogAsync($"Ejected disc {job.Disc.DriveLetter}");
            }

            // Step 3: Transcode if enabled
            if (_settings.Settings.TranscodeAfterRip)
            {
                job.Status = RipStatus.Transcoding;
                job.CurrentOperation = "Transcoding...";
                await UpdateJobAsync(job);

                var rippedFiles = Directory.GetFiles(tempPath, "*.mkv");
                var transcodeProgress = new Progress<double>(p =>
                {
                    job.Progress = 0.7 + (p * 0.3); // Transcode is 30% of total
                    _ = UpdateJobAsync(job);
                });

                foreach (var file in rippedFiles)
                {
                    var outputFile = Path.Combine(
                        _settings.Settings.OutputPath,
                        job.Metadata?.GetFormattedFolderName() ?? job.Disc.VolumeLabel,
                        Path.GetFileName(file));

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

                    await _handBrakeService.TranscodeAsync(
                        file,
                        outputFile,
                        _settings.Settings.HandBrakePreset,
                        _settings.Settings.VideoQuality,
                        transcodeProgress);
                }

                // Clean up temp files
                Directory.Delete(tempPath, true);
            }
            else
            {
                // Move files to output
                var outputFolder = Path.Combine(
                    _settings.Settings.OutputPath,
                    job.Metadata?.GetFormattedFolderName() ?? job.Disc.VolumeLabel);

                Directory.CreateDirectory(outputFolder);

                var rippedFiles = Directory.GetFiles(tempPath, "*.mkv");
                foreach (var file in rippedFiles)
                {
                    var destFile = Path.Combine(outputFolder, Path.GetFileName(file));
                    File.Move(file, destFile);
                }

                Directory.Delete(tempPath, true);
            }

            // Complete
            job.Status = RipStatus.Completed;
            job.Progress = 100;
            job.CurrentOperation = "Complete";
            job.CompletedAt = DateTime.Now;
            await UpdateJobAsync(job);
            
            JobCompleted?.Invoke(this, job);
            await _notificationService.SendAsync("Rip Complete", $"Successfully ripped {job.Metadata?.Title ?? job.Disc.VolumeLabel}");
            await _logService.LogAsync($"Completed job {job.Id}");
        }
        catch (Exception ex)
        {
            job.Status = RipStatus.Failed;
            job.ErrorMessage = ex.Message;
            await UpdateJobAsync(job);
            await _logService.LogErrorAsync("Job processing failed", ex);
            await _notificationService.SendAsync("Rip Failed", ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Guid> AddJobAsync(RipJob job)
    {
        _jobs.TryAdd(job.Id, job);
        JobAdded?.Invoke(this, job);
        await _logService.LogAsync($"Added job {job.Id} for disc {job.Disc.VolumeLabel}");
        return job.Id;
    }

    public async Task<Guid> AddManualJobAsync(DiscInfo disc, MediaMetadata? metadata, List<TitleInfo> selectedTitles)
    {
        var job = new RipJob
        {
            Disc = disc,
            Status = RipStatus.Pending,
            Metadata = metadata,
            Titles = selectedTitles,
            SelectedTitleIndices = selectedTitles.Select(t => t.Index).ToList()
        };

        _jobs.TryAdd(job.Id, job);
        JobAdded?.Invoke(this, job);
        await _logService.LogAsync($"Added manual job {job.Id} with {selectedTitles.Count} selected titles");
        
        // Start processing immediately
        _ = ProcessManualJobAsync(job);
        
        return job.Id;
    }

    public async Task UpdateJobAsync(RipJob job)
    {
        _jobs[job.Id] = job;
        JobUpdated?.Invoke(this, job);
        await Task.CompletedTask;
    }

    public Task<RipJob?> GetJobAsync(Guid id)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task<List<RipJob>> GetAllJobsAsync()
    {
        return Task.FromResult(_jobs.Values.OrderByDescending(j => j.CreatedAt).ToList());
    }

    public Task<List<RipJob>> GetActiveJobsAsync()
    {
        var activeStatuses = new[] { RipStatus.Pending, RipStatus.Detecting, RipStatus.FetchingMetadata, RipStatus.Ripping, RipStatus.Transcoding };
        return Task.FromResult(_jobs.Values.Where(j => activeStatuses.Contains(j.Status)).ToList());
    }

    public Task ProcessQueueAsync()
    {
        // Process all pending jobs
        var pendingJobs = _jobs.Values.Where(j => j.Status == RipStatus.Pending).ToList();
        foreach (var job in pendingJobs)
        {
            _ = ProcessJobAsync(job);
        }
        return Task.CompletedTask;
    }

    private async Task ProcessJobAsync(RipJob job)
    {
        await _semaphore.WaitAsync();
        
        try
        {
            // Step 1: Scan disc
            job.Status = RipStatus.Detecting;
            job.CurrentOperation = "Scanning disc...";
            await UpdateJobAsync(job);

            var progress = new Progress<string>(msg =>
            {
                job.CurrentOperation = msg;
                _ = UpdateJobAsync(job);
            });

            job.Titles = await _makeMkvService.ScanDiscAsync(job.Disc.DriveLetter, progress);

            // Step 2: Fetch metadata using FileBot-style auto-matching
            job.Status = RipStatus.FetchingMetadata;
            job.CurrentOperation = "Fetching metadata...";
            await UpdateJobAsync(job);

            // Use AutoMatch for intelligent metadata retrieval
            job.Metadata = await _metadataService.AutoMatchAsync(job.Disc.VolumeLabel);
            
            if (job.Metadata == null)
            {
                // Fallback: try to determine type manually
                var mediaType = await _metadataService.DetermineMediaTypeAsync(job.Disc.VolumeLabel);
                job.Metadata = new MediaMetadata
                {
                    Type = mediaType,
                    Title = job.Disc.VolumeLabel
                };
            }

            // Step 3: Filter titles intelligently (remove extras for TV shows)
            var filteredTitles = _titleFilter.FilterTitles(
                job.Titles, 
                job.Metadata.Type, 
                includeExtras: !_settings.Settings.RipMainFeatureOnly
            );

            // Step 4: Select titles to rip
            if (_settings.Settings.AutoRip)
            {
                // Auto mode: use filtered titles
                job.SelectedTitleIndices = filteredTitles.Select(t => t.Index).ToList();
                
                await _logService.LogAsync(
                    $"Auto-selected {job.SelectedTitleIndices.Count} titles " +
                    $"({job.Metadata.Type}: {job.Metadata.Title ?? job.Metadata.SeriesName})"
                );
            }
            else
            {
                // Manual mode: user will select titles
                // For now, default to filtered titles
                job.SelectedTitleIndices = filteredTitles.Select(t => t.Index).ToList();
                
                // TODO: Show title selection dialog
                // This would pause the job and wait for user input
                // For now, we proceed with auto-selection
            }

            // Step 5: Rip titles
            job.Status = RipStatus.Ripping;
            job.CurrentOperation = "Ripping disc...";
            await UpdateJobAsync(job);

            var tempPath = Path.Combine(_settings.Settings.TempPath, job.Id.ToString());
            Directory.CreateDirectory(tempPath);

            var ripProgress = new Progress<double>(p =>
            {
                job.Progress = p * 0.7; // Ripping is 70% of total progress
                _ = UpdateJobAsync(job);
            });

            var ripSuccess = await _makeMkvService.RipTitlesAsync(
                job.Disc.DriveLetter,
                job.SelectedTitleIndices,
                tempPath,
                ripProgress);

            if (!ripSuccess)
            {
                job.Status = RipStatus.Failed;
                job.ErrorMessage = "Failed to rip disc";
                await UpdateJobAsync(job);
                await _notificationService.SendAsync("Rip Failed", $"Failed to rip {job.Metadata.Title}");
                return;
            }

            // Step 6: Eject disc
            if (_settings.Settings.EjectWhenComplete)
            {
                await _discDetection.EjectDiscAsync(job.Disc.DriveLetter);
            }

            // Step 6: Transcode
            if (_settings.Settings.AutoTranscode)
            {
                job.Status = RipStatus.Transcoding;
                job.CurrentOperation = "Transcoding...";
                await UpdateJobAsync(job);

                var mkvFiles = Directory.GetFiles(tempPath, "*.mkv");
                var outputFolder = Path.Combine(_settings.Settings.OutputBasePath, job.Metadata.GetFormattedFolderName());
                Directory.CreateDirectory(outputFolder);

                foreach (var mkvFile in mkvFiles)
                {
                    var outputFile = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(mkvFile) + ".mp4");
                    
                    var transcodeProgress = new Progress<double>(p =>
                    {
                        job.Progress = 70 + (p * 0.3); // Transcoding is 30% of total
                        _ = UpdateJobAsync(job);
                    });

                    await _handBrakeService.TranscodeAsync(mkvFile, outputFile, transcodeProgress);
                }

                job.OutputPath = outputFolder;
            }
            else
            {
                // Just move MKV files
                var mkvFiles = Directory.GetFiles(tempPath, "*.mkv");
                var outputFolder = Path.Combine(_settings.Settings.OutputBasePath, job.Metadata.GetFormattedFolderName());
                Directory.CreateDirectory(outputFolder);

                foreach (var mkvFile in mkvFiles)
                {
                    var outputFile = Path.Combine(outputFolder, Path.GetFileName(mkvFile));
                    File.Move(mkvFile, outputFile, true);
                }

                job.OutputPath = outputFolder;
            }

            // Step 7: Complete
            job.Status = RipStatus.Completed;
            job.Progress = 100;
            job.CompletedAt = DateTime.Now;
            job.CurrentOperation = "Completed";
            await UpdateJobAsync(job);

            JobCompleted?.Invoke(this, job);
            await _notificationService.SendAsync("Rip Complete", $"Successfully ripped {job.Metadata.Title}");
            await _logService.LogAsync($"Completed job {job.Id}: {job.Metadata.Title}");
        }
        catch (Exception ex)
        {
            job.Status = RipStatus.Failed;
            job.ErrorMessage = ex.Message;
            await UpdateJobAsync(job);
            await _logService.LogAsync($"Job {job.Id} failed: {ex.Message}");
            await _notificationService.SendAsync("Rip Failed", $"Error: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
