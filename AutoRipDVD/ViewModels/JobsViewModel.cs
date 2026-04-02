using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

public partial class JobsViewModel : ObservableObject
{
    private readonly IRipJobQueue _jobQueue;

    [ObservableProperty]
    private ObservableCollection<RipJob> _allJobs = new();

    [ObservableProperty]
    private RipJob? _selectedJob;

    public JobsViewModel(IRipJobQueue jobQueue)
    {
        _jobQueue = jobQueue;
        _jobQueue.JobAdded += OnJobChanged;
        _jobQueue.JobUpdated += OnJobChanged;
        _jobQueue.JobCompleted += OnJobChanged;
        
        _ = LoadJobsAsync();
    }

    private async Task LoadJobsAsync()
    {
        var jobs = await _jobQueue.GetAllJobsAsync();
        AllJobs = new ObservableCollection<RipJob>(jobs);
    }

    private void OnJobChanged(object? sender, RipJob job)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadJobsAsync();
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadJobsAsync();
    }

    [RelayCommand]
    private void ViewJob(RipJob job)
    {
        SelectedJob = job;
    }
}
