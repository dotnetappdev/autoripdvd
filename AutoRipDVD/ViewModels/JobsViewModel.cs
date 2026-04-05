using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;

namespace AutoRipDVD.ViewModels;

public partial class JobsViewModel : ObservableObject, IDisposable
{
    private readonly IRipJobQueue _jobQueue;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private ObservableCollection<RipJob> _allJobs = new();

    [ObservableProperty]
    private RipJob? _selectedJob;

    public JobsViewModel(IRipJobQueue jobQueue)
    {
        _jobQueue = jobQueue;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _jobQueue.JobAdded    += OnJobChanged;
        _jobQueue.JobUpdated  += OnJobChanged;
        _jobQueue.JobCompleted += OnJobChanged;

        _ = LoadJobsAsync();
    }

    private async Task LoadJobsAsync()
    {
        var jobs = await _jobQueue.GetAllJobsAsync();
        _dispatcherQueue.TryEnqueue(() =>
        {
            AllJobs = new ObservableCollection<RipJob>(jobs);
        });
    }

    private void OnJobChanged(object? sender, RipJob job)
    {
        _dispatcherQueue.TryEnqueue(async () =>
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

    public void Dispose()
    {
        _jobQueue.JobAdded    -= OnJobChanged;
        _jobQueue.JobUpdated  -= OnJobChanged;
        _jobQueue.JobCompleted -= OnJobChanged;
    }
}
