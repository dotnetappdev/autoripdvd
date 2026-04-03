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

    /// <summary>Packs alias + path into a single CommandParameter string for the Preview button.</summary>
    private static string GetSoundParam(string alias, string path) => $"{alias}|{path}";
    
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
