using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AutoRipDVD.Services;
using System.Collections.Generic;
using AutoRipDVD.Models;
using System.Linq;
using System;

namespace AutoRipDVD.Views;

public sealed partial class RenamerDialog : ContentDialog
{
    private readonly ISettingsService _settings;
    private readonly IMetadataService _metadataService;
    private readonly IFileNamingService _namingService;

    private List<string> _files = new();
    private List<MediaMetadata> _candidates = new();
    private string? _selectedFile;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

    private List<(string src, string dst, string? backup)> _pendingMappings = new();
    private List<(string src, string dst, string? backup)> _lastAppliedMappings = new();
    
    private void FilesListView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // show count and clear candidates/previews when selection changes
        var selected = FilesListView.SelectedItems?.Cast<string>().ToList() ?? new List<string>();
        ApiStatusText.Text = $"{selected.Count} file(s) selected";
        CandidatesListView.ItemsSource = null;
        PreviewBox.Text = string.Empty;
        _candidates.Clear();
        _selectedFile = null;
    }

    public RenamerDialog()
    {
        this.InitializeComponent();
        _settings = App.Host.Services.GetRequiredService<ISettingsService>();
        _metadataService = App.Host.Services.GetRequiredService<IMetadataService>();
        _namingService = App.Host.Services.GetRequiredService<IFileNamingService>();
        Loaded += RenamerDialog_Loaded;
    }

    private void RenamerDialog_Loaded(object? sender, RoutedEventArgs e)
    {
        var s = _settings.Settings;
        var keys = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.TmdbApiKey)) keys.Add("TMDb: configured"); else keys.Add("TMDb: not configured");
        if (!string.IsNullOrWhiteSpace(s.OmdbApiKey)) keys.Add("OMDb: configured"); else keys.Add("OMDb: not configured");
        if (!string.IsNullOrWhiteSpace(s.TvdbApiKey)) keys.Add("TVDB: configured"); else keys.Add("TVDB: not configured");
        if (!string.IsNullOrWhiteSpace(s.OmdbApiKey)) keys.Add("IMDb via OMDb: available if OMDb key present");

        KeysText.Text = string.Join("\n", keys);
    }

    private async Task SearchCandidatesForNameAsync(string nameOnly)
    {
        ApiStatusText.Text = "Searching metadata sources...";
        try
        {
            // Try AutoMatch first
            var auto = await _metadataService.AutoMatchAsync(nameOnly);
            var list = await _metadataService.SearchAsync(nameOnly);

            _candidates = new List<MediaMetadata>();
            if (auto != null) _candidates.Add(auto);
            foreach (var l in list)
                if (!_candidates.Any(c => c.ImdbId == l.ImdbId && c.Title == l.Title))
                    _candidates.Add(l);

            // Improve scoring: sort by Rating desc then Year proximity
            _candidates = _candidates.OrderByDescending(c => c.Rating ?? 0.0).ThenByDescending(c => c.Year ?? 0).ToList();

            CandidatesListView.ItemsSource = _candidates;
            ApiStatusText.Text = $"Found {_candidates.Count} candidates";
        }
        catch (Exception ex)
        {
            ApiStatusText.Text = "Search failed: " + ex.Message;
        }
    }

    private void CandidatesListView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CandidatesListView.SelectedItem is MediaMetadata meta)
        {
            // use first selected file for preview purpose
            var firstSelected = FilesListView.SelectedItems?.Cast<string>().FirstOrDefault();
            var fileForPreview = firstSelected ?? _files.FirstOrDefault();
            if (fileForPreview == null) return;

            _selectedFile = fileForPreview;
            var ext = System.IO.Path.GetExtension(fileForPreview) ?? ".mkv";
            string outputRoot = OutputRootBox.Text;
            if (string.IsNullOrWhiteSpace(outputRoot))
                outputRoot = _settings.Settings.DashboardOutputPath.IfEmpty(_settings.Settings.OutputPath);

            string proposed = meta.Type == MediaType.TVShow
                ? _namingService.GetTVShowEpisodeFilePath(meta, outputRoot, 1, ext)
                : _namingService.GetMovieFilePath(meta, outputRoot, ext);

            PreviewBox.Text = proposed;
        }
    }

    private void AddMappingButton_Click(object? sender, RoutedEventArgs e)
    {
        var selectedFiles = FilesListView.SelectedItems?.Cast<string>().ToList();
        if (selectedFiles == null || selectedFiles.Count == 0) return;
        if (CandidatesListView.SelectedItem is not MediaMetadata meta) return;

        string outputRoot = OutputRootBox.Text;
        if (string.IsNullOrWhiteSpace(outputRoot))
            outputRoot = _settings.Settings.DashboardOutputPath.IfEmpty(_settings.Settings.OutputPath);

        foreach (var fileName in selectedFiles)
        {
            var fullSrc = _files.FirstOrDefault(f => System.IO.Path.GetFileName(f) == fileName) ?? fileName;
            var ext = System.IO.Path.GetExtension(fullSrc) ?? ".mkv";
            string proposed = meta.Type == MediaType.TVShow
                ? _namingService.GetTVShowEpisodeFilePath(meta, outputRoot, 1, ext)
                : _namingService.GetMovieFilePath(meta, outputRoot, ext);

            _pendingMappings.Add((fullSrc, proposed, null));
        }

        MappingsListView.ItemsSource = _pendingMappings.Select(m => $"{System.IO.Path.GetFileName(m.src)} → {m.dst}").ToList();
    }

    private void MappingsListView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MappingsListView.SelectedIndex >= 0 && MappingsListView.SelectedIndex < _pendingMappings.Count)
        {
            var mapping = _pendingMappings[MappingsListView.SelectedIndex];
            SelectedMappingEditBox.Text = mapping.dst;
        }
    }

    private void UpdateMappingButton_Click(object? sender, RoutedEventArgs e)
    {
        if (MappingsListView.SelectedIndex < 0 || MappingsListView.SelectedIndex >= _pendingMappings.Count) return;
        var idx = MappingsListView.SelectedIndex;
        var current = _pendingMappings[idx];
        var updatedDst = SelectedMappingEditBox.Text?.Trim() ?? current.dst;
        _pendingMappings[idx] = (current.src, updatedDst, current.backup);
        MappingsListView.ItemsSource = _pendingMappings.Select(m => $"{System.IO.Path.GetFileName(m.src)} → {m.dst}").ToList();
    }

    private async void ApplyMappingsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pendingMappings.Count == 0)
        {
            var dlg = new ContentDialog { Title = "No mappings", Content = "No pending mappings to apply.", CloseButtonText = "OK", XamlRoot = XamlRoot };
            _ = dlg.ShowAsync();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Apply mappings",
            Content = $"Apply {_pendingMappings.Count} rename(s)?",
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        var res = await confirm.ShowAsync();
        if (res != ContentDialogResult.Primary) return;

        _lastAppliedMappings = new List<(string, string, string?)>();

        foreach (var (src, dst, _) in _pendingMappings)
        {
            try
            {
                var backup = (string?)null;
                var dir = System.IO.Path.GetDirectoryName(dst);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir!);
                if (System.IO.File.Exists(dst))
                {
                    backup = dst + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                    System.IO.File.Move(dst, backup);
                }
                System.IO.File.Move(src, dst);
                _lastAppliedMappings.Add((src, dst, backup));
            }
            catch (Exception ex)
            {
                var err = new ContentDialog { Title = "Error", Content = "Failed to rename: " + ex.Message, CloseButtonText = "OK", XamlRoot = XamlRoot };
                _ = err.ShowAsync();
            }
        }

        _pendingMappings.Clear();
        MappingsListView.ItemsSource = null;

        var done = new ContentDialog { Title = "Done", Content = "Rename(s) applied.", CloseButtonText = "OK", XamlRoot = XamlRoot };
        _ = done.ShowAsync();
    }

    private async void UndoLastButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastAppliedMappings == null || _lastAppliedMappings.Count == 0)
        {
            var dlg = new ContentDialog { Title = "Nothing to undo", Content = "No previous rename batch to undo.", CloseButtonText = "OK", XamlRoot = XamlRoot };
            _ = dlg.ShowAsync();
            return;
        }

        foreach (var (src, dst, backup) in _lastAppliedMappings)
        {
            try
            {
                if (System.IO.File.Exists(dst))
                {
                    var dir = System.IO.Path.GetDirectoryName(src);
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir!);
                    System.IO.File.Move(dst, src);
                }
                if (!string.IsNullOrWhiteSpace(backup) && System.IO.File.Exists(backup))
                {
                    // restore backup to destination
                    System.IO.File.Move(backup, dst);
                }
            }
            catch { }
        }

        _lastAppliedMappings.Clear();
        var done = new ContentDialog { Title = "Undo complete", Content = "Last rename batch has been undone.", CloseButtonText = "OK", XamlRoot = XamlRoot };
        _ = done.ShowAsync();
    }

    private static string FormatCandidate(MediaMetadata c)
        => c.Type == MediaType.TVShow ? $"[TV] {c.SeriesName} ({c.Year})" : $"[Movie] {c.Title} ({c.Year})";

    private void UseDashboardOutput_Click(object? sender, RoutedEventArgs e)
    {
        OutputRootBox.Text = _settings.Settings.DashboardOutputPath;
    }

    private async void SelectFolderBtn_Click(object? sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;

        SelectedFolderBox.Text = folder.Path;

        // Enumerate files (non-recursive) and show a simple preview
        try
        {
            _files = System.IO.Directory.GetFiles(folder.Path).OrderBy(n => n).ToList();
            var names = _files.Select(f => System.IO.Path.GetFileName(f)).ToList();
            FilesListView.ItemsSource = names;
        }
        catch (Exception ex)
        {
            FilesListView.ItemsSource = new List<string> { "Error listing files: " + ex.Message };
        }
    }

    protected override void OnPrimaryButtonClick(ContentDialogButtonClickEventArgs args)
    {
        // Apply selected renames/moves
        args.Cancel = true; // we'll close manually after completion

        // Build rename map: for each selected file and selected candidate, compute target path
        var map = new List<(string src, string dst)>();
        if (FilesListView.ItemsSource == null) return;

        try
        {
            var selectedIndex = FilesListView.SelectedIndex;
            if (selectedIndex < 0)
            {
                var dlg = new ContentDialog { Title = "No file selected", Content = "Select a file and candidate to apply.", CloseButtonText = "OK", XamlRoot = XamlRoot };
                _ = dlg.ShowAsync();
                return;
            }

            var fileName = FilesListView.Items[selectedIndex] as string;
            if (fileName == null) return;
            var fullSrc = _files[selectedIndex];

            if (CandidatesListView.SelectedIndex < 0)
            {
                var dlg = new ContentDialog { Title = "No candidate selected", Content = "Please select a match candidate for the selected file.", CloseButtonText = "OK", XamlRoot = XamlRoot };
                _ = dlg.ShowAsync();
                return;
            }

            var meta = _candidates[CandidatesListView.SelectedIndex];
            var ext = System.IO.Path.GetExtension(fullSrc) ?? ".mkv";
            string outputRoot = OutputRootBox.Text;
            if (string.IsNullOrWhiteSpace(outputRoot))
                outputRoot = _settings.Settings.DashboardOutputPath.IfEmpty(_settings.Settings.OutputPath);

            string proposed;
            if (meta.Type == MediaType.TVShow)
                proposed = _namingService.GetTVShowEpisodeFilePath(meta, outputRoot, 1, ext);
            else
                proposed = _namingService.GetMovieFilePath(meta, outputRoot, ext);

            map.Add((fullSrc, proposed));

            // Confirm with user
            var confirm = new ContentDialog
            {
                Title = "Confirm Rename",
                Content = $"Rename {System.IO.Path.GetFileName(fullSrc)} → {proposed}",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = XamlRoot
            };
            var res = await confirm.ShowAsync();
            if (res != ContentDialogResult.Primary) return;

            // Perform rename/move
            foreach (var (src, dst) in map)
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(dst);
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir!);
                    if (System.IO.File.Exists(dst))
                    {
                        // choose to overwrite with a timestamped backup
                        var bak = dst + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        System.IO.File.Move(dst, bak);
                    }
                    System.IO.File.Move(src, dst);
                }
                catch (Exception ex)
                {
                    var err = new ContentDialog { Title = "Error", Content = "Failed to rename: " + ex.Message, CloseButtonText = "OK", XamlRoot = XamlRoot };
                    _ = err.ShowAsync();
                }
            }

            var done = new ContentDialog { Title = "Done", Content = "Rename(s) applied.", CloseButtonText = "OK", XamlRoot = XamlRoot };
            _ = done.ShowAsync();
            Hide();
        }
        finally
        {
            _ = _settings.SaveSettingsAsync();
        }
    }
}
