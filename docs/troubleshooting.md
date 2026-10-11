# Troubleshooting

This page covers general problems on Windows, Linux, and macOS. For AI-specific issues see [AI Troubleshooting](ai/troubleshooting.md).

## The application does not start

| Check | Action |
|-------|--------|
| .NET runtime | Install the [.NET 10.0 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (on Windows choose **.NET Desktop Runtime**) |
| Log file | Read the rolling `app<yyyyMMdd>.log` in the per-user data folder for the exact failure |
| Corrupt settings | Delete `Settings.dat` from the per-user data folder to reset to defaults |
| Permissions | Screenshots are the only files written next to the executable; if saving them fails, move the application to a user-writable folder such as `~/apps/FindRomCover` or rely on the fallback to the per-user data folder. Logs and settings already live in the per-user data folder |
| Antivirus | Some scanners flag single-file .NET executables; allow the file if you trust it |
| Linux/macOS | Ensure the binary is executable (`chmod +x FindRomCover`) and start it from a terminal once to see any startup message |

A fatal startup error is shown in a dialog and written to the log.

## The embedded web view is missing or blank

The Google Web and Bing Web tabs use the platform web view:

| Platform | Runtime | Notes |
|----------|---------|-------|
| Windows | Microsoft Edge WebView2 Runtime | Pre-installed on most Windows 10/11 systems |
| macOS | WKWebView | Part of the operating system |
| Linux | WebKitGTK | Install the WebKitGTK runtime from your distribution's package manager |

If the runtime is unavailable, FindRomCover explains the problem and can open the runtime download page or the system browser instead; all other features keep working. To recover the embedded tabs:

1. Follow the in-app prompt to open the runtime download page (or install WebKitGTK on Linux).
2. Install the runtime and restart FindRomCover.
3. If the problem persists, update your operating system and reinstall the runtime.

## No search results

| Tab | What to try |
|-----|-------------|
| Local Files | Lower the similarity threshold, switch algorithm, check the image folder |
| Google Web / Bing Web | Refine the Extra Query; verify the browser loads at all |
| Google API | Check the API key, quota, and Extra Query |

Also make sure the selected game has a meaningful name; enable MAME descriptions for arcade titles.

## "API Key is not set"

Open `Settings > API Settings...` and enter a Google API key. The Local Files and web tabs do not need a key.

## Images are not saving or converting

1. Verify the image folder is writable.
2. For web searches, save the image manually inside the image folder — automatic one-click saving is not possible due to browser security.
3. Confirm a game is selected; the watcher renames new images to the selected game.
4. Check the log for `ImageFolderWatcher` or conversion errors.

## "Image could not be downloaded" or non-image responses

Some image hosts answer automated requests with an HTTP 200 status but send an HTML block page instead of image data. FindRomCover detects this before converting:

1. The download is skipped with a log warning naming the host, the returned Content-Type, and the start of the payload.
2. When a provider thumbnail is available (Google API results, AI Batch Fill), it is tried automatically — but it is only saved if it meets the **Min cover width** setting (default 200 px); undersized thumbnails are rejected and the candidate is skipped.
3. Otherwise the candidate is skipped and the next one is tried.

This is a host-side block, not an application bug — try another result, another search tab, or a different model run. Entries mentioning `no decode delegate` for the same reason are logged as warnings, not errors.

## Missing or corrupted MAME data (`mame.dat`)

`mame.dat` ships with the application and is copied to the output folder on build.

- If it is missing, copy a fresh one next to the `FindRomCover` executable.
- If it is corrupt, replace it with a fresh copy.
- Without it, MAME descriptions are unavailable and searches fall back to cleaned filenames.

## Settings were reset unexpectedly

FindRomCover quarantines unreadable files instead of failing:

| File | Meaning |
|------|---------|
| `Settings.dat.corrupt` | Settings database that could not be read; defaults were restored |
| `settings.dat.legacy` | Backup of the old encrypted settings file after SQLite migration |
| `settings.xml.migrated` | Legacy `settings.xml` moved aside after a successful migration |
| `QueryHistory.dat.corrupt` | Query-history database that could not be read; a fresh one was created |

The quarantine file appears in the folder that held the unreadable file (the per-user data folder or the application folder). To recover, restore a backup of the per-user data folder. See [Data Storage](configuration/data-storage.md).

## The application is slow

| Cause | Suggestion |
|-------|------------|
| Network image folder | Move covers to a local disk |
| Huge collection | Narrow the supported extensions; rely on the trigram index |
| Slow local AI model | Lower **Image max size** and **Max candidates**, raise the AI timeout |
| Many results loading | Lower the thumbnail size |

## Errors keep appearing

1. Open the log window (`Settings > Show/Hide Log Window`).
2. Note the error message and when it happens.
3. Search the [GitHub Issues](https://github.com/purelogiccode/FindRomCover/issues) for the message.
4. If it is new, open an issue with the log excerpt and reproduction steps.

## Reporting a bug

Include:

- the application version and OS/architecture;
- steps to reproduce;
- the relevant log entries;
- whether Google API or AI Assist was involved.

Remove any paths or information you do not want to share before posting. Automatic bug reports and their privacy implications are described in [Bug Reports](configuration/bug-reports.md).

## Linux and macOS notes

- **Case-sensitive file systems** — cover filenames must match the ROM base name exactly. On Windows the check is case-insensitive; on Linux and macOS `sonic.png` does not cover `Sonic.zip`.
- **Web view** — install WebKitGTK from your distribution's package manager; WKWebView is included with macOS.
- **Tray icon** — minimizing hides the window to the tray; some Linux desktop environments do not display tray icons, so use the taskbar or close the window instead.
- **Screenshots** — the `Screenshot` folder next to the executable is tried first; the per-user data folder is used when the application folder is not writable.
- **Settings encryption** — the key is derived from the machine name, so copying `Settings.dat` between machines invalidates stored API keys.

## Related pages

- [FAQ](faq.md)
- [Bug Reports](configuration/bug-reports.md)
- [AI Troubleshooting](ai/troubleshooting.md)
- [Logs & Diagnostics](configuration/logs.md)
- [Data Storage](configuration/data-storage.md)
