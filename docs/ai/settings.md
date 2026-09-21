# AI Settings

`Settings > AI Settings...` configures the provider, the model, and how AI picks behave. All values are stored in the encrypted settings database.

## Provider and model

| Field | Description |
|-------|-------------|
| **Enable AI-assisted image selection** | Master switch; without it no AI features are available |
| **Provider** | OpenRouter, OpenAI, Anthropic, Gemini, GLM, Local, or a custom endpoint |
| **Base URL** | Editable API base URL; preset per provider |
| **API Key** | Provider key; stored encrypted and never logged |
| **Model** | Model identifier; use **Test / Load Models** to pick from the provider list |
| **Test / Load Models** | Verifies the connection and loads the model list |
| **Filter box** | Narrows the loaded model list by text |
| **Vision-capable only** | Hides models that most likely cannot analyze images (default on) |

The loaded list is cached per provider and base URL for 7 days, so it is available instantly on later visits.

## Limits & behavior

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| **Timeout (seconds)** | 90 | 10–300 | How long to wait for the provider before giving up |
| **Max candidates sent** | 6 | 1–20 | How many top-ranked local images are sent to the model |
| **Candidate similarity (%)** | 70 | 0–100 | Only local images with filename similarity at or above this value are sent to the AI |
| **Image max size (px)** | 512 | 128–2048 | Images are downscaled to this maximum dimension before upload |
| **Min cover width (px)** | 200 | 0–2048 | Downloaded covers narrower than this are rejected as too low resolution (0 disables the check) |
| **Auto-save threshold (%)** | 80 | 0–100 | Minimum confidence required for automatic saving |

### Behavior toggles

| Toggle | Default | Effect |
|--------|---------|--------|
| **Automatically save the AI pick when confidence is above the threshold** | Off | The AI-selected image is saved and the game leaves the missing list without further clicks |
| **Run AI Pick automatically when a game is selected** | Off | Starts the AI analysis right after the local search finishes |
| **Verify images with AI before renaming or saving** | Off | The watcher asks the model to confirm that a newly saved image matches the selected game |
| **Skip missing covers already queried in previous sessions** | On | Batch Fill will not ask the AI twice for the same cover, even across restarts |

## Query history

The **Clear query history** button forgets every cover that was already queried. The number of remembered entries is shown next to the button. See [Query History](query-history.md) for retention details.

## Tuning guidance

| Goal | Suggested changes |
|------|-------------------|
| Lower cost | Reduce **Max candidates**, raise **Candidate similarity**, keep the verdict cache enabled |
| Fewer wrong auto-saves | Raise **Auto-save threshold** to 90 or disable auto-save |
| Faster picks | Reduce **Image max size** to 384 and **Max candidates** to 4 |
| More thorough picking | Raise **Max candidates** to 10–15 and lower **Candidate similarity** to 50 |
| Slow local models | Raise **Timeout** to 180–300 seconds |

> **Note:** The **Candidate similarity** value is separate from the main window's similarity threshold. The main window threshold controls which local images are listed; the AI value controls which of those are sent to the model.

## Where settings live

All AI settings — including provider, base URL, model, and the encrypted API key — are stored in `%LocalAppData%\FindRomCover\Settings.dat`. See [Data Storage](../configuration/data-storage.md).

## Related pages

- [Providers & Models](providers.md)
- [Picking Covers](picking-covers.md)
- [Batch Fill](batch-fill.md)
- [Query History](query-history.md)
