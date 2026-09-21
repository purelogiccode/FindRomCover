# Settings Overview

FindRomCover stores all settings in a single encrypted SQLite database. Most settings are changed through menus and windows; there is no separate settings file to edit by hand.

## Where settings are changed

| Setting group | Where |
|---------------|-------|
| Theme and accent color | `Theme` menu |
| Similarity algorithm and threshold | `Set Similarity Algorithm` and `Set Similarity Threshold` menus |
| Thumbnail size | `Set Thumbnail Size` menu |
| Google API key | `Settings > API Settings...` |
| AI provider, model, limits, behavior | `Settings > AI Settings...` |
| Supported ROM extensions | `Settings > Edit Supported Extensions...` |
| MAME descriptions | `Settings > Use MAME Descriptions` |
| Log window visibility | `Settings > Show/Hide Log Window` |

## Settings reference

### Search and matching

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Similarity threshold | 70% | 10–90 | Minimum filename similarity for local results |
| Similarity algorithm | Jaro-Winkler Distance | — | Jaccard, Jaro-Winkler, or Levenshtein |
| Search engine / active tab | Bing Web | — | Last used search tab |
| Use MAME descriptions | Off | — | Use MAME descriptions instead of cleaned filenames |
| Supported extensions | 140+ extensions | — | ROM file extensions that are scanned |
| Last image folder | — | — | Restored on startup |

### Display

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Base theme | Dark | Light, Dark | Window theme |
| Accent color | Blue | 20+ colors | Theme accent |
| Thumbnail size | 300 px | 100–500 | Google API thumbnail size |
| Max images to load | 30 | — | Maximum results loaded per search |

### API and network

| Setting | Default | Description |
|---------|---------|-------------|
| Google API key | empty | Key for the Google Custom Search API |
| API timeout | 30 s | HTTP timeout for Google API requests |
| Image loader retries | 3 | Retry attempts for image downloads |
| Image loader retry delay | 200 ms | Delay between retries |
| Bug report API key / URL | built-in | Used by the error reporting pipeline |

### AI Vision Assist

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| AI assist enabled | Off | — | Master switch |
| Provider | OpenRouter | 8 options | Cloud, local, or custom |
| Base URL | provider preset | — | API endpoint |
| API key | empty | — | Stored encrypted |
| Model | provider preset | — | Vision-capable model ID |
| Timeout | 90 s | 10–300 | Per-request timeout |
| Max candidates sent | 6 | 1–20 | Images sent to the model |
| Image max size | 512 px | 128–2048 | Downscale before upload |
| Min cover width | 200 px | 0–2048 | Reject narrower downloads (0 disables) |
| Auto-save threshold | 80% | 0–100 | Minimum confidence for auto-save |
| Candidate similarity | 70% | 0–100 | Filename pre-filter for AI candidates |
| Auto-save | Off | — | Save confident picks automatically |
| Auto-run | Off | — | Start AI pick after each local search |
| Verify on save | Off | — | AI-check images before renaming |
| Skip previously queried | On | — | Use the query history |

See [AI Settings](../ai/settings.md) for the full AI reference.

## Resetting settings

| Goal | Method |
|------|--------|
| Reset everything | Close FindRomCover and delete `%LocalAppData%\FindRomCover\Settings.dat`; defaults are recreated on the next start |
| Reset one area | Change the values through the menus/windows; there is no per-area reset |
| Keep a backup | Copy `Settings.dat` while the application is closed |

> **Note:** Deleting `Settings.dat` removes stored API keys. Have them ready before resetting.

## Related pages

- [Themes & Appearance](themes.md)
- [Similarity Algorithms](similarity.md)
- [Supported Extensions](extensions.md)
- [Data Storage](data-storage.md)
