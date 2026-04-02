using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;

namespace AutoRipDVD.ViewModels;

public partial class TitleSelectionViewModel : ObservableObject
{
    private readonly IMakeMkvService _makeMkvService;
    private readonly IMetadataService _metadataService;
    private readonly ITitleFilterService _titleFilterService;
    private readonly ILogService _logService;

    [ObservableProperty]
    private string _driveLetter = string.Empty;

    [ObservableProperty]
    private DiscInfo? _discInfo;

    [ObservableProperty]
    private ObservableCollection<SelectableTitleInfo> _titles = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasScanned;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan";

    [ObservableProperty]
    private MediaMetadata? _detectedMetadata;

    [ObservableProperty]
    private bool _autoFilterEnabled = true;

    [ObservableProperty]
    private int _selectedCount;

    public TitleSelectionViewModel(
        IMakeMkvService makeMkvService,
        IMetadataService metadataService,
        ITitleFilterService titleFilterService,
        ILogService logService)
    {
        _makeMkvService = makeMkvService;
        _metadataService = metadataService;
        _titleFilterService = titleFilterService;
        _logService = logService;
    }

    [RelayCommand]
    private async Task ScanDiscAsync()
    {
        if (string.IsNullOrWhiteSpace(DriveLetter))
        {
            StatusMessage = "Please enter a drive letter";
            return;
        }

        IsScanning = true;
        StatusMessage = "Scanning disc...";
        Titles.Clear();
        SelectedCount = 0;

        try
        {
            // Scan disc with MakeMKV
            var titles = await _makeMkvService.ScanDiscAsync(DriveLetter);
            _logService.Log($"Found {titles.Count} titles on disc {DriveLetter}");

            if (titles.Count == 0)
            {
                StatusMessage = "No titles found on disc";
                HasScanned = true;
                return;
            }

            // Create DiscInfo
            DiscInfo = new DiscInfo
            {
                DriveLetter = DriveLetter,
                DiscType = DetermineDiscType(DriveLetter),
                VolumeLabel = GetVolumeLabel(DriveLetter)
            };

            // Try to auto-detect metadata
            if (!string.IsNullOrEmpty(DiscInfo.VolumeLabel))
            {
                StatusMessage = "Detecting metadata...";
                DetectedMetadata = await _metadataService.AutoMatchAsync(DiscInfo.VolumeLabel);
                
                if (DetectedMetadata != null)
                {
                    _logService.Log($"Auto-matched: {DetectedMetadata.Title} ({DetectedMetadata.Year})");
                }
            }

            // Convert to SelectableTitleInfo
            foreach (var title in titles)
            {
                var selectableTitle = new SelectableTitleInfo
                {
                    Title = title,
                    IsSelected = false
                };
                
                selectableTitle.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableTitleInfo.IsSelected))
                    {
                        UpdateSelectedCount();
                    }
                };

                Titles.Add(selectableTitle);
            }

            // Apply auto-filter if enabled
            if (AutoFilterEnabled && DetectedMetadata != null)
            {
                await ApplyAutoFilterAsync();
            }
            else
            {
                // Select main feature by default
                var mainFeature = Titles.FirstOrDefault(t => t.Title.IsMainFeature);
                if (mainFeature != null)
                {
                    mainFeature.IsSelected = true;
                }
            }

            StatusMessage = $"Found {Titles.Count} titles";
            HasScanned = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            _logService.LogError("Scan failed", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ApplyAutoFilterAsync()
    {
        if (DetectedMetadata == null || Titles.Count == 0)
            return;

        StatusMessage = "Applying intelligent filter...";

        try
        {
            var titleInfos = Titles.Select(t => t.Title).ToList();
            var filtered = await _titleFilterService.FilterTitlesAsync(
                titleInfos,
                DetectedMetadata.MediaType);

            // Unselect all first
            foreach (var title in Titles)
            {
                title.IsSelected = false;
            }

            // Select filtered titles
            foreach (var filteredTitle in filtered)
            {
                var selectableTitle = Titles.FirstOrDefault(t => t.Title.Index == filteredTitle.Index);
                if (selectableTitle != null)
                {
                    selectableTitle.IsSelected = true;
                }
            }

            _logService.Log($"Auto-filter selected {filtered.Count} of {Titles.Count} titles");
            StatusMessage = $"Auto-filter selected {filtered.Count} titles";
        }
        catch (Exception ex)
        {
            _logService.LogError("Auto-filter failed", ex);
            StatusMessage = "Auto-filter failed";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var title in Titles)
        {
            title.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var title in Titles)
        {
            title.IsSelected = false;
        }
    }

    [RelayCommand]
    private void SelectMainFeature()
    {
        foreach (var title in Titles)
        {
            title.IsSelected = false;
        }

        var mainFeature = Titles.FirstOrDefault(t => t.Title.IsMainFeature);
        if (mainFeature != null)
        {
            mainFeature.IsSelected = true;
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Titles.Count(t => t.IsSelected);
    }

    private DiscType DetermineDiscType(string driveLetter)
    {
        try
        {
            var drivePath = $"{driveLetter}:\\";
            if (Directory.Exists(Path.Combine(drivePath, "BDMV")))
                return DiscType.BluRay;
            if (Directory.Exists(Path.Combine(drivePath, "VIDEO_TS")))
                return DiscType.DVD;
        }
        catch { }

        return DiscType.Unknown;
    }

    private string GetVolumeLabel(string driveLetter)
    {
        try
        {
            var driveInfo = new DriveInfo(driveLetter);
            return driveInfo.VolumeLabel;
        }
        catch
        {
            return string.Empty;
        }
    }

    public List<TitleInfo> GetSelectedTitles()
    {
        return Titles.Where(t => t.IsSelected).Select(t => t.Title).ToList();
    }
}

public partial class SelectableTitleInfo : ObservableObject
{
    [ObservableProperty]
    private TitleInfo _title = null!;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => 
        $"Title {Title.Index}: {Title.Name} ({FormatDuration(Title.Duration)})";

    public string Details =>
        $"{Title.ChapterCount} chapters • {FormatSize(Title.Size)} • {Title.VideoCodec} • {Title.AudioCodec}";

    public string TypeBadge =>
        Title.IsMainFeature ? "Main Feature" : "Extra";

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration:mm\\:ss}"
            : $"{duration:mm\\:ss}";
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
