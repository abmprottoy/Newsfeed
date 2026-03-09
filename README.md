# Newsfeed

WinUI 3 desktop ticker app for tracking live world headlines from multiple sources.

## Features

- Unpackaged WinUI 3 app shell (`net9.0-windows10.0.19041.0`).
- Bottom-docked ticker-style layout for continuous monitoring.
- Two display modes:
  - continuous horizontal scroll
  - vertical slide rotation
- Multi-source polling with relevance filtering based on focus terms.
- Auto refresh every 2 minutes.
- Built-in fallback headlines when live feeds fail.

## Sources

Current default sources are defined in [FeedService.cs](Newsfeed/Services/FeedService.cs):

- Al Jazeera Live (homepage liveblog discovery + AMP updates)
- BBC World RSS
- The Guardian World RSS
- Bloomberg Politics RSS
- Bloomberg Markets RSS
- WSJ World RSS

## Requirements

- Windows 10/11
- .NET 9 SDK

## Run

```powershell
dotnet restore .\Newsfeed\Newsfeed.csproj
dotnet build .\Newsfeed\Newsfeed.csproj -p:Platform=x64
dotnet run --project .\Newsfeed\Newsfeed.csproj -p:Platform=x64
```

VS Code tasks are available in [.vscode/tasks.json](.vscode/tasks.json):

- `restore`
- `build`
- `run`

## Customize

- Change focus terms in [MainViewModel.cs](Newsfeed/ViewModels/MainViewModel.cs).
- Add or remove feeds in [FeedService.cs](Newsfeed/Services/FeedService.cs).
- Update ticker behavior in:
  - [ContinuousTickerControl.xaml.cs](Newsfeed/Controls/ContinuousTickerControl.xaml.cs)
  - [VerticalTickerControl.xaml.cs](Newsfeed/Controls/VerticalTickerControl.xaml.cs)
