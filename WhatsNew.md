# FindRomCover 3.2.0 — What's New

> Unreleased — included in the next build. Download links will be added when 3.2.0 ships.

## AI Vision Assist (New)

- **AI Pick Best** on the Local Files and Google API tabs: a vision-capable model looks at the candidate images and highlights the best cover (green badge, moved to the front).
- **Cloud or local models**: OpenRouter, OpenAI, Anthropic (Claude), Google Gemini and GLM (Zhipu/Z.AI), plus local OpenAI-compatible servers (Ollama, LM Studio) and custom OpenAI/Anthropic-compatible endpoints.
- **Default OpenRouter model** is now `qwen/qwen3.7-flash`, the cheapest vision-capable model on OpenRouter (roughly $0.21 per 1,000 cover picks).
- **AI Settings** window: provider presets, encrypted API key storage, an editable model picker with **Test / Load Models**, request timeout, candidate/image limits, auto-save threshold, auto-run and verify-on-save toggles.
- **Smart model picker**: the loaded model list is filtered to vision-capable models by default (using provider modality metadata where available and a model-name heuristic otherwise), searchable with a filter box, and cached per provider for 7 days so it is available instantly next time.
- **Verify before saving**: the folder watcher asks the model to confirm that a newly saved image matches the selected game before renaming it. Mismatches are left untouched and reported instead of being silently renamed.
- **AI Batch Fill**: process the whole missing-covers list — local candidates first, optional Google API fallback — saving only when the confidence is above the threshold, with live progress, cancel and per-item results.
- **AI candidate similarity threshold**: the existing filename matcher now pre-filters candidates — only local images whose filename similarity is at or above the configured value (default 70%) are sent to the AI, saving tokens and reducing noise.
- **No duplicate queries**: missing covers that were already queried (manually or by batch fill) are remembered across sessions in the SQLite database `QueryHistory.dat`, so the AI is not asked twice for the same cover. Batch Fill skips them, and the history can be reviewed and cleared in AI Settings.
- Images are downscaled before upload (default 512px) and verdicts are cached for 30 days to keep API usage low.
- **Fixed empty AI responses**: reasoning models (GPT-5 Nano, Qwen3.7 Flash, Gemini thinking models) could spend the entire 400-token output limit on internal reasoning and return no text; the limit is now 4,096 tokens, and a clear error explains the cause if a model still runs out.
- **More reliable batch downloads**: images are requested with browser-like headers, and when a source blocks the full-size Google API image, the provider-hosted thumbnail is used as a fallback instead of failing.
- **Accurate already-filled detection**: cover detection now recognizes every supported image format and sanitized filenames, so the missing list no longer keeps games whose covers are already on disk; Batch Fill reports them once and removes them, and the list refreshes after every run.

## Fixes & Improvements

- **Image download failures no longer trigger bug reports** — a dead or unreachable image host (connection timeout, refused connection, reset) is now logged as a warning and the thumbnail fallback is still tried, instead of aborting the download and filing an automatic bug report ([#67379](https://github.com/purelogiccode/FindRomCover/issues/67379), [#67382](https://github.com/purelogiccode/FindRomCover/issues/67382), [#67383](https://github.com/purelogiccode/FindRomCover/issues/67383), [#67432](https://github.com/purelogiccode/FindRomCover/issues/67432)).
- **"File is being used by another process" when saving covers** — each save attempt now writes to a uniquely named temp file and write failures after all retries return a clear error dialog instead of an unhandled exception, fixing collisions from double-clicks, AI auto-save racing a manual save, and antivirus/sync locks ([#67436](https://github.com/purelogiccode/FindRomCover/issues/67436), [#67437](https://github.com/purelogiccode/FindRomCover/issues/67437), [#67438](https://github.com/purelogiccode/FindRomCover/issues/67438)).
- **Save failure details in the UI** — when a cover cannot be saved, the dialog and log now include the real reason (locked file, permissions) instead of a generic "Failed to save the image." message.

## Under the Hood

- New `IVisionModelClient` abstraction with native Anthropic Messages API and Gemini `generateContent` adapters; OpenAI, GLM, local and custom OpenAI-compatible endpoints share one adapter, while custom Anthropic-compatible endpoints reuse the Anthropic adapter. Shared prompts, JSON parsing, timeouts and friendly error messages.
- **Settings moved to SQLite**: all settings (including API keys, encrypted) now live in `%LocalAppData%\FindRomCover\Settings.dat`. The old encrypted `settings.dat` is migrated automatically on first start and kept as `settings.dat.legacy`.
- **Full documentation**: a professional documentation site (GitHub Pages) and a synchronized GitHub wiki with side navigation, including release notes and recommended AI models. CI publishes both.
- AI request failures are logged as warnings and never trigger the automatic bug-report pipeline.
- Version bumped to **3.2.0** (not released yet).
- GitHub Actions CI now builds and tests every push and pull request on Windows.

---

# FindRomCover 3.1.0 — What's New

## Fixes & Improvements

- **Automatic scan after selecting a folder** — Browsing to your ROM or Image folder now scans automatically as soon as both folders are set. No more wondering why the list stays empty after using "Browse..." ([#1](https://github.com/purelogiccode/FindRomCover/issues/1))
- **Clear scan feedback** — The status bar now reports how many ROM files matched the supported extensions and how many are missing covers. If nothing matches, you get an explicit message instead of a silently empty list. ([#1](https://github.com/purelogiccode/FindRomCover/issues/1))
- **Refresh after editing supported extensions** — Changing the extension list (Settings > Edit Supported Extensions...) now re-scans immediately instead of showing stale results.
- **Repository migrated** — The project now lives at [purelogiccode/FindRomCover](https://github.com/purelogiccode/FindRomCover). The built-in update checker follows the new releases page.

## Under the Hood

- Hardened settings loading: a corrupt `settings.dat` is quarantined as `settings.dat.corrupt` and defaults are restored instead of failing.
- Added a managed PBKDF2 fallback so settings decryption no longer crashes on systems where the OS cryptographic implementation fails.
- Friendlier Google Custom Search API error messages (invalid API key, quota exceeded, bad requests) instead of raw JSON dumps.
- More robust link opening with an `explorer.exe` fallback for systems without a default browser association.
- Version bumped to **3.1.0**.

## Download

- `release_3.1.0_win-x64.zip` — Windows x64
- `release_3.1.0_win-arm64.zip` — Windows ARM64

Both builds are framework-dependent and require the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Extract and run `FindRomCover.exe`.
