# Architecture

FindRomCover is a cross-platform desktop application built with **Avalonia 12.1.4** and targeting `net10.0` (C# 14). The same codebase runs on Windows, Linux, and macOS. The design is pragmatic and layered: windows own presentation and orchestration, managers own persistence, and services own focused pieces of behavior. There is no heavyweight MVVM framework — views use code-behind together with `INotifyPropertyChanged` for bindable state.

## High-level layers

| Layer | Location | Responsibility |
|-------|----------|----------------|
| Presentation | `FindRomCover/*.axaml`, `MainWindow.*.cs` | Windows, dialogs, commands, event handling |
| Application | `FindRomCover/App.axaml.cs` | Startup, dependency injection, themes, global exception handling, update check |
| Domain models | `FindRomCover/Models` | Plain data records, DTOs, settings, and enums |
| Managers | `FindRomCover/Managers` | Settings persistence, settings database, MAME data |
| Services | `FindRomCover/Services` | Image processing, similarity, search helpers, watchers, logging, dialogs, updates |
| AI services | `FindRomCover/Services/Ai` | Vision clients, picking, batch fill, caches, history |
| Interfaces | `FindRomCover/Interfaces` | `IAudioService`, `IVisionModelClient` |
| Controls and styles | `FindRomCover/Controls`, `FindRomCover/Styles` | `ProgressRing` custom control, shared control styles |
| API providers | `FindRomCover/ApiProvider` | Google Custom Search integration |

## Startup and dependency injection

The entry point is `Program.Main`, which boots the Avalonia classic desktop lifetime. Initialization logic lives in `App.OnFrameworkInitializationCompleted`:

1. initializes Serilog logging (`LogService.Initialize`);
2. configures core services — the UI dispatcher used by logging and the `ImageSources` bitmap factories;
3. registers global exception handlers for the UI dispatcher, background tasks, and the application domain;
4. builds a `ServiceCollection` and registers `SettingsManager` as a singleton;
5. records an anonymous startup hit with `ApplicationStatsService.RecordStartupAsync` (fire-and-forget);
6. sets ImageMagick resource limits (512 MB memory, 4 threads);
7. parses command-line arguments for startup folders;
8. creates the `DebugWindow` (live log viewer);
9. applies the stored theme through `ThemePalette`;
10. opens `MainWindow`;
11. schedules cleanup of orphaned temporary files from interrupted conversions;
12. starts a background GitHub release check and prompts the user when an update is available.

Static services such as `LogService`, `ImageProcessor`, and the AI services are used directly; the DI container is intentionally small.

## Presentation

`MainWindow` is split across partial files:

| File | Responsibility |
|------|----------------|
| `MainWindow.axaml` / `MainWindow.axaml.cs` | Layout, menus, folder handling, missing covers list, themes, tray, updates, screenshots |
| `MainWindow.LocalSearch.cs` | Local Files tab, similarity search, manual and AI pick |
| `MainWindow.WebSearch.cs` | Google Web, Bing Web, and Google API tabs |

Additional windows: `SettingsWindow` (extensions), `ApiSettingsWindow` (Google key), `AiSettingsWindow` (AI configuration), `AiBatchWindow` (batch fill), `DebugWindow` (live log), `AboutWindow`.

## Persistence

| Component | Responsibility |
|-----------|----------------|
| `SettingsManager` | Loads and saves every setting, encrypts secrets, migrates legacy files |
| `SettingsDatabase` | Thin SQLite key/value store used by `SettingsManager` |
| `AiQueryHistory` | SQLite-backed record of covers already queried |
| `AiVerdictCache` | JSON cache of AI verdicts, shared through a single process-wide instance |

Settings are written as key/value rows; secrets are AES-encrypted with a machine-derived PBKDF2 key before storage. Legacy files (`settings.xml` and the older `settings.dat`) are migrated automatically and kept until the SQLite save succeeds. Unreadable settings files are quarantined instead of being overwritten, and a corrupt `QueryHistory.dat` is quarantined and recreated so AI history never disables the feature permanently.

## Services

| Service | Responsibility |
|---------|----------------|
| `ImageProcessor` | Format conversion to PNG, orientation, retries, temporary-file cleanup |
| `ImageLoader` | Loads thumbnails with retry and resource limits |
| `ImageSaveService` | Downloads images and saves them through the conversion pipeline |
| `ImageFolderWatcher` | Watches the image folder, renames and converts new covers |
| `CoverFileResolver` | Locates existing covers across every supported image format |
| `SimilarityCalculator` | Jaccard, Jaro-Winkler, and Levenshtein scoring and ranking |
| `NgramIndex` | Trigram pre-filter for large image folders |
| `SearchQueryHelper` | Filename cleaning and sanitization |
| `WebSearchService` | Builds Google and Bing image-search URLs |
| `UpdateCheckService` | Checks GitHub releases for updates |
| `TrayIconManager` | Avalonia tray icon with Show and Exit entries; balloon notifications are a no-op |
| `LogService` | Serilog configuration, live log stream, transient/watcher markers |
| `ErrorLogger` | Local bug-report files and the remote bug-report API |
| `BugReportSink` | Forwards Warning-and-higher events to `ErrorLogger`, suppressing transient failures |
| `HttpClientHelper` | Shared HTTP client lifecycle |
| `UrlService` | Safe URL opening with fallbacks |
| `Dialogs` | Cross-platform message and question dialogs |
| `LocalAudioService` / `IAudioService` | Optional click sounds with a silent fallback |
| `ScreenshotService` | F8 window capture with app-folder and per-user fallbacks |
| `ApplicationStatsService` | Anonymous, best-effort startup statistics |
| `AppDataPaths` | Central per-user data folder (`%LocalAppData%\FindRomCover`) |
| `ButtonFactory` / `DelegateCommand` | Reusable UI helpers |

## AI subsystem

The AI subsystem is built around one abstraction so providers are interchangeable:

```text
AiAssistService ──uses──> IVisionModelClient
        │                        ├── OpenAiCompatibleVisionClient (OpenAI, GLM, local, custom OpenAI-compatible)
        │                        ├── AnthropicVisionClient      (Anthropic, custom Anthropic-compatible)
        │                        └── GeminiVisionClient         (Gemini)
        ├── VisionClientFactory  (provider -> client)
        ├── VisionImagePreparer  (downscale, encode)
        ├── VisionModelCatalog   (model list fetching/parsing)
        ├── AiVerdictCache       (shared, atomic verdict reuse)
        └── AiQueryHistory       (skip previously queried, corrupt-file quarantine)
```

`AiAssistService` serializes requests with a `SemaphoreSlim` and always uses the shared `AiVerdictCache`, so the main window and the batch window never read-modify-write over each other. `AiBatchFillService` orchestrates batch runs on top of `AiAssistService`, with optional Google API fallback. All clients share prompts, JSON parsing, timeout handling, and friendly error messages through `VisionModelClientBase`. Each client also merges the optional custom request-parameter JSON from AI Settings into its serialized request body, so provider parameters can be tuned without code changes.

## Data flow: finding a cover

1. **Scan** — enumerate ROM files, compare base names with image files, build the missing list.
2. **Search** — run the active tab:
   - Local Files: `SimilarityCalculator` (with `NgramIndex`) ranks images.
   - Web tabs: `WebSearchService` builds a URL and `NativeWebView` displays it (WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux). If the embedded view is unavailable, the page opens in the system browser instead.
   - Google API: `Google.FetchImagesFromGoogleAsync` returns image results.
3. **Pick** — the user clicks an image, or `AiAssistService` ranks candidates with a vision model.
4. **Save** — `ImageProcessor` / `ImageSaveService` convert to PNG and write `[gamename].png`.
5. **Refresh** — the watcher and save paths remove the entry from the missing list.

## Error handling and resilience

- Global handlers catch UI-thread, task, and AppDomain exceptions; fatal errors are logged and shown to the user.
- `BugReportSink` forwards Warning-and-higher events to the bug-report API, but suppresses transient environmental failures (network, statistics, update checks) and file-system watcher errors through structured markers.
- Image operations retry transient I/O failures and skip corrupt files.
- The settings pipeline quarantines corrupt files and falls back to defaults instead of failing to start.
- AI failures are warnings: a failed pick never blocks the normal workflow.

## Security

- API keys are AES-encrypted with a machine-derived key and never logged.
- Filenames are sanitized before they are used as paths.
- URL opening goes through `UrlService` with an `explorer.exe` fallback.
- ImageMagick resource limits (512 MB memory, 4 threads) protect against pathological images.
- AI image downloads are bounded, and screenshot file names are collision-safe.

## Testing and CI

The test project covers services, managers, models, and AI clients with xUnit, FluentAssertions, and Moq. GitHub Actions builds and tests every push and pull request on Windows, enforces formatting and analyzer rules, and scans the code with CodeQL. See [Testing](testing.md) and [CI/CD](ci-cd.md).

## Related pages

- [Project Structure](project-structure.md)
- [Building & Running](building.md)
- [Testing](testing.md)
