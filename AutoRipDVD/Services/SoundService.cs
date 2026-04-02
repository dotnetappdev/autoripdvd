using AutoRipDVD.Models;
using System.Runtime.InteropServices;

namespace AutoRipDVD.Services;

public enum SoundEvent
{
    RipCompleted,
    DiscEjected,
    RipFailed,
    RipStarted
}

public interface ISoundService
{
    Task PlayAsync(SoundEvent evt);
}

/// <summary>
/// Plays sounds on rip events.
///
/// Priority: custom .wav file path → Windows system sound alias → silent fallback.
/// All playback is asynchronous (SND_ASYNC) so it never blocks the rip pipeline.
/// </summary>
public class SoundService : ISoundService
{
    // winmm.dll PlaySound flags
    private const uint SND_ASYNC     = 0x0001; // play asynchronously
    private const uint SND_NODEFAULT = 0x0002; // silence if sound not found (no system beep)
    private const uint SND_ALIAS     = 0x10000; // pszSound is a Windows event/alias name
    private const uint SND_FILENAME  = 0x20000; // pszSound is a file path

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);

    private readonly ISettingsService _settings;
    private readonly ILogService _logService;

    // Available system sound aliases exposed for UI dropdowns
    public static readonly IReadOnlyList<string> SystemSoundAliases = new[]
    {
        "SystemAsterisk",      // ℹ Information / completion chime
        "SystemExclamation",   // ⚠ Warning
        "SystemHand",          // ✖ Critical stop / error
        "SystemNotification",  // 🔔 Notification
        "MailBeep",            // 📧 Mail
        "SystemQuestion",      // ❓ Question
        "SystemStart",         // Windows start-up sound (may be silent)
    };

    public SoundService(ISettingsService settings, ILogService logService)
    {
        _settings = settings;
        _logService = logService;
    }

    public Task PlayAsync(SoundEvent evt)
    {
        var s = _settings.Settings;

        if (!s.EnableSounds)
            return Task.CompletedTask;

        bool enabled;
        string filePath;
        string alias;

        switch (evt)
        {
            case SoundEvent.RipCompleted:
                enabled  = s.PlaySoundOnCompletion;
                filePath = s.CompletionSoundPath;
                alias    = s.CompletionSoundAlias.IfEmpty("SystemAsterisk");
                break;
            case SoundEvent.DiscEjected:
                enabled  = s.PlaySoundOnEjection;
                filePath = s.EjectionSoundPath;
                alias    = s.EjectionSoundAlias.IfEmpty("SystemNotification");
                break;
            case SoundEvent.RipFailed:
                enabled  = s.PlaySoundOnError;
                filePath = s.ErrorSoundPath;
                alias    = s.ErrorSoundAlias.IfEmpty("SystemHand");
                break;
            case SoundEvent.RipStarted:
                enabled  = s.PlaySoundOnRipStart;
                filePath = s.RipStartSoundPath;
                alias    = s.RipStartSoundAlias.IfEmpty("SystemExclamation");
                break;
            default:
                return Task.CompletedTask;
        }

        if (!enabled)
            return Task.CompletedTask;

        // Run on a background thread so we never block the caller
        return Task.Run(() => Play(filePath, alias));
    }

    private void Play(string filePath, string alias)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                // Play custom WAV file
                PlaySound(filePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
            }
            else
            {
                // Play Windows system sound by alias
                PlaySound(alias, IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT);
            }
        }
        catch (Exception ex)
        {
            // Never let a sound error bubble up into the rip pipeline
            _ = _logService.LogAsync($"[Sound] Play failed: {ex.Message}");
        }
    }

    /// <summary>Preview a sound immediately — used from the Settings UI.</summary>
    public void Preview(string filePath, string alias)
    {
        Play(filePath, alias);
    }
}
