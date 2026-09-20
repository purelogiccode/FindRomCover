# Image Handling

FindRomCover does more than download files: it normalizes everything to PNG, watches the image folder for new arrivals, and renames them to match the selected game. This page describes the full pipeline.

## Supported input formats

The following image formats are recognized and converted to PNG:

| Format | Extensions |
|--------|------------|
| Common web formats | `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.webp` |
| High-efficiency formats | `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2` |
| TIFF | `.tiff`, `.tif` |

Conversion is performed by Magick.NET (ImageMagick) with automatic orientation correction and configurable resource limits.

## Cover file naming

Covers are always saved as `[rom base name].png` in the image folder, where `[rom base name]` is the ROM filename without its extension. For example:

- ROM: `Sonic the Hedgehog (USA).zip`
- Cover: `Sonic the Hedgehog (USA).png`

Because the comparison ignores case and image extension, covers in other formats are recognized too — but newly saved covers are converted to PNG for consistency.

## The file-system watcher

The watcher monitors the image folder (including subfolders) and reacts to newly created image files:

1. **Detect** — a new image appears in the image folder.
2. **Wait for stability** — the watcher waits until the file is no longer being written.
3. **Verify (optional)** — when **Verify images with AI before renaming or saving** is enabled, the image is checked against the selected game first.
4. **Convert** — non-PNG images are converted to PNG.
5. **Rename** — the file is renamed to match the currently selected missing ROM.
6. **Refresh** — the game is removed from the missing covers list.

If a file with the target name already exists, a confirmation dialog asks whether to overwrite it.

> **Tip:** The watcher is what makes the Google Web and Bing Web workflow hands-free. Save the image anywhere inside the image folder with any filename — it will be renamed for you.

## Retries and robustness

- Transient failures such as file locks and slow network drives are retried with a short delay.
- Corrupt or misformatted images are detected immediately and skipped without repeated retries.
- Orphaned temporary files from interrupted conversions are cleaned up on startup.
- Access-denied situations on the watched folder are logged as warnings, and the watcher attempts to recover automatically instead of crashing.

## Automatic removal from the missing list

Once a cover with the correct name exists, the game is removed from the missing list the next time the list refreshes. Refreshes happen automatically after watcher activity and saves, or manually with **F5**.

## Manual image management

| Task | How |
|------|-----|
| Replace a cover | Save the new image into the image folder; confirm the overwrite prompt |
| Add a cover manually | Copy any supported image into the image folder and rename it to match the ROM, or let the watcher do it while the game is selected |
| Delete a cover | Delete the PNG from the image folder and press **F5** to re-scan |
| Inspect a cover | Open it in your default viewer from the image folder |

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| Image is not converted | Check that the format is supported and that the image folder is writable |
| Image is not renamed | Ensure a game is selected in the missing list; check the log for watcher warnings |
| Conversion fails repeatedly | The file may be corrupt; try saving it again from the source |
| Overwrite prompt keeps appearing | A cover with that name already exists; confirm to replace it |
| Watcher misses files on a network share | Copy files to a local folder first; network notifications can be unreliable |

## Related pages

- [File Formats](../reference/file-formats.md)
- [Web Search](web-search.md)
- [AI Vision Assist](../ai/index.md)
- [Logs & Diagnostics](../configuration/logs.md)
