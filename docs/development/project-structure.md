# Project Structure

The repository contains the Avalonia application, the test project, documentation, and CI workflows.

```text
CSharp_FindRomCover/
├── FindRomCover/                     # Avalonia application (net10.0)
│   ├── ApiProvider/                  # Google Custom Search integration
│   │   └── Google.cs
│   ├── Assets/                       # Bundled resources
│   │   ├── audio/
│   │   │   └── click.mp3             # UI click sound
│   │   └── images/                   # UI images (logo, donate, broken image, ...)
│   ├── icon/                         # Application icon (icon.ico/png)
│   ├── Controls/                     # Custom Avalonia controls
│   │   └── ProgressRing.cs
│   ├── Interfaces/                   # Service abstractions
│   │   ├── IAudioService.cs
│   │   └── IVisionModelClient.cs
│   ├── Managers/                     # Settings, settings database, MAME data
│   │   ├── SettingsManager.cs
│   │   ├── SettingsDatabase.cs
│   │   └── MameManager.cs
│   ├── Models/                       # Data models, DTOs, and enums
│   │   ├── SettingsData.cs
│   │   ├── AiVisionOptions.cs
│   │   ├── AiBatchOutcome.cs
│   │   ├── AiPickResult.cs
│   │   ├── BugReportModel.cs
│   │   ├── MissingImageItem.cs
│   │   └── ...
│   ├── Services/                     # Business logic and utilities
│   │   ├── Ai/                       # AI vision subsystem
│   │   │   ├── AiAssistService.cs
│   │   │   ├── AiBatchFillService.cs
│   │   │   ├── AiQueryHistory.cs
│   │   │   ├── AiVerdictCache.cs
│   │   │   ├── VisionClientFactory.cs
│   │   │   ├── VisionModelClientBase.cs
│   │   │   ├── OpenAiCompatibleVisionClient.cs
│   │   │   ├── AnthropicVisionClient.cs
│   │   │   ├── GeminiVisionClient.cs
│   │   │   ├── VisionImagePreparer.cs
│   │   │   └── VisionModelCatalog.cs
│   │   ├── AppDataPaths.cs
│   │   ├── ApplicationStatsService.cs
│   │   ├── BugReportSink.cs
│   │   ├── CoverFileResolver.cs
│   │   ├── ErrorLogger.cs
│   │   ├── ImageFolderWatcher.cs
│   │   ├── ImageLoader.cs
│   │   ├── ImageProcessor.cs
│   │   ├── ImageSaveService.cs
│   │   ├── LocalAudioService.cs
│   │   ├── LogService.cs
│   │   ├── NgramIndex.cs
│   │   ├── ScreenshotService.cs
│   │   ├── SimilarityCalculator.cs
│   │   ├── TrayIconManager.cs
│   │   ├── UpdateCheckService.cs
│   │   ├── UrlService.cs
│   │   ├── WebSearchService.cs
│   │   └── ...                       # dialogs, command and button helpers, HTTP helper
│   ├── Styles/
│   │   └── AppStyles.axaml           # Shared control styles
│   ├── Program.cs                    # Avalonia bootstrap
│   ├── App.axaml / App.axaml.cs      # Startup, DI, themes, global handlers
│   ├── app.manifest                  # Windows application manifest
│   ├── AppConstants.cs               # Constants and provider presets
│   ├── ThemePalette.cs               # Theme and accent palette application
│   ├── AssemblyInfo.cs               # InternalsVisibleTo FindRomCover.Tests
│   ├── MainWindow.axaml / .axaml.cs  # Main window and orchestration
│   ├── MainWindow.LocalSearch.cs     # Local Files tab logic
│   ├── MainWindow.WebSearch.cs       # Web and Google API tab logic
│   ├── AiSettingsWindow.axaml(.cs)   # AI configuration dialog
│   ├── AiBatchWindow.axaml(.cs)      # Batch fill dialog
│   ├── ApiSettingsWindow.axaml(.cs)  # Google API key dialog
│   ├── SettingsWindow.axaml(.cs)     # Supported extensions dialog
│   ├── DebugWindow.axaml(.cs)        # Live log viewer
│   ├── AboutWindow.axaml(.cs)
│   └── mame.dat                      # MAME machine data (copied to the output)
├── FindRomCover.Tests/               # xUnit test project (net10.0-windows)
│   ├── ApiProvider/
│   ├── Managers/
│   ├── Models/
│   ├── Services/
│   │   └── Ai/
│   ├── AppConstantsTests.cs
│   ├── TestBootstrap.cs              # Isolates tests from real settings and the API
│   └── xunit.runner.json             # Disables collection parallelization
├── docs/                             # Documentation site (MkDocs Material)
│   ├── index.md
│   ├── getting-started/
│   ├── user-guide/
│   ├── ai/
│   ├── configuration/
│   ├── reference/
│   ├── development/
│   ├── release-notes.md
│   ├── faq.md
│   ├── troubleshooting.md
│   ├── license.md
│   └── assets/images/
├── scripts/
│   └── Sync-Wiki.ps1                 # Generates the GitHub wiki from docs/
├── .github/workflows/
│   ├── ci.yml                        # Build and test
│   ├── docs.yml                      # Publish GitHub Pages and wiki
│   └── release.yml                   # Build bundles and publish GitHub releases
├── mkdocs.yml                        # Documentation site configuration
├── CSharp_FindRomCover.sln
├── global.json                       # .NET SDK version
├── README.md
├── RecommendedModels.md
├── LICENSE.txt
├── screenshot.png / screenshot2.png
└── WhatsNew.md
```

