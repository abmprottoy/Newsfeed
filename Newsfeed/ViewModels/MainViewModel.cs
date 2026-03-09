using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Newsfeed.Models;
using Newsfeed.Services;

namespace Newsfeed.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly FeedService _feedService = new();
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private TickerMode _selectedMode = TickerMode.ContinuousScroll;
    private string _statusLine = "Preparing live sources...";
    private string _titleBarSubtitle = "Connecting to live sources...";
    private NewsHeadline? _selectedHeadline;
    private int _headlineCount;

    public MainViewModel()
    {
        Headlines = [];
        FocusTerms =
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
        SourcesLine = $"Sources: {string.Join("  •  ", _feedService.DefaultSources.Select(source => source.Name))}";

        _refreshTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMinutes(2);
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NewsHeadline> Headlines { get; }

    public IReadOnlyList<string> FocusTerms { get; }

    public string StatusLine
    {
        get => _statusLine;
        private set => SetProperty(ref _statusLine, value);
    }

    public string TitleBarSubtitle
    {
        get => _titleBarSubtitle;
        private set => SetProperty(ref _titleBarSubtitle, value);
    }

    public string FocusLine => "Watching: Al Jazeera liveblog, Iran conflict, Gulf security, oil and diplomacy";

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
            if (EqualityComparer<TickerMode>.Default.Equals(_selectedMode, value))
            {
                return;
            }

            _selectedMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMode)));
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
        TitleBarSubtitle = $"{HeadlineCount} headlines  •  Updated {snapshot.RefreshedAt:h:mm tt}";
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

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
        _feedService.Dispose();
    }

    private async void RefreshTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        await RefreshAsync();
    }

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
