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
4. Click **Cancel** to stop after the current item — already saved covers stay saved, and the summary shows "Batch canceled. Already saved covers were kept."

The window header shows the number of missing covers and how many of them are already in the query history, for example `120 missing cover(s) available (14 already queried).` The already-queried count appears only here, before the run starts; it is not part of the final summary.

## Per-item decision flow

For every item, FindRomCover performs these steps in order:

1. **Cover already exists?** If a cover for the game is already in the image folder, the item is skipped.
2. **Already queried?** If the item is in the query history and the skip option is on, it is skipped without an API call.
3. **Local candidates** — the top local images above the **Candidate similarity** threshold are sent to the model with the local pick prompt.
4. **Confident local match?** If the pick has a valid index and the confidence is at or above the **Auto-save threshold**, the image is saved as `[gamename].png` and the item is removed from the query history.
5. **Google API fallback** (when enabled and a Google key is configured) — Google image results are ranked by the model with the API-fallback prompt; a confident pick is downloaded, optionally verified, and saved.
6. **No confident match** — the item is reported as `SkippedLowConfidence` and, when the model actually evaluated candidates, recorded in the query history (`local-no-match` or `api-no-match`) so it is not re-queried later. A downloaded image rejected by AI verification is recorded as `api-verification-rejected`.

## Outcomes

Each item ends with one of these outcomes:

| Outcome | Meaning |
|---------|---------|
| **FilledFromLocal** | A local image was chosen and saved |
| **FilledFromApi** | A Google API image was chosen, downloaded, and saved |
| **SkippedLowConfidence** | The model evaluated candidates but none reached the auto-save threshold, or AI verification rejected the downloaded image ("AI verification rejected the downloaded image.") |
| **SkippedAlreadyExists** | A cover for this game already exists (or appeared while the API download was being verified) |
| **SkippedAlreadyQueried** | The cover was queried in a previous session and the skip option is on |
| **NoCandidates** | No local candidates and no API fallback results were available |
| **Failed** | An error occurred (API failure, download failure, save failure) |

> **Note:** The internal outcome enum also contains a `Canceled` value, but Batch Fill never produces it. Cancelling aborts the run, and the summary shows "Batch canceled. Already saved covers were kept."

After a run, the window shows exactly this summary format:

```text
Done: {filled} filled, {skipped} skipped, {failed} failed.
```

`filled` counts `FilledFromLocal` and `FilledFromApi`; `skipped` counts `SkippedLowConfidence`, `SkippedAlreadyExists`, `SkippedAlreadyQueried`, and `NoCandidates`; `failed` counts `Failed`. The number of items skipped as already queried is shown only in the pre-run header.

## Requests and file safety

- **Up to three model requests per item**: one local pick, one API pick when the fallback runs, and one verification when **Verify images with AI before renaming or saving** is enabled. The fallback also makes one Google Custom Search API query per item. Cached verdicts reduce the number of paid requests.
- **API downloads never overwrite a cover directly.** The image is downloaded to a temporary `[gamename].png.tmp<hex>` file, verified (if enabled), and only then atomically moved into place. On cancellation, rejection, or failure the temporary file is deleted.
- **The watcher is kept out of the way.** The expected-file registration is refreshed before the final move — a download plus verification can outlive the watcher's deduplication window — and it is cleaned up when the run is cancelled or the save fails.

## Requirements and limits

- **A configured AI provider** — see [Providers & Models](providers.md).
- **A Google API key** only if you enable the API fallback.
- One local model request per item; with API fallback and verification enabled, up to three (minus cached verdicts). Estimate costs with [Recommended Models](recommended-models.md).
- Local models process sequentially and can be slow; keep runs small (10–25 items) when using Ollama or LM Studio.

## Practical tips

| Goal | Suggestion |
|------|------------|
| Cheapest run | Keep API fallback off; use local candidates and the cheapest vision model |
| Highest fill rate | Enable API fallback; raise **Max candidates** and lower **Auto-save threshold** slightly |
| Avoid wrong saves | Raise the **Auto-save threshold** to 90 and keep **Verify images with AI** enabled |
| Resume later | Leave **Skip already queried** on; the history remembers completed work |
| Test before bulk | Run with **Max items per run** set to 5 and inspect the results |

## Interpreting results

- Items marked **FilledFromLocal** or **FilledFromApi** have covers on disk and will disappear from the missing list.
- **SkippedLowConfidence** items stay in the list; you can fill them manually or re-run with a lower threshold. Items whose downloaded image was rejected by AI verification are remembered as `api-verification-rejected`.
- **Failed** items can be retried — failures are not written to the query history.
- **SkippedAlreadyQueried** items are intentionally ignored; clear the history in [AI Settings](settings.md) if you want to retry them.

## Related pages

- [AI Settings](settings.md)
- [Query History](query-history.md)
- [Picking Covers](picking-covers.md)
- [AI Troubleshooting](troubleshooting.md)
