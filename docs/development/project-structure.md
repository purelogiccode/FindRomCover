# Project Structure

The repository contains the WPF application, the test project, documentation, and CI workflows.

```text
CSharp_FindRomCover/
├── FindRomCover/                     # WPF application (net10.0-windows)
│   ├── ApiProvider/                  # Google Custom Search integration
│   │   └── Google.cs
│   ├── Managers/                     # Settings, settings database, MAME data
│   │   ├── SettingsManager.cs
│   │   ├── SettingsDatabase.cs
│   │   └── MameManager.cs
│   ├── models/                       # Data models and DTOs
│   │   ├── SettingsData.cs
│   │   ├── AiVisionOptions.cs
│   │   ├── AiBatchOutcome.cs
│   │   ├── AiPickResult.cs
│   │   ├── MissingImageItem.cs
│   │   └── ...
│   ├── Services/                     # Business logic and utilities
│   │   ├── Ai/                       # AI vision subsystem
│   │   │   ├── AiAssistService.cs
│   │   │   ├── AiBatchFillService.cs
│   │   │   ├── AiQueryHistory.cs
│   │   │   ├── AiVerdictCache.cs
│   │   │   ├── IVisionModelClient.cs
│   │   │   ├── VisionClientFactory.cs
│   │   │   ├── VisionModelClientBase.cs
│   │   │   ├── OpenAiCompatibleVisionClient.cs
│   │   │   ├── AnthropicVisionClient.cs
│   │   │   ├── GeminiVisionClient.cs
│   │   │   ├── VisionImagePreparer.cs
│   │   │   └── VisionModelCatalog.cs
│   │   ├── ImageProcessor.cs
│   │   ├── ImageFolderWatcher.cs
│   │   ├── SimilarityCalculator.cs
│   │   ├── NgramIndex.cs
│   │   ├── SearchQueryHelper.cs
│   │   ├── UpdateCheckService.cs
│   │   └── ...
│   ├── audio/                        # UI sound assets
│   ├── icon/                         # Application icon
│   ├── images/                       # UI image assets
│   ├── App.xaml / App.xaml.cs        # Startup, DI, themes, global handlers
│   ├── AppConstants.cs               # Constants and provider presets
│   ├── MainWindow.xaml / .xaml.cs    # Main window and orchestration
│   ├── MainWindow.LocalSearch.cs     # Local Files tab logic
│   ├── MainWindow.WebSearch.cs       # Web and Google API tab logic
│   ├── AiSettingsWindow.xaml         # AI configuration dialog
│   ├── AiBatchWindow.xaml            # Batch fill dialog
│   ├── ApiSettingsWindow.xaml        # Google API key dialog
│   ├── SettingsWindow.xaml           # Supported extensions dialog
│   ├── DebugWindow.xaml              # Live log viewer
│   └── AboutWindow.xaml
├── FindRomCover.Tests/               # xUnit test project
│   ├── ApiProvider/
│   ├── Managers/
│   ├── Models/
│   ├── Services/
│   │   └── Ai/
│   ├── AppConstantsTests.cs
│   └── TestBootstrap.cs
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
│   └── docs.yml                      # Publish GitHub Pages and wiki
├── mkdocs.yml                        # Documentation site configuration
├── CSharp_FindRomCover.sln
├── global.json                       # .NET SDK version
├── README.md
├── RecommendedModels.md
├── LICENSE.txt
├── screenshot.png / screenshot2.png
└── WhatsNew.md
```

## Key entry points

| File | Role |
|------|------|
| `FindRomCover/App.xaml.cs` | Application startup, DI, theme, exception handling, update check |
| `FindRomCover/MainWindow.xaml.cs` | Main window orchestration, scanning, tray, themes |
| `FindRomCover/MainWindow.LocalSearch.cs` | Local similarity search and AI pick |
| `FindRomCover/MainWindow.WebSearch.cs` | WebView2 tabs and Google API tab |
| `FindRomCover/Managers/SettingsManager.cs` | Settings, encryption, migration |
| `FindRomCover/Services/Ai/AiAssistService.cs` | Vision-model picking |
| `FindRomCover/Services/Ai/AiBatchFillService.cs` | Batch fill orchestration |
| `FindRomCover/ApiProvider/Google.cs` | Google Custom Search API |

## Conventions

- **One top-level type per file** (enforced by analyzer MA0048).
- **Nullable enabled**, implicit usings, C# 14.
- **Analyzers**: Meziantou, Roslynator, Microsoft.CodeAnalysis — warnings are treated seriously; the CI build is expected to be warning-free.
- **No comments** in code unless they add real value; names and structure should carry the intent.
- Models are plain classes or records; services are sealed classes with static helpers where stateless.

## Related pages

- [Architecture](architecture.md)
- [Building & Running](building.md)
- [Testing](testing.md)
