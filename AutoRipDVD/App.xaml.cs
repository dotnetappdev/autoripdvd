using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using AutoRipDVD.ViewModels;
using AutoRipDVD.Views;
using Newtonsoft.Json;
using Sentry;

namespace AutoRipDVD;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;
    public static AppConfig Config { get; private set; } = new();

    public App()
    {
        Config = LoadAppConfig();
        InitializeSentry(Config.Sentry);
        InitializeComponent();

        // Capture unhandled exceptions from any source
        UnhandledException += (_, e) =>
        {
            SentrySdk.CaptureException(e.Exception);
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            SentrySdk.CaptureException(e.Exception);
            e.SetObserved();
        };

        ComWrappersSupport.InitializeComWrappers();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // ── Infrastructure ──────────────────────────────────────────
                services.AddSingleton<IDatabase, DatabaseService>();

                // ── Core services ───────────────────────────────────────────
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<ToolDetectionService>();
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<ISoundService, SoundService>();
                services.AddSingleton<IDiscDetectionService, DiscDetectionService>();
                services.AddSingleton<IMakeMkvService, MakeMkvService>();

                // ── Disc analysis services ──────────────────────────────────
                services.AddSingleton<IIfoParserService, IfoParserService>();
                services.AddSingleton<ICopyProtectionService, CopyProtectionService>();
                services.AddSingleton<IFfprobeService, FfprobeService>();
                services.AddSingleton<ITranscodePresetService, TranscodePresetService>();
                services.AddSingleton<ISubtitleService, SubtitleService>();
                services.AddSingleton<IDiscAnalyzerService, DiscAnalyzerService>();
                services.AddSingleton<IIsoCreatorService, IsoCreatorService>();
                services.AddSingleton<IDiscPreviewService, DiscPreviewService>();

                // ── Transcoding ─────────────────────────────────────────────
                services.AddSingleton<IHandBrakeService, HandBrakeService>();

                // ── Media pipeline ──────────────────────────────────────────
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
                services.AddTransient<TranscodeViewModel>();
                services.AddTransient<TrackSelectorViewModel>();
                services.AddTransient<SubtitleLanguagePickerViewModel>();

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

        // Seed API keys from appsettings.json if not already configured in the DB
        SeedApiKeys(settings.Settings);

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

    private static AppConfig LoadAppConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { /* non-fatal — fall back to defaults */ }
        return new AppConfig();
    }

    private static void InitializeSentry(AppConfig.SentryConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.Dsn)) return;

        SentrySdk.Init(o =>
        {
            o.Dsn = cfg.Dsn;
            o.Environment = cfg.Environment;
            o.TracesSampleRate = cfg.TracesSampleRate;
            o.AttachStacktrace = cfg.AttachStacktrace;
            o.Release = "autorip-dvd@2.1.0";
        });
    }

    private static void SeedApiKeys(Models.AppSettings s)
    {
        var keys = Config.ApiKeys;
        if (string.IsNullOrEmpty(s.OmdbApiKey) && !string.IsNullOrEmpty(keys.OmdbApiKey))
            s.OmdbApiKey = keys.OmdbApiKey;
        if (string.IsNullOrEmpty(s.TmdbApiKey) && !string.IsNullOrEmpty(keys.TmdbApiKey))
            s.TmdbApiKey = keys.TmdbApiKey;
        if (string.IsNullOrEmpty(s.TvdbApiKey) && !string.IsNullOrEmpty(keys.TvdbApiKey))
            s.TvdbApiKey = keys.TvdbApiKey;
        if (string.IsNullOrEmpty(s.AnidbApiKey) && !string.IsNullOrEmpty(keys.AnidbApiKey))
            s.AnidbApiKey = keys.AnidbApiKey;
    }
}
