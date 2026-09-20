# Release Notes

Complete version history for FindRomCover. Download links for each release are available on the [Releases](https://github.com/purelogiccode/FindRomCover/releases) page.

## 3.2.0 — Unreleased

> Included in the next build. Download links will be added when 3.2.0 ships.

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
- **Fixed empty AI responses**: reasoning models could spend the entire 400-token output limit on internal reasoning and return no text; the limit is now 4,096 tokens, and a clear error explains the cause if a model still runs out.

### Under the Hood

- New `IVisionModelClient` abstraction with native Anthropic Messages API and Gemini `generateContent` adapters; OpenAI, GLM, local, and custom OpenAI-compatible endpoints share one adapter, while custom Anthropic-compatible endpoints reuse the Anthropic adapter.
- **Settings moved to SQLite**: all settings (including encrypted API keys) now live in `%LocalAppData%\FindRomCover\Settings.dat`. The old encrypted `settings.dat` is migrated automatically on first start and kept as `settings.dat.legacy`.
- AI request failures are logged as warnings and never trigger the automatic bug-report pipeline.
- Version bumped to **3.2.0** (not released yet).
- GitHub Actions CI now builds and tests every push and pull request on Windows.

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
- **Less log noise** — benign events logged at Debug level; the bug-report sink only sends Error-level events.
- Dependency updates: Magick.NET 14.15.0, MessagePack 3.1.8, WebView2 1.0.4078.44, Serilog 4.4.0.

## 3.0.0 — 2026-07-06

Major release that introduced the tabbed interface and local similarity search.

### New Features

- **Tabbed search interface** — Local Files, Google Web, Bing Web, and Google API tabs, each keeping its own results.
- **Local file similarity search** — Jaro-Winkler, Levenshtein, and Jaccard algorithms with a 10–90% threshold and trigram indexing for large folders.
- **System tray integration** — minimize to tray with restore/exit and balloon notifications.
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
