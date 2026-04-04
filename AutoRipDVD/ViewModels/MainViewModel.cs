using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IRipJobQueue _jobQueue;
    private readonly IDiscDetectionService _discDetection;
    private readonly ILogService _logService;

    [ObservableProperty]
    private ObservableCollection<RipJob> _activeJobs = new();

    [ObservableProperty]
    private ObservableCollection<string> _recentLogs = new();

    [ObservableProperty]
    private RipJob? _selectedJob;

    /// <summary>True when at least one DVD or Blu-ray disc is currently inserted in a drive.</summary>
    [ObservableProperty]
    private bool _hasDiscInserted;

    /// <summary>Drive letter of the first detected DVD/Blu-ray disc (empty when none).</summary>
    [ObservableProperty]
    private string _detectedDriveLetter = string.Empty;

    public MainViewModel(
        IRipJobQueue jobQueue,
        IDiscDetectionService discDetection,
        ILogService logService)
    {
        _jobQueue = jobQueue;
        _discDetection = discDetection;
        _logService = logService;

        // Subscribe to events
        _jobQueue.JobAdded    += OnJobAdded;
        _jobQueue.JobUpdated  += OnJobUpdated;
        _jobQueue.JobCompleted += OnJobCompleted;
        _logService.LogAdded  += OnLogAdded;

        _discDetection.DiscInserted += OnDiscInserted;
        _discDetection.DiscEjected  += OnDiscEjected;

        _ = LoadActiveJobsAsync();
        _ = LoadRecentLogsAsync();
        _ = RefreshDiscStateAsync();
    }

    private async Task LoadActiveJobsAsync()
    {
        var jobs = await _jobQueue.GetActiveJobsAsync();
        ActiveJobs = new ObservableCollection<RipJob>(jobs);
    }

    private async Task LoadRecentLogsAsync()
    {
        var logs = await _logService.GetLogsAsync(50);
        RecentLogs = new ObservableCollection<string>(logs);
    }

    /// <summary>Checks current drive state on startup and sets HasDiscInserted accordingly.</summary>
    private async Task RefreshDiscStateAsync()
    {
        var discs = await _discDetection.GetInsertedDiscsAsync();
        var disc  = discs.FirstOrDefault(d => d.DiscType is DiscType.DVD or DiscType.BluRay);
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            HasDiscInserted      = disc != null;
            DetectedDriveLetter  = disc?.DriveLetter ?? string.Empty;
        });
    }

    private void OnDiscInserted(object? sender, DiscInfo disc)
    {
        if (disc.DiscType is not (DiscType.DVD or DiscType.BluRay)) return;
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            HasDiscInserted     = true;
            DetectedDriveLetter = disc.DriveLetter;
        });
    }

    private void OnDiscEjected(object? sender, string driveLetter)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            // Re-check in case another disc is still inserted
            var discs = await _discDetection.GetInsertedDiscsAsync();
            var disc  = discs.FirstOrDefault(d => d.DiscType is DiscType.DVD or DiscType.BluRay);
            HasDiscInserted     = disc != null;
            DetectedDriveLetter = disc?.DriveLetter ?? string.Empty;
        });
    }

    private void OnJobAdded(object? sender, RipJob job)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            ActiveJobs.Insert(0, job);
        });
    }

    private void OnJobUpdated(object? sender, RipJob job)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            var existing = ActiveJobs.FirstOrDefault(j => j.Id == job.Id);
            if (existing != null)
            {
                var index = ActiveJobs.IndexOf(existing);
                ActiveJobs[index] = job;
            }
        });
    }

    private void OnJobCompleted(object? sender, RipJob job)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            var existing = ActiveJobs.FirstOrDefault(j => j.Id == job.Id);
            if (existing != null)
            {
                ActiveJobs.Remove(existing);
            }
        });
    }

    private void OnLogAdded(object? sender, string log)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            RecentLogs.Insert(0, log);
            if (RecentLogs.Count > 50)
            {
                RecentLogs.RemoveAt(RecentLogs.Count - 1);
            }
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadActiveJobsAsync();
        await LoadRecentLogsAsync();
    }

    [RelayCommand]
    private async Task ManualRipAsync()
    {
        // This will be called from the UI - dialog creation handled there
        // The command just needs to exist for data binding
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EjectDiscAsync(string driveLetter)
    {
        await _discDetection.EjectDiscAsync(driveLetter);
    }
}
