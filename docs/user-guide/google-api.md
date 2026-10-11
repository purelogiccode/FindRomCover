# Google Custom Search API

The **Google API** tab fetches image results programmatically through the Google Custom Search JSON API and shows them as clickable thumbnails. It requires a Google API key, and unlike the web tabs it supports the **AI Pick Best** workflow directly.

## Setup

1. Open the [Google Cloud Console](https://console.cloud.google.com).
2. Create a project (or select an existing one).
3. Enable the **Custom Search JSON API** for the project.
4. Create an API key under **APIs & Services > Credentials**.
5. In FindRomCover, open `Settings > API Settings...`, paste the key, and click **Save**.

The search engine used by FindRomCover is preconfigured; you only need the API key.

!!! note
    The API key is stored encrypted in the settings database and is never written to log files.

## Usage

1. Select a game in the missing covers list.
2. Switch to the **Google API** tab. Thumbnails load automatically.
3. Adjust the **Extra Query** to refine results.
4. Click a thumbnail to save it as the cover. The image is downloaded, converted to PNG if necessary, and saved as the sanitized `[gamename].png`.

Thumbnail size follows `Set Thumbnail Size` (100–500 pixels), the same setting that controls the Local Files previews.

## How results are fetched

- Each request asks Google for a fixed **10 results**.
- Results whose link is empty are skipped, so fewer than 10 thumbnails may appear.
- The query re-runs **every time the Google API tab becomes active with a game selected**. Results are not cached: switching away and back issues a fresh request and replaces the previous thumbnails.
- If the full-size image cannot be downloaded, or if it is narrower than the **Min cover width** setting (default 200 px, configured in [AI Settings](../ai/settings.md)), FindRomCover tries the provider-hosted thumbnail as a fallback before giving up on that result.
- If no Google API key is set when you open the tab, FindRomCover explains that a key is required and opens `Settings > API Settings...`.

## AI Pick Best

When [AI Vision Assist](../ai/index.md) is enabled, **AI Pick Best** works on this tab: the model inspects the Google results and highlights the best cover, which is moved to the front and marked with a green AI badge. See [Picking Covers](../ai/picking-covers.md).

## Quotas and errors

The Custom Search JSON API has a free daily quota (100 queries per day at the time of writing). Because the query re-runs on every tab activation, frequent tab switching can consume the quota faster. FindRomCover translates API failures into readable messages:

| Response | Meaning | What to do |
|----------|---------|------------|
| 400 | Bad request | Check the query and key |
| 401 | Unauthorized | The API key was rejected; re-enter it under `Settings > API Settings...` |
| 403 | Forbidden | The API is not enabled for the key, or billing/quota restrictions apply |
| 429 | Rate limit / quota exceeded | Wait until the quota resets or enable billing in Google Cloud |

Failures are logged as warnings with the full details available in the [log window](../configuration/logs.md).

## Cost control

- One query returns at most 10 results; there is no paging.
- Every activation of the Google API tab with a selected game sends a new request, so avoid switching tabs repeatedly while hunting for one cover.
- The free tier is usually sufficient for manual cover hunting. For AI batch runs with API fallback, keep an eye on the daily quota — see [Batch Fill](../ai/batch-fill.md).

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| "API Key is not set" | Add the key under `Settings > API Settings...` |
| No results for a valid game | Refine the Extra Query; some titles are simply not indexed |
| All requests fail after many uses | You likely hit the daily quota; wait for the reset |
| Thumbnails load but clicking fails | Check the log for download or conversion errors and verify write access to the image folder |
| A narrow image was rejected | It was below **Min cover width** and the thumbnail fallback also did not qualify; lower the setting or pick another result |

## Related pages

- [Web Search](web-search.md)
- [AI Vision Assist](../ai/index.md)
- [Image Handling](image-handling.md)
- [Troubleshooting](../troubleshooting.md)
