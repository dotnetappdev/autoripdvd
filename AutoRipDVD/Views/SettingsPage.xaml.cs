using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AutoRipDVD.ViewModels;

namespace AutoRipDVD.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    private void AboutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Frame.Navigate(typeof(AboutPage));
    }

    private async void BrowseFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Controls.Button b || ViewModel == null) return;
        var tag = b.Tag as string ?? string.Empty;

        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            // WinUI3 requires initializing the picker with a window handle
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }
            catch { /* best-effort */ }

            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;

            switch (tag)
            {
                case "OutputBase": ViewModel.OutputBasePath = folder.Path; break;
                case "TempPath": ViewModel.TempPath = folder.Path; break;
                case "MoviesOutputPath": ViewModel.MoviesOutputPath = folder.Path; break;
                case "TvOutputPath": ViewModel.TvOutputPath = folder.Path; break;
                case "MusicOutputPath": ViewModel.MusicOutputPath = folder.Path; break;
            }
        }
        catch
        {
            var dlg = new ContentDialog
            {
                Title = "Folder picker unavailable",
                Content = "No system folder picker could be opened. Please paste the path manually.",
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            _ = await dlg.ShowAsync();
        }
    }

    /// <summary>Packs alias + path into a single CommandParameter string for the Preview button.</summary>
    private string GetSoundParam(string alias, string path) => $"{alias}|{path}";
    
    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (ViewModel.HasChanges)
        {
            // Cancel navigation temporarily
            e.Cancel = true;
            
            // Ask user what to do with unsaved changes
            bool canNavigate = await ViewModel.ConfirmDiscardChangesAsync(XamlRoot);
            
            if (canNavigate)
            {
                // User saved or discarded changes, allow navigation
                Frame.Navigate(e.SourcePageType, e.Parameter);
            }
            // else: User cancelled, stay on settings page
        }
        
        base.OnNavigatingFrom(e);
    }
}
