# Logs & Diagnostics

FindRomCover uses Serilog for structured logging. Logs are written to rolling files and can be viewed live inside the application.

## The log window

Open the log window with `Settings > Show/Hide Log Window`. It shows a live stream of log entries with:

- **timestamp**
- **severity level**
- **message**
- optional exception details

The window is the fastest way to understand what the application is doing and why an operation failed. It can stay open while you work.

## Log files

| File | Contents |
|------|----------|
| `app<yyyyMMdd>.log` | Full application log, rolling daily, 7 files retained |
| `ApiLogError.txt` | Reports queued for the automatic bug-report API; an accepted report is removed after a successful send |
| `UserLogError.txt` | Simplified error list intended for user reference |
| `InternalLog.txt` | Internal diagnostics from the error logger |

All four are written to the per-user data folder (the same folder as `Settings.dat`) — see [Data Storage](data-storage.md) for the location on each operating system. They are never written next to the executable.

## Severity levels

Only the levels below exist; there is no `Verbose` level. The minimum level is `Debug`.

| Level | Meaning | Examples |
|-------|---------|----------|
| Debug | Fine-grained diagnostics | File watcher events, conversion skips |
| Information | Normal operations | Application start, settings saved |
| Warning | Recoverable problems | AI request failure, file disappeared after wait, access denied on the watched folder |
| Error | Failed operations | Image conversion failure, API errors, unhandled dispatcher exceptions |
| Fatal | Application cannot continue | Startup failure, unhandled AppDomain exception |

## What is logged

- Application startup, shutdown, and version information.
- Folder scans and missing-cover counts.
- Image conversions, retries, and watcher activity.
- API errors with provider messages (never API keys).
- AI request failures as warnings.
- Unhandled exceptions from the UI thread, background tasks, and the application domain.

## What is not logged

- API keys. Secret values are masked or omitted from every log entry.
- Image contents. Only filenames and paths are logged.
- Settings database contents.

## The error reporting pipeline

Events at **Warning level and above** are collected and can be sent to the Pure Logic Code bug-report API, which helps the developer fix problems. Two categories are never forwarded:

- **Transient warnings** logged through `LogService.WarningTransient` — expected environmental failures such as network errors, the update check, the screenshot folder fallback, settings and AI cache I/O, and secret encryption.
- **Watcher errors** logged through `LogService.ErrorWatcher` — per-file image-folder watcher failures such as corrupt or locked images.

These entries stay in the local log file and log window but do not create automatic reports. Reports contain the exception, a stack trace, and environment information — no personal files or images.

See [Bug Reports](bug-reports.md) for the full trigger rules and payload, and [Privacy](privacy.md) for every network request the application makes.

## Reading a log entry

Typical entry:

```text
2026-09-20 18:35:12.481 [Warning] AI batch fill: item 'Some Game (USA)' failed.
System.Net.Http.HttpRequestException: Connection refused
```

The timestamp and level tell you when and how serious the problem is; the message usually names the exact operation and item.

## Sharing logs when reporting an issue

1. Reproduce the problem.
2. Open the log window and note the error entries.
3. Attach the rolling `app<yyyyMMdd>.log` (or the relevant part) to your GitHub issue.
4. Remove any information you do not want to share — logs contain file paths.

## Troubleshooting logging itself

| Symptom | Solution |
|---------|----------|
| No log files | The per-user data folder could not be created; check permissions for your user profile |
| Log window is empty | Logging initializes on startup; restart the application |
| Log grows very large | Files roll over automatically and 7 files are retained |

## Related pages

- [Bug Reports](bug-reports.md)
- [Privacy](privacy.md)
- [Troubleshooting](../troubleshooting.md)
- [AI Troubleshooting](../ai/troubleshooting.md)
- [Data Storage](data-storage.md)
