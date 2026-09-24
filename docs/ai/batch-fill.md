# Batch Fill

**AI Fill Missing Covers...** processes many missing covers in one run. For each item it looks at local candidates first and, optionally, falls back to the Google Custom Search API. It is the fastest way to clear a large missing list with AI Assist.

## Starting a batch

1. Open the missing covers list and click **AI Fill Missing Covers...**.
2. Configure the run:

| Option | Default | Description |
|--------|---------|-------------|
| **Use Google API fallback when local candidates are inconclusive** | On when a Google API key is configured (otherwise disabled) | If the AI cannot find a confident local match, search Google images instead (requires a Google API key) |
| **Skip covers already queried in previous sessions** | On | Do not ask the AI twice for the same cover; managed in [AI Settings](settings.md) |
| **Max items per run** | 25 | How many missing covers to process in this run (1–500) |

3. Click **Start**. Progress and per-item results appear in the list.
4. Click **Cancel** to stop after the current item — already saved covers stay saved.

## Per-item decision flow

For every item, FindRomCover performs these steps in order:

1. **Cover already exists?** If the target PNG is already in the image folder, the item is skipped.
2. **Already queried?** If the item is in the query history and the skip option is on, it is skipped without an API call.
3. **Local candidates** — the top local images above the **Candidate similarity** threshold are sent to the model.
4. **Confident local match?** If the confidence is at or above the **Auto-save threshold**, the image is saved as `[gamename].png` and the item is removed from the query history.
5. **Google API fallback** (when enabled and a Google key is configured) — Google image results are ranked by the model; a confident pick is downloaded and saved.
6. **No confident match** — the item is reported and, when the model actually evaluated candidates, recorded in the query history so it is not re-queried later.

## Outcomes

Each item ends with one of these outcomes:

| Outcome | Meaning |
|---------|---------|
| **FilledFromLocal** | A local image was chosen and saved |
| **FilledFromApi** | A Google API image was chosen, downloaded, and saved |
| **SkippedLowConfidence** | The model evaluated candidates but none reached the auto-save threshold |
| **SkippedAlreadyExists** | A cover for this game already exists |
| **SkippedAlreadyQueried** | The cover was queried in a previous session and the skip option is on |
| **NoCandidates** | No local candidates and no API fallback results were available |
| **Failed** | An error occurred (API failure, download failure, save failure) |
| **Canceled** | The run was cancelled before this item completed |

The summary after a run counts each outcome, including how many were skipped because they had already been queried.

## Requirements and limits

- **A configured AI provider** — see [Providers & Models](providers.md).
- **A Google API key** only if you enable the API fallback.
- One model request per item, so a 100-item run makes roughly 100 requests (minus cached verdicts). Estimate costs with [Recommended Models](recommended-models.md).
- Local models process sequentially and can be slow; keep runs small (10–25 items) when using Ollama or LM Studio.

## Practical tips

| Goal | Suggestion |
|------|------------|
| Cheapest run | Keep API fallback off; use local candidates and the cheapest vision model |
| Highest fill rate | Enable API fallback; raise **Max candidates** and lower **Auto-save threshold** slightly |
| Avoid wrong saves | Raise the **Auto-save threshold** to 90 |
| Resume later | Leave **Skip already queried** on; the history remembers completed work |
| Test before bulk | Run with **Max items per run** set to 5 and inspect the results |

## Interpreting results

- Items marked **FilledFromLocal** or **FilledFromApi** have covers on disk and will disappear from the missing list.
- **SkippedLowConfidence** items stay in the list; you can fill them manually or re-run with a lower threshold.
- **Failed** items can be retried — failures are not written to the query history.
- **SkippedAlreadyQueried** items are intentionally ignored; clear the history in [AI Settings](settings.md) if you want to retry them.

## Related pages

- [AI Settings](settings.md)
- [Query History](query-history.md)
- [Picking Covers](picking-covers.md)
- [AI Troubleshooting](troubleshooting.md)
