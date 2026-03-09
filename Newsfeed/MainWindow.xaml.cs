using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Newsfeed.Models;
using Newsfeed.ViewModels;
using System.Diagnostics;
using System.Text.Json;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Newsfeed;

public sealed partial class MainWindow : Window
{
    private const int DefaultWindowHeight = 320;
    private const int MinWindowHeight = 300;
    private const int MinWindowWidth = 1100;
    private const int TickerMargin = 12;
    private const int DefaultTickerWidth = 1400;
    private static readonly JsonSerializerOptions WindowStateJsonOptions = new(JsonSerializerDefaults.Web);
    private bool _isInitialized;
    private bool _isPlaced;
    private bool _isChromeInitialized;
    private bool _isApplyingMinimumSize;
    private AppWindow? _appWindow;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;

        UpdateModeVisuals();
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_isChromeInitialized)
        {
            InitializeWindowChrome();
            _isChromeInitialized = true;
        }

        if (!_isPlaced)
        {
            ConfigureWindow();
            _isPlaced = true;
        }

        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await ViewModel.InitializeAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
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
        ViewModel.SelectedMode = TickerMode.ContinuousScroll;
        UpdateModeVisuals();
    }

    private void StackModeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedMode = TickerMode.VerticalSlide;
        UpdateModeVisuals();
    }

    private void UpdateModeVisuals()
    {
        var isScroll = ViewModel.SelectedMode == TickerMode.ContinuousScroll;
        ContinuousTicker.Visibility = isScroll ? Visibility.Visible : Visibility.Collapsed;
        VerticalTicker.Visibility = isScroll ? Visibility.Collapsed : Visibility.Visible;
        ScrollModeButton.Style = (Style)Application.Current.Resources[isScroll ? "AccentButtonStyle" : "DefaultButtonStyle"];
        StackModeButton.Style = (Style)Application.Current.Resources[isScroll ? "DefaultButtonStyle" : "AccentButtonStyle"];
    }

    private void ConfigureWindow()
    {
        if (_appWindow is null)
        {
            return;
        }

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        _appWindow.Changed -= AppWindow_Changed;
        _appWindow.Changed += AppWindow_Changed;

        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var state = LoadWindowState();
        var maxWidth = Math.Max(640, workArea.Width - (TickerMargin * 2));
        var maxHeight = Math.Max(240, workArea.Height - (TickerMargin * 2));
        var width = Math.Clamp(state?.Width ?? DefaultTickerWidth, Math.Min(MinWindowWidth, maxWidth), maxWidth);
        var height = Math.Clamp(state?.Height ?? DefaultWindowHeight, Math.Min(MinWindowHeight, maxHeight), maxHeight);
        var defaultX = workArea.X + Math.Max(TickerMargin, (workArea.Width - width) / 2);
        var defaultY = workArea.Y + workArea.Height - height - TickerMargin;
        var x = state?.X ?? defaultX;
        var y = state?.Y ?? defaultY;

        x = Math.Clamp(x, workArea.X, workArea.X + Math.Max(0, workArea.Width - width));
        y = Math.Clamp(y, workArea.Y, workArea.Y + Math.Max(0, workArea.Height - height));

        _appWindow.Move(new PointInt32(x, y));
        _appWindow.Resize(new SizeInt32(width, height));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        SaveWindowState();
        ViewModel.Dispose();
    }

    private void Ticker_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var headline = ViewModel.SelectedMode == TickerMode.ContinuousScroll
            ? ContinuousTicker.ActiveHeadline
            : VerticalTicker.ActiveHeadline;

        if (headline is not null)
        {
            OpenHeadline(headline);
        }
    }

    private void InitializeWindowChrome()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        titleBar.ButtonBackgroundColor = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
        titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
        titleBar.ButtonForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonHoverForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonPressedForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange)
        {
            EnforceMinimumWindowSize(sender);
        }

        if (args.DidPositionChange || args.DidSizeChange)
        {
            SaveWindowState();
        }
    }

    private void EnforceMinimumWindowSize(AppWindow sender)
    {
        if (_isApplyingMinimumSize)
        {
            return;
        }

        var currentSize = sender.Size;
        var targetWidth = Math.Max(MinWindowWidth, currentSize.Width);
        var targetHeight = Math.Max(MinWindowHeight, currentSize.Height);
        if (targetWidth == currentSize.Width && targetHeight == currentSize.Height)
        {
            return;
        }

        _isApplyingMinimumSize = true;
        try
        {
            sender.Resize(new SizeInt32(targetWidth, targetHeight));
        }
        finally
        {
            _isApplyingMinimumSize = false;
        }
    }

    private static void OpenHeadline(NewsHeadline headline)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = headline.Url,
            UseShellExecute = true
        });
    }

    private static string GetWindowStatePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Newsfeed");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "window-state.json");
    }

    private WindowState? LoadWindowState()
    {
        try
        {
            var path = GetWindowStatePath();
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<WindowState>(File.ReadAllText(path), WindowStateJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void SaveWindowState()
    {
        if (_appWindow is null)
        {
            return;
        }

        try
        {
            var state = new WindowState(
                _appWindow.Position.X,
                _appWindow.Position.Y,
                _appWindow.Size.Width,
                _appWindow.Size.Height);
            var path = GetWindowStatePath();
            File.WriteAllText(path, JsonSerializer.Serialize(state, WindowStateJsonOptions));
        }
        catch
        {
        }
    }

    private sealed record WindowState(int X, int Y, int Width, int Height);
}
