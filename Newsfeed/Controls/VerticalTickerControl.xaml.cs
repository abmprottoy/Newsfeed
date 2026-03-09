using System.Collections.Specialized;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Newsfeed.Models;

namespace Newsfeed.Controls;

public sealed partial class VerticalTickerControl : UserControl
{
    public static readonly DependencyProperty HeadlinesProperty =
        DependencyProperty.Register(
            nameof(Headlines),
            typeof(IList<NewsHeadline>),
            typeof(VerticalTickerControl),
            new PropertyMetadata(null, OnHeadlinesChanged));

    public static readonly DependencyProperty SelectedHeadlineProperty =
        DependencyProperty.Register(
            nameof(SelectedHeadline),
            typeof(NewsHeadline),
            typeof(VerticalTickerControl),
            new PropertyMetadata(null, OnSelectedHeadlineChanged));

    private readonly DispatcherQueueTimer _rotationTimer;
    private Storyboard? _transitionStoryboard;
    private INotifyCollectionChanged? _collectionSubscription;
    private int _currentIndex;

    public VerticalTickerControl()
    {
        InitializeComponent();
        _rotationTimer = DispatcherQueue.CreateTimer();
        _rotationTimer.Interval = TimeSpan.FromSeconds(5);
        _rotationTimer.Tick += RotationTimer_Tick;
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
        var control = (VerticalTickerControl)d;
        control.RewireCollection(e.OldValue as INotifyCollectionChanged, e.NewValue as INotifyCollectionChanged);
        control.ResetTicker();
    }

    private static void OnSelectedHeadlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((VerticalTickerControl)d).ResetTicker();
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
        ResetTicker();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        ResetTicker();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClipGeometry.Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
    }

    private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        _rotationTimer.Stop();
        _transitionStoryboard?.Stop();
    }

    private void ResetTicker()
    {
        _transitionStoryboard?.Stop();
        _currentIndex = 0;

        if (Headlines is null || Headlines.Count == 0)
        {
            ActiveHeadline = null;
            CurrentText.Text = "Waiting for headlines...";
            IncomingText.Text = string.Empty;
            _rotationTimer.Stop();
            return;
        }

        _currentIndex = GetSelectedIndex();

        CurrentTransform.Y = 0;
        CurrentText.Opacity = 1;
        IncomingTransform.Y = 54;
        IncomingText.Opacity = 0;
        CurrentText.Text = FormatHeadline(Headlines[_currentIndex]);
        ActiveHeadline = Headlines[_currentIndex];

        if (Headlines.Count > 1)
        {
            _rotationTimer.Start();
        }
        else
        {
            _rotationTimer.Stop();
        }
    }

    private void RotationTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (Headlines is null || Headlines.Count < 2)
        {
            return;
        }

        var nextIndex = (_currentIndex + 1) % Headlines.Count;
        IncomingText.Text = FormatHeadline(Headlines[nextIndex]);
        IncomingTransform.Y = 54;
        IncomingText.Opacity = 0;

        var outgoingY = new DoubleAnimation
        {
            From = 0,
            To = -54,
            Duration = TimeSpan.FromMilliseconds(450),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(outgoingY, CurrentTransform);
        Storyboard.SetTargetProperty(outgoingY, nameof(TranslateTransform.Y));

        var outgoingOpacity = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(450),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(outgoingOpacity, CurrentText);
        Storyboard.SetTargetProperty(outgoingOpacity, nameof(Opacity));

        var incomingY = new DoubleAnimation
        {
            From = 54,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(450),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(incomingY, IncomingTransform);
        Storyboard.SetTargetProperty(incomingY, nameof(TranslateTransform.Y));

        var incomingOpacity = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(450),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(incomingOpacity, IncomingText);
        Storyboard.SetTargetProperty(incomingOpacity, nameof(Opacity));

        _transitionStoryboard = new Storyboard();
        _transitionStoryboard.Children.Add(outgoingY);
        _transitionStoryboard.Children.Add(outgoingOpacity);
        _transitionStoryboard.Children.Add(incomingY);
        _transitionStoryboard.Children.Add(incomingOpacity);
        _transitionStoryboard.Completed += (_, _) =>
        {
            _currentIndex = nextIndex;
            CurrentText.Text = IncomingText.Text;
            CurrentText.Opacity = 1;
            CurrentTransform.Y = 0;
            IncomingText.Opacity = 0;
            IncomingTransform.Y = 54;
            ActiveHeadline = Headlines?[_currentIndex];
        };
        _transitionStoryboard.Begin();
    }

    private static string FormatHeadline(NewsHeadline headline)
    {
        var suffix = headline.IsMock ? " [demo]" : string.Empty;
        return $"{headline.SourceName}  |  {headline.Title}  |  {headline.RelativePublishedText}{suffix}";
    }

    private int GetSelectedIndex()
    {
        if (Headlines is null || Headlines.Count == 0 || SelectedHeadline is null)
        {
            return 0;
        }

        for (var index = 0; index < Headlines.Count; index++)
        {
            if (string.Equals(Headlines[index].Url, SelectedHeadline.Url, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }
}
