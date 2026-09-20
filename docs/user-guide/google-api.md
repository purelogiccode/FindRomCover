# Google Custom Search API

The **Google API** tab fetches image results programmatically through the Google Custom Search JSON API and shows them as clickable thumbnails. It requires a Google API key, and unlike the web tabs it supports the **AI Pick Best** workflow directly.

## Setup

1. Open the [Google Cloud Console](https://console.cloud.google.com).
2. Create a project (or select an existing one).
3. Enable the **Custom Search JSON API** for the project.
4. Create an API key under **APIs & Services > Credentials**.
5. In FindRomCover, open `Settings > API Settings...`, paste the key, and click **Save**.

The search engine used by FindRomCover is preconfigured; you only need the API key.

> **Note:** The API key is stored encrypted in the settings database and is never written to log files.

## Usage

1. Select a game in the missing covers list.
2. Switch to the **Google API** tab. Thumbnails load automatically.
3. Adjust the **Extra Query** to refine results.
4. Click a thumbnail to save it as the cover. The image is downloaded, converted to PNG if necessary, and saved as `[gamename].png`.

Thumbnail size is configurable under `Set Thumbnail Size` (100–500 pixels).

## AI Pick Best

When [AI Vision Assist](../ai/index.md) is enabled, **AI Pick Best** works on this tab: the model inspects the Google results and highlights the best cover. See [Picking Covers](../ai/picking-covers.md).

## Quotas and errors

The Custom Search JSON API has a free daily quota (100 queries per day at the time of writing). FindRomCover translates API failures into readable messages:

| Response | Meaning | What to do |
|----------|---------|------------|
| 400 | Bad request | Check the query and key |
| 403 | Forbidden | The API is not enabled for the key, or billing/quota restrictions apply |
| 429 | Rate limit / quota exceeded | Wait until the quota resets or enable billing in Google Cloud |
| Invalid API key | Key rejected | Re-enter the key under `Settings > API Settings...` |

Failures are logged as warnings with the full details available in the [log window](../configuration/logs.md).

## Cost control

- Results are fetched only when you select a game and the tab is active.
- Thumbnails are cached while the session is open; switching back to the tab does not necessarily re-query.
- The free tier is usually sufficient for manual cover hunting. For AI batch runs with API fallback, keep an eye on the daily quota — see [Batch Fill](../ai/batch-fill.md).

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| "API Key is not set" | Add the key under `Settings > API Settings...` |
| No results for a valid game | Refine the Extra Query; some titles are simply not indexed |
| All requests fail after many uses | You likely hit the daily quota; wait for the reset |
| Thumbnails load but clicking fails | Check the log for download or conversion errors and verify write access to the image folder |

## Related pages

- [Web Search](web-search.md)
- [AI Vision Assist](../ai/index.md)
- [Troubleshooting](../troubleshooting.md)
