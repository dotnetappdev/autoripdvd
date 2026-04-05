using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;

namespace AutoRipDVD.ViewModels;

public partial class LogsViewModel : ObservableObject, IDisposable
{
    private readonly ILogService _logService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private ObservableCollection<string> _logs = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _logService.LogAdded += OnLogAdded;

        _ = LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        var logs = await _logService.GetLogsAsync(500);
        _dispatcherQueue.TryEnqueue(() =>
        {
            Logs = new ObservableCollection<string>(logs);
        });
    }

    private void OnLogAdded(object? sender, string log)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            Logs.Insert(0, log);
            if (Logs.Count > 500)
                Logs.RemoveAt(Logs.Count - 1);
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadLogsAsync();
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        await _logService.ClearLogsAsync();
        _dispatcherQueue.TryEnqueue(() => Logs.Clear());
    }

    partial void OnFilterTextChanged(string value)
    {
        _ = ApplyFilterAsync();
    }

    private async Task ApplyFilterAsync()
    {
        var allLogs = await _logService.GetLogsAsync(500);
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? allLogs
            : allLogs.Where(l => l.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

        _dispatcherQueue.TryEnqueue(() =>
        {
            Logs = new ObservableCollection<string>(filtered);
        });
    }

    public void Dispose()
    {
        _logService.LogAdded -= OnLogAdded;
    }
}
