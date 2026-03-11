using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Newsfeed.Models;
using Newsfeed.ViewModels;
using System.ComponentModel;
using System.Diagnostics;

namespace Newsfeed.Pages;

public sealed partial class HomePage : Page
{
    private const double CompactHeightThreshold = 430;

    public MainViewModel? ViewModel { get; private set; }

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        ViewModel = e.Parameter as MainViewModel;
        DataContext = ViewModel;

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        UpdateModeVisuals();
        UpdateLayoutMode();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        base.OnNavigatedFrom(e);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        RefreshButton.IsEnabled = false;
        try
        {
            await ViewModel.RefreshAsync();
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void ScrollModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SelectedMode = TickerMode.ContinuousScroll;
        UpdateModeVisuals();
    }

    private void StackModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SelectedMode = TickerMode.VerticalSlide;
        UpdateModeVisuals();
    }

    private void Ticker_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var headline = ViewModel.SelectedMode == TickerMode.ContinuousScroll
            ? ContinuousTicker.ActiveHeadline
            : VerticalTicker.ActiveHeadline;

        if (headline is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = headline.Url,
            UseShellExecute = true
        });
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedMode))
        {
            UpdateModeVisuals();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.PreferCompactTickerLayout))
        {
            UpdateLayoutMode();
        }
    }

    private void PageLayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutMode();
    }

    private void CompactViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PreferCompactTickerLayout = !ViewModel.PreferCompactTickerLayout;
        }

        UpdateLayoutMode();
    }

    private void UpdateModeVisuals()
    {
        var isScroll = ViewModel?.SelectedMode != TickerMode.VerticalSlide;
        ContinuousTicker.Visibility = isScroll ? Visibility.Visible : Visibility.Collapsed;
        VerticalTicker.Visibility = isScroll ? Visibility.Collapsed : Visibility.Visible;
        ScrollModeButton.Style = (Style)Application.Current.Resources[isScroll ? "AccentButtonStyle" : "DefaultButtonStyle"];
        StackModeButton.Style = (Style)Application.Current.Resources[isScroll ? "DefaultButtonStyle" : "AccentButtonStyle"];
        UpdateCompactButtonState();
    }

    private void UpdateLayoutMode()
    {
        var isCompact = IsCompactLayoutActive();

        IntroSection.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        IntroRow.Height = isCompact ? new GridLength(0) : GridLength.Auto;
        IntroSpacerRow.Height = isCompact ? new GridLength(0) : new GridLength(16);
        ContentSpacerRow.Height = isCompact ? new GridLength(10) : new GridLength(18);
        SummaryTextSection.Spacing = isCompact ? 2 : 4;
        SummarySection.MinHeight = isCompact ? 36 : 0;
        SummaryActionsSection.VerticalAlignment = isCompact ? VerticalAlignment.Center : VerticalAlignment.Top;
        SourcesTextBlock.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        HeadlinesPane.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        HeadlinesColumn.Width = isCompact ? new GridLength(0) : new GridLength(360);
        TickerColumn.Width = new GridLength(1, GridUnitType.Star);
        PageLayoutRoot.Padding = isCompact ? new Thickness(16, 10, 16, 16) : new Thickness(24, 18, 24, 24);
        ContentSection.ColumnSpacing = isCompact ? 0 : 16;
        Grid.SetColumn(TickerPane, isCompact ? 0 : 1);
        Grid.SetColumnSpan(TickerPane, isCompact ? 2 : 1);
        UpdateCompactButtonState();
    }

    private bool IsCompactLayoutActive()
    {
        return IsCompactForcedByHeight() || ViewModel?.PreferCompactTickerLayout == true;
    }

    private bool IsCompactForcedByHeight()
    {
        return ActualHeight <= CompactHeightThreshold;
    }

    private void UpdateCompactButtonState()
    {
        var isCompact = IsCompactLayoutActive();
        var isForcedByHeight = IsCompactForcedByHeight();
        var isPreferredCompact = ViewModel?.PreferCompactTickerLayout == true;

        CompactViewButton.Style = (Style)Application.Current.Resources[isCompact ? "AccentButtonStyle" : "DefaultButtonStyle"];
        CompactViewButton.IsEnabled = true;
        CompactViewButtonText.Text = isForcedByHeight && !isPreferredCompact
            ? "Compact: Auto"
            : isPreferredCompact
                ? "Compact: On"
                : "Compact: Off";
        ToolTipService.SetToolTip(
            CompactViewButton,
            isForcedByHeight
                ? "Compact layout is active because the window is short. Click to keep it on when the window grows."
                : isPreferredCompact
                    ? "Compact layout is pinned on."
                    : "Compact layout is off.");
    }
}
