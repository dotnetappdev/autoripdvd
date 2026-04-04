using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AutoRipDVD.Services;

namespace AutoRipDVD.Views;

public sealed partial class AboutPage : Page
{
    private readonly ToolDetectionService _tools;
    private readonly ISettingsService _settings;

    public AboutPage()
    {
        this.InitializeComponent();
        _tools = App.Host.Services.GetRequiredService<ToolDetectionService>();
        _settings = App.Host.Services.GetRequiredService<ISettingsService>();
        Loaded += AboutPage_Loaded;
    }

    private async void AboutPage_Loaded(object? s, RoutedEventArgs e)
    {
        try
        {
            var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "(unknown)";
            VersionText.Text = ver;

            var mkPath = _settings.Settings.MakeMkvPath;
            var hbPath = _settings.Settings.HandBrakePath;

            var mkVer = _tools.ProbeVersion(mkPath) ?? _tools.ProbeVersion(_tools.DetectMakeMkvPath() ?? string.Empty) ?? "Not found";
            var hbVer = _tools.ProbeVersion(hbPath) ?? _tools.ProbeVersion(_tools.DetectHandBrakePath() ?? string.Empty) ?? "Not found";

            MakeMkvText.Text = $"MakeMKV: {mkVer} ({mkPath})";
            HandBrakeText.Text = $"HandBrake: {hbVer} ({hbPath})";
        }
        catch
        {
            // ignore
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }
}
