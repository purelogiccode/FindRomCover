# Picking Covers

This page describes the interactive AI workflows: **AI Pick Best**, automatic picking, automatic saving, and verification before renaming.

## AI Pick Best

**AI Pick Best** is available on the **Local Files** and **Google API** tabs when AI Assist is enabled.

1. Select a game and let the normal search finish.
2. Click **AI Pick Best**.
3. The top candidates are downscaled and sent to the model together with the game name.
4. The model returns a verdict with a confidence score and a short reason.
5. The chosen image is highlighted with a green badge and moved to the front of the results.

What happens next depends on your settings:

- If **Automatically save the AI pick** is enabled and the confidence is at or above the **Auto-save threshold**, the image is saved immediately.
- Otherwise, click the highlighted image to save it yourself.

If no candidate is convincing, the model reports a low confidence and no image is saved.

## Candidate selection

Only local images whose filename similarity is at or above **Candidate similarity** (default 70%) are considered. Among those, the top **Max candidates sent** (default 6) are uploaded after downscaling to **Image max size** (default 512 px).

For the Google API tab, the displayed thumbnails are the candidates.

## Auto-run

When **Run AI Pick automatically when a game is selected** is enabled, the AI pick starts as soon as the local search finishes — no button press required. This is convenient for large lists when combined with auto-save.

## Auto-save and confidence

The model returns a confidence percentage for its choice. Auto-save happens only when:

1. the AI pick produced a result,
2. the confidence is at or above the threshold, and
3. the image can be downloaded/converted and saved.

Otherwise the result is shown but not saved. This makes it safe to combine auto-run and auto-save for unattended processing.

## Verify before renaming or saving

When **Verify images with AI before renaming or saving** is enabled, the image folder watcher asks the model to confirm that a newly saved image matches the selected game:

- **Match** — the image is renamed to the game and converted to PNG as usual.
- **Mismatch** — the image is left untouched and a message is logged so you can review it.

This is particularly useful with the Google Web and Bing Web workflow, where it is easy to save the wrong image by accident.

## Verdict caching

AI verdicts are cached for 30 days (up to 2,000 entries) in `%LocalAppData%\FindRomCover\ai-cache.json`. Repeating the same pick — for example after switching tabs and back — does not call the provider again. The cache is keyed by the image set and game, so changing candidates produces a fresh verdict.

## What the model sees

The prompt asks the model to:

- choose the image that best represents the game's cover art;
- ignore screenshots, logos, and unrelated images;
- return a strict JSON object with the chosen index, a confidence score, and a brief reason.

Models without native JSON mode are prompted to output JSON as text, which FindRomCover parses.

## Cost example

A pick with 6 images costs roughly 6,000 input tokens and a few hundred output tokens. With the default OpenRouter model `qwen/qwen3.7-flash`, that is about **$0.0002 per pick** — around **$0.21 per 1,000 picks**. See [Recommended Models](recommended-models.md) for a comparison of alternatives.

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| "No candidates above the similarity threshold" | Lower **Candidate similarity** in AI Settings or lower the main similarity threshold |
| The model returns no text | The model may be a reasoning model that ran out of output budget; see [AI Troubleshooting](troubleshooting.md) |
| Low confidence every time | Try a stronger model, or check that the game name is meaningful (enable MAME descriptions) |
| Pick is slow | Reduce **Image max size** and **Max candidates**; local models are inherently slower |
| Auto-save never triggers | Lower the **Auto-save threshold**; check the confidence shown in the result message |

## Related pages

- [AI Settings](settings.md)
- [Batch Fill](batch-fill.md)
- [Query History](query-history.md)
- [AI Troubleshooting](troubleshooting.md)
