# Release Notes

Complete version history for FindRomCover. Download links for each release are available on the [Releases](https://github.com/purelogiccode/FindRomCover/releases) page.

## 3.4.0 — 2026-10-21

### New

- **Custom AI request parameters** — AI Settings now has an advanced JSON editor whose contents are merged into every vision request body. Tune temperature, `top_p`, output-token budgets, reasoning options, or provider-specific fields without an application update. Nested objects are merged recursively, `null` removes a default, and structural keys are protected. See [AI Settings](ai/settings.md#custom-request-parameters) for examples.

### Fixes & Improvements

- **Missing WebView2 runtime handled cleanly** — a missing or incompatible WebView2 runtime/loader is classified as an environmental failure, logged as a transient warning, and no longer produces a generic error dialog or automatic bug report ([#68439](https://github.com/purelogiccode/FindRomCover/issues/68439), [#68440](https://github.com/purelogiccode/FindRomCover/issues/68440)).
- **Web-tab covers convert reliably** — Google Web and Bing Web saves use the same tolerant Magick.NET pipeline as manual saves. Mislabeled `.png`/`.jpg` files, WebP/JPEG content with the wrong extension, truncated PNGs, and `.jfif`/`.jpe`/`.apng` files are normalized to real PNGs. The watcher waits for a stable file and retries transient failures.
- **AI parameter validation** — invalid custom JSON is rejected when saving; an invalid hand-edited value is ignored and logged at runtime.

### Under the Hood

- **930 passing tests** (up from 885).
- CI now covers warnings-as-errors builds, coverage artifacts, cross-platform builds, formatting/analyzers, CodeQL, dependency review, grouped Dependabot updates, release version validation, and documentation/wiki generation on every pull request.
- `master` has rulesets that block force pushes and deletion and require all CI, CodeQL, Pages, and wiki checks before a pull request merges.
- Meziantou.Analyzer 3.0.315.
- Version bumped to **3.4.0**.

### Downloads

- `release_3.4.0_win-x64.zip` — Windows x64
- `release_3.4.0_win-arm64.zip` — Windows ARM64
- `release_3.4.0_linux-x64.zip` — Linux x64
- `release_3.4.0_linux-arm64.zip` — Linux ARM64
- `release_3.4.0_osx-arm64.zip` — macOS Apple Silicon

> **Note:** there is no Intel-Mac build: Magick.NET 15 (the image conversion engine) no longer ships a macOS x64 native library.

All builds are framework-dependent single-file executables and require the [.NET 10.0 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (the base runtime; the Windows Desktop Runtime is not required because the UI is Avalonia). Each archive also contains `LICENSE.txt`, `README.md`, and `WhatsNew.md`.

## 3.3.1 — 2026-10-16

### Fixes & Improvements

- **Web view startup/shutdown errors removed** — Avalonia's WebView2 adapter can throw `Operation aborted` (E_ABORT) while the window opens or `ERROR_INVALID_STATE` while it closes; both are framework races, not application bugs. They are now recognized in the global exception handlers and logged as transient events instead of appearing as runtime errors ([#68285](https://github.com/purelogiccode/FindRomCover/issues/68285), [#68286](https://github.com/purelogiccode/FindRomCover/issues/68286), [#68251](https://github.com/purelogiccode/FindRomCover/issues/68251), [#68252](https://github.com/purelogiccode/FindRomCover/issues/68252)).
- **Writable WebView2 profile** — each web tab gets its own data folder under `%LocalAppData%\FindRomCover\WebView2`, so the embedded browser initializes even from launcher-managed or read-only installations instead of aborting.
- **Tolerant image conversion** — "Use This Image" no longer fails with `Magick.NET error` for files whose extension does not match their content (or partially written PNGs with CRC mismatches). The strict extension-based read falls back to content detection with `ignore-crc`, mirroring the display path ([#68280](https://github.com/purelogiccode/FindRomCover/issues/68280), [#68282](https://github.com/purelogiccode/FindRomCover/issues/68282), [#68283](https://github.com/purelogiccode/FindRomCover/issues/68283)).
- **Real error details in save failures** — the `Magick.NET` log context now includes the exception message instead of only the source path, so field reports are diagnosable.
- **Single model ID enforcement** — pasting multiple model IDs (for example the whole recommended-models list) into the AI Settings Model box made every request fail with "... is not a valid model ID" ([#68255](https://github.com/purelogiccode/FindRomCover/issues/68255)). Saving or testing now keeps only the first ID and informs the user; `GetEffectiveAiModel` applies the same rule at runtime, so hand-edited settings cannot send a combined ID.
- **Screenshot delegate fix** — the screenshot write delegate no longer captures the bitmap across its `using` scope.

### Under the Hood

- **885 passing tests** (up from 879): regressions cover content-sniffing image conversion, multi-ID model resolution, and screenshot saving.
- **Single-file bundles** — `IncludeNativeLibrariesForSelfExtract=true` embeds Skia, HarfBuzz, Magick.NET, and SQLite natives in the executable; builds remain framework-dependent (the .NET 10 runtime is never bundled).
- The legacy WPF project was removed from the repository and the application icon restored for every window, the binary, and the tray (`FindRomCover/icon/`).
- Dependency updates: Avalonia.Controls.WebView 12.1.1, MessagePack 3.1.12, Microsoft.Data.Sqlite / DependencyInjection 10.0.13, EFCore analyzers 10.0.13, Meziantou.Analyzer 3.0.303, .NET test SDK 18.11.0.
- Version bumped to **3.3.1**.

### Downloads

- `release_3.3.1_win-x64.zip` — Windows x64
- `release_3.3.1_win-arm64.zip` — Windows ARM64
- `release_3.3.1_linux-x64.zip` — Linux x64
- `release_3.3.1_linux-arm64.zip` — Linux ARM64
- `release_3.3.1_osx-arm64.zip` — macOS Apple Silicon

> **Note:** there is no Intel-Mac build: Magick.NET 15 (the image conversion engine) no longer ships a macOS x64 native library.

All builds are framework-dependent single-file executables and require the [.NET 10.0 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (the **Desktop Runtime** on Windows). Extract and run `FindRomCover.exe` on Windows or `FindRomCover` on Linux/macOS. Each archive also contains `LICENSE.txt`, `README.md`, and `WhatsNew.md`.

## 3.3.0 — 2026-10-11

### Cross-Platform Release

- **Avalonia UI** — the application was migrated from WPF to **Avalonia 12.1.4** and now runs on Windows, Linux, and macOS from a single codebase. Windows and views use the `.axaml`/`.axaml.cs` extensions, and the MahApps look (exact palette, accent colors, dark/light themes) is preserved.
- **Embedded web views** — WebView2 was replaced by Avalonia's NativeWebView, which uses WebView2 on Windows, WKWebView on macOS, and WebKitGTK on Linux. When the engine is missing, the affected tab falls back to the system browser and a one-time prompt links to the correct runtime per operating system.
- **Per-OS services** — click sounds (MCI on Windows, `afplay` on macOS, `paplay`/`ffplay`/`mpg123`/`aplay` on Linux, with a silent fallback when none is available), screenshots, and data paths all use platform-appropriate implementations.
- **Solution cleanup** — the solution contains only `FindRomCover` and `FindRomCover.Tests`; the former WPF project has been removed from the repository (its icon assets live on in `FindRomCover/icon/`). Service interfaces moved to `Interfaces/` and models/enums to `Models/`; asynchronous methods now use the `Async` suffix throughout.

### Logging and Diagnostics

- **Full logging pipeline** — Serilog remains the logging core, now with a `BugReportSink` that forwards every **Warning-or-higher** event to the bug-report API while suppressing transient environmental failures (network errors, startup statistics, update checks, audio devices) and file-system watcher errors through structured markers.
- **Richer bug reports** — automatic reports include the Windows, Linux, or macOS version; the error logger only removes the accepted report from its pending queue, so unsent reports are never wiped.
- **Log collection hardened** — log collection is locked so entries cannot be lost or truncated during concurrent reporting.

### Startup, Updates, and Screenshots

- **Startup statistics** — `ApplicationStatsService` records an anonymous, best-effort startup hit; failures are transient and never filed as bugs.
- **GitHub update check** — the application checks the latest GitHub release at startup and offers to open the download page when a newer version exists.
- **F8 screenshots** — screenshots are saved to a `Screenshot` folder next to the application, falling back to `%LocalAppData%\FindRomCover` when that folder is not writable (for example a `Program Files` installation), with collision-safe file names.

### UI Polish

- Tooltips added to every control in every window.
- A custom `ProgressRing` control and shared `AppStyles.axaml` replace the previous MahApps equivalents.
- Lifecycle and threading fixes across `DelegateCommand`, `TrayIconManager`, `ProgressRing`, and the AI batch window.

### AI Assist

- **Atomic verdict cache** — `AiVerdictCache` writes atomically, normalizes keys, and is shared process-wide, so the main window and the batch window can never overwrite each other's entries; corrupt entries are discarded on read.
- **Corrupt history quarantine** — a non-SQLite or corrupt `QueryHistory.dat` is moved to `QueryHistory.dat.corrupt` and a fresh database is created, so a bad file cannot disable the feature forever.
- **Bounded downloads** — AI candidate image downloads enforce a maximum size and a timeout.
- **Cancellation cleanup** — AI Batch Fill cleans up temporary files and pre-registration state on cancellation and refreshes registrations before moving files.
- **Prompt and parse robustness** — verdict parsing prefers the JSON object in a response, accepts integral confidence values, and cancellation is handled consistently; malformed responses produce clear warnings instead of failures.

### Settings and Reliability

- **Safe secret encryption** — stored secrets are never overwritten with empty values, and encryption failures are reported as transient warnings instead of crashing the save pipeline.
- **Migration safety** — a legacy `settings.xml` is kept until the migrated SQLite save succeeds, and null legacy values no longer break migration.
- **Correct cover targets** — recycled context menus refresh their target, so "Use This Image" always saves to the currently selected ROM.
- **Protected query history** — a healthy `QueryHistory.dat` is only quarantined on real SQLite corruption, never on transient I/O failures.
- **Quieter bug reports** — environmental failures (network, invalid keys, corrupt user images, unwatcheable folders) are logged locally instead of filing automatic reports; genuine failures still report exactly once.
- **UI fixes** — window commands are assigned before the bound data context (so F5 and Escape work), invalid folder paths cancel running scans (including in-flight AI picks), and the debug window trims removed entries.
- **Dialog fallback** — the fallback dialog path returns the entered text instead of an empty result.

### Under the Hood

- **879 passing tests** (685 `[Fact]` plus 194 `[InlineData]` theory cases) across 68 files; `xunit.runner.json` disables collection parallelization, and `TestBootstrap` isolates tests from the real settings folder and the bug-report API.
- Version bumped to **3.3.0**.

### Downloads

- `release_3.3.0_win-x64.zip` — Windows x64
- `release_3.3.0_win-arm64.zip` — Windows ARM64
- `release_3.3.0_linux-x64.zip` — Linux x64
- `release_3.3.0_linux-arm64.zip` — Linux ARM64
- `release_3.3.0_osx-arm64.zip` — macOS Apple Silicon

> **Note:** there is no Intel-Mac build: Magick.NET 15 (the image conversion engine) no longer ships a macOS x64 native library.

All builds are framework-dependent and require the [.NET 10.0 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (the **Desktop Runtime** on Windows). Extract and run `FindRomCover.exe` on Windows or `FindRomCover` on Linux/macOS. Each archive also contains `LICENSE.txt`, `README.md`, and `WhatsNew.md`.

## 3.2.2 — 2026-09-25

### Improvements

- **Settings menu cleanup** — `Ignore Bracketed Text in Matching` now lives under the **Settings** menu instead of the top level, grouping it with the other matching and display toggles.

### Under the Hood

- Removed an unused `ParseDouble` helper from the AI Settings window.
- Normalized for-loop spacing in `ImageSaveService`.
- Added CI, Docs, .NET, downloads, stars, issues, last-commit, and wiki badges to the README.

### Downloads

- `release_3.2.2_win-x64.zip`
- `release_3.2.2_win-arm64.zip`

## 3.2.1 — 2026-09-24

### Improvements

- **New default OpenRouter model** — the OpenRouter provider now defaults to `google/gemma-4-26b-a4b-it`, which follows the cover/box-art instructions more reliably than the previous default. Existing installations keep their configured model; the new default applies to fresh setups and whenever the Model field is left empty.

### Downloads

- `release_3.2.1_win-x64.zip`
- `release_3.2.1_win-arm64.zip`

## 3.2.0 — 2026-09-24

### AI Vision Assist (New)

- **AI Pick Best** on the Local Files and Google API tabs: a vision-capable model looks at the candidate images and highlights the best cover (green badge, moved to the front).
- **Cloud or local models**: OpenRouter, OpenAI, Anthropic (Claude), Google Gemini and GLM (Zhipu/Z.AI), plus local OpenAI-compatible servers (Ollama, LM Studio) and custom OpenAI/Anthropic-compatible endpoints.
- **Default OpenRouter model** is now `qwen/qwen3.7-flash`, the cheapest vision-capable model on OpenRouter (roughly $0.21 per 1,000 cover picks).
- **AI Settings** window: provider presets, encrypted API key storage, an editable model picker with **Test / Load Models**, request timeout, candidate/image limits, auto-save threshold, auto-run and verify-on-save toggles.
- **Smart model picker**: the loaded model list is filtered to vision-capable models by default (using provider modality metadata where available and a model-name heuristic otherwise), searchable with a filter box, and cached per provider for 7 days.
- **Verify before saving**: the folder watcher asks the model to confirm that a newly saved image matches the selected game before renaming it. Mismatches are left untouched and reported.
- **AI Batch Fill**: process the whole missing-covers list — local candidates first, optional Google API fallback — saving only when the confidence is above the threshold, with live progress, cancel, and per-item results.
- **AI candidate similarity threshold**: the filename matcher pre-filters candidates; only local images whose filename similarity is at or above the configured value (default 70%) are sent to the AI.
- **No duplicate queries**: covers already queried are remembered across sessions in the SQLite database `QueryHistory.dat`; Batch Fill skips them, and the history can be reviewed and cleared in AI Settings.
- Images are downscaled before upload (default 512px) and verdicts are cached for 30 days.
- **Minimum cover size** — downloads narrower than the configured **Min cover width** (default 200px) are rejected, so tiny thumbnails never become covers.
- **Cover-first API picks** — when ranking Google API results, the model is told to prefer real cover art or an in-game screenshot over a photo of a physical cartridge, cart, or disc.
- **Fixed empty AI responses**: reasoning models could spend the entire 400-token output limit on internal reasoning and return no text; the limit is now 4,096 tokens, and a clear error explains the cause if a model still runs out.
- **More reliable batch downloads**: images are requested with browser-like headers, and when a source blocks the full-size Google API image, the provider-hosted thumbnail is used as a fallback instead of failing.
- **Accurate already-filled detection**: cover detection now recognizes every supported image format and sanitized filenames, so the missing list no longer keeps games whose covers are already on disk; Batch Fill reports them once and removes them, and the list refreshes after every run.

### Fixes & Improvements

- **Ignore bracketed text when matching** — a new `Ignore Bracketed Text in Matching` menu option (enabled by default) removes balanced `(...)`, `[...]`, and `{...}` groups from ROM and image names before scoring, so `Game (USA) [En]` matches a cover named `Game`. Works with all three similarity algorithms and with AI candidate pre-filtering.
- **Image download failures no longer trigger bug reports** — a dead or unreachable image host (connection timeout, refused connection, reset) is now logged as a warning and the thumbnail fallback is still tried, instead of aborting the download and filing an automatic bug report.
- **"File is being used by another process" when saving covers** — each save attempt now writes to a uniquely named temp file and write failures after all retries return a clear error dialog instead of an unhandled exception, fixing collisions from double-clicks, AI auto-save racing a manual save, and antivirus/sync locks.
- **Save failure details in the UI** — when a cover cannot be saved, the dialog and log now include the real reason (locked file, permissions) instead of a generic "Failed to save the image." message.
- **Watched folders recover automatically** — a folder that becomes temporarily unreadable (permissions, disconnected drive, antivirus) is logged as a warning, the watcher retries with backoff, and it no longer files automatic bug reports.
- **Missing-list selection is kept** — removing an item keeps the selection on the nearest entry instead of resetting to the top.
- **Anthropic and Gemini providers now actually work** — the provider adapters were never selected at runtime, so those requests went to an OpenAI-compatible endpoint and failed. Picks and verification now use the correct native adapter per provider.
- **Covers are protected from accidental replacement** — Google API downloads are verified before they replace an existing cover, the folder watcher no longer overwrites an existing PNG, and startup cleanup only removes the app's own temp files instead of any file containing `.tmp`.
- **Picks never land on the wrong game** — verification, saving, and missing-list removal use the ROM that was selected when the action started, even if the selection changes while the AI is still working.
- **Google API timeouts handled properly** — a timeout now shows a real error instead of "Search canceled." with a stuck progress state, and no longer aborts an entire AI batch fill.
- **OpenAI reasoning models fixed** — `gpt-5-nano` and o-series models are called with `max_completion_tokens` and without `temperature`, which the OpenAI API requires.
- **Large missing lists no longer freeze the AI Batch window** — query-history status is loaded in a single query instead of one database open per item.
- **Sturdier settings storage** — an unreadable settings database is preserved as `Settings.dat.corrupt` instead of being overwritten with defaults, and legacy settings are kept until the SQLite save succeeds.

### Under the Hood

- New `IVisionModelClient` abstraction with native Anthropic Messages API and Gemini `generateContent` adapters; OpenAI, GLM, local, and custom OpenAI-compatible endpoints share one adapter, while custom Anthropic-compatible endpoints reuse the Anthropic adapter.
- **Settings moved to SQLite**: all settings (including encrypted API keys) now live in `%LocalAppData%\FindRomCover\Settings.dat`. The old encrypted `settings.dat` is migrated automatically on first start and kept as `settings.dat.legacy`.
- **Full documentation**: a professional documentation site (GitHub Pages) and a synchronized GitHub wiki with side navigation, including release notes and recommended AI models. CI publishes both.
- **Dependency updates**: Magick.NET, NAudio, Microsoft.Extensions.DependencyInjection, the Roslyn/Meziantou analyzers and the .NET test SDK were updated.
- **Log writes are serialized**: the error logger clears and appends under one lock, so entries cannot be lost or truncated during concurrent reporting.
- AI request failures are logged as warnings and never trigger the automatic bug-report pipeline.
- Version bumped to **3.2.0**.
- GitHub Actions CI now builds and tests every push and pull request on Windows.

### Downloads

- `release_3.2.0_win-x64.zip`
- `release_3.2.0_win-arm64.zip`

## 3.1.0 — 2026-09-02

### Fixes & Improvements

- **Automatic scan after selecting a folder** — browsing to the ROM or Image folder now scans automatically as soon as both folders are set ([#1](https://github.com/purelogiccode/FindRomCover/issues/1)).
- **Clear scan feedback** — the status bar reports how many ROM files matched the supported extensions and how many are missing covers; an explicit message replaces a silently empty list ([#1](https://github.com/purelogiccode/FindRomCover/issues/1)).
- **Refresh after editing supported extensions** — changing the extension list re-scans immediately.
- **Repository migrated** — the project now lives at [purelogiccode/FindRomCover](https://github.com/purelogiccode/FindRomCover); the update checker follows the new releases page.

### Under the Hood

- A corrupt `settings.dat` is quarantined as `settings.dat.corrupt` and defaults are restored instead of failing.
- Managed PBKDF2 fallback so settings decryption no longer crashes on systems where the OS cryptographic implementation fails.
- Friendlier Google Custom Search API error messages (invalid API key, quota exceeded, bad requests).
- More robust link opening with an `explorer.exe` fallback.

### Downloads

- `release_3.1.0_win-x64.zip`
- `release_3.1.0_win-arm64.zip`

## 3.0.1 — 2026-07-12

- **WebView2 graceful fallback** — when the runtime is missing, web search tabs are unavailable but the rest of the application works; no repeated error logs.
- **Image conversion improvements** — corrupt or misformatted images are detected immediately and skipped without retries; retries remain for transient I/O failures.
- **Less log noise** — benign events logged at Debug level; the bug-report sink only sends Error-level events *(changed in 3.3.0: the sink forwards Warning and higher events, with transient environmental failures suppressed)*.
- Dependency updates: Magick.NET 14.15.0, MessagePack 3.1.8, WebView2 1.0.4078.44, Serilog 4.4.0.

## 3.0.0 — 2026-07-06

Major release that introduced the tabbed interface and local similarity search.

### New Features

- **Tabbed search interface** — Local Files, Google Web, Bing Web, and Google API tabs, each keeping its own results.
- **Local file similarity search** — Jaro-Winkler, Levenshtein, and Jaccard algorithms with a 10–90% threshold and trigram indexing for large folders.
- **System tray integration** — minimize to tray with restore/exit and balloon notifications *(changed in 3.3.0: tray balloons are not supported by Avalonia cross-platform and are a no-op; restore and exit remain)*.
- **Keyboard shortcuts** — F5 refresh and more.
- **Image folder watcher** — detects new covers, converts them to PNG, renames them to the selected game, and updates the missing list.
- **Delete ROM from missing list** — right-click to permanently delete the ROM/ISO after confirmation.

### Improvements

- Settings encrypted with AES using a machine-specific PBKDF2 key; legacy `settings.xml` migrated automatically.
- Serilog structured logging with a live log viewer and rolling 7-day files.
- Dependency injection with `Microsoft.Extensions.DependencyInjection`.
- Extended image format support (HEIC, HEIF, ICO, SVG, JXL, JP2, AVIF) and Magick.NET resource limits.
- Graceful startup/shutdown, orphaned temp-file cleanup, and global exception handlers.
- Better Google API error messages for 429, 403, and 400 responses.
- Filename sanitization to prevent path traversal.

### Breaking Changes

- PlaySound service replaced by `IAudioService` with a null fallback.
- `settings.xml` no longer used.
- `MameManager` no longer a serializable DTO; use `MameData`.

## 2.9.0 — 2026-05-25

- Fixed similarity calculation for empty strings (now 100% instead of NaN or 0).
- Handled `InvalidWmpVersionException` in the audio service.
- Added `UpdateCheckOrchestrator` and `UpdateNotificationInfo` for cleaner update flow.
- Extensions sorted alphabetically; accessibility tooltips added.
- Improved error handling and shutdown robustness; fatal startup dialog added.
- Dependency and test coverage updates.

## 2.8.0 — 2026-05-12

- **Performance**: trigram indexing for large image folders, Levenshtein early bailout, pre-computed Jaccard n-grams, pooled buffers in Jaro-Winkler, progressive image loading.
- **Features**: GitHub update checking, anonymized usage tracking, cross-instance settings synchronization.
- **UI/UX**: tooltips throughout, improved empty states, loading overlay styles.
- **Testing**: new unit test project with 214 tests.
- Removed `MameManager`, replaced by `MameDataService`.

## 2.7.0 — 2026-04-14

- Comprehensive XML documentation across services and models.
- Code quality improvements and smaller refactors.
- Dependency updates and additional tests.

## Older releases

| Version | Date | Highlights |
|---------|------|------------|
| [2.6.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.6.0) | 2026-02-22 | Key UI improvements: loading overlays with progress indicators |
| [2.5.1](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.5.1) | 2026-01-21 | Dependency updates and image loading resilience |
| [2.5.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.5.0) | 2026-01-08 | Magick.NET replaces GDI+ for image processing |
| [2.4.2](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.4.2) | 2026-01-03 | Improved error logging |
| [2.4.1](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.4.1) | 2025-11-28 | Audio service crash prevention |
| [2.4.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.4.0) | 2025-11-18 | Upgraded to .NET 10 and C# 14 |
| [2.3.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.3.0) | 2025-09-17 | Codebase update |
| [2.2.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release_2.2.0) | 2025-07-26 | UI updates |
| [2.1.1](https://github.com/purelogiccode/FindRomCover/releases/tag/release2.1.1) | 2025-07-02 | Library updates |
| [2.1](https://github.com/purelogiccode/FindRomCover/releases/tag/release2.1) | 2025-04-29 | ButtonFactory and command enhancements |
| [2.0.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release2.0.0) | 2025-03-15 | Codebase update |
| [1.10](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.10) | 2025-03-07 | GDI+ error fixes, more robust image saving |
| [1.8.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.8.0) | 2025-01-14 | .NET Core 9, library updates, bug fixes |
| [1.7.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.7.0) | 2024-12-08 | Alphabetical sorting for missing covers |
| [1.6.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.6.0) | 2024-11-28 | Right-click context menu |
| [1.4.0](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.4.0) | 2024-05-20 | Code improvements and bug fixes |
| [1.3.0.4](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.3.0.4) | 2024-02-28 | Added Jaccard and Levenshtein similarity algorithms |
| [1.2.0.3](https://github.com/purelogiccode/FindRomCover/releases/tag/release1.2.0.3) | 2024-02-05 | Initial release |

## Related pages

- [Installation](getting-started/installation.md)
- [Recommended Models](ai/recommended-models.md)
- [GitHub Releases](https://github.com/purelogiccode/FindRomCover/releases)
