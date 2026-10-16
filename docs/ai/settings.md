# AI Settings

`Settings > AI Settings...` configures the provider, the model, and how AI picks behave. Only the **API key** is stored encrypted; the other AI settings are stored as plain configuration values in the settings database.

## Provider and model

| Field | Description |
|-------|-------------|
| **Enable AI-assisted image selection** | Master switch; without it no AI features are available |
| **Provider** | OpenRouter, OpenAI, Anthropic, Gemini, GLM, Local, or a custom endpoint |
| **Base URL** | Editable API base URL; preset per provider. Custom (OpenAI-compatible) has no preset and requires one |
| **API Key** | Provider key; stored encrypted and never logged. Required for OpenRouter, OpenAI, Anthropic, Gemini and GLM; optional for Local and custom endpoints |
| **Model** | Model identifier; use **Test / Load Models** to pick from the provider list. Enter exactly **one** ID — if you paste several (for example the whole recommended-models list), only the first one is kept and the app tells you when the field was adjusted |
| **Test / Load Models** | Verifies the connection and loads the model list from `<Base URL>/models` |
| **Filter box** | Narrows the loaded model list by text |
| **Vision-capable only** | Hides models that most likely cannot analyze images (default on) |

A **Get an OpenRouter API key** hyperlink below the API Key field opens the OpenRouter key page in your browser.

### Validation rules

When you save with AI Assist enabled:

- The Base URL must be an absolute URL if you entered one (for example `https://openrouter.ai/api/v1` or `http://localhost:11434/v1`).
- Custom providers require both a Base URL and a Model.
- OpenRouter, OpenAI, Anthropic, Gemini, and GLM require an API key.
- Local never requires an API key.
- The Model field must contain a single model ID. Pasted multi-ID text (for example the whole recommended-models list) is reduced to the first ID, and the app shows a "Model Adjusted" message when it changes the field.

The loaded model list is cached per provider and lowercased base URL for 7 days in `%LocalAppData%\FindRomCover\ai-models.json`. If a connection test fails, the previously cached list stays loaded; after changing the base URL the list is empty until you click **Test / Load Models**.

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
| **Run AI Pick automatically when a game is selected** | Off | Starts the AI analysis right after a local search on the Local Files tab or a Google API search on the Google API tab |
| **Verify images with AI before renaming or saving** | Off | Applies to the image folder watcher, manual local saves, manual Google API saves, and batch API downloads. Manual saves show a confirmation prompt when the AI considers the image a mismatch; Batch Fill rejects the download automatically and remembers it in the query history |
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

AI settings are stored in `%LocalAppData%\FindRomCover\Settings.dat`. Only the API key (`AiApiKey`) is AES-encrypted; the provider, base URL, model, timeouts, thresholds, and toggles are plain key/value rows. The provider model list lives in `ai-models.json`, AI verdicts in `ai-cache.json`, and skipped covers in `QueryHistory.dat`. See [Data Storage](../configuration/data-storage.md).

## Related pages

- [Providers & Models](providers.md)
- [Picking Covers](picking-covers.md)
- [Batch Fill](batch-fill.md)
- [Query History](query-history.md)
