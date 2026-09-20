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
| `app.log` | Full application log, rolling, 7-day retention |
| `error.log` | Error-level and above |
| `error_user.log` | Simplified error list intended for user reference |

All three are written next to `FindRomCover.exe`.

## Severity levels

| Level | Meaning | Examples |
|-------|---------|----------|
| Verbose / Debug | Fine-grained diagnostics | File watcher events, conversion skips |
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

Error-level events are collected and can be sent to the Pure Logic Code bug-report API, which helps the developer fix crashes.

- Only **Error**-level events trigger reports; warnings are not sent.
- AI failures are logged as warnings and never trigger the pipeline.
- Reports contain the exception, a stack trace, and environment information — no personal files.

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
3. Attach `error.log` (or the relevant part of `app.log`) to your GitHub issue.
4. Remove any information you do not want to share — logs contain file paths.

## Troubleshooting logging itself

| Symptom | Solution |
|---------|----------|
| No log files | The application folder may not be writable; move FindRomCover to a user-writable folder |
| Log window is empty | Logging initializes on startup; restart the application |
| Log grows very large | Files roll over automatically and are kept for 7 days |

## Related pages

- [Troubleshooting](../troubleshooting.md)
- [AI Troubleshooting](../ai/troubleshooting.md)
- [Data Storage](data-storage.md)
