# Troubleshooting

This page covers general problems. For AI-specific issues see [AI Troubleshooting](ai/troubleshooting.md).

## The application does not start

| Check | Action |
|-------|--------|
| .NET Desktop Runtime | Install the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Log file | Read `error.log` next to the executable for the exact failure |
| Corrupt settings | Delete `%LocalAppData%\FindRomCover\Settings.dat` to reset to defaults |
| Permissions | Move the application to a user-writable folder such as `C:\Tools\FindRomCover` |
| Antivirus | Some scanners flag single-file .NET executables; allow the file if you trust it |

A fatal startup error is shown in a dialog and written to the log.

## "WebView2 Runtime Missing" or "WebView2 component is not ready"

The Google Web and Bing Web tabs need the Microsoft Edge WebView2 Runtime.

1. Follow the in-app prompt to download the runtime from Microsoft.
2. Install it and restart FindRomCover.
3. If the problem persists, update Windows and reinstall the runtime.

All other features keep working without WebView2.

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

## Missing or corrupted MAME data (`mame.dat`)

`mame.dat` ships with the application and is copied to the output folder on build.

- If it is missing, copy a fresh one next to `FindRomCover.exe`.
- If it is corrupt, replace it with a fresh copy.
- Without it, MAME descriptions are unavailable and searches fall back to cleaned filenames.

## Settings were reset unexpectedly

FindRomCover quarantines unreadable settings instead of failing:

| File | Meaning |
|------|---------|
| `settings.dat.legacy` | Backup of the old encrypted settings file after SQLite migration |
| `settings.dat.corrupt` | Settings database that could not be read; defaults were restored |

To recover, restore a backup of `%LocalAppData%\FindRomCover`. See [Data Storage](configuration/data-storage.md).

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

- FindRomCover version and Windows version/architecture;
- steps to reproduce;
- the relevant log entries;
- whether Google API or AI Assist was involved.

Remove any paths or information you do not want to share before posting.

## Related pages

- [FAQ](faq.md)
- [AI Troubleshooting](ai/troubleshooting.md)
- [Logs & Diagnostics](configuration/logs.md)
- [Data Storage](configuration/data-storage.md)
