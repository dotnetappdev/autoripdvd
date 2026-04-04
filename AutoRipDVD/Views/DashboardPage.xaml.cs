using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using AutoRipDVD.ViewModels;
using AutoRipDVD.Services;
using AutoRipDVD.Models;

namespace AutoRipDVD.Views;

// ── Value converters used by DashboardPage ─────────────────────────────────────

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

public sealed partial class DashboardPage : Page
{
    public MainViewModel ViewModel { get; }

    public bool HasNoActiveJobs => !ViewModel.ActiveJobs.Any();

    public DashboardPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
    }

    // ── Auto-Rip Start / Stop ─────────────────────────────────────────────────

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
            XamlRoot     = XamlRoot,
            Title        = "Auto-Rip Enabled",
            Content      = "Discs will be ripped automatically when inserted.",
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
            XamlRoot     = XamlRoot,
            Title        = "Auto-Rip Stopped",
            Content      = $"Auto-rip disabled. {active.Count} active job(s) will be cancelled.",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    // ── Manual Rip ────────────────────────────────────────────────────────────

    private async void ManualRipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var vm = App.Host.Services.GetRequiredService<TitleSelectionViewModel>();
        var dialog = new TitleSelectionDialog(vm) { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
            await StartManualRipAsync(vm);
    }

    private async void OpenDiscButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var discDetection = App.Host.Services.GetRequiredService<IDiscDetectionService>();
        var discs = await discDetection.GetInsertedDiscsAsync();

        if (discs.Count == 0)
        {
            await ShowInfoDialogAsync("No Disc Found", "Please insert a DVD or Blu-ray disc.");
            return;
        }

        var disc = discs.First(d => d.DiscType is DiscType.DVD or DiscType.BluRay)
                   ?? discs.First();

        var vm = App.Host.Services.GetRequiredService<TitleSelectionViewModel>();
        vm.DriveLetter = disc.DriveLetter;

        var dialog = new TitleSelectionDialog(vm) { XamlRoot = XamlRoot };

        // Pre-scan
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
            await queue.AddManualJobAsync(vm.DiscInfo, vm.DetectedMetadata, selected);
        }
    }

    // ── Disc Info ─────────────────────────────────────────────────────────────

    private async void ViewDiscInfoButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var discDetection = App.Host.Services.GetRequiredService<IDiscDetectionService>();
        var discs = await discDetection.GetInsertedDiscsAsync();

        if (discs.Count == 0)
        {
            await ShowInfoDialogAsync("No Disc Found", "Please insert a DVD or Blu-ray disc.");
            return;
        }

        var infoDialog = new DiscInfoDialog(discs.First()) { XamlRoot = XamlRoot };
        await infoDialog.ShowAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot        = XamlRoot,
            Title           = title,
            Content         = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
