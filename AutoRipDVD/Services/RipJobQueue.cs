using AutoRipDVD.Models;
using AutoRipDVD.Database.Repositories;
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
    void CancelJob(Guid id);
    void CancelAll();
    void EnableAutoRip();
    void DisableAutoRip();
}

public class RipJobQueue : IRipJobQueue
{
    private readonly ConcurrentDictionary<Guid, RipJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly IMakeMkvService _makeMkvService;
    private readonly IHandBrakeService _handBrakeService;
    private readonly IMetadataService _metadataService;
    private readonly ITitleFilterService _titleFilter;
    private readonly IFileNamingService _fileNaming;
    private readonly IDiscDetectionService _discDetection;
    private readonly ISettingsService _settings;
    private readonly ILogService _logService;
    private readonly INotificationService _notificationService;
    private readonly ISoundService _soundService;
    private readonly IDatabase _database;
    private readonly IDiscAnalyzerService _discAnalyzer;
    private readonly ITranscodePresetService _presets;
    private readonly IIsoCreatorService _isoCreator;

    // Auto-rip enable/disable toggle (separate from settings so we can start/stop at runtime)
    private bool _autoRipActive;

    public event EventHandler<RipJob>? JobAdded;
    public event EventHandler<RipJob>? JobUpdated;
    public event EventHandler<RipJob>? JobCompleted;

    public RipJobQueue(
        IMakeMkvService makeMkvService,
        IHandBrakeService handBrakeService,
        IMetadataService metadataService,
        ITitleFilterService titleFilter,
        IFileNamingService fileNaming,
        IDiscDetectionService discDetection,
        ISettingsService settings,
        ILogService logService,
        INotificationService notificationService,
        ISoundService soundService,
        IDatabase database,
        IDiscAnalyzerService discAnalyzer,
        ITranscodePresetService presets,
        IIsoCreatorService isoCreator)
    {
        _makeMkvService      = makeMkvService;
        _handBrakeService    = handBrakeService;
        _metadataService     = metadataService;
        _titleFilter         = titleFilter;
        _fileNaming          = fileNaming;
        _discDetection       = discDetection;
        _settings            = settings;
        _logService          = logService;
        _notificationService = notificationService;
        _soundService        = soundService;
        _database            = database;
        _discAnalyzer        = discAnalyzer;
        _presets             = presets;
        _isoCreator          = isoCreator;

        _autoRipActive = settings.Settings.AutoRip;
        _discDetection.DiscInserted += OnDiscInserted;
    }

    // ── Disc insertion handler ─────────────────────────────────────────────────

