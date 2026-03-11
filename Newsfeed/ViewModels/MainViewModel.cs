using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Newsfeed.Models;
using Newsfeed.Services;

namespace Newsfeed.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private const int DefaultRefreshIntervalMinutes = 2;
    private static readonly JsonSerializerOptions SettingsJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DefaultFocusTerms =
    [
        "Iran",
        "Tehran",
        "Israel",
        "Gulf",
        "Middle East",
        "Bahrain",
        "Saudi",
        "Qatar",
        "UAE",
        "Iraq",
        "Lebanon",
        "Syria",
        "Hormuz",
        "missile",
        "drone",
        "strike",
        "airstrike",
        "ceasefire",
        "oil",
        "shipping",
        "nuclear"
    ];

    private readonly FeedService _feedService = new();
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly ObservableCollection<string> _focusTerms;
    private readonly List<TickerModeOption> _modeOptions;
    private readonly List<RefreshIntervalOption> _refreshIntervalOptions;
    private string _statusLine = "Preparing live sources...";
    private string _liveTitleBarSubtitle = "Connecting to live sources...";
    private NewsHeadline? _selectedHeadline;
    private TickerMode _selectedMode;
    private int _headlineCount;
    private int _refreshIntervalMinutes;
    private bool _isNormalizingFocusTerms;
    private bool _isSettingsViewActive;
    private bool _preferCompactTickerLayout;

    public MainViewModel()
    {
        Headlines = [];
        SourceNames = _feedService.DefaultSources.Select(source => source.Name).ToList();
        SuggestedFocusTerms = DefaultFocusTerms
            .Concat(
            [
                "diplomacy",
                "sanctions",
                "energy",
                "ports",
                "Red Sea",
                "Suez",
                "evacuation"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _modeOptions =
        [
            new(TickerMode.ContinuousScroll, "Continuous scroll"),
            new(TickerMode.VerticalSlide, "Vertical slide")
        ];

        _refreshIntervalOptions =
        [
            new(1, "Every minute"),
            new(2, "Every 2 minutes"),
            new(5, "Every 5 minutes"),
            new(10, "Every 10 minutes")
        ];

        var settings = LoadSettings();
        _selectedMode = settings?.SelectedMode ?? TickerMode.ContinuousScroll;
        _refreshIntervalMinutes = NormalizeRefreshInterval(settings?.RefreshIntervalMinutes ?? DefaultRefreshIntervalMinutes);
        _preferCompactTickerLayout = settings?.PreferCompactTickerLayout ?? false;

        _focusTerms = new ObservableCollection<string>(settings?.FocusTerms?.Where(term => !string.IsNullOrWhiteSpace(term)) ?? DefaultFocusTerms);
        _focusTerms.CollectionChanged += FocusTerms_CollectionChanged;
        NormalizeFocusTerms();

        SourcesLine = $"Sources: {string.Join("  •  ", SourceNames)}";

        _refreshTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMinutes(_refreshIntervalMinutes);
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NewsHeadline> Headlines { get; }

    public ObservableCollection<string> FocusTerms => _focusTerms;

    public IReadOnlyList<string> SuggestedFocusTerms { get; }

    public IReadOnlyList<string> SourceNames { get; }

    public IReadOnlyList<TickerModeOption> ModeOptions => _modeOptions;

    public IReadOnlyList<RefreshIntervalOption> RefreshIntervalOptions => _refreshIntervalOptions;

    public TickerModeOption SelectedModeOption
    {
        get => _modeOptions.First(option => option.Value == SelectedMode);
        set
        {
            if (value.Value == SelectedMode)
            {
                return;
            }

            SelectedMode = value.Value;
            OnPropertyChanged();
        }
    }

    public RefreshIntervalOption SelectedRefreshIntervalOption
    {
        get => _refreshIntervalOptions.First(option => option.Minutes == RefreshIntervalMinutes);
        set
        {
            if (value.Minutes == RefreshIntervalMinutes)
            {
                return;
            }

            RefreshIntervalMinutes = value.Minutes;
            OnPropertyChanged();
        }
    }

    public string StatusLine
    {
        get => _statusLine;
        private set => SetProperty(ref _statusLine, value);
    }

    public string TitleBarSubtitle => _isSettingsViewActive
        ? "Settings"
        : _liveTitleBarSubtitle;

    public string FocusLine => FocusTerms.Count == 0
        ? "Watching: all recent headlines"
        : $"Watching: {BuildFocusSummary()}";

    public string SourcesLine { get; }

    public NewsHeadline? SelectedHeadline
    {
        get => _selectedHeadline;
        set => SetProperty(ref _selectedHeadline, value);
    }

    public int HeadlineCount
    {
        get => _headlineCount;
        private set => SetProperty(ref _headlineCount, value);
    }

    public TickerMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!SetProperty(ref _selectedMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedModeDescription));
            OnPropertyChanged(nameof(SelectedModeOption));
            SaveSettings();
        }
    }

    public string SelectedModeDescription => SelectedMode switch
    {
        TickerMode.VerticalSlide => "Slides one headline at a time like a focused alert stack.",
        _ => "Keeps headlines moving continuously for ambient monitoring."
    };

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set
        {
            var normalized = NormalizeRefreshInterval(value);
            if (!SetProperty(ref _refreshIntervalMinutes, normalized))
            {
                return;
            }

            _refreshTimer.Interval = TimeSpan.FromMinutes(normalized);
            OnPropertyChanged(nameof(RefreshCadenceLine));
            OnPropertyChanged(nameof(SelectedRefreshIntervalOption));
            SaveSettings();
        }
    }

    public string RefreshCadenceLine => $"Checks live sources every {RefreshIntervalMinutes} minute{(RefreshIntervalMinutes == 1 ? string.Empty : "s")}.";

    public bool PreferCompactTickerLayout
    {
        get => _preferCompactTickerLayout;
        set
        {
            if (!SetProperty(ref _preferCompactTickerLayout, value))
            {
                return;
            }

            SaveSettings();
        }
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        _refreshTimer.Start();
    }

    public async Task RefreshAsync()
    {
        StatusLine = $"Refreshing {DateTimeOffset.Now:HH:mm:ss}...";

        var snapshot = await _feedService.RefreshAsync(FocusTerms, _disposeTokenSource.Token);
        var incomingHeadlines = snapshot.Headlines.ToList();
        var headlinesChanged = HaveHeadlinesChanged(incomingHeadlines);

        if (headlinesChanged)
        {
            Headlines.Clear();
            foreach (var headline in incomingHeadlines)
            {
                Headlines.Add(headline);
            }
        }

        if (SelectedHeadline is not null)
        {
            SelectedHeadline = Headlines.FirstOrDefault(headline => string.Equals(headline.Url, SelectedHeadline.Url, StringComparison.OrdinalIgnoreCase))
                ?? Headlines.FirstOrDefault();
        }
        else
        {
            SelectedHeadline = Headlines.FirstOrDefault();
        }

        HeadlineCount = Headlines.Count;
        var issueSuffix = snapshot.Errors.Count > 0 ? $"  •  {snapshot.Errors.Count} source issue(s)" : string.Empty;
        StatusLine = $"Updated at {snapshot.RefreshedAt:h:mm:ss tt}{issueSuffix}";
        SetLiveTitleBarSubtitle($"{HeadlineCount} headlines  •  Updated {snapshot.RefreshedAt:h:mm tt}");
    }

    public void SetSettingsViewActive(bool isActive)
    {
        if (_isSettingsViewActive == isActive)
        {
            return;
        }

        _isSettingsViewActive = isActive;
        OnPropertyChanged(nameof(TitleBarSubtitle));
    }

    public void Dispose()
    {
        _focusTerms.CollectionChanged -= FocusTerms_CollectionChanged;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
        _feedService.Dispose();
    }

    private string BuildFocusSummary()
    {
        const int visibleTerms = 5;
        var orderedTerms = FocusTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Take(visibleTerms)
            .ToList();

        var summary = string.Join(", ", orderedTerms);
        var extraCount = Math.Max(0, FocusTerms.Count - orderedTerms.Count);
        return extraCount > 0 ? $"{summary} +{extraCount} more" : summary;
    }

    private void FocusTerms_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isNormalizingFocusTerms)
        {
            return;
        }

        NormalizeFocusTerms();
        OnPropertyChanged(nameof(FocusLine));
        SaveSettings();
    }

    private void NormalizeFocusTerms()
    {
        var normalized = FocusTerms
            .Select(NormalizeFocusTerm)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (FocusTerms.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return;
        }

        _isNormalizingFocusTerms = true;
        try
        {
            FocusTerms.Clear();
            foreach (var term in normalized)
            {
                FocusTerms.Add(term);
            }
        }
        finally
        {
            _isNormalizingFocusTerms = false;
        }
    }

    private static string NormalizeFocusTerm(string term)
    {
        return string.Join(
            " ",
            term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static int NormalizeRefreshInterval(int minutes)
    {
        return minutes switch
        {
            1 or 2 or 5 or 10 => minutes,
            _ => DefaultRefreshIntervalMinutes
        };
    }

    private bool HaveHeadlinesChanged(IReadOnlyList<NewsHeadline> incomingHeadlines)
    {
        if (Headlines.Count != incomingHeadlines.Count)
        {
            return true;
        }

        for (var index = 0; index < Headlines.Count; index++)
        {
            var current = Headlines[index];
            var incoming = incomingHeadlines[index];

            if (!string.Equals(current.SourceName, incoming.SourceName, StringComparison.Ordinal) ||
                !string.Equals(current.Title, incoming.Title, StringComparison.Ordinal) ||
                !string.Equals(current.Url, incoming.Url, StringComparison.Ordinal) ||
                current.PublishedAt != incoming.PublishedAt ||
                current.IsMock != incoming.IsMock)
            {
                return true;
            }
        }

        return false;
    }

    private void SetLiveTitleBarSubtitle(string value)
    {
        if (_liveTitleBarSubtitle == value)
        {
            return;
        }

        _liveTitleBarSubtitle = value;
        OnPropertyChanged(nameof(TitleBarSubtitle));
    }

    private async void RefreshTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        await RefreshAsync();
    }

    private static string GetSettingsPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Newsfeed");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    private SettingsState? LoadSettings()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SettingsState>(File.ReadAllText(path), SettingsJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var state = new SettingsState(
                FocusTerms.Where(term => !string.IsNullOrWhiteSpace(term)).ToList(),
                SelectedMode,
                RefreshIntervalMinutes,
                PreferCompactTickerLayout);
            File.WriteAllText(GetSettingsPath(), JsonSerializer.Serialize(state, SettingsJsonOptions));
        }
        catch
        {
        }
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed record TickerModeOption(TickerMode Value, string Label);

    public sealed record RefreshIntervalOption(int Minutes, string Label);

    private sealed record SettingsState(
        IReadOnlyList<string> FocusTerms,
        TickerMode SelectedMode,
        int RefreshIntervalMinutes,
        bool PreferCompactTickerLayout);
}
