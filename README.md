# Newsfeed

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/)
[![Last Commit](https://img.shields.io/github/last-commit/abmprottoy/Newsfeed)](https://github.com/abmprottoy/Newsfeed/commits/main)
[![Repo Stars](https://img.shields.io/github/stars/abmprottoy/Newsfeed)](https://github.com/abmprottoy/Newsfeed/stargazers)

WinUI 3 desktop news ticker for tracking live world headlines in a compact, dockable Windows shell.

## Demo

https://github.com/user-attachments/assets/02905eec-c7be-470f-8e7b-fcb108b45f6d

If the embedded player does not render on your device, open the direct link above.

## Features

- Unpackaged WinUI 3 desktop app targeting `net9.0-windows10.0.19041.0`
- Native-feeling shell with custom title bar, back navigation, and settings page
- Two ticker modes:
  - Continuous horizontal scroll
  - Vertical slide rotation
- Editable focus terms with inline tokens using `CommunityToolkit.WinUI.Controls.TokenizingTextBox`
- Compact ticker mode for docked or lower-screen monitoring
- Multi-source polling with relevance filtering and fallback headlines
- Configurable refresh cadence and persisted local settings

## Tech Stack

| Area | Choice |
| --- | --- |
| UI | WinUI 3 |
| Language | C# |
| Runtime | .NET 9 |
| App Type | Unpackaged desktop app |
| Data Sources | RSS + HTML parsing + JSON-LD extraction |

## Quick Start

### Requirements

- Windows 10/11
- .NET 9 SDK

### Run From Source

```powershell
dotnet restore .\Newsfeed\Newsfeed.csproj
dotnet build .\Newsfeed\Newsfeed.csproj -p:Platform=x64
dotnet run --project .\Newsfeed\Newsfeed.csproj -p:Platform=x64
```

## Publish A Release Build

To produce a self-contained unpackaged build for distribution:

```powershell
dotnet publish .\Newsfeed\Newsfeed.csproj `
  -c Release `
  -f net9.0-windows10.0.19041.0 `
  -p:RuntimeIdentifierOverride=win-x64 `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  --self-contained true
```

That produces a runnable folder under:

```text
Newsfeed\bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\publish\
```

You can zip that folder and attach it to a GitHub Release for end users.

## Source Feeds

Initial sources are configured in [FeedService.cs](Newsfeed/Services/FeedService.cs):

- Al Jazeera Live
- BBC World RSS
- The Guardian World RSS
- Bloomberg Politics RSS
- Bloomberg Markets RSS
- WSJ World RSS

## Project Structure

```text
Newsfeed/
├─ Controls/
├─ Models/
├─ Pages/
├─ Services/
├─ ViewModels/
├─ App.xaml
├─ MainWindow.xaml
└─ Newsfeed.csproj
```

## Customization

- Change focus terms and persisted defaults in [MainViewModel.cs](Newsfeed/ViewModels/MainViewModel.cs)
- Add or remove feeds in [FeedService.cs](Newsfeed/Services/FeedService.cs)
- Update ticker behavior in:
  - [ContinuousTickerControl.xaml.cs](Newsfeed/Controls/ContinuousTickerControl.xaml.cs)
  - [VerticalTickerControl.xaml.cs](Newsfeed/Controls/VerticalTickerControl.xaml.cs)
- Adjust the shell and settings experience in:
  - [MainWindow.xaml](Newsfeed/MainWindow.xaml)
  - [HomePage.xaml](Newsfeed/Pages/HomePage.xaml)
  - [SettingsPage.xaml](Newsfeed/Pages/SettingsPage.xaml)
