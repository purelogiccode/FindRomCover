# Architecture

FindRomCover is a WPF desktop application for Windows built on .NET 10. It follows a pragmatic layered design: windows own presentation and orchestration, managers own persistence, and services own focused pieces of behavior.

## High-level layers

| Layer | Location | Responsibility |
|-------|----------|----------------|
| Presentation | `FindRomCover/*.xaml`, `MainWindow.*.cs` | Windows, dialogs, commands, event handling |
| Application | `App.xaml.cs` | Startup, dependency injection, themes, global exception handling, update check |
| Domain models | `FindRomCover/models` | Plain data records and DTOs |
| Managers | `FindRomCover/Managers` | Settings persistence, settings database, MAME data |
| Services | `FindRomCover/Services` | Image processing, similarity, search helpers, watchers, logging, updates |
| AI services | `FindRomCover/Services/Ai` | Vision clients, picking, batch fill, caches, history |
| API providers | `FindRomCover/ApiProvider` | Google Custom Search integration |

## Startup and dependency injection

`App.OnStartup`:

1. initializes Serilog logging;
2. registers global exception handlers for the UI dispatcher, background tasks, and the application domain;
3. builds a `ServiceCollection` and registers `SettingsManager` as a singleton;
4. parses command-line arguments for startup folders;
5. cleans up orphaned temporary files from interrupted conversions;
6. applies the stored theme;
7. opens `MainWindow`;
8. starts a background update check.

Static services such as `LogService`, `ImageProcessor`, and the AI services are used directly; the DI container is intentionally small.

## Presentation

`MainWindow` is split across partial files:

| File | Responsibility |
|------|----------------|
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Layout, menus, folder handling, missing covers list, themes, tray, updates |
| `MainWindow.LocalSearch.cs` | Local Files tab, similarity search, manual and AI pick |
| `MainWindow.WebSearch.cs` | Google Web, Bing Web, and Google API tabs |

Additional windows: `SettingsWindow` (extensions), `ApiSettingsWindow` (Google key), `AiSettingsWindow` (AI configuration), `AiBatchWindow` (batch fill), `DebugWindow` (live log), `AboutWindow`.

## Persistence

| Component | Responsibility |
|-----------|----------------|
| `SettingsManager` | Loads and saves every setting, encrypts secrets, migrates legacy files |
| `SettingsDatabase` | Thin SQLite key/value store used by `SettingsManager` |
| `AiQueryHistory` | SQLite-backed record of covers already queried |
| `AiVerdictCache` | JSON cache of AI verdicts and provider model lists |

Settings are written as key/value rows; secrets are AES-encrypted with a machine-derived PBKDF2 key before storage. Legacy encrypted files and `settings.xml` are migrated automatically.

## Services

| Service | Responsibility |
|---------|----------------|
| `ImageProcessor` | Format conversion to PNG, orientation, retries, temporary-file cleanup |
| `ImageLoader` | Loads thumbnails with retry and resource limits |
| `ImageSaveService` | Downloads images and saves them through the conversion pipeline |
| `ImageFolderWatcher` | Watches the image folder, renames and converts new covers |
| `SimilarityCalculator` | Jaccard, Jaro-Winkler, and Levenshtein scoring and ranking |
| `NgramIndex` | Trigram pre-filter for large image folders |
| `SearchQueryHelper` | Filename cleaning and sanitization |
| `WebSearchService` | Builds Google and Bing image-search URLs |
| `UpdateCheckService` | Checks GitHub releases for updates |
| `SystemTrayIcon` | Tray icon, balloon notifications, restore/exit |
| `LogService` | Serilog configuration, live log stream, masking |
| `ErrorLogger` / `BugReport` / `BugReportSink` | Error capture and optional reporting |
| `HttpClientHelper` | Shared HTTP client lifecycle |
| `UrlService` | Safe URL opening with fallbacks |
| `LocalAudioService` / `IAudioService` | Optional click sounds with a null fallback |
| `ScreenshotService` | F8 window capture |
| `ApplicationStatsService` | Anonymous, best-effort usage statistics |
| `ButtonFactory` / `DelegateCommand` | Reusable UI helpers |

## AI subsystem

The AI subsystem is built around one abstraction so providers are interchangeable:

```
AiAssistService ──uses──> IVisionModelClient
        │                        ├── OpenAiCompatibleVisionClient (OpenAI, GLM, local, custom OpenAI-compatible)
        │                        ├── AnthropicVisionClient      (Anthropic, custom Anthropic-compatible)
        │                        └── GeminiVisionClient         (Gemini)
        ├── VisionClientFactory  (provider -> client)
        ├── VisionImagePreparer  (downscale, encode)
        ├── VisionModelCatalog   (model list fetching/parsing)
        ├── AiVerdictCache       (verdict reuse)
        └── AiQueryHistory       (skip previously queried)
```

`AiBatchFillService` orchestrates batch runs on top of `AiAssistService`, with optional Google API fallback.

All clients share prompts, JSON parsing, timeout handling, and friendly error messages through `VisionModelClientBase`.

## Data flow: finding a cover

1. **Scan** — enumerate ROM files, compare base names with image files, build the missing list.
2. **Search** — run the active tab:
   - Local Files: `SimilarityCalculator` (with `NgramIndex`) ranks images.
   - Web tabs: `WebSearchService` builds a URL, WebView2 displays it.
   - Google API: `Google.FetchImagesFromGoogleAsync` returns image results.
3. **Pick** — user clicks an image, or `AiAssistService` ranks candidates with a vision model.
4. **Save** — `ImageProcessor` / `ImageSaveService` convert to PNG and write `[gamename].png`.
5. **Refresh** — the watcher and save paths remove the entry from the missing list.

## Error handling and resilience

- Global handlers catch UI-thread, task, and AppDomain exceptions; fatal errors are logged and shown to the user.
- Every operation logs failures with context; warnings do not trigger bug reports.
- Image operations retry transient I/O failures and skip corrupt files.
- The settings pipeline quarantines corrupt files and falls back to defaults instead of failing to start.
- AI failures are warnings: a failed pick never blocks the normal workflow.

## Security

- API keys are AES-encrypted with a machine-derived key and never logged.
- Filenames are sanitized before they are used as paths.
- URL opening goes through `UrlService` with an `explorer.exe` fallback.
- ImageMagick resource limits (512 MB memory, 4 threads) protect against pathological images.

## Testing and CI

The test project covers services, managers, models, and AI clients with xUnit, FluentAssertions, and Moq. GitHub Actions builds and tests every push and pull request on Windows. See [Testing](testing.md) and [CI/CD](ci-cd.md).

## Related pages

- [Project Structure](project-structure.md)
- [Building & Running](building.md)
- [Testing](testing.md)
