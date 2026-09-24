# Local Files Search

The **Local Files** tab finds cover images that already exist in your image folder by comparing filenames with the selected ROM. It is the fastest and completely offline way to fill gaps — for example when your covers use slightly different names than your ROMs.

## How it works

1. Select a game in the missing covers list.
2. FindRomCover compares the ROM name (or MAME description) against every image filename in your image folder. With **Ignore Bracketed Text in Matching** on (the default), region and revision tags such as `(USA)` and `[En]` are ignored on both sides.
3. Images whose similarity score is at or above the configured threshold are listed, ranked from best to worst.
4. Click an image to save it as the cover: it is converted to PNG if necessary and stored as `[gamename].png`.

For large collections a trigram index pre-filters candidates so that only plausible matches are compared in detail.

## Similarity algorithms

Choose the algorithm under `Set Similarity Algorithm`:

| Algorithm | Best for | Notes |
|-----------|----------|-------|
| **Jaro-Winkler Distance** | Most collections (default) | Rewards common prefixes; tolerant of small differences |
| **Jaccard Similarity** | Filenames with reordered words | Compares sets of character n-grams |
| **Levenshtein Distance** | Strict edit-distance matching | Counts insertions, deletions, and substitutions; early-exit optimization |

See [Similarity Algorithms](../configuration/similarity.md) for guidance on choosing and tuning.

## Similarity threshold

`Set Similarity Threshold` sets the minimum score (10%–90%) an image must reach to appear in the results. The default is 70%.

- **Raise it** (80–90%) for precise matches and short result lists.
- **Lower it** (40–60%) when filenames differ a lot, for example when your images use full game titles and your ROMs use abbreviations.

The threshold also pre-filters the candidates sent to AI Vision Assist, where a separate **Candidate similarity** value is configured — see [AI Settings](../ai/settings.md).

## Result list

Each result shows a thumbnail and the filename. The list is ranked by similarity. Hover for a larger preview where available.

Actions:

- **Left-click** an image to use it as the cover.
- **Right-click** for context actions such as copying the image filename.

## AI Pick Best

When [AI Vision Assist](../ai/index.md) is enabled, the **AI Pick Best** button sends the top-ranked local candidates to a vision model, which chooses the best-looking cover. The winner is highlighted with a green badge and moved to the front. Depending on your settings the pick can be saved automatically. See [Picking Covers](../ai/picking-covers.md).

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| No results | Lower the similarity threshold, switch algorithm, or verify the image folder contains covers |
| Wrong image ranked first | Raise the threshold or use AI Pick Best |
| Results take long on a network drive | Copy the image folder to a local disk |
| A cover is never matched | Rename the image to match the ROM, or assign it manually from the list |

## Related pages

- [Similarity Algorithms](../configuration/similarity.md)
- [Image Handling](image-handling.md)
- [AI Vision Assist](../ai/index.md)
