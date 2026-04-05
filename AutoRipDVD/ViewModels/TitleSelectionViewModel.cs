using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;

namespace AutoRipDVD.ViewModels;

public partial class TitleSelectionViewModel : ObservableObject
{
    private readonly IMakeMkvService    _makeMkvService;
    private readonly IMetadataService   _metadataService;
    private readonly ITitleFilterService _titleFilterService;
    private readonly ISettingsService   _settings;
    private readonly ILogService        _logService;

    // ── Scan inputs ───────────────────────────────────────────────────────────

    /// <summary>Pre-set by the dashboard Movie/TV toggle before the dialog opens.</summary>
    public MediaType DefaultMediaType { get; set; } = MediaType.Movie;

    [ObservableProperty]
    private string _driveLetter = string.Empty;

    [ObservableProperty]
    private DiscInfo? _discInfo;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasScanned;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan";

    [ObservableProperty]
    private MediaMetadata? _detectedMetadata;

    partial void OnDetectedMetadataChanged(MediaMetadata? value)
    {
        if (value != null)
            IsTvSeries = value.Type == MediaType.TVShow;
    }

    [ObservableProperty]
    private bool _autoFilterEnabled = true;

    // ── Disc tree (MakeMKV-style) ─────────────────────────────────────────────

    /// <summary>Root nodes for the left-panel TreeView (one DiscRootNode per scanned disc).</summary>
    [ObservableProperty]
    private ObservableCollection<DiscRootNode> _discTree = new();

    // ── Right-panel selection state ───────────────────────────────────────────

    [ObservableProperty]
    private DiscTreeNode? _selectedNode;

    [ObservableProperty]
    private string _selectedNodeInfo = "Select an item to see details.";

    /// <summary>Editable name for the selected title (shown in Properties "Name" field).</summary>
    [ObservableProperty]
    private string _selectedNodeName = string.Empty;

    [ObservableProperty]
    private bool _selectedNodeNameEditable = false;

    // ── Output folder display ─────────────────────────────────────────────────

    [ObservableProperty]
    private string _outputFolderDisplay = string.Empty;

    // User-chosen override: treat this disc as a TV series when set, and optionally override output folder
    [ObservableProperty]
    private bool _isTvSeries = false;

    [ObservableProperty]
    private string _outputOverride = string.Empty;

    // ── Selection count (status bar) ──────────────────────────────────────────

    [ObservableProperty]
    private int _selectedTitleCount;

    [ObservableProperty]
    private int _totalTitleCount;

    // ── Flat title list (legacy – kept for filter commands) ───────────────────

    private List<TitleTreeNode> _titleNodes = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public TitleSelectionViewModel(
        IMakeMkvService     makeMkvService,
        IMetadataService    metadataService,
        ITitleFilterService titleFilterService,
        ISettingsService    settings,
        ILogService         logService)
    {
        _makeMkvService     = makeMkvService;
        _metadataService    = metadataService;
        _titleFilterService = titleFilterService;
        _settings           = settings;
        _logService         = logService;

        OutputFolderDisplay = _settings.Settings.OutputPath;
        // Leave output override empty by default; pre-select TV checkbox if detected metadata later
    }

