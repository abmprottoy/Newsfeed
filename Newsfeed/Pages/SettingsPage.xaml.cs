using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newsfeed.ViewModels;

namespace Newsfeed.Pages;

public sealed partial class SettingsPage : Page
{
    public MainViewModel? ViewModel { get; private set; }

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel = e.Parameter as MainViewModel;
        DataContext = ViewModel;
    }

    private void FocusTermsBox_TokenItemAdding(TokenizingTextBox sender, TokenItemAddingEventArgs args)
    {
        var normalized = string.Join(
            " ",
            args.TokenText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (string.IsNullOrWhiteSpace(normalized) ||
            ViewModel?.FocusTerms.Any(term => string.Equals(term, normalized, StringComparison.OrdinalIgnoreCase)) == true)
        {
            args.Cancel = true;
            return;
        }

        args.Item = normalized;
    }
}
