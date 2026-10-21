# Data Storage

FindRomCover keeps configuration, caches, and logs in a per-user data folder; the MAME database ships next to the executable.

## Data folder

| Platform | Location |
|----------|----------|
| Windows | `%LocalAppData%\FindRomCover` (typically `C:\Users\<you>\AppData\Local\FindRomCover`) |
| Linux | `~/.local/share/FindRomCover` |
| macOS | `~/Library/Application Support/FindRomCover` |

The application creates the folder on first start. Writable data is kept here because the installation folder is not writable for typical setups.

## Files

| File | Format | Purpose | Retention |
|------|--------|---------|-----------|
| `Settings.dat` | SQLite | All settings, including encrypted API keys | Until deleted |
| `QueryHistory.dat` | SQLite | Covers already queried with AI Assist | 180 days, max 100,000 entries |
| `QueryHistory.dat.corrupt` | SQLite | Quarantined query-history database that could not be read | Until deleted |
| `ai-cache.json` | JSON | Cached AI verdicts | 30 days, max 2,000 entries |
| `ai-models.json` | JSON | Cached model lists per provider and base URL | 7 days |
| `app<yyyyMMdd>.log` | Text | Rolling daily application log | 7 files |
| `ApiLogError.txt` | Text | Reports queued for the bug-report API; an accepted report is removed after a successful send | Until the send succeeds |
| `UserLogError.txt` | Text | Simplified error list for user reference | Until deleted |
| `InternalLog.txt` | Text | Internal error-logger diagnostics | Until deleted |
| `Settings.dat.corrupt` | Any | Quarantined settings database that could not be read | Until deleted |
| `settings.dat.legacy` | Encrypted legacy file | Backup of a pre-SQLite settings file | Until deleted |
| `Screenshot\Screenshot_<timestamp>.png` | PNG | Screenshots saved with **F8** when the application folder is not writable | Until deleted |

> **Note:** `settings.dat.legacy` may appear in the data folder or next to the executable, depending on where the legacy file was found. `Settings.dat.corrupt` is created in the folder that held the unreadable file.

## Application folder

These files live next to the `FindRomCover` executable:

| File | Purpose |
|------|---------|
| `mame.dat` | MAME game database used for descriptions |
| `settings.dat.legacy` | Legacy settings backup after migration (if migrated from the application folder) |
| `settings.dat.corrupt` | Quarantined legacy settings file that could not be read |
| `settings.xml.migrated` | Legacy `settings.xml` moved aside after a successful migration |

## Settings database

`Settings.dat` is a SQLite database with a simple key/value table:

```sql
CREATE TABLE Settings (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

Every setting is stored under its property name, for example `BaseTheme`, `SimilarityThreshold`, `AiProvider`, `AiModel`, `AiRequestParameters`. List values such as `SupportedExtensions` are stored as JSON arrays.

### Encryption

Sensitive values are encrypted with AES using a PBKDF2 key derived from `Environment.MachineName` plus a constant application salt and password:

- `GoogleKey`
- `AiApiKey`
- `BugReportApiKey`

The key is **not tied to the user account**. Copying `Settings.dat` to another user account on the same machine still decrypts the secrets, because the machine name is unchanged. Copying it to another machine does not: the derived key differs, the secrets cannot be decrypted, and they must be entered again. All other values are stored in clear text inside the database; the database itself is not encrypted.

### Migration from older versions

When FindRomCover starts and the settings database is not a SQLite database, it assumes the legacy encrypted `settings.dat` format and:

1. decrypts the file;
2. applies every setting it contains, including API keys;
3. writes the settings to the new SQLite database;
4. renames the old file to `settings.dat.legacy` as a backup.

The same migration runs for a legacy `settings.xml`, which is moved to `settings.xml.migrated` after a successful conversion. Legacy files may be found in the data folder or next to the executable. If a legacy file cannot be decrypted, it is preserved as a `.legacy` backup and defaults are used instead. If a SQLite database is corrupt, it is renamed to `Settings.dat.corrupt` and defaults are restored.

## Query history database

`QueryHistory.dat` uses non-pooled SQLite connections, so the file is never held open:

```sql
CREATE TABLE QueryHistory (
    TargetPath TEXT PRIMARY KEY COLLATE NOCASE,
    QueriedUtc TEXT NOT NULL,
    Outcome    TEXT NOT NULL
);
```

Entries older than 180 days are removed, and the table is trimmed to 100,000 rows when it grows beyond that. If the file cannot be read as SQLite, it is quarantined as `QueryHistory.dat.corrupt` and a fresh database is created. See [Query History](../ai/query-history.md).

## Caches

| Cache | Key | What is stored |
|-------|-----|----------------|
| `ai-cache.json` | Game and candidate set | AI verdicts, including confidence and choice |
| `ai-models.json` | Provider and base URL | The list of models returned by the provider |

Both caches are best-effort: deleting them only means the next request is slower or costs a little more.

## Backing up and restoring

**Backup**

1. Close FindRomCover.
2. Copy the whole per-user data folder.

**Restore**

1. Close FindRomCover.
2. Replace the folder with your backup.
3. Start FindRomCover.

> **Caution:** Settings encryption is tied to the machine name, not to the user account. A settings database copied to another user account on the same machine still decrypts; on another machine it does not, and API keys must be entered again.

## Cleaning up

| Goal | Action |
|------|--------|
| Free space | Delete `ai-cache.json` and `ai-models.json` |
| Reset query history | Use **Clear query history** in AI Settings, or delete `QueryHistory.dat` |
| Full reset | Delete `Settings.dat` (also removes stored API keys) |
| Remove legacy backups | Delete `settings.dat.legacy`, `settings.dat.corrupt`, `settings.xml.migrated`, and `QueryHistory.dat.corrupt` |

## Related pages

- [Settings Overview](index.md)
- [Query History](../ai/query-history.md)
- [Logs & Diagnostics](logs.md)
- [Bug Reports](bug-reports.md)
