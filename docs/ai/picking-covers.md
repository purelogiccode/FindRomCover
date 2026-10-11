# Picking Covers

This page describes the interactive AI workflows: **AI Pick Best**, automatic picking, automatic saving, and verification before renaming or saving.

## AI Pick Best

**AI Pick Best** is available on the **Local Files** and **Google API** tabs when AI Assist is enabled.

1. Select a game and let the normal search finish.
2. Click **AI Pick Best**.
3. The top candidates are downscaled and sent to the model together with the game name.
4. The model returns a verdict with a confidence score and a short reason.
5. The chosen image is highlighted with an `AI pick NN%` badge, the badge is cleared from every other candidate, and the picked image is moved to the front of the results.

What happens next depends on your settings:

- If **Automatically save the AI pick** is enabled and the confidence is at or above the **Auto-save threshold**, the image is saved immediately.
- Otherwise, click the highlighted image to save it yourself.

If no candidate is convincing, the model reports a low confidence and no image is saved.

## Candidate selection

Only local images whose filename similarity is at or above **Candidate similarity** (default 70%) are considered. Among those, the top **Max candidates sent** (default 6) are uploaded after downscaling to **Image max size** (default 512 px).

For the Google API tab, the displayed thumbnails are the candidates.

## Auto-run

When **Run AI Pick automatically when a game is selected** is enabled, the AI pick starts without a button press:

- on the **Local Files** tab, as soon as the local search finishes;
- on the **Google API** tab, as soon as the API search finishes.

This is convenient for large lists when combined with auto-save.

## Auto-save and confidence

The model returns a confidence percentage for its choice. Auto-save happens only when:

1. the AI pick produced a valid candidate index (`bestIndex` within the candidate list),
2. the confidence is at or above the threshold, and
3. the image can be downloaded/converted and saved.

Otherwise the result is shown but not saved. This makes it safe to combine auto-run and auto-save for unattended processing.

## Verify before renaming or saving

When **Verify images with AI before renaming or saving** is enabled, every save path is verified by the model:

| Path | Behavior on mismatch |
|------|----------------------|
| Image folder watcher | The image is left untouched and a message is shown; no rename or conversion happens |
| Manual local save | A confirmation prompt appears ("Save it anyway?"); saving continues only if you confirm |
| Manual Google API save | The download is verified before it replaces anything, and a "Save it anyway?" prompt asks whether to keep the downloaded file |
| Batch API download | The downloaded image is rejected, deleted, and reported as **SkippedLowConfidence** ("AI verification rejected the downloaded image.") |

When the AI considers an image a match, or verification cannot run because of an error, saving proceeds as usual. Rejections in Batch Fill are recorded in the query history with the outcome `api-verification-rejected`.

## Verdict caching

AI verdicts are cached for 30 days (up to 2,000 entries) in `%LocalAppData%\FindRomCover\ai-cache.json`. Repeating the same pick — for example after switching tabs and back — does not call the provider again. Both successful and declined verdicts are cached, so a repeated "no match" is also free.

The cache key combines the prompt version, the request kind (local pick, API pick, or verification), the provider, the model, the ROM and search names, the image hashes, and the candidate source indexes. Changing candidates, model, provider, or prompt version produces a fresh verdict. The cache is written atomically (a temporary file is replaced into place), so a crash mid-write cannot corrupt it.

## What the model sees

The local pick prompt asks the model to:

- choose the image that best represents the game's official cover or box art;
- return `-1` when no candidate is genuine cover art for that exact game;
- reply with a strict JSON object containing the chosen index, a confidence score, and a brief reason.

The API-fallback prompt makes one concession: it prefers a game cover or an in-game screenshot over a photo of the physical cartridge, cart, or disc, and explicitly accepts an in-game screenshot when no cover art exists for the game.

Models without native JSON mode are prompted to output JSON as text, which FindRomCover extracts and parses.

## Cost example

A pick with 6 images costs roughly 6,000 input tokens and a few hundred output tokens. With the default OpenRouter model `google/gemma-4-26b-a4b-it`, that is about **$0.0006 per pick** — around **$0.60 per 1,000 picks**. See [Recommended Models](recommended-models.md) for a comparison of alternatives.

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| "No candidates above the similarity threshold" | Lower **Candidate similarity** in AI Settings or lower the main similarity threshold |
| The model returns no text | The model may be a reasoning model that ran out of output budget; see [AI Troubleshooting](troubleshooting.md) |
| Low confidence every time | Try a stronger model, or check that the game name is meaningful (enable MAME descriptions) |
| Pick is slow | Reduce **Image max size** and **Max candidates**; local models are inherently slower |
| Auto-save never triggers | Lower the **Auto-save threshold**; check the confidence shown in the result message |
| A manual save asks for confirmation | The AI did not recognize the image as a cover; confirm to save anyway or pick another image |

## Related pages

- [AI Settings](settings.md)
- [Batch Fill](batch-fill.md)
- [Query History](query-history.md)
- [AI Troubleshooting](troubleshooting.md)
