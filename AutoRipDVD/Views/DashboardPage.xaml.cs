using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using AutoRipDVD.ViewModels;
using AutoRipDVD.Services;
using AutoRipDVD.Models;

namespace AutoRipDVD.Views;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}


public class BoolToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public class BoolInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public sealed partial class DashboardPage : Page
{
    public MainViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object? sender, RoutedEventArgs e)
    {
        // Initialize view model and then populate recent folders dropdown
        await ViewModel.InitializeAsync();
        try
        {
            var settings = App.Host.Services.GetRequiredService<ISettingsService>().Settings;
            RecentFoldersCombo.ItemsSource = settings.RecentOutputFolders ?? new List<string>();
        }
        catch { }
    }

    private async void StartRippingButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var settings = App.Host.Services.GetRequiredService<ISettingsService>();
        settings.Settings.AutoRip = true;
        await settings.SaveSettingsAsync();

        // Tell the queue to start accepting auto-rip jobs
        App.Host.Services.GetRequiredService<IRipJobQueue>().EnableAutoRip();

        // Check for discs already in the drive
        var discDetection = App.Host.Services.GetRequiredService<IDiscDetectionService>();
        var discs = await discDetection.GetInsertedDiscsAsync();
        var queue = App.Host.Services.GetRequiredService<IRipJobQueue>();

        foreach (var disc in discs.Where(d => d.DiscType is DiscType.DVD or DiscType.BluRay))
        {
            var job = new RipJob { Disc = disc, Status = RipStatus.Pending };
            await queue.AddJobAsync(job);
        }

        await queue.ProcessQueueAsync();

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Auto-Rip Enabled",
            Content = "Discs will be ripped automatically when inserted.",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void StopRippingButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var settings = App.Host.Services.GetRequiredService<ISettingsService>();
        settings.Settings.AutoRip = false;
        await settings.SaveSettingsAsync();

        var queue = App.Host.Services.GetRequiredService<IRipJobQueue>();

        // Cancel all in-progress jobs
        var active = await queue.GetActiveJobsAsync();
        queue.CancelAll();

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Auto-Rip Stopped",
            Content = $"Auto-rip disabled. {active.Count} active job(s) will be cancelled.",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void ManualRipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var vm = App.Host.Services.GetRequiredService<TitleSelectionViewModel>();
        var dialog = new TitleSelectionDialog(vm) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
            await StartManualRipAsync(vm);
    }

    private void ContentType_Checked(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Update ViewModel based on which radio is selected
        ViewModel.IsMovieType = MovieRadio.IsChecked == true;
    }

    private async void BrowseOutputPath_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Show a folder picker and set the OutputPathOverride on the ViewModel
        var picker = new Windows.Storage.Pickers.FolderPicker();
        // WinUI3 desktop requires initializing the picker with a window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ViewModel.OutputPathOverride = folder.Path;

            // Persist dashboard quick-output selection to settings so the queue can use it
            try
            {
                var settings = App.Host.Services.GetRequiredService<ISettingsService>();
                settings.Settings.DashboardOutputPath = folder.Path;
                // update recent folders list (most-recent first, unique, max 10)
                var recent = settings.Settings.RecentOutputFolders ?? new List<string>();
                recent.RemoveAll(x => string.Equals(x, folder.Path, StringComparison.OrdinalIgnoreCase));
                recent.Insert(0, folder.Path);
                if (recent.Count > 10) recent = recent.Take(10).ToList();
                settings.Settings.RecentOutputFolders = recent;
                await settings.SaveSettingsAsync();

                RecentFoldersCombo.ItemsSource = recent;
            }
            catch { }
        }
        }

    private async void RecentFoldersCombo_SelectionChanged(object? sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (RecentFoldersCombo.SelectedItem is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var settings = App.Host.Services.GetRequiredService<ISettingsService>();
                settings.Settings.DashboardOutputPath = path;
                await settings.SaveSettingsAsync();
                ViewModel.OutputPathOverride = path;
            }
            catch { }
        }
    }

    private async void MoviesOutputPathBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        var path = ViewModel.MoviesOutputPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (System.IO.Directory.Exists(path)) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Folder does not exist",
            Content = $"The folder '{path}' does not exist. Create it?",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel"
        };
        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            try { System.IO.Directory.CreateDirectory(path); }
            catch { }
        }
        else
        {
            ViewModel.MoviesOutputPath = string.Empty;
        }
    }

    private async void TvOutputPathBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        var path = ViewModel.TvOutputPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (System.IO.Directory.Exists(path)) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Folder does not exist",
            Content = $"The folder '{path}' does not exist. Create it?",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel"
        };
        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            try { System.IO.Directory.CreateDirectory(path); }
            catch { }
        }
        else
        {
            ViewModel.TvOutputPath = string.Empty;
        }
    }

    private async void BrowseMoviesOutputPath_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ViewModel.MoviesOutputPath = folder.Path;
        }
    }

    private async void BrowseTvOutputPath_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ViewModel.TvOutputPath = folder.Path;
        }
    }

    private async void OpenRenamer_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new RenamerDialog();
        dialog.XamlRoot = XamlRoot;
        await dialog.ShowAsync();
    }
    

    private async void ViewDiscInfoButton_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var driveLetter = ViewModel.DetectedDriveLetter;
        if (string.IsNullOrEmpty(driveLetter))
        {
            await ShowInfoDialogAsync("No Disc Found", "Please insert a DVD or Blu-ray disc.");
            return;
        }

        var discDetection = App.Host.Services.GetRequiredService<IDiscDetectionService>();
        var discs = await discDetection.GetInsertedDiscsAsync();
        var disc = discs.FirstOrDefault(d => d.DriveLetter == driveLetter);

        string content;
        if (disc != null)
        {
            content = $"Drive: {disc.DriveLetter}\nVolume: {disc.VolumeLabel}\nType: {disc.DiscType}";
        }
        else
        {
            content = $"Drive: {driveLetter}";
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Disc Info",
            Content = content,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void OpenDiscButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var driveLetter = ViewModel.DetectedDriveLetter;
        if (string.IsNullOrEmpty(driveLetter))
        {
            await ShowInfoDialogAsync("No Disc Found", "Please insert a DVD or Blu-ray disc.");
            return;
        }

        var vm = App.Host.Services.GetRequiredService<TitleSelectionViewModel>();
        vm.DriveLetter = driveLetter;

        var dialog = new TitleSelectionDialog(vm) { XamlRoot = XamlRoot };

        // Auto-trigger scan so the tree is ready when the dialog opens
        await vm.ScanDiscCommand.ExecuteAsync(null);

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await StartManualRipAsync(vm);
    }

    private async Task StartManualRipAsync(TitleSelectionViewModel vm)
    {
        var selected = vm.GetSelectedTitles();
        if (selected.Count > 0 && vm.DiscInfo != null)
        {
            var queue = App.Host.Services.GetRequiredService<IRipJobQueue>();
            bool? forceTv = vm.IsTvSeries ? true : (bool?)null;
            string? outOverride = string.IsNullOrWhiteSpace(vm.OutputOverride) ? null : vm.OutputOverride;
            // If no manual override specified in dialog, use dashboard per-media override or dashboard quick output
            if (string.IsNullOrWhiteSpace(outOverride))
            {
                outOverride = ViewModel.IsMovieType && !string.IsNullOrWhiteSpace(ViewModel.MoviesOutputPath)
                    ? ViewModel.MoviesOutputPath
                    : (!ViewModel.IsMovieType && !string.IsNullOrWhiteSpace(ViewModel.TvOutputPath) ? ViewModel.TvOutputPath : null);

                if (string.IsNullOrWhiteSpace(outOverride))
                {
                    // fallback to dashboard quick output
                    var s = App.Host.Services.GetRequiredService<ISettingsService>().Settings;
                    outOverride = string.IsNullOrWhiteSpace(s.DashboardOutputPath) ? null : s.DashboardOutputPath;
                }
            }

            await queue.AddManualJobAsync(vm.DiscInfo, vm.DetectedMetadata, selected, forceTv, outOverride);
        }
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
