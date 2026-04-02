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

    public MainViewModel(
        IRipJobQueue jobQueue,
        IDiscDetectionService discDetection,
        ILogService logService)
    {
        _jobQueue = jobQueue;
        _discDetection = discDetection;
        _logService = logService;

        // Subscribe to events
        _jobQueue.JobAdded += OnJobAdded;
        _jobQueue.JobUpdated += OnJobUpdated;
        _jobQueue.JobCompleted += OnJobCompleted;
        _logService.LogAdded += OnLogAdded;

        _ = LoadActiveJobsAsync();
        _ = LoadRecentLogsAsync();
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
