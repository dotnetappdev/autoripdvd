using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoRipDVD.Models;
using AutoRipDVD.Services;
using System.Collections.ObjectModel;

namespace AutoRipDVD.ViewModels;

/// <summary>
/// AnyDVD HD–style language selection panel.
///
/// Displays every subtitle / audio language as a selectable item in a
/// two-column grid. Each item shows:
///   • A coloured circle / flag emoji as visual identifier
///   • The English language name
///   • A checkbox / highlight (selected = red/accent, like AnyDVD HD)
///
/// Selection is saved to AppSettings.PreferredSubtitleLanguages and
/// AppSettings.PreferredAudioLanguages (comma-separated ISO 639-2 codes).
/// </summary>
public partial class SubtitleLanguagePickerViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILogService      _log;

    // ── Observable state ──────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<LanguageItem> _subtitleLanguages = new();
    [ObservableProperty] private ObservableCollection<LanguageItem> _audioLanguages    = new();
    [ObservableProperty] private string _searchQuery    = string.Empty;
    [ObservableProperty] private bool   _hasUnsavedChanges = false;
    [ObservableProperty] private string _subtitleLanguageSummary = string.Empty;
    [ObservableProperty] private string _audioLanguageSummary    = string.Empty;

    // Which tab is active (Subtitles / Audio)
    [ObservableProperty] private int _selectedTabIndex = 0;

    public bool IsSubtitleTab => SelectedTabIndex == 0;
    public bool IsAudioTab    => SelectedTabIndex == 1;

    public ObservableCollection<LanguageItem> FilteredSubtitleLanguages { get; } = new();
    public ObservableCollection<LanguageItem> FilteredAudioLanguages    { get; } = new();

    public SubtitleLanguagePickerViewModel(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log      = log;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public void Initialise()
    {
        var currentSubLangs = ParseLangList(_settings.Settings.PreferredSubtitleLanguages);
        var currentAudLangs = ParseLangList(_settings.Settings.PreferredAudioLanguages);

        SubtitleLanguages.Clear();
        AudioLanguages.Clear();

        foreach (var lang in AllLanguages)
        {
            SubtitleLanguages.Add(new LanguageItem(lang)
            {
                IsSelected = currentSubLangs.Contains(lang.Code639_2)
            });
            AudioLanguages.Add(new LanguageItem(lang)
            {
                IsSelected = currentAudLangs.Contains(lang.Code639_2)
            });
        }

        // Wire change notifications so HasUnsavedChanges updates
        foreach (var item in SubtitleLanguages)
            item.PropertyChanged += (_, _) => { HasUnsavedChanges = true; UpdateSummaries(); };
        foreach (var item in AudioLanguages)
            item.PropertyChanged += (_, _) => { HasUnsavedChanges = true; UpdateSummaries(); };

        ApplyFilter();
        UpdateSummaries();
        HasUnsavedChanges = false;
    }

    // ── Search / filter ───────────────────────────────────────────────────────

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSubtitleTab));
        OnPropertyChanged(nameof(IsAudioTab));
    }

    private void ApplyFilter()
    {
        var q = SearchQuery.Trim().ToLowerInvariant();

        FilteredSubtitleLanguages.Clear();
        foreach (var item in SubtitleLanguages)
            if (q.Length == 0 || item.Language.EnglishName.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || item.Language.NativeName.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || item.Language.Code639_2.Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredSubtitleLanguages.Add(item);

        FilteredAudioLanguages.Clear();
        foreach (var item in AudioLanguages)
            if (q.Length == 0 || item.Language.EnglishName.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || item.Language.NativeName.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || item.Language.Code639_2.Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredAudioLanguages.Add(item);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveAsync()
    {
        var subCodes = SubtitleLanguages.Where(l => l.IsSelected).Select(l => l.Language.Code639_2);
        var audCodes = AudioLanguages.Where(l => l.IsSelected).Select(l => l.Language.Code639_2);

        _settings.Settings.PreferredSubtitleLanguages = string.Join(",", subCodes);
        _settings.Settings.PreferredAudioLanguages    = string.Join(",", audCodes);

        await _settings.SaveSettingsAsync();
        HasUnsavedChanges = false;

        await _log.LogAsync($"Subtitle language prefs saved: {_settings.Settings.PreferredSubtitleLanguages}");
        await _log.LogAsync($"Audio language prefs saved:    {_settings.Settings.PreferredAudioLanguages}");
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        foreach (var item in SubtitleLanguages)
            item.IsSelected = item.Language.Code639_2 == "eng";
        foreach (var item in AudioLanguages)
            item.IsSelected = item.Language.Code639_2 == "eng";

        UpdateSummaries();
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    public void SelectAll()
    {
        var list = IsSubtitleTab ? SubtitleLanguages : AudioLanguages;
        foreach (var item in list) item.IsSelected = true;
        HasUnsavedChanges = true;
        UpdateSummaries();
    }

    [RelayCommand]
    public void SelectNone()
    {
        var list = IsSubtitleTab ? SubtitleLanguages : AudioLanguages;
        foreach (var item in list) item.IsSelected = false;
        HasUnsavedChanges = true;
        UpdateSummaries();
    }

    [RelayCommand]
    public void ToggleLanguage(LanguageItem item)
    {
        item.IsSelected = !item.IsSelected;
        HasUnsavedChanges = true;
        UpdateSummaries();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateSummaries()
    {
        var subSel = SubtitleLanguages.Where(l => l.IsSelected).ToList();
        var audSel = AudioLanguages.Where(l => l.IsSelected).ToList();

        SubtitleLanguageSummary = subSel.Count == 0
            ? "None selected"
            : subSel.Count == SubtitleLanguages.Count
                ? "All languages"
                : string.Join(", ", subSel.Take(4).Select(l => l.Language.EnglishName))
                  + (subSel.Count > 4 ? $" +{subSel.Count - 4} more" : string.Empty);

        AudioLanguageSummary = audSel.Count == 0
            ? "None selected"
            : audSel.Count == AudioLanguages.Count
                ? "All languages"
                : string.Join(", ", audSel.Take(4).Select(l => l.Language.EnglishName))
                  + (audSel.Count > 4 ? $" +{audSel.Count - 4} more" : string.Empty);
    }

    private static HashSet<string> ParseLangList(string csv)
        => new(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
               StringComparer.OrdinalIgnoreCase);

    // ── Full language catalogue (ISO 639-2) ───────────────────────────────────
    // Same coverage as AnyDVD HD's language list + commonly found on discs

    public static readonly IReadOnlyList<LanguageDefinition> AllLanguages = new List<LanguageDefinition>
    {
        new("ara", "Arabic",              "العربية",           "🇸🇦", "#006C35"),
        new("bul", "Bulgarian",           "Български",         "🇧🇬", "#FFFFFF"),
        new("zho", "Chinese (Simplified)","简体中文",           "🇨🇳", "#DE2910"),
        new("chi", "Chinese (Trad.)",     "繁體中文",           "🇹🇼", "#DE2910"),
        new("hrv", "Croatian",            "Hrvatski",           "🇭🇷", "#FF0000"),
        new("ces", "Czech",               "Čeština",            "🇨🇿", "#D7141A"),
        new("dan", "Danish",              "Dansk",              "🇩🇰", "#C60C30"),
        new("nld", "Dutch",               "Nederlands",         "🇳🇱", "#AE1C28"),
        new("eng", "English",             "English",            "🇬🇧", "#012169"),
        new("est", "Estonian",            "Eesti",              "🇪🇪", "#0072CE"),
        new("fin", "Finnish",             "Suomi",              "🇫🇮", "#003580"),
        new("fra", "French",              "Français",           "🇫🇷", "#002395"),
        new("deu", "German",              "Deutsch",            "🇩🇪", "#000000"),
        new("ell", "Greek",               "Ελληνικά",          "🇬🇷", "#0D5EAF"),
        new("heb", "Hebrew",              "עברית",             "🇮🇱", "#0038B8"),
        new("hin", "Hindi",               "हिन्दी",           "🇮🇳", "#FF9933"),
        new("hun", "Hungarian",           "Magyar",             "🇭🇺", "#CE2939"),
        new("ind", "Indonesian",          "Bahasa Indonesia",   "🇮🇩", "#CE1126"),
        new("ita", "Italian",             "Italiano",           "🇮🇹", "#009246"),
        new("jpn", "Japanese",            "日本語",             "🇯🇵", "#BC002D"),
        new("kor", "Korean",              "한국어",             "🇰🇷", "#003478"),
        new("lav", "Latvian",             "Latviešu",           "🇱🇻", "#9E3039"),
        new("lit", "Lithuanian",          "Lietuvių",           "🇱🇹", "#006A44"),
        new("nor", "Norwegian",           "Norsk",              "🇳🇴", "#EF2B2D"),
        new("fas", "Persian",             "فارسی",             "🇮🇷", "#239F40"),
        new("pol", "Polish",              "Polski",             "🇵🇱", "#DC143C"),
        new("por", "Portuguese",          "Português",          "🇵🇹", "#006600"),
        new("por", "Portuguese (Brazil)", "Português (Brasil)", "🇧🇷", "#009C3B"),
        new("ron", "Romanian",            "Română",             "🇷🇴", "#002B7F"),
        new("rus", "Russian",             "Русский",            "🇷🇺", "#D52B1E"),
        new("srp", "Serbian",             "Srpski",             "🇷🇸", "#C6363C"),
        new("slk", "Slovak",              "Slovenčina",         "🇸🇰", "#FFFFFF"),
        new("slv", "Slovenian",           "Slovenščina",        "🇸🇮", "#003DA5"),
        new("spa", "Spanish",             "Español",            "🇪🇸", "#AA151B"),
        new("swe", "Swedish",             "Svenska",            "🇸🇪", "#006AA7"),
        new("tha", "Thai",                "ภาษาไทย",           "🇹🇭", "#A51931"),
        new("tur", "Turkish",             "Türkçe",             "🇹🇷", "#E30A17"),
        new("ukr", "Ukrainian",           "Українська",         "🇺🇦", "#005BBB"),
        new("vie", "Vietnamese",          "Tiếng Việt",         "🇻🇳", "#DA251D"),
        new("cat", "Catalan",             "Català",             "🇪🇸", "#CF2B36"),
        new("msa", "Malay",               "Bahasa Melayu",      "🇲🇾", "#CC0001"),
    }
    .OrderBy(l => l.EnglishName)
    .ToList()
    .AsReadOnly();
}

// ── Language item (per-row observable) ───────────────────────────────────────

public partial class LanguageItem : ObservableObject
{
    public LanguageDefinition Language { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionIndicatorColor))]
    private bool _isSelected;

    /// <summary>Accent / highlight colour matching AnyDVD HD's red selected state.</summary>
    public string SelectionIndicatorColor
        => IsSelected ? "#CC0000" : "Transparent";

    public string FlagEmoji    => Language.FlagEmoji;
    public string EnglishName  => Language.EnglishName;
    public string NativeName   => Language.NativeName;
    public string Iso6392Code  => Language.Code639_2;

    /// <summary>Compact label: "English (eng)"</summary>
    public string DisplayLabel
        => Language.NativeName != Language.EnglishName
            ? $"{Language.EnglishName} — {Language.NativeName}"
            : Language.EnglishName;

    public LanguageItem(LanguageDefinition lang) => Language = lang;
}

// ── Language definition ───────────────────────────────────────────────────────

public record LanguageDefinition(
    string Code639_2,
    string EnglishName,
    string NativeName,
    string FlagEmoji,
    string AccentHex);
