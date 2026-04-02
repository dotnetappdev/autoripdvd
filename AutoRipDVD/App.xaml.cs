using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using AutoRipDVD.Services;
using AutoRipDVD.ViewModels;
using AutoRipDVD.Views;

namespace AutoRipDVD;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        
        // Initialize WindowsAppSDK for unpackaged deployment
        ComWrappersSupport.InitializeComWrappers();
        
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Database
                services.AddSingleton<IDatabase, DatabaseService>();
                
                // Services
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IDiscDetectionService, DiscDetectionService>();
                services.AddSingleton<IMakeMkvService, MakeMkvService>();
                services.AddSingleton<IHandBrakeService, HandBrakeService>();
                services.AddSingleton<IMetadataService, MetadataService>();
                services.AddSingleton<ITitleFilterService, TitleFilterService>();
                services.AddSingleton<IRipJobQueue, RipJobQueue>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<ILogService, LogService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<LogsViewModel>();
                services.AddTransient<JobsViewModel>();
                services.AddTransient<TitleSelectionViewModel>();

                // Views
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Host.StartAsync();

        // Initialize database
        var database = Host.Services.GetRequiredService<IDatabase>();
        await database.InitializeAsync();

        // Load settings
        var settings = Host.Services.GetRequiredService<ISettingsService>();
        await settings.LoadSettingsAsync();

        MainWindow = Host.Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();

        // Start disc detection service
        var discDetection = Host.Services.GetRequiredService<IDiscDetectionService>();
        await discDetection.StartMonitoringAsync();
    }
}
