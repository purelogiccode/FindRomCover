# Query History

Query history prevents FindRomCover from asking the AI the same question twice. If a cover was already inspected in a previous session — manually or by Batch Fill — it is remembered and skipped.

## Why it exists

AI requests cost money and time. When a game has no good cover in your local folder and no good Google result, re-asking every session is wasteful. The history turns "no match" into a durable answer.

## What gets remembered

| Event | History effect |
|-------|----------------|
| Manual AI pick that finds a pick | Recorded as `manual` |
| Manual AI pick that finds no match | Not recorded |
| Manual AI pick that fails before producing a verdict | Not recorded |
| Batch item where the model found no confident local match | Recorded as `local-no-match` |
| Batch item where the model found no confident API match | Recorded as `api-no-match` |
| Batch API download rejected by AI verification | Recorded as `api-verification-rejected` |
| Cover saved by Batch Fill (local or API) | Removed from the history |
| Cover saved manually | Not removed — the entry stays until it expires |
| Failed item (API error, download or save failure) | Not recorded — safe to retry |
| Item with no candidates at all | Not recorded |

The key is the full target cover path, matched case-insensitively.

## Where it is stored

| Property | Value |
|----------|-------|
| File | `%LocalAppData%\FindRomCover\QueryHistory.dat` |
| Format | SQLite database (non-pooled connections) |
| Retention | 180 days |
| Maximum entries | 100,000 (oldest entries are trimmed automatically) |
| Table | `QueryHistory(TargetPath, QueriedUtc, Outcome)` |

Every operation opens a fresh non-pooled connection, and access is serialized by a process-wide lock. The database is still locked while a write is in progress, so close FindRomCover before backing it up or inspecting it with a SQLite tool.

Expired rows are pruned lazily: a lookup that finds only an expired entry deletes it, and writing a new entry removes all expired rows and trims the table to 100,000 rows. Until then, the file may temporarily contain entries older than the retention period.

If `QueryHistory.dat` is not a valid SQLite database, it is quarantined to `QueryHistory.dat.corrupt` and a fresh, empty database is created automatically.

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
