namespace AutoRipDVD.Services;

public interface INotificationService
{
    Task SendAsync(string title, string message);
}

public class NotificationService : INotificationService
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _httpClient;

    public NotificationService(ISettingsService settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
    }

    public async Task SendAsync(string title, string message)
    {
        if (!_settings.Settings.EnableNotifications)
            return;

        if (string.IsNullOrEmpty(_settings.Settings.NotificationWebhook))
            return;

        try
        {
            // Generic webhook format (can be adapted for different services)
            var payload = new
            {
                title,
                message,
                timestamp = DateTime.Now
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            await _httpClient.PostAsync(_settings.Settings.NotificationWebhook, content);
        }
        catch
        {
            // Silently fail notifications
        }
    }
}
