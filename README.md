# Newsfeed

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/)
[![Last Commit](https://img.shields.io/github/last-commit/abmprottoy/Newsfeed)](https://github.com/abmprottoy/Newsfeed/commits/main)
[![Repo Stars](https://img.shields.io/github/stars/abmprottoy/Newsfeed)](https://github.com/abmprottoy/Newsfeed/stargazers)

WinUI 3 desktop ticker app for tracking live world headlines from multiple sources.

## Demo

https://github.com/user-attachments/assets/4f4c72ed-906c-4eaa-8e01-9b0d9f20e725

If the embedded player does not render on your device, open the direct link above.

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Quick Start](#quick-start)
- [Source Feeds](#source-feeds)
- [Project Structure](#project-structure)
- [Customization](#customization)

## Features

- Unpackaged WinUI 3 app shell (`net9.0-windows10.0.19041.0`)
- Bottom-docked ticker-style layout for continuous monitoring
- Two display modes:
  - Continuous horizontal scroll
  - Vertical slide rotation
- Multi-source polling with focus-term relevance filtering
- Auto refresh every 2 minutes
- Built-in fallback headlines when live feeds fail

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

### Run

```powershell
dotnet restore .\Newsfeed\Newsfeed.csproj
dotnet build .\Newsfeed\Newsfeed.csproj -p:Platform=x64
dotnet run --project .\Newsfeed\Newsfeed.csproj -p:Platform=x64
```

### VS Code Tasks

Tasks are defined in [.vscode/tasks.json](.vscode/tasks.json):

- `restore`
- `build`
- `run`

## Source Feeds

Initial sources are configured in [FeedService.cs](Newsfeed/Services/FeedService.cs):

- Al Jazeera Live (homepage liveblog discovery + AMP updates)
- BBC World RSS
- The Guardian World RSS
- Bloomberg Politics RSS
- Bloomberg Markets RSS
- WSJ World RSS

These are extendable by adding new feed URLs and parsing logic as needed.

## Project Structure

```text
Newsfeed/
├─ Controls/
├─ Models/
├─ Services/
├─ ViewModels/
├─ App.xaml
├─ MainWindow.xaml
└─ Newsfeed.csproj
```

## Customization

- Change focus terms in [MainViewModel.cs](Newsfeed/ViewModels/MainViewModel.cs)
- Add or remove feeds in [FeedService.cs](Newsfeed/Services/FeedService.cs)
- Update ticker behavior in:
  - [ContinuousTickerControl.xaml.cs](Newsfeed/Controls/ContinuousTickerControl.xaml.cs)
  - [VerticalTickerControl.xaml.cs](Newsfeed/Controls/VerticalTickerControl.xaml.cs)
