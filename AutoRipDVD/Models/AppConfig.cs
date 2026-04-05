namespace AutoRipDVD.Models;

public class AppConfig
{
    public SentryConfig Sentry { get; set; } = new();
    public ApiKeysConfig ApiKeys { get; set; } = new();

    public class SentryConfig
    {
        public string Dsn { get; set; } = string.Empty;
        public string Environment { get; set; } = "production";
        public double TracesSampleRate { get; set; } = 0.1;
        public bool AttachStacktrace { get; set; } = true;
    }

    public class ApiKeysConfig
    {
        public string OmdbApiKey { get; set; } = string.Empty;
        public string TmdbApiKey { get; set; } = string.Empty;
        public string TvdbApiKey { get; set; } = string.Empty;
        public string AnidbApiKey { get; set; } = string.Empty;
    }
}
