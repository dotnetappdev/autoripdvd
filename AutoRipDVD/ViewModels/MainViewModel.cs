using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Dispatching;

namespace AutoRipDVD.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IRipJobQueue _jobQueue;
    private readonly IDiscDetectionService _discDetection;
    private readonly ILogService _logService;
    private readonly ISettingsService _settings;
    private readonly DispatcherQueue _dispatcherQueue;

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

    /// <summary>True = treat the next rip as a Movie; false = TV Series.</summary>
    [ObservableProperty]
    private bool _isMovieType = true;

    /// <summary>User-specified output path override. Empty = use the per-media path from Settings.</summary>
    [ObservableProperty]
    private string _outputPathOverride = string.Empty;

    // Dashboard-editable per-media output paths (editable from Dashboard)
    private string _moviesOutputPath = string.Empty;
    public string MoviesOutputPath
    {
        get => _moviesOutputPath;
        set
        {
            if (SetProperty(ref _moviesOutputPath, value))
            {
                _settings.Settings.MoviesOutputPath = value ?? string.Empty;
                _ = _settings.SaveSettingsAsync();
                OnPropertyChanged(nameof(EffectiveOutputPath));
            }
        }
    }

    private string _tvOutputPath = string.Empty;
    public string TvOutputPath
    {
        get => _tvOutputPath;
        set
        {
            if (SetProperty(ref _tvOutputPath, value))
            {
                _settings.Settings.TvOutputPath = value ?? string.Empty;
                _ = _settings.SaveSettingsAsync();
                OnPropertyChanged(nameof(EffectiveOutputPath));
            }
        }
    }

    /// <summary>Effective output path shown as placeholder in the UI (reflects IsMovieType + Settings).</summary>
    public string EffectiveOutputPath
    {
        get
        {
            if (_isMovieType)
                return string.IsNullOrEmpty(_settings.Settings.MoviesOutputPath)
                    ? _settings.Settings.OutputPath
                    : _settings.Settings.MoviesOutputPath;

            return string.IsNullOrEmpty(_settings.Settings.TvOutputPath)
                ? _settings.Settings.OutputPath
                : _settings.Settings.TvOutputPath;
        }
    }

    /// <summary>True when there are no active rip jobs.</summary>
    public bool HasNoActiveJobs => ActiveJobs == null || ActiveJobs.Count == 0;

    // Used to track and detach CollectionChanged handlers when the ActiveJobs collection is replaced
    private ObservableCollection<RipJob>? _lastActiveJobsCollection;
    private NotifyCollectionChangedEventHandler? _activeJobsCollectionChangedHandler;

    partial void OnActiveJobsChanged(ObservableCollection<RipJob>? value)
    {
        AttachActiveJobsCollectionChanged(value);
    }

    private void AttachActiveJobsCollectionChanged(ObservableCollection<RipJob>? newCol)
    {
        if (_lastActiveJobsCollection != null && _activeJobsCollectionChangedHandler != null)
            _lastActiveJobsCollection.CollectionChanged -= _activeJobsCollectionChangedHandler;

        _lastActiveJobsCollection = newCol;

        if (newCol != null)
        {
            _activeJobsCollectionChangedHandler = (_, __) => OnPropertyChanged(nameof(HasNoActiveJobs));
            newCol.CollectionChanged += _activeJobsCollectionChangedHandler;
        }

        // Ensure UI reflects current state
        OnPropertyChanged(nameof(HasNoActiveJobs));
    }

    public MainViewModel(
        IRipJobQueue jobQueue,
        IDiscDetectionService discDetection,
        ILogService logService,
        ISettingsService settings)
    {
        _jobQueue = jobQueue;
        _discDetection = discDetection;
        _logService = logService;
        _settings = settings;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Subscribe to events
        _jobQueue.JobAdded    += OnJobAdded;
        _jobQueue.JobUpdated  += OnJobUpdated;
        _jobQueue.JobCompleted += OnJobCompleted;
        _logService.LogAdded  += OnLogAdded;

        _discDetection.DiscInserted += OnDiscInserted;
        _discDetection.DiscEjected  += OnDiscEjected;

        // Initialize dashboard-editable paths from persisted settings
        _moviesOutputPath = _settings.Settings.MoviesOutputPath ?? string.Empty;
        _tvOutputPath     = _settings.Settings.TvOutputPath ?? string.Empty;

        // (Movies/Tv path setters persist directly)

        // Update EffectiveOutputPath when IsMovieType changes
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsMovieType))
                OnPropertyChanged(nameof(EffectiveOutputPath));
        };

        // Ensure we listen for changes to the initial ActiveJobs collection so HasNoActiveJobs updates
        AttachActiveJobsCollectionChanged(ActiveJobs);
    }

    public async Task InitializeAsync()
    {
        await LoadActiveJobsAsync();
        await LoadRecentLogsAsync();
        await RefreshDiscStateAsync();
    }

    // EffectiveOutputPath updates are handled via PropertyChanged subscription in constructor

    private async Task LoadActiveJobsAsync()
    {
        var jobs = await _jobQueue.GetActiveJobsAsync();
        _dispatcherQueue.TryEnqueue(() =>
        {
            ActiveJobs = new ObservableCollection<RipJob>(jobs);
        });
    }

    private async Task LoadRecentLogsAsync()
    {
        var logs = await _logService.GetLogsAsync(50);
        _dispatcherQueue.TryEnqueue(() =>
        {
            RecentLogs = new ObservableCollection<string>(logs);
        });
    }

    /// <summary>Checks current drive state on startup and sets HasDiscInserted accordingly.</summary>
    private async Task RefreshDiscStateAsync()
    {
        var discs = await _discDetection.GetInsertedDiscsAsync();
        var disc  = discs.FirstOrDefault(d => d.DiscType is DiscType.DVD or DiscType.BluRay);
        _dispatcherQueue.TryEnqueue(() =>
        {
            HasDiscInserted      = disc != null;
            DetectedDriveLetter  = disc?.DriveLetter ?? string.Empty;
        });
    }

    private void OnDiscInserted(object? sender, DiscInfo disc)
    {
        if (disc.DiscType is not (DiscType.DVD or DiscType.BluRay)) return;
        _dispatcherQueue.TryEnqueue(() =>
        {
            HasDiscInserted     = true;
            DetectedDriveLetter = disc.DriveLetter;
        });
    }

    private void OnDiscEjected(object? sender, string driveLetter)
    {
        _dispatcherQueue.TryEnqueue(async () =>
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
        _dispatcherQueue.TryEnqueue(() =>
        {
            ActiveJobs.Insert(0, job);
        });
    }

    private void OnJobUpdated(object? sender, RipJob job)
    {
        _dispatcherQueue.TryEnqueue(() =>
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
        _dispatcherQueue.TryEnqueue(() =>
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
        _dispatcherQueue.TryEnqueue(() =>
        {
            RecentLogs.Insert(0, log);
            if (RecentLogs.Count > 50)
                RecentLogs.RemoveAt(RecentLogs.Count - 1);
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
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EjectDiscAsync(string driveLetter)
    {
        await _discDetection.EjectDiscAsync(driveLetter);
    }

    public void Dispose()
    {
        _jobQueue.JobAdded    -= OnJobAdded;
        _jobQueue.JobUpdated  -= OnJobUpdated;
        _jobQueue.JobCompleted -= OnJobCompleted;
        _logService.LogAdded  -= OnLogAdded;
        _discDetection.DiscInserted -= OnDiscInserted;
        _discDetection.DiscEjected  -= OnDiscEjected;

        if (_lastActiveJobsCollection != null && _activeJobsCollectionChangedHandler != null)
            _lastActiveJobsCollection.CollectionChanged -= _activeJobsCollectionChangedHandler;
    }
}

