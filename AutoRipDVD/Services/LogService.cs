using System.Collections.Concurrent;

namespace AutoRipDVD.Services;

public interface ILogService
{
    event EventHandler<string>? LogAdded;
    Task LogAsync(string message);
    // Convenience/legacy methods
    void Log(string message);
    Task LogErrorAsync(string message, Exception? ex = null);
    void LogError(string message, Exception? ex = null);
    Task<List<string>> GetLogsAsync(int count = 100);
    Task ClearLogsAsync();
}

public class LogService : ILogService
{
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly int _maxLogs = 1000;

    public event EventHandler<string>? LogAdded;

    public Task LogAsync(string message)
    {
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        _logs.Enqueue(logEntry);
        
        // Keep only the last _maxLogs entries
        while (_logs.Count > _maxLogs)
        {
            _logs.TryDequeue(out _);
        }

        LogAdded?.Invoke(this, logEntry);
        return Task.CompletedTask;
    }

    public void Log(string message)
    {
        // Fire-and-forget convenience wrapper
        _ = LogAsync(message);
    }

    public Task LogErrorAsync(string message, Exception? ex = null)
    {
        var full = ex == null ? message : $"{message}: {ex.Message}";
        return LogAsync(full);
    }

    public void LogError(string message, Exception? ex = null)
    {
        _ = LogErrorAsync(message, ex);
    }

    public Task<List<string>> GetLogsAsync(int count = 100)
    {
        var logs = _logs.TakeLast(count).ToList();
        return Task.FromResult(logs);
    }

    public Task ClearLogsAsync()
    {
        _logs.Clear();
        return Task.CompletedTask;
    }
}
