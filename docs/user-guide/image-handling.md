# Image Handling

FindRomCover does more than download files: it normalizes everything to PNG, watches the image folder for new arrivals, and renames them to match the selected game. This page describes the full pipeline.

## Supported input formats

The following image formats are recognized and converted to PNG:

| Format | Extensions |
|--------|------------|
| Common web formats | `.png`, `.apng`, `.jpg`, `.jpeg`, `.jfif`, `.jpe`, `.bmp`, `.gif`, `.webp` |
| High-efficiency formats | `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2` |
| TIFF | `.tiff`, `.tif` |
| Vector and icon | `.svg`, `.ico` |

Conversion is performed by Magick.NET (ImageMagick) with automatic orientation correction. Resource limits are fixed and not user-configurable: the image pipeline is allowed 512 MB of memory and 4 worker threads, applied once at application startup.

## Cover file naming

Covers are saved as `[rom base name].png` in the top level of the image folder, where `[rom base name]` is the ROM filename without its extension. The name is sanitized before it is written:

- invalid filename characters are replaced with `_`;
- directory separators are removed and `..` sequences are stripped;
- trailing dots and whitespace are trimmed;
- reserved device names (for example `CON` or `PRN`) are prefixed to avoid conflicts;
- if nothing usable remains, the cover is named `unnamed`.

For example:

- ROM: `Sonic the Hedgehog (USA).zip`
- Cover: `Sonic the Hedgehog (USA).png`

Because matching ignores the image extension and recognizes many formats, covers in other formats are recognized too — but newly saved covers are converted to PNG for consistency.

## The file-system watcher

The watcher monitors the **top level** of the image folder (subfolders are not watched) and reacts to newly created image files:

1. **Detect** — a new image appears in the image folder.
2. **Wait for stability** — the watcher waits until the file is no longer being written.
3. **Verify (optional)** — when **Verify images with AI before renaming or saving** is enabled, the image is checked against the selected game first.
4. **Rename** — the file is renamed to match the currently selected missing ROM.
5. **Convert** — images are converted to PNG with Magick.NET. A file named `.png` whose bytes are actually JPEG/WebP (common when saving from a browser) or whose PNG data is truncated is normalized to a real PNG in place.
6. **Refresh** — the game is removed from the missing covers list.

The watcher never overwrites an existing cover:

- If a file with the target name already exists, the new file is renamed to a free name such as `Game (1).png`.
- For non-PNG sources, if the PNG cover already exists, the conversion is refused and the dropped file is kept under its current name (reported as "kept as-is").

There is no overwrite confirmation dialog in the watcher workflow.

!!! tip
    The watcher is what makes the Google Web and Bing Web workflow hands-free. Save the image in the top level of the image folder with any filename — it will be renamed for you.

## Retries and robustness

- Transient failures such as file locks and slow network drives are retried with a short delay.
- Conversion uses the same Magick.NET path as manual saves: content sniffing handles files whose extension lies about their contents, and PNG CRC mismatches from partial downloads are tolerated.
- A file is only processed after its size has been stable for about a second (waiting up to 20 seconds), so browser downloads are not converted mid-write.
- Corrupt files are reported after a few short retries and skipped without repeated error dialogs.
- Orphaned temporary files from interrupted conversions are cleaned up on startup.
- Access-denied situations on the watched folder are logged as warnings, and the watcher attempts to recover automatically instead of crashing.

## Automatic removal from the missing list

Once a cover with the correct name exists, the game is removed from the missing list the next time the list refreshes. Refreshes happen automatically after watcher activity and saves, or manually with **F5**.

## Manual image management

| Task | How |
|------|-----|
| Replace a cover | Click the replacement image on the Local Files or Google API tab while the game is selected — the existing PNG is replaced. Alternatively, delete the old PNG first and then drop or save the new image, so the watcher can rename it |
| Add a cover manually | Copy any supported image into the top level of the image folder and rename it to match the ROM, or let the watcher do it while the game is selected |
| Delete a cover | Delete the PNG from the image folder and press **F5** to re-scan |
| Inspect a cover | Right-click the image on the Local Files tab and choose **Open File**, or open it directly from the image folder |

!!! note
    A plain file drop never overwrites an existing cover. If the target name is taken, the dropped file is stored under a free name instead.

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| Image is not converted | Check that the format is supported and that the image folder is writable |
| Image is not renamed | Ensure a game is selected in the missing list; check the log for watcher warnings |
| Dropped image got a `(1)` suffix | A cover with the target name already exists and was protected; replace it through the Local Files tab or delete it first |
| Non-PNG file was "kept as-is" | The PNG cover already exists; remove it before converting the dropped file |
| Conversion fails repeatedly | The file may be corrupt; try saving it again from the source |
| Watcher misses files on a network share | Copy files to a local folder first; network notifications can be unreliable |

## Related pages

- [File Formats](../reference/file-formats.md)
- [Web Search](web-search.md)
- [AI Vision Assist](../ai/index.md)
- [Logs & Diagnostics](../configuration/logs.md)
