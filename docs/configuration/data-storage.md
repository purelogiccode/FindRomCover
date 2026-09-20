# Data Storage

FindRomCover keeps configuration and caches in `%LocalAppData%\FindRomCover` and writes logs and its MAME database next to the executable. This page lists every file, what it contains, and how long it is kept.

## Data folder

Default location: `%LocalAppData%\FindRomCover` (typically `C:\Users\<you>\AppData\Local\FindRomCover`).

| File | Format | Purpose | Retention |
|------|--------|---------|-----------|
| `Settings.dat` | SQLite | All settings, including encrypted API keys | Until deleted |
| `QueryHistory.dat` | SQLite | Covers already queried with AI Assist | 180 days, max 10,000 entries |
| `ai-cache.json` | JSON | Cached AI verdicts | 30 days, max 2,000 entries |
| `ai-models.json` | JSON | Cached model lists per provider and base URL | 7 days |
| `settings.dat.legacy` | Encrypted legacy file | Backup of the pre-SQLite settings file | Until deleted |

## Application folder

These files live next to `FindRomCover.exe`:

| File | Purpose |
|------|---------|
| `mame.dat` | MAME game database used for descriptions |
| `app.log` | Rolling application log |
| `error.log` | Error-level log |
| `error_user.log` | Simplified error list for user reference |
| `settings.dat.legacy` | Legacy settings backup after migration (if migrated from the app folder) |
| `settings.dat.corrupt` | Quarantined settings database that could not be read |

## Settings database

`Settings.dat` is a SQLite database with a simple key/value table:

```sql
CREATE TABLE Settings (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

Every setting is stored under its property name, for example `BaseTheme`, `SimilarityThreshold`, `AiProvider`, `AiModel`. List values such as `SupportedExtensions` are stored as JSON arrays.

### Encryption

Sensitive values are encrypted with AES using a machine-derived PBKDF2 key before they are written:

- `GoogleKey`
- `AiApiKey`
- `BugReportApiKey`

Other values are stored in clear text inside the database. The database itself is not encrypted, but secrets in it are.

### Migration from older versions

When FindRomCover starts and `Settings.dat` is not a SQLite database, it assumes the old encrypted settings format and:

1. decrypts the file;
2. applies every setting it contains, including API keys;
3. writes the settings to the new SQLite database;
4. renames the old file to `settings.dat.legacy` as a backup.

The same migration runs for an old `settings.xml`, which is also moved aside after conversion. If the legacy file cannot be decrypted, it is preserved as a backup and defaults are used instead. If a SQLite database is corrupt, it is renamed to `settings.dat.corrupt` and defaults are restored.

## Query history database

`QueryHistory.dat` uses non-pooled SQLite connections, so the file is never held open:

```sql
CREATE TABLE QueryHistory (
    TargetPath TEXT PRIMARY KEY COLLATE NOCASE,
    QueriedUtc TEXT NOT NULL,
    Outcome    TEXT NOT NULL
);
```

Entries older than 180 days are removed, and the table is trimmed to 10,000 rows when it grows beyond that. See [Query History](../ai/query-history.md).

## Caches

| Cache | Key | What is stored |
|-------|-----|----------------|
| `ai-cache.json` | Game and candidate set | AI verdicts, including confidence and choice |
| `ai-models.json` | Provider and base URL | The list of models returned by the provider |

Both caches are best-effort: deleting them only means the next request is slower or costs a little more.

## Backing up and restoring

**Backup**

1. Close FindRomCover.
2. Copy the whole `%LocalAppData%\FindRomCover` folder.

**Restore**

1. Close FindRomCover.
2. Replace the folder with your backup.
3. Start FindRomCover.

> **Caution:** Settings encryption is tied to the Windows user and machine. A settings database copied to a different user account or PC may not be decryptable, and API keys would need to be entered again.

## Cleaning up

| Goal | Action |
|------|--------|
| Free space | Delete `ai-cache.json` and `ai-models.json` |
| Reset query history | Use **Clear query history** in AI Settings, or delete `QueryHistory.dat` |
| Full reset | Delete `Settings.dat` (also removes stored API keys) |
| Remove legacy backups | Delete `settings.dat.legacy` and `settings.dat.corrupt` |

## Related pages

- [Settings Overview](index.md)
- [Query History](../ai/query-history.md)
- [Logs & Diagnostics](logs.md)