    // ── Scan disc ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ScanDiscAsync()
    {
        if (string.IsNullOrWhiteSpace(DriveLetter))
        {
            StatusMessage = "Please enter a drive letter";
            return;
        }

        IsScanning = true;
        StatusMessage = "Scanning disc…";
        DiscTree.Clear();
        _titleNodes.Clear();
        SelectedNode = null;
        SelectedTitleCount = 0;
        TotalTitleCount    = 0;

        try
        {
            var titles = await _makeMkvService.ScanDiscAsync(DriveLetter);
            _logService.Log($"Found {titles.Count} titles on disc {DriveLetter}");

            if (titles.Count == 0)
            {
                StatusMessage = "No titles found on disc";
                HasScanned = true;
                return;
            }

            // Build DiscInfo
            DiscInfo = new DiscInfo
            {
                DriveLetter = DriveLetter,
                DiscType    = DetermineDiscType(DriveLetter),
                VolumeLabel = GetVolumeLabel(DriveLetter)
            };

            // Try metadata lookup
            if (!string.IsNullOrEmpty(DiscInfo.VolumeLabel))
            {
                StatusMessage = "Detecting metadata…";
                DetectedMetadata = await _metadataService.AutoMatchAsync(DiscInfo.VolumeLabel);
                if (DetectedMetadata != null)
                    _logService.Log($"Auto-matched: {DetectedMetadata.Title} ({DetectedMetadata.Year})");
            }

            // Build the disc tree
            var root = new DiscRootNode(DiscInfo.DiscType, DiscInfo.VolumeLabel);
            BuildTitleNodes(root, titles);
            DiscTree.Add(root);

            // Apply intelligent filter if possible
            if (AutoFilterEnabled && DetectedMetadata != null)
                await ApplyAutoFilterAsync();
            else
                SelectMainFeatureByDefault();

            TotalTitleCount    = _titleNodes.Count;
            UpdateSelectedTitleCount();

            StatusMessage = $"Found {titles.Count} title(s)";
            HasScanned    = true;
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

    // ── Build tree nodes from TitleInfo list ──────────────────────────────────

    private void BuildTitleNodes(DiscRootNode root, List<TitleInfo> titles)
    {
        foreach (var title in titles)
        {
            var titleNode = new TitleTreeNode(title) { IsChecked = false };

            // Chapters sub-node
            if (title.ChapterCount > 0)
                titleNode.Children.Add(new ChaptersNode(title.ChapterCount));

            // Video sub-node
            if (!string.IsNullOrEmpty(title.VideoCodec))
                titleNode.Children.Add(new VideoTrackNode(title.VideoCodec, title.Resolution));

            // Audio track sub-nodes
            for (int i = 0; i < title.AudioTracks.Count; i++)
                titleNode.Children.Add(new AudioTrackNode(i, title.AudioTracks[i]));

            // Subtitle track sub-nodes
            for (int i = 0; i < title.SubtitleTracks.Count; i++)
                titleNode.Children.Add(new SubtitleTrackNode(i, title.SubtitleTracks[i]));

            // Subscribe to checkbox changes for count updates
            titleNode.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DiscTreeNode.IsChecked))
                    UpdateSelectedTitleCount();
            };

            root.Children.Add(titleNode);
            _titleNodes.Add(titleNode);
        }
    }

    // ── Selected node → right panel ───────────────────────────────────────────

    partial void OnSelectedNodeChanged(DiscTreeNode? value)
    {
        if (value == null)
        {
            SelectedNodeInfo         = "Select an item in the tree to see details.";
            SelectedNodeName         = string.Empty;
            SelectedNodeNameEditable = false;
            return;
        }

        SelectedNodeInfo = value.BuildInfoText();

        if (value is TitleTreeNode titleNode)
        {
            SelectedNodeName         = titleNode.Title.Name;
            SelectedNodeNameEditable = true;
        }
        else
        {
            SelectedNodeName         = string.Empty;
            SelectedNodeNameEditable = false;
        }
    }

    partial void OnSelectedNodeNameChanged(string value)
    {
        // Write back edited name to the title's info object
        if (SelectedNode is TitleTreeNode titleNode && !string.IsNullOrEmpty(value))
            titleNode.Title.Name = value;
    }

    // ── Filter commands ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ApplyAutoFilterAsync()
    {
        if (DetectedMetadata == null || _titleNodes.Count == 0) return;

        StatusMessage = "Applying intelligent filter…";

        try
        {
            var titleInfos = _titleNodes.Select(n => n.Title).ToList();
            var filtered   = await _titleFilterService.FilterTitlesAsync(
                titleInfos, DetectedMetadata.MediaType);

            foreach (var node in _titleNodes)
                node.IsChecked = false;

            foreach (var fi in filtered)
            {
                var match = _titleNodes.FirstOrDefault(n => n.Title.Index == fi.Index);
                if (match != null) match.IsChecked = true;
            }

            _logService.Log($"Auto-filter selected {filtered.Count} of {_titleNodes.Count} titles");
            StatusMessage = $"Auto-filter selected {filtered.Count} title(s)";
        }
        catch (Exception ex)
        {
            _logService.LogError("Auto-filter failed", ex);
            StatusMessage = "Auto-filter failed";
        }

        UpdateSelectedTitleCount();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var node in _titleNodes) node.IsChecked = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var node in _titleNodes) node.IsChecked = false;
    }

    [RelayCommand]
    private void SelectMainFeature()
    {
        foreach (var node in _titleNodes) node.IsChecked = false;
        var main = _titleNodes.FirstOrDefault(n => n.Title.IsMainFeature);
        if (main != null) main.IsChecked = true;
    }

    private void SelectMainFeatureByDefault()
    {
        var main = _titleNodes.FirstOrDefault(n => n.Title.IsMainFeature);
        if (main != null) main.IsChecked = true;
    }

    private void UpdateSelectedTitleCount()
        => SelectedTitleCount = _titleNodes.Count(n => n.IsChecked);

    // ── Public result accessor ────────────────────────────────────────────────

    /// <summary>Returns titles that are checked, with per-title track selections.</summary>
    public List<TitleInfo> GetSelectedTitles()
        => _titleNodes
            .Where(n => n.IsChecked)
            .Select(n => n.Title)
            .ToList();

    /// <summary>
    /// Returns a dictionary mapping title index → lists of checked audio/subtitle track indices.
    /// Used to pass granular track selections into the rip job.
    /// </summary>
    public Dictionary<int, (List<int> AudioIndices, List<int> SubtitleIndices)> GetSelectedTrackIndices()
    {
        var result = new Dictionary<int, (List<int>, List<int>)>();
        foreach (var titleNode in _titleNodes.Where(n => n.IsChecked))
        {
            var audio    = titleNode.AudioNodes
                               .Where(a => a.IsChecked)
                               .Select(a => a.TrackIndex)
                               .ToList();
            var subtitle = titleNode.SubtitleNodes
                               .Where(s => s.IsChecked)
                               .Select(s => s.TrackIndex)
                               .ToList();
            result[titleNode.Title.Index] = (audio, subtitle);
        }
        return result;
    }

    // ── Drive helpers ─────────────────────────────────────────────────────────

    private static DiscType DetermineDiscType(string driveLetter)
    {
        try
        {
            var root = $"{driveLetter.TrimEnd(':', '\\')}:\\";
            if (Directory.Exists(Path.Combine(root, "BDMV")))   return DiscType.BluRay;
            if (Directory.Exists(Path.Combine(root, "VIDEO_TS"))) return DiscType.DVD;
        }
        catch { }
        return DiscType.Unknown;
    }

    private static string GetVolumeLabel(string driveLetter)
    {
        try { return new DriveInfo(driveLetter).VolumeLabel; }
        catch { return string.Empty; }
    }
}

// ── Legacy flat SelectableTitleInfo (kept for any callers that reference it) ──

public partial class SelectableTitleInfo : ObservableObject
{
    [ObservableProperty] private TitleInfo _title = null!;
    [ObservableProperty] private bool _isSelected;

    public string DisplayName =>
        $"Title {Title.Index}: {Title.Name} ({FormatDuration(Title.Duration)})";

    public string Details =>
        $"{Title.ChapterCount} chapters • {FormatSize(Title.SizeBytes)} • {Title.VideoCodec} • {Title.AudioCodec}";

    public string TypeBadge => Title.IsMainFeature ? "Main Feature" : "Extra";

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration:mm\\:ss}"
            : $"{duration:mm\\:ss}";

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }
}
