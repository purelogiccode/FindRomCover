# Bug Reports

FindRomCover can send automatic bug reports to the developer when something goes wrong. This page explains what triggers a report, what is included, and how to turn automatic reporting off. See [Privacy](privacy.md) for the complete data-flow overview.

## When a report is sent

| Condition | Forwarded to the bug-report API? |
|-----------|----------------------------------|
| Log event at **Warning** level or above | Yes |
| Warning marked as transient (`LogService.WarningTransient`) | No |
| Watcher error (`LogService.ErrorWatcher`) | No |
| Debug or Information | No |

Transient warnings cover expected environmental problems: network failures, the GitHub update check, the screenshot folder fallback, settings and AI cache I/O, and secret encryption. Watcher errors cover per-file problems in the image-folder watcher, such as corrupt, locked, or dropped images. These entries remain in the local log but never create automatic reports, so an offline machine or a problematic image cannot flood the bug tracker.

## What a report contains

### Environment details

| Field | Description |
|-------|-------------|
| Date | Local date and time the report was created |
| Application name | `FindRomCover` |
| Application version | Running version, for example `3.3.0` |
| OS version | Runtime OS description |
| Architecture | For example `X64` or `Arm64` |
| Bitness | `32-bit` or `64-bit` |
| Friendly OS version | `Windows 11`, `Linux <version>`, `macOS <version>`, and so on |
| Processor count | Number of logical processors |
| Base directory | Folder the application runs from |
| Temp path | System temporary directory |

### Error details

The context message describing what was happening, followed by the exception type, message, source, and stack trace. When the exception wraps an inner exception, the inner exception is included as well. The message section is capped at about 4,000 characters and the stack trace at about 8,000 characters.

## Local queue files

Every report is written to `UserLogError.txt` and `ApiLogError.txt` in the per-user data folder before it is sent:

- `ApiLogError.txt` is the pending queue. When the API accepts a report, only that report is removed; reports that failed earlier or were queued concurrently stay in the file for the next attempt.
- `UserLogError.txt` keeps the full history of reports for your reference and is not trimmed automatically.

See [Data Storage](data-storage.md) for the folder location on Windows, Linux, and macOS.

## API endpoints

| Service | Endpoint | Purpose |
|---------|----------|---------|
| Bug-report API (`bugreport`) | `https://www.purelogiccode.com/bugreport/api/send-bug-report` | Receives automatic reports from the error logger |
| ApplicationStats API | `https://www.purelogiccode.com/ApplicationStats/stats` | Receives the anonymous startup statistics hit |
| ApplicationStats bug-report route | `https://www.purelogiccode.com/ApplicationStats/bugreport` | Bug-report route on the same ApplicationStats backend |

## What may be included

- Log messages and context text can contain local file paths and URLs, for example an image path or an API address.
- API keys are never included; secret values are masked or omitted before a report is written.
- No personal files, cover images, or settings contents are uploaded.

## Turning automatic reports off

There is no in-app switch. Reports require network access, so you can prevent them by:

- running FindRomCover offline, or
- blocking `www.purelogiccode.com` in your firewall.

Reports that cannot be sent remain queued in the local log files. See [Privacy](privacy.md) for the other requests the application makes.

## Filing a report manually

Automatic reports are anonymous. If you want a reply or a tracked issue, open a manual report on [GitHub Issues](https://github.com/purelogiccode/FindRomCover/issues) and include:

- the application version and OS/architecture;
- steps to reproduce;
- the relevant log entries;
- whether Google API or AI Assist was involved.

Remove any paths or information you do not want to share before posting.

## Related pages

- [Logs & Diagnostics](logs.md)
- [Privacy](privacy.md)
- [Data Storage](data-storage.md)
- [Troubleshooting](../troubleshooting.md)
