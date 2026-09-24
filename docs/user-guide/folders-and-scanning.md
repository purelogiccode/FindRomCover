# Folders & Scanning

FindRomCover compares two folders: the **ROM folder** with your game files and the **image folder** with your cover images. This page explains how the scan works and how to get accurate results.

## Setting the folders

1. Click **Browse...** next to **ROM Folder** and select the folder that contains your games. Subfolders are scanned recursively.
2. Click **Browse...** next to **Image Folder** and select the folder where covers are stored.
3. The scan starts automatically as soon as both paths are valid. You can also press **F5** or click **Check for Missing Images** at any time.

Folder paths are validated before scanning. If a path does not exist, a warning is shown and the path is not saved.

## What the scan does

1. Enumerates every file under the ROM folder whose extension is in the [supported extensions](../configuration/extensions.md) list.
2. For each ROM file, looks for a cover image with the same base name in the image folder. Recognized image formats include `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.tiff`, `.tif`, `.webp`, `.avif`, `.heic`, `.heif`, `.ico`, `.svg`, `.jxl`, and `.jp2`.
3. Adds every ROM without a matching image to the **Missing Covers** list.

The status bar reports the outcome:

| Message | Meaning |
|---------|---------|
| `Found n ROM files, m missing covers` | Scan completed; `m` games need artwork |
| `No ROM files found matching the supported extensions` | The ROM folder is empty or the extensions do not match; check `Settings > Edit Supported Extensions...` |
| `No missing covers found` | Every ROM already has a cover |

> **Note:** A ROM is considered covered when an image with the same base name exists in the image folder. The comparison is case-insensitive and ignores the image extension, so `Sonic (USA).zip` is matched by `Sonic (USA).png`.

## Automatic re-scan triggers

The list is refreshed automatically after:

- selecting a new ROM or image folder;
- editing the supported extensions;
- saving a cover through the Local Files or Google API tabs;
- the image folder watcher renaming a new image to the selected game;
- a completed AI Batch Fill run.

## Command-line pre-fill

You can start FindRomCover with folders already set:

```text
FindRomCover.exe "C:\Path\To\ImageFolder" "C:\Path\To\RomFolder"
```

- One argument is treated as the image folder.
- Two arguments set the image folder and the ROM folder and trigger a scan on startup.

See [Command-Line Arguments](../reference/command-line.md).

## MAME descriptions

When `Settings > Use MAME Descriptions` is enabled, searches use the full MAME game description instead of the cleaned ROM filename. This can produce better web results for arcade titles. The data comes from `mame.dat` in the application folder — see [MAME data](web-search.md#mame-data).

## Performance tips

- Keep the image folder on a local disk for fast scans; network shares work but are slower.
- Large collections benefit from the built-in trigram index, which pre-filters candidates before the full similarity comparison.
- Use a narrower supported-extension list if your ROM folder contains unrelated files.

## Related pages

- [Supported Extensions](../configuration/extensions.md)
- [Local Files Search](local-files.md)
- [Image Handling](image-handling.md)
