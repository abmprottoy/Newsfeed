using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Newsfeed.Models;

namespace Newsfeed.Controls;

public sealed partial class ContinuousTickerControl : UserControl
{
    public static readonly DependencyProperty HeadlinesProperty =
        DependencyProperty.Register(
            nameof(Headlines),
            typeof(IList<NewsHeadline>),
            typeof(ContinuousTickerControl),
            new PropertyMetadata(null, OnHeadlinesChanged));

    public static readonly DependencyProperty SelectedHeadlineProperty =
        DependencyProperty.Register(
            nameof(SelectedHeadline),
            typeof(NewsHeadline),
            typeof(ContinuousTickerControl),
            new PropertyMetadata(null, OnSelectedHeadlineChanged));

    private Storyboard? _storyboard;
    private INotifyCollectionChanged? _collectionSubscription;
    private IReadOnlyList<NewsHeadline> _displayHeadlines = [];

    public ContinuousTickerControl()
    {
        InitializeComponent();
    }

    public IList<NewsHeadline>? Headlines
    {
        get => (IList<NewsHeadline>?)GetValue(HeadlinesProperty);
        set => SetValue(HeadlinesProperty, value);
    }

    public NewsHeadline? SelectedHeadline
    {
        get => (NewsHeadline?)GetValue(SelectedHeadlineProperty);
        set => SetValue(SelectedHeadlineProperty, value);
    }

    public NewsHeadline? ActiveHeadline { get; private set; }

    private static void OnHeadlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ContinuousTickerControl)d;
        control.RewireCollection(e.OldValue as INotifyCollectionChanged, e.NewValue as INotifyCollectionChanged);
        control.RebuildTicker();
    }

    private static void OnSelectedHeadlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ContinuousTickerControl)d).RebuildTicker();
    }

    private void RewireCollection(INotifyCollectionChanged? oldCollection, INotifyCollectionChanged? newCollection)
    {
        if (oldCollection is not null)
        {
            oldCollection.CollectionChanged -= Headlines_CollectionChanged;
        }

        _collectionSubscription = newCollection;

        if (_collectionSubscription is not null)
        {
            _collectionSubscription.CollectionChanged += Headlines_CollectionChanged;
        }
    }

    private void Headlines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTicker();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        RebuildTicker();
    }

    private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClipGeometry.Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        RebuildTicker();
    }

    private void RebuildTicker()
    {
        StopAnimation();

        var headlines = Headlines;
        if (headlines is null || headlines.Count == 0 || ActualWidth <= 0)
        {
            _displayHeadlines = [];
            ActiveHeadline = null;
            TickerText.Text = "Loading live headlines...";
            TickerCloneText.Text = TickerText.Text;
            return;
        }

        _displayHeadlines = GetDisplayHeadlines(headlines, SelectedHeadline);
        ActiveHeadline = _displayHeadlines.FirstOrDefault();

        var text = string.Join("     ●     ", _displayHeadlines.Select(FormatHeadline));
        TickerText.Text = text;
        TickerCloneText.Text = text;

        DispatcherQueue.TryEnqueue(StartAnimation);
    }

    private void StartAnimation()
    {
        StopAnimation();

        TickerPanel.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var halfWidth = Math.Max(1, TickerPanel.DesiredSize.Width / 2);

        TickerTransform.X = ActualWidth;

        var animation = new DoubleAnimation
        {
            From = ActualWidth,
            To = -halfWidth,
            Duration = TimeSpan.FromSeconds(Math.Max(14, (ActualWidth + halfWidth) / 90)),
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, TickerTransform);
        Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.X));

        _storyboard = new Storyboard();
        _storyboard.Children.Add(animation);
        _storyboard.Begin();
    }

    private void StopAnimation()
    {
        _storyboard?.Stop();
        _storyboard = null;
    }

    private static string FormatHeadline(NewsHeadline headline)
    {
        var suffix = headline.IsMock ? " [demo]" : string.Empty;
        return $"{headline.SourceName}: {headline.Title} • {headline.RelativePublishedText}{suffix}";
    }

    private static IReadOnlyList<NewsHeadline> GetDisplayHeadlines(IList<NewsHeadline> headlines, NewsHeadline? selectedHeadline)
    {
        if (headlines.Count == 0)
        {
            return [];
        }

        if (selectedHeadline is null)
        {
            return headlines.ToList();
        }

        var selectedIndex = headlines
            .Select((headline, index) => (headline, index))
            .FirstOrDefault(item => string.Equals(item.headline.Url, selectedHeadline.Url, StringComparison.OrdinalIgnoreCase))
            .index;

        return selectedIndex <= 0
            ? headlines.ToList()
            : headlines.Skip(selectedIndex).Concat(headlines.Take(selectedIndex)).ToList();
    }
}
