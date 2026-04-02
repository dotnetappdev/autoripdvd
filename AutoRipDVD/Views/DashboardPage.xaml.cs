using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AutoRipDVD.ViewModels;
using AutoRipDVD.Services;

namespace AutoRipDVD.Views;

public sealed partial class DashboardPage : Page
{
    public MainViewModel ViewModel { get; }

    public bool HasNoActiveJobs => !ViewModel.ActiveJobs.Any();

    public DashboardPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
    }

    private async void ManualRipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Create the title selection dialog
        var titleSelectionViewModel = App.Host.Services.GetRequiredService<TitleSelectionViewModel>();
        var dialog = new TitleSelectionDialog(titleSelectionViewModel)
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // User clicked "Start Ripping"
            var selectedTitles = titleSelectionViewModel.GetSelectedTitles();
            
            if (selectedTitles.Count > 0 && titleSelectionViewModel.DiscInfo != null)
            {
                var jobQueue = App.Host.Services.GetRequiredService<IRipJobQueue>();
                await jobQueue.AddManualJobAsync(
                    titleSelectionViewModel.DiscInfo,
                    titleSelectionViewModel.DetectedMetadata,
                    selectedTitles);
            }
        }
    }

    private async void StartRippingButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Enable auto-rip mode
        var settings = App.Host.Services.GetRequiredService<ISettingsService>();
        settings.Settings.AutoRip = true;
        await settings.SaveSettingsAsync();

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Auto-Rip Enabled",
            Content = "Automatic ripping is now enabled. Discs will be ripped automatically when inserted.",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void StopRippingButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Disable auto-rip mode and cancel active jobs
        var settings = App.Host.Services.GetRequiredService<ISettingsService>();
        settings.Settings.AutoRip = false;
        await settings.SaveSettingsAsync();

        var activeJobs = ViewModel.ActiveJobs.Where(j => 
            j.Status != AutoRipDVD.Models.RipStatus.Completed && 
            j.Status != AutoRipDVD.Models.RipStatus.Failed).ToList();

        if (activeJobs.Count > 0)
        {
            foreach (var job in activeJobs)
            {
                job.Status = AutoRipDVD.Models.RipStatus.Cancelled;
                var jobQueue = App.Host.Services.GetRequiredService<IRipJobQueue>();
                await jobQueue.UpdateJobAsync(job);
            }
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Auto-Rip Disabled",
            Content = $"Automatic ripping is now disabled. {activeJobs.Count} active job(s) cancelled.",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void OpenDiscButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Show manual title selection dialog for the currently inserted disc
        var discDetection = App.Host.Services.GetRequiredService<IDiscDetectionService>();
        var discs = await discDetection.GetInsertedDiscsAsync();

        if (discs.Count == 0)
        {
            var errorDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "No Disc Found",
                Content = "Please insert a DVD or Blu-ray disc",
                CloseButtonText = "OK"
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Open manual selection dialog with first disc
        var disc = discs.First();
        var titleSelectionViewModel = App.Host.Services.GetRequiredService<TitleSelectionViewModel>();
        titleSelectionViewModel.DriveLetter = disc.DriveLetter;
        
        var dialog = new TitleSelectionDialog(titleSelectionViewModel)
        {
            XamlRoot = this.XamlRoot
        };

        // Automatically scan the disc
        await titleSelectionViewModel.ScanDiscCommand.ExecuteAsync(null);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var selectedTitles = titleSelectionViewModel.GetSelectedTitles();
            
            if (selectedTitles.Count > 0 && titleSelectionViewModel.DiscInfo != null)
            {
                var jobQueue = App.Host.Services.GetRequiredService<IRipJobQueue>();
                await jobQueue.AddManualJobAsync(
                    titleSelectionViewModel.DiscInfo,
                    titleSelectionViewModel.DetectedMetadata,
                    selectedTitles);
            }
        }
    }

    private async void ViewDiscInfoButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var discDetection = App.Host.Services.GetRequiredService<IDiscDetectionService>();
        var discs = await discDetection.GetInsertedDiscsAsync();

        if (discs.Count == 0)
        {
            var errorDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "No Disc Found",
                Content = "Please insert a DVD or Blu-ray disc",
                CloseButtonText = "OK"
            };
            await errorDialog.ShowAsync();
            return;
        }

        var infoDialog = new DiscInfoDialog(discs.First())
        {
            XamlRoot = this.XamlRoot
        };
        await infoDialog.ShowAsync();
    }
}
