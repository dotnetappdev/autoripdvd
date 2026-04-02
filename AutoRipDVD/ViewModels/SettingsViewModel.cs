using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AutoRipDVD.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHandBrakeService _handBrakeService;
    
    // Store original values for change detection and cancel functionality
    private AppSettings _originalSettings = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _makeMkvPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _handBrakePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _outputBasePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _omdbApiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _tvdbApiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _autoRip;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _ripMainFeatureOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _ejectWhenComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _autoTranscode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private int _minimumTitleLengthSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _handBrakePreset = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private int _quality;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private bool _enableNotifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _notificationWebhook = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private ElementTheme _selectedTheme;

    [ObservableProperty]
    private List<string> _availablePresets = new();
    
    // Track if settings have been modified
    public bool HasChanges => 
        MakeMkvPath != _originalSettings.MakeMkvPath ||
        HandBrakePath != _originalSettings.HandBrakePath ||
        OutputBasePath != _originalSettings.OutputPath ||
        OmdbApiKey != _originalSettings.OmdbApiKey ||
        TvdbApiKey != _originalSettings.TvdbApiKey ||
        AutoRip != _originalSettings.AutoRip ||
        RipMainFeatureOnly != _originalSettings.RipMainFeatureOnly ||
        EjectWhenComplete != _originalSettings.EjectWhenComplete ||
        AutoTranscode != _originalSettings.TranscodeAfterRip ||
        MinimumTitleLengthSeconds != _originalSettings.MinimumTitleLengthSeconds ||
        HandBrakePreset != _originalSettings.HandBrakePreset ||
        Quality != _originalSettings.VideoQuality ||
        EnableNotifications != _originalSettings.EnableNotifications ||
        NotificationWebhook != _originalSettings.NotificationWebhook ||
        GetThemeString() != _originalSettings.Theme;

    public SettingsViewModel(ISettingsService settingsService, IHandBrakeService handBrakeService)
    {
        _settingsService = settingsService;
        _handBrakeService = handBrakeService;

        LoadSettings();
        _ = LoadPresetsAsync();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
        
        // Store original values for change detection
        _originalSettings = new AppSettings
        {
            MakeMkvPath = settings.MakeMkvPath,
            HandBrakePath = settings.HandBrakePath,
            OutputPath = settings.OutputPath,
            OmdbApiKey = settings.OmdbApiKey,
            TvdbApiKey = settings.TvdbApiKey,
            AutoRip = settings.AutoRip,
            RipMainFeatureOnly = settings.RipMainFeatureOnly,
            EjectWhenComplete = settings.EjectWhenComplete,
            TranscodeAfterRip = settings.TranscodeAfterRip,
            MinimumTitleLengthSeconds = settings.MinimumTitleLengthSeconds,
            HandBrakePreset = settings.HandBrakePreset,
            VideoQuality = settings.VideoQuality,
            EnableNotifications = settings.EnableNotifications,
            NotificationWebhook = settings.NotificationWebhook,
            Theme = settings.Theme
        };
        
        MakeMkvPath = settings.MakeMkvPath;
        HandBrakePath = settings.HandBrakePath;
        OutputBasePath = settings.OutputPath;
        OmdbApiKey = settings.OmdbApiKey;
        TvdbApiKey = settings.TvdbApiKey;
        AutoRip = settings.AutoRip;
        RipMainFeatureOnly = settings.RipMainFeatureOnly;
        EjectWhenComplete = settings.EjectWhenComplete;
        AutoTranscode = settings.TranscodeAfterRip;
        MinimumTitleLengthSeconds = settings.MinimumTitleLengthSeconds;
        HandBrakePreset = settings.HandBrakePreset;
        Quality = settings.VideoQuality;
        EnableNotifications = settings.EnableNotifications;
        NotificationWebhook = settings.NotificationWebhook;
        
        SelectedTheme = settings.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private async Task LoadPresetsAsync()
    {
        try
        {
            AvailablePresets = await _handBrakeService.GetPresetsAsync();
        }
        catch
        {
            // Ignore errors
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var settings = _settingsService.Settings;
        
        settings.MakeMkvPath = MakeMkvPath;
        settings.HandBrakePath = HandBrakePath;
        settings.OutputPath = OutputBasePath;
        settings.OmdbApiKey = OmdbApiKey;
        settings.TvdbApiKey = TvdbApiKey;
        settings.AutoRip = AutoRip;
        settings.RipMainFeatureOnly = RipMainFeatureOnly;
        settings.EjectWhenComplete = EjectWhenComplete;
        settings.TranscodeAfterRip = AutoTranscode;
        settings.MinimumTitleLengthSeconds = MinimumTitleLengthSeconds;
        settings.HandBrakePreset = HandBrakePreset;
        settings.VideoQuality = Quality;
        settings.EnableNotifications = EnableNotifications;
        settings.NotificationWebhook = NotificationWebhook;
        settings.Theme = GetThemeString();

        await _settingsService.SaveSettingsAsync();
        
        // Update original settings to reflect saved state
        LoadSettings();
    }
    
    [RelayCommand]
    private void Cancel()
    {
        // Restore original values
        MakeMkvPath = _originalSettings.MakeMkvPath;
        HandBrakePath = _originalSettings.HandBrakePath;
        OutputBasePath = _originalSettings.OutputPath;
        OmdbApiKey = _originalSettings.OmdbApiKey;
        TvdbApiKey = _originalSettings.TvdbApiKey;
        AutoRip = _originalSettings.AutoRip;
        RipMainFeatureOnly = _originalSettings.RipMainFeatureOnly;
        EjectWhenComplete = _originalSettings.EjectWhenComplete;
        AutoTranscode = _originalSettings.TranscodeAfterRip;
        MinimumTitleLengthSeconds = _originalSettings.MinimumTitleLengthSeconds;
        HandBrakePreset = _originalSettings.HandBrakePreset;
        Quality = _originalSettings.VideoQuality;
        EnableNotifications = _originalSettings.EnableNotifications;
        NotificationWebhook = _originalSettings.NotificationWebhook;
        
        SelectedTheme = _originalSettings.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
    
    public async Task<bool> ConfirmDiscardChangesAsync(XamlRoot xamlRoot)
    {
        if (!HasChanges)
            return true;

        var dialog = new ContentDialog
        {
            Title = "Unsaved Changes",
            Content = "You have unsaved changes. Do you want to save them before closing?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Discard",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            await ApplyAsync();
            return true;
        }
        else if (result == ContentDialogResult.Secondary)
        {
            Cancel();
            return true;
        }
        
        return false; // User cancelled
    }
    
    private string GetThemeString()
    {
        return SelectedTheme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => "System"
        };
    }

    [RelayCommand]
    private void BrowseMakeMkv()
    {
        // TODO: Implement file picker
    }

    [RelayCommand]
    private void BrowseHandBrake()
    {
        // TODO: Implement file picker
    }
    
    [RelayCommand]
    private void BrowseOutput()
    {
        // TODO: Implement folder picker
    }
}
