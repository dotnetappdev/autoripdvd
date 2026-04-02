using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogService _logService;

    [ObservableProperty]
    private ObservableCollection<string> _logs = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
        _logService.LogAdded += OnLogAdded;
        
        _ = LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        var logs = await _logService.GetLogsAsync(500);
        Logs = new ObservableCollection<string>(logs);
    }

    private void OnLogAdded(object? sender, string log)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            Logs.Insert(0, log);
            if (Logs.Count > 500)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
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
        Logs.Clear();
    }

    partial void OnFilterTextChanged(string value)
    {
        _ = ApplyFilterAsync();
    }

    private async Task ApplyFilterAsync()
    {
        var allLogs = await _logService.GetLogsAsync(500);
        
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            Logs = new ObservableCollection<string>(allLogs);
        }
        else
        {
            var filtered = allLogs.Where(l => l.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            Logs = new ObservableCollection<string>(filtered);
        }
    }
}
