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

        ComWrappersSupport.InitializeComWrappers();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // ── Infrastructure ──────────────────────────────────────────
                services.AddSingleton<IDatabase, DatabaseService>();

                // ── Core services ───────────────────────────────────────────
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IDiscDetectionService, DiscDetectionService>();
                services.AddSingleton<IMakeMkvService, MakeMkvService>();
                services.AddSingleton<IHandBrakeService, HandBrakeService>();
                services.AddSingleton<IMetadataService, MetadataService>();
                services.AddSingleton<ITitleFilterService, TitleFilterService>();
                services.AddSingleton<IFileNamingService, FileNamingService>();
                services.AddSingleton<IRipJobQueue, RipJobQueue>();

                // ── ViewModels ──────────────────────────────────────────────
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<LogsViewModel>();
                services.AddTransient<JobsViewModel>();
                services.AddTransient<TitleSelectionViewModel>();

                // ── Views ───────────────────────────────────────────────────
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Host.StartAsync();

        var database = Host.Services.GetRequiredService<IDatabase>();
        await database.InitializeAsync();

        var settings = Host.Services.GetRequiredService<ISettingsService>();
        await settings.LoadSettingsAsync();

        MainWindow = Host.Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();

        var discDetection = Host.Services.GetRequiredService<IDiscDetectionService>();
        await discDetection.StartMonitoringAsync();

        // If auto-rip was enabled, check for already-inserted discs
        if (settings.Settings.AutoRip)
        {
            var discs = await discDetection.GetInsertedDiscsAsync();
            var queue = Host.Services.GetRequiredService<IRipJobQueue>();
            foreach (var disc in discs.Where(d => d.DiscType is DiscType.DVD or DiscType.BluRay))
            {
                var job = new Models.RipJob { Disc = disc, Status = Models.RipStatus.Pending };
                await queue.AddJobAsync(job);
                await queue.ProcessQueueAsync();
            }
        }
    }
}
