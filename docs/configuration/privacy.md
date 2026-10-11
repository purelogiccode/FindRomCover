# Privacy

FindRomCover runs locally and does not include analytics or tracking. This page lists every network request the application can make, what it contains, and how to reduce what leaves your machine.

## What leaves the machine

| Request | When | Data |
|---------|------|------|
| Startup statistics | Once per launch | Anonymous POST to `https://www.purelogiccode.com/ApplicationStats/stats` with `{"applicationId":"findromcover","version":"<version>"}` |
| Update check | Once per launch | GET to the public GitHub Releases API for the latest release tag |
| Automatic bug reports | On Warning level or above | Environment details, the error message, and exception details; may include local file paths and URLs |
| User-configured services | Only when you use them | Google Custom Search API queries, AI provider requests (including candidate images), and image downloads from search results |

### Startup statistics

At every launch the application sends a small anonymous hit to the ApplicationStats endpoint with the application identifier `findromcover` and the running version. The request uses a 15-second timeout and is fire-and-forget: it never blocks startup, and failures are ignored. No machine identifier, user name, or usage details are included.

### Update check

At startup the application asks the public GitHub Releases API for the latest release and compares its tag with the running version. Only the release tag (and release metadata such as the download page URL) is returned; no personal data is sent.

### Automatic bug reports

Events at Warning level or above are sent to the Pure Logic Code bug-report API unless they are marked as transient or watcher-related. A report contains environment details, the error message, and the exception stack trace. Log messages can contain local file paths and URLs. API keys, images, and settings contents are never included. See [Bug Reports](bug-reports.md) for the exact rules.

### User-configured services

These requests only happen when you use the corresponding feature:

- **Google Custom Search API** — the search query and your API key are sent to Google.
- **AI providers** — the prompt and the candidate cover images are sent to the provider you configured (cloud or local).
- **Image downloads** — when you save a result, the image URL is fetched from the hosting site.

## What is not collected

- No telemetry beyond the single anonymous startup statistics hit.
- No analytics, advertising, or cross-session tracking identifiers.
- No personal files, ROM contents, or cover images.
- No settings contents and no API keys in bug reports.
- No crash dumps or memory snapshots.

## Where your data is stored

- Settings live in `Settings.dat` (SQLite) in the per-user data folder. API keys are encrypted; the rest of the settings values are stored in plain text inside the database.
- Query history, AI caches, and logs are local files that are never uploaded. See [Data Storage](data-storage.md).

> **Note:** The encryption key for API keys is derived from the machine name, not from a user password. Anyone with access to the machine and the file can decrypt the secrets.

## Opting out

There is no in-app opt-out switch for the startup statistics or the automatic bug reports. To reduce or prevent data leaving the machine:

- run FindRomCover offline, or block `www.purelogiccode.com` (bug reports and statistics) and `api.github.com` (update check) in your firewall;
- avoid the Google API and cloud AI providers — Local Files search, the embedded browser, and local AI models (Ollama, LM Studio) work without sending data to those services.

## Related pages

- [Bug Reports](bug-reports.md)
- [Data Storage](data-storage.md)
- [Logs & Diagnostics](logs.md)
- [FAQ](../faq.md)
