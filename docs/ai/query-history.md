# Query History

Query history prevents FindRomCover from asking the AI the same question twice. If a cover was already inspected in a previous session — manually or by Batch Fill — it is remembered and skipped.

## Why it exists

AI requests cost money and time. When a game has no good cover in your local folder and no good Google result, re-asking every session is wasteful. The history turns "no match" into a durable answer.

## What gets remembered

| Event | History effect |
|-------|----------------|
| Manual AI pick that finds no local match | Recorded |
| Batch item where the model found no confident local match | Recorded as `local-no-match` |
| Batch item where the model found no confident API match | Recorded as `api-no-match` |
| Cover successfully saved (manual or batch) | Removed from the history |
| Failed item (API error, download or save failure) | Not recorded — safe to retry |
| Item with no candidates at all | Not recorded |

The key is the full target cover path, matched case-insensitively.

## Where it is stored

| Property | Value |
|----------|-------|
| File | `%LocalAppData%\FindRomCover\QueryHistory.dat` |
| Format | SQLite database |
| Retention | 180 days |
| Maximum entries | 10,000 (oldest entries are trimmed automatically) |
| Table | `QueryHistory(TargetPath, QueriedUtc, Outcome)` |

The database uses non-pooled connections, so the file is never locked and can be backed up or inspected with any SQLite tool.

## Controlling the history

| Task | How |
|------|-----|
| Ignore history for a batch run | Uncheck **Skip covers already queried in previous sessions** in the [Batch Fill](batch-fill.md) window |
| Change the default for future runs | Toggle the same option in [AI Settings](settings.md) |
| Forget everything | Click **Clear query history** in AI Settings; the entry count is shown next to the button |
| Retry a single game | Clear the history, or wait for the 180-day expiry |

## Relationship to the verdict cache

The two stores are complementary:

| Store | Purpose | Lifetime |
|-------|---------|----------|
| **Query history** (`QueryHistory.dat`) | "We already asked and there was no match — do not ask again" | 180 days |
| **Verdict cache** (`ai-cache.json`) | "We asked and here is the exact answer — reuse it for free" | 30 days, 2,000 entries |

The verdict cache stores actual results (including successful picks) so repeated picks are instant; the query history stores the decision to skip.

## Privacy

The history contains only local file paths, timestamps, and outcome labels. It is never uploaded anywhere.

## Related pages

- [AI Settings](settings.md)
- [Batch Fill](batch-fill.md)
- [Data Storage](../configuration/data-storage.md)
