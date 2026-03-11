using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newsfeed.Pages;
using Newsfeed.ViewModels;
using System.Text.Json;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Newsfeed;

public sealed partial class MainWindow : Window
{
    private const int DefaultWindowHeight = 560;
    private const int DefaultWindowWidth = 1240;
    private const int MinWindowHeight = 280;
    private const int MinWindowWidth = 980;
    private const int WindowMargin = 20;
    private static readonly JsonSerializerOptions WindowStateJsonOptions = new(JsonSerializerDefaults.Web);
    private bool _isInitialized;
    private bool _isPlaced;
    private bool _isChromeInitialized;
    private bool _isApplyingMinimumSize;
    private AppWindow? _appWindow;
    private ShellPage _currentPage = ShellPage.Home;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        ContentFrame.Navigated += ContentFrame_Navigated;

        RootNavigationView.SelectedItem = HomeNavigationItem;
        NavigateToPage(ShellPage.Home, useAnimation: false);
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

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var targetPage = args.IsSettingsSelected ? ShellPage.Settings : ShellPage.Home;
        NavigateToPage(targetPage, useAnimation: true);
    }

    private void RootNavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        NavigateBack();
    }

    private void AppTitleBar_BackRequested(TitleBar sender, object args)
    {
        NavigateBack();
    }

    private void RootLayout_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(RootLayout).Properties;

        if (properties.IsXButton1Pressed)
        {
            NavigateBack();
            e.Handled = true;
            return;
        }

        if (properties.IsXButton2Pressed)
        {
            NavigateForward();
            e.Handled = true;
        }
    }

    private void NavigateToPage(ShellPage targetPage, bool useAnimation)
    {
        if (ContentFrame.Content is not null && targetPage == _currentPage)
        {
            return;
        }

        var targetType = targetPage switch
        {
            ShellPage.Settings => typeof(SettingsPage),
            _ => typeof(HomePage)
        };

        NavigationTransitionInfo transition = useAnimation
            ? new SlideNavigationTransitionInfo
            {
                Effect = targetPage > _currentPage
                    ? SlideNavigationTransitionEffect.FromRight
                    : SlideNavigationTransitionEffect.FromLeft
            }
            : new SuppressNavigationTransitionInfo();

        ViewModel.SetSettingsViewActive(targetPage == ShellPage.Settings);
        ContentFrame.Navigate(targetType, ViewModel, transition);
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        _currentPage = GetShellPage(e.SourcePageType);
        ViewModel.SetSettingsViewActive(_currentPage == ShellPage.Settings);
        RootNavigationView.SelectedItem = _currentPage == ShellPage.Settings
            ? RootNavigationView.SettingsItem
            : HomeNavigationItem;
        RootNavigationView.IsBackEnabled = ContentFrame.CanGoBack;
        AppTitleBar.IsBackButtonEnabled = ContentFrame.CanGoBack;
    }

    private void NavigateBack()
    {
        if (!ContentFrame.CanGoBack)
        {
            return;
        }

        ContentFrame.GoBack(new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromLeft
        });
    }

    private void NavigateForward()
    {
        if (!ContentFrame.CanGoForward)
        {
            return;
        }

        ContentFrame.GoForward();
    }

    private static ShellPage GetShellPage(Type? pageType)
    {
        return pageType == typeof(SettingsPage)
            ? ShellPage.Settings
            : ShellPage.Home;
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
        var maxWidth = Math.Max(960, workArea.Width - (WindowMargin * 2));
        var maxHeight = Math.Max(560, workArea.Height - (WindowMargin * 2));
        var width = Math.Clamp(state?.Width ?? DefaultWindowWidth, Math.Min(MinWindowWidth, maxWidth), maxWidth);
        var height = Math.Clamp(state?.Height ?? DefaultWindowHeight, Math.Min(MinWindowHeight, maxHeight), maxHeight);
        var defaultX = workArea.X + Math.Max(WindowMargin, (workArea.Width - width) / 2);
        var defaultY = workArea.Y + workArea.Height - height - WindowMargin;
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
        titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
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

    private enum ShellPage
    {
        Home = 0,
        Settings = 1
    }
}