> **Note:** `CSharp_FindRomCover.sln` contains just `FindRomCover` and `FindRomCover.Tests`. The former WPF project has been removed from the repository; its icon assets live on in `FindRomCover/icon/`.

## Key entry points

| File | Role |
|------|------|
| `FindRomCover/Program.cs` | Avalonia bootstrap and desktop lifetime |
| `FindRomCover/App.axaml.cs` | Application startup, DI, theme, exception handling, update check |
| `FindRomCover/MainWindow.axaml.cs` | Main window orchestration, scanning, tray, themes, screenshots |
| `FindRomCover/MainWindow.LocalSearch.cs` | Local similarity search and AI pick |
| `FindRomCover/MainWindow.WebSearch.cs` | Google/Bing web views and the Google API tab |
| `FindRomCover/Managers/SettingsManager.cs` | Settings, encryption, migration |
| `FindRomCover/Services/Ai/AiAssistService.cs` | Vision-model picking |
| `FindRomCover/Services/Ai/AiBatchFillService.cs` | Batch fill orchestration |
| `FindRomCover/ApiProvider/Google.cs` | Google Custom Search API |

## Conventions

- **One top-level type per file** — a project convention that keeps navigation predictable; it is not enforced by an analyzer rule.
- **Nullable enabled**, implicit usings, C# 14.
- **Analyzers**: Meziantou, Roslynator, Microsoft.CodeAnalysis — warnings are treated seriously; the CI build is expected to be warning-free. A few Meziantou rules are intentionally disabled in `.editorconfig` (`MA0004`, `MA0015`, `MA0016`, `MA0046`, `MA0051`).
- **No comments** in code unless they add real value; names and structure should carry the intent.
- **Async methods end with `Async`**; public and internal members carry XML documentation (the project generates a documentation file).
- **`InternalsVisibleTo FindRomCover.Tests`** lets tests reach internal seams such as `SettingsManager.DefaultSettingsDirectoryOverride`.
- Models are plain classes or records; services are sealed classes with static helpers where stateless.

## Related pages

- [Architecture](architecture.md)
- [Building & Running](building.md)
- [Testing](testing.md)