    private async void OnDiscInserted(object? sender, DiscInfo disc)
    {
        if (disc.DiscType != DiscType.DVD && disc.DiscType != DiscType.BluRay)
            return;

        await _logService.LogAsync($"Detected {disc.DiscType} in {disc.DriveLetter}: \"{disc.VolumeLabel}\"");

        if (!_autoRipActive) return;

        var job = new RipJob { Disc = disc, Status = RipStatus.Pending };
        await AddJobAsync(job);
        _ = ProcessJobAsync(job);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<Guid> AddJobAsync(RipJob job)
    {
        _jobs.TryAdd(job.Id, job);
        JobAdded?.Invoke(this, job);
        await _logService.LogAsync($"Queued job {job.Id} ({job.Disc.VolumeLabel})");
        await PersistJobAsync(job);
        return job.Id;
    }

    public async Task<Guid> AddManualJobAsync(DiscInfo disc, MediaMetadata? metadata, List<TitleInfo> selectedTitles)
    {
        var job = new RipJob
        {
            Disc                 = disc,
            Status               = RipStatus.Pending,
            Metadata             = metadata,
            Titles               = selectedTitles,
            SelectedTitleIndices = selectedTitles.Select(t => t.Index).ToList()
        };

        _jobs.TryAdd(job.Id, job);
        JobAdded?.Invoke(this, job);
        await _logService.LogAsync($"Queued manual job {job.Id} — {selectedTitles.Count} titles selected");
        await PersistJobAsync(job);
        _ = ProcessManualJobAsync(job);
        return job.Id;
    }

    public async Task UpdateJobAsync(RipJob job)
    {
        _jobs[job.Id] = job;
        JobUpdated?.Invoke(this, job);
        await PersistJobAsync(job);
    }

    public Task<RipJob?> GetJobAsync(Guid id)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task<List<RipJob>> GetAllJobsAsync()
        => Task.FromResult(_jobs.Values.OrderByDescending(j => j.CreatedAt).ToList());

    public Task<List<RipJob>> GetActiveJobsAsync()
    {
        var active = new[] { RipStatus.Pending, RipStatus.Detecting, RipStatus.FetchingMetadata, RipStatus.Ripping, RipStatus.Transcoding };
        return Task.FromResult(_jobs.Values.Where(j => active.Contains(j.Status)).ToList());
    }

    public Task ProcessQueueAsync()
    {
        foreach (var job in _jobs.Values.Where(j => j.Status == RipStatus.Pending))
            _ = ProcessJobAsync(job);
        return Task.CompletedTask;
    }

    public void CancelJob(Guid id)
    {
        if (_cancellations.TryGetValue(id, out var cts))
            cts.Cancel();
    }

    public void CancelAll()
    {
        foreach (var cts in _cancellations.Values)
            cts.Cancel();
    }

    public void EnableAutoRip()  { _autoRipActive = true; }
    public void DisableAutoRip() { _autoRipActive = false; CancelAll(); }

    // ── Automatic job processing ───────────────────────────────────────────────

    private async Task ProcessJobAsync(RipJob job)
    {
        using var cts = new CancellationTokenSource();
        _cancellations[job.Id] = cts;
        var ct = cts.Token;

        await _semaphore.WaitAsync(ct);
        try
        {
            if (ct.IsCancellationRequested) { await CancelJobAsync(job); return; }

            // Step 1: Deep disc analysis (IFO parse + copy protection detection)
            if (_settings.Settings.RunDiscAnalysisBeforeRip)
            {
                await UpdateStatusAsync(job, RipStatus.Detecting, "Analysing disc structure…");
                var analysisProgress = new Progress<string>(msg => { job.CurrentOperation = msg; _ = UpdateJobAsync(job); });
                var analysis = await _discAnalyzer.AnalyseDiscAsync(job.Disc, null, analysisProgress);

                if (analysis.Protection.IsProtected)
                    await _logService.LogAsync($"Protection detected: {analysis.Protection.ProtectionSummary}");

                // Pre-populate titles from IFO if we got them
                if (analysis.Titles.Count > 0)
                    job.Titles = analysis.Titles;
            }

            // Step 2: Scan disc with MakeMKV (gets full stream details)
            await UpdateStatusAsync(job, RipStatus.Detecting, "Scanning disc with MakeMKV…");
            var scanProgress = new Progress<string>(msg => { job.CurrentOperation = msg; _ = UpdateJobAsync(job); });
            var mkvTitles = await _makeMkvService.ScanDiscAsync(job.Disc.DriveLetter, scanProgress);
            if (mkvTitles.Count > 0)
                job.Titles = mkvTitles; // MakeMKV scan is more authoritative

            if (ct.IsCancellationRequested) { await CancelJobAsync(job); return; }

            // Step 2: Fetch metadata
            await UpdateStatusAsync(job, RipStatus.FetchingMetadata, "Fetching metadata...");
            job.Metadata = await _metadataService.AutoMatchAsync(job.Disc.VolumeLabel);
            job.Metadata ??= new MediaMetadata { Title = job.Disc.VolumeLabel, Type = MediaType.Movie };

            // Record match in history
            if (!string.IsNullOrEmpty(job.Metadata.ImdbId))
                await _database.MatchHistory.RecordMatchAsync(new MatchHistoryRecord(
                    DiscLabel:    job.Disc.VolumeLabel,
                    MatchedTitle: job.Metadata.Title.IfEmpty(job.Metadata.SeriesName),
                    MatchedId:    job.Metadata.ImdbId,
                    Source:       "OMDb",
                    MediaType:    job.Metadata.Type.ToString(),
                    Year:         job.Metadata.Year,
                    MatchedAt:    DateTime.UtcNow));

            // Step 3: Filter titles
            var includeExtras = !_settings.Settings.RipMainFeatureOnly;
            var filtered = _titleFilter.FilterTitles(job.Titles, job.Metadata.Type, includeExtras);
            job.SelectedTitleIndices = filtered.Select(t => t.Index).ToList();
            await _logService.LogAsync($"Selected {job.SelectedTitleIndices.Count} of {job.Titles.Count} titles for {job.Metadata.Title}");

            await RipAndTranscodeAsync(job, ct);
        }
        catch (OperationCanceledException)
        {
            await CancelJobAsync(job);
        }
        catch (Exception ex)
        {
            await FailJobAsync(job, ex.Message);
            await _notificationService.SendAsync("Rip Failed", $"{job.Metadata?.Title ?? job.Disc.VolumeLabel}: {ex.Message}");
        }
        finally
        {
            _cancellations.TryRemove(job.Id, out _);
            _semaphore.Release();
        }
    }

    // ── Manual job processing ─────────────────────────────────────────────────

    private async Task ProcessManualJobAsync(RipJob job)
    {
        using var cts = new CancellationTokenSource();
        _cancellations[job.Id] = cts;
        var ct = cts.Token;

        await _semaphore.WaitAsync(ct);
        try
        {
            if (ct.IsCancellationRequested) { await CancelJobAsync(job); return; }
            await RipAndTranscodeAsync(job, ct);
        }
        catch (OperationCanceledException)
        {
            await CancelJobAsync(job);
        }
        catch (Exception ex)
        {
            await FailJobAsync(job, ex.Message);
            await _notificationService.SendAsync("Rip Failed", ex.Message);
        }
        finally
        {
            _cancellations.TryRemove(job.Id, out _);
            _semaphore.Release();
        }
    }

    // ── Core rip + transcode pipeline ─────────────────────────────────────────

    private async Task RipAndTranscodeAsync(RipJob job, CancellationToken ct)
    {
        var tempPath = Path.Combine(_settings.Settings.TempPath, job.Id.ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            // Rip
            await UpdateStatusAsync(job, RipStatus.Ripping, "Ripping disc...");
            await _soundService.PlayAsync(SoundEvent.RipStarted);

            var ripProgress = new Progress<double>(p =>
            {
                job.Progress = p * 0.7;
                _ = UpdateJobAsync(job);
            });

            var statsProgress = new Progress<MakeMkvRipStats>(stats =>
            {
                job.RipStats         = stats;
                job.CurrentOperation = BuildRipOperation(stats);
                _ = UpdateJobAsync(job);
            });

            var ripOk = await _makeMkvService.RipTitlesAsync(
                job.Disc.DriveLetter, job.SelectedTitleIndices, tempPath,
                ripProgress, statsProgress, ct);

            if (!ripOk)
            {
                await FailJobAsync(job, "MakeMKV rip failed");
                await _soundService.PlayAsync(SoundEvent.RipFailed);
                await _notificationService.SendAsync("Rip Failed", $"{job.Metadata?.Title ?? job.Disc.VolumeLabel}");
                return;
            }

            if (ct.IsCancellationRequested) { await CancelJobAsync(job); return; }

            // ── ISO creation (before eject, disc still in drive) ──────────────
            if (_settings.Settings.AutoCreateIso)
                await CreateIsoForJobAsync(job, ct);

            // Eject (after ISO is done so the disc is still accessible during ISO creation)
            if (_settings.Settings.EjectWhenComplete && !_settings.Settings.CreateIsoInParallel)
            {
                await _discDetection.EjectDiscAsync(job.Disc.DriveLetter);
                await _logService.LogAsync($"Ejected {job.Disc.DriveLetter}");
                await _soundService.PlayAsync(SoundEvent.DiscEjected);
            }

            // Transcode or move
            var mkvFiles = Directory.GetFiles(tempPath, "*.mkv");

            if (_settings.Settings.TranscodeAfterRip)
            {
                await UpdateStatusAsync(job, RipStatus.Transcoding, "Transcoding...");
                var outRoot = _settings.Settings.OutputPath;
                int fileIdx = 0;

                foreach (var mkv in mkvFiles)
                {
                    if (ct.IsCancellationRequested) { await CancelJobAsync(job); return; }
                    var outputPath = GetOutputPath(job, mkv, fileIdx++, outRoot);
                    // Enqueue rename record so we keep track of files created and can rename later if needed
                    string recId = Guid.NewGuid().ToString();
                    try
                    {
                        var rec = new AutoRipDVD.Database.Repositories.FileRenameRecord(
                            recId,
                            job.Id.ToString(),
                            mkv,
                            outputPath,
                            false,
                            DateTime.UtcNow,
                            null
                        );
                        await _database.FileRenames.AddAsync(rec);
                    }
                    catch
                    {
                        // don't break ripping on DB failures
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                    var transcodeProgress = new Progress<double>(p =>
                    {
                        job.Progress = 70.0 + (p / mkvFiles.Length * 0.3) + (fileIdx - 1.0) / mkvFiles.Length * 30.0;
                        _ = UpdateJobAsync(job);
                    });

                    var activePreset = _presets.GetDefault();
                    await _handBrakeService.TranscodeWithPresetAsync(
                        mkv, outputPath, activePreset, null, transcodeProgress, ct);
                    job.OutputPath = Path.GetDirectoryName(outputPath) ?? outRoot;
                    // Mark as processed after transcode/move
                    try { await _database.FileRenames.MarkProcessedAsync(recId); } catch { }
                }

                Directory.Delete(tempPath, true);
            }
            else
            {
                // Move MKV files directly
                var outRoot = _settings.Settings.OutputPath;
                int fileIdx = 0;

                foreach (var mkv in mkvFiles)
                {
                    var outputPath = GetOutputPath(job, mkv, fileIdx++, outRoot);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    // Enqueue rename record before moving
                    string recId = Guid.NewGuid().ToString();
                    try
                    {
                        var rec = new AutoRipDVD.Database.Repositories.FileRenameRecord(
                            recId,
                            job.Id.ToString(),
                            mkv,
                            outputPath,
                            false,
                            DateTime.UtcNow,
                            null
                        );
                        await _database.FileRenames.AddAsync(rec);
                    }
                    catch { }

                    File.Move(mkv, outputPath, overwrite: true);
                    job.OutputPath = Path.GetDirectoryName(outputPath) ?? outRoot;

                    // Mark as processed after move
                    try { await _database.FileRenames.MarkProcessedAsync(recId); } catch { }
                }

                Directory.Delete(tempPath, true);
            }

            // Complete
            job.Status         = RipStatus.Completed;
            job.Progress       = 100;
            job.CurrentOperation = "Complete";
            job.CompletedAt    = DateTime.Now;
            await UpdateJobAsync(job);
            JobCompleted?.Invoke(this, job);
            await _soundService.PlayAsync(SoundEvent.RipCompleted);
            await _notificationService.SendAsync("Rip Complete", $"✓ {job.Metadata?.Title ?? job.Disc.VolumeLabel}");
            await _logService.LogAsync($"Job {job.Id} completed → {job.OutputPath}");
        }
        finally
        {
            // Clean up temp if it still exists (e.g. after error)
            if (Directory.Exists(tempPath))
            {
                try { Directory.Delete(tempPath, true); } catch { /* ignore */ }
            }
        }
    }

    // ── FileBot-style output path resolution ───────────────────────────────────

    private string GetOutputPath(RipJob job, string sourceFile, int titleIndex, string outputRoot)
    {
        var meta = job.Metadata;
        var ext  = _settings.Settings.TranscodeAfterRip ? ".mkv" : Path.GetExtension(sourceFile);

        if (meta == null)
            return Path.Combine(outputRoot, job.Disc.VolumeLabel, Path.GetFileName(sourceFile));

        if (meta.Type == MediaType.TVShow)
        {
            // Assign episode number from disc title order
            var epMeta = new MediaMetadata
            {
                Type          = MediaType.TVShow,
                SeriesName    = meta.SeriesName,
                Title         = meta.Title,
                SeasonNumber  = meta.SeasonNumber ?? 1,
                EpisodeNumber = titleIndex + 1,
                EpisodeTitle  = meta.EpisodeTitle,
                Year          = meta.Year
            };
            return _fileNaming.GetTVShowEpisodeFilePath(epMeta, outputRoot, titleIndex + 1, ext);
        }
        else
        {
            return _fileNaming.GetMovieFilePath(meta, outputRoot, ext);
        }
    }

    // ── ISO creation helper ───────────────────────────────────────────────────

    private async Task CreateIsoForJobAsync(RipJob job, CancellationToken ct)
    {
        var s = _settings.Settings;

        // Determine ISO output path
        var isoRoot = string.IsNullOrWhiteSpace(s.IsoOutputPath) ? s.OutputPath : s.IsoOutputPath;
        Directory.CreateDirectory(isoRoot);

        var discName = job.Metadata?.Title.IfEmpty(job.Disc.VolumeLabel)
                       ?? job.Disc.VolumeLabel.IfEmpty("disc");
        var safeTitle = string.Join("_", discName.Split(Path.GetInvalidFileNameChars()));
        var isoFileName = $"{safeTitle}.iso";
        var isoPath     = Path.Combine(isoRoot, isoFileName);

        await UpdateStatusAsync(job, RipStatus.CreatingIso, $"Creating ISO image: {isoFileName}");

        var isoProgress = new Progress<IsoProgress>(p =>
        {
            job.CurrentOperation = p.StatusMessage;
            job.Progress         = p.PercentComplete;
            _ = UpdateJobAsync(job);
        });

        var result = await _isoCreator.CreateIsoAsync(
            job.Disc, isoPath, s.DefaultIsoMode, isoProgress, ct);

        if (result.Success)
        {
            job.IsoPath = result.IsoPath;
            await _logService.LogAsync($"ISO created: {result.FormattedSize} → {result.IsoPath}");
            await _notificationService.SendAsync("ISO Created",
                $"✓ {discName} ({result.FormattedSize})");
        }
        else
        {
            await _logService.LogAsync($"ISO creation failed: {result.ErrorMessage}");
            // Non-fatal – rip can still continue
        }

        // Eject after ISO if that option is set
        if (s.EjectAfterIso && result.Success)
        {
            await _discDetection.EjectDiscAsync(job.Disc.DriveLetter);
            await _soundService.PlayAsync(SoundEvent.DiscEjected);
        }
    }

    // ── Stats helpers ─────────────────────────────────────────────────────────

    private static string BuildRipOperation(MakeMkvRipStats stats)
    {
        if (stats.OverallProgressValue > 0)
            return $"{stats.OverallProgressLabel} ({stats.OverallProgressValue:F0}%)";
        if (!string.IsNullOrEmpty(stats.TitleProgressLabel))
            return stats.TitleProgressLabel;
        return "Ripping disc…";
    }

    // ── Status helpers ────────────────────────────────────────────────────────

    private async Task UpdateStatusAsync(RipJob job, RipStatus status, string operation)
    {
        job.Status           = status;
        job.CurrentOperation = operation;
        await UpdateJobAsync(job);
    }

    private async Task CancelJobAsync(RipJob job)
    {
        job.Status           = RipStatus.Cancelled;
        job.CurrentOperation = "Cancelled";
        await UpdateJobAsync(job);
        await _logService.LogAsync($"Job {job.Id} cancelled");
    }

    private async Task FailJobAsync(RipJob job, string message)
    {
        job.Status       = RipStatus.Failed;
        job.ErrorMessage = message;
        await UpdateJobAsync(job);
        await _soundService.PlayAsync(SoundEvent.RipFailed);
        await _logService.LogAsync($"Job {job.Id} failed: {message}");
    }

    // ── Database persistence ──────────────────────────────────────────────────

    private async Task PersistJobAsync(RipJob job)
    {
        try
        {
            await _database.Jobs.UpsertAsync(new JobRecord(
                Id:                 job.Id.ToString(),
                DiscLabel:          job.Disc.VolumeLabel,
                DiscType:           job.Disc.DiscType.ToString(),
                DriveLetter:        job.Disc.DriveLetter,
                MediaTitle:         job.Metadata == null
                                        ? null
                                        : (string.IsNullOrWhiteSpace(job.Metadata.Title)
                                            ? (string.IsNullOrWhiteSpace(job.Metadata.SeriesName) ? null : job.Metadata.SeriesName)
                                            : job.Metadata.Title),
                MediaType:          job.Metadata?.Type.ToString(),
                Season:             job.Metadata?.SeasonNumber,
                Episode:            job.Metadata?.EpisodeNumber,
                Year:               job.Metadata?.Year,
                ImdbId:             job.Metadata?.ImdbId,
                Status:             job.Status.ToString(),
                Progress:           job.Progress,
                OutputPath:         job.OutputPath,
                ErrorMessage:       job.ErrorMessage,
                TitleCount:         job.Titles.Count,
                SelectedTitleCount: job.SelectedTitleIndices.Count,
                CreatedAt:          job.CreatedAt,
                CompletedAt:        job.CompletedAt));
        }
        catch
        {
            // Don't let DB errors interrupt the rip
        }
    }
}
