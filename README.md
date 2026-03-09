# Newsfeed

Native WinUI desktop ticker for monitoring live headlines near the bottom of the screen.

## What is included

- Unpackaged WinUI 3 desktop app shell.
- Bottom-docked window layout sized for a TV-style ticker.
- Two ticker modes:
  - infinite horizontal scroll
  - vertical slide-up rotation
- Live source polling with focus-term filtering for Iran-related coverage.
- Offline/mock fallback headlines so the shell still runs when feeds are blocked.

## Current sources

- Al Jazeera homepage headline scrape
- BBC World RSS
- The Guardian World RSS

The focus terms are currently hardcoded in [MainViewModel.cs](/E:/Codegraphs/Newsfeed/Newsfeed/ViewModels/MainViewModel.cs) for Iran monitoring.

## Run

1. Restore packages:

```powershell
dotnet restore .\Newsfeed\Newsfeed.csproj
```

2. Build:

```powershell
dotnet build .\Newsfeed\Newsfeed.csproj
```

3. Run from Visual Studio or with `dotnet run --project .\Newsfeed\Newsfeed.csproj`.

There is also a workspace run action in [.vscode/tasks.json](/E:/Codegraphs/Newsfeed/.vscode/tasks.json):

- `run` launches the WinUI app from the repo root.
- `build` and `restore` are available separately if you want them.

## Extend

- Add or change sources in [FeedService.cs](/E:/Codegraphs/Newsfeed/Newsfeed/Services/FeedService.cs).
- Adjust watch keywords in [MainViewModel.cs](/E:/Codegraphs/Newsfeed/Newsfeed/ViewModels/MainViewModel.cs).
- Add more ticker behaviors by following the pattern used in [ContinuousTickerControl.xaml.cs](/E:/Codegraphs/Newsfeed/Newsfeed/Controls/ContinuousTickerControl.xaml.cs) and [VerticalTickerControl.xaml.cs](/E:/Codegraphs/Newsfeed/Newsfeed/Controls/VerticalTickerControl.xaml.cs).
