# Folders & Scanning

FindRomCover compares two folders: the **ROM folder** with your game files and the **image folder** with your cover images. This page explains how the scan works and how to get accurate results.

## Setting the folders

1. Click **Browse...** next to **ROM Folder** and select the folder that contains your games. Subfolders are scanned recursively.
2. Click **Browse...** next to **Image Folder** and select the folder where covers are stored.
3. The scan starts automatically as soon as a folder is chosen with a **Browse...** button and both paths are valid. You can also click **Check for Missing Images** or press **F5** at any time.

Typing a path into a folder box and pressing **Enter** only validates the path and enables the **Check for Missing Images** button; it does not start a scan. Invalid paths are rejected with a warning.

## What the scan does

1. Enumerates every file under the ROM folder (including subfolders) whose extension is in the [supported extensions](../configuration/extensions.md) list.
2. For each ROM file, looks for a cover image with the same base name in the **top level** of the image folder. Recognized image formats are `.png`, `.jpg`, `.jpeg`, `.avif`, `.bmp`, `.gif`, `.webp`, `.tiff`, `.tif`, `.ico`, `.svg`, `.jxl`, `.jp2`, `.heic`, and `.heif`.
3. Adds every ROM without a matching image to the **Missing Covers** list, sorted alphabetically.

The status bar reports the outcome:

| Message | Meaning |
|---------|---------|
| `Found {n} ROM file(s) matching the supported extensions; {m} missing cover(s).` | Scan completed; `n` files matched, `m` games need artwork |
| `No ROM files found in "{path}" matching the supported extensions.` | The ROM folder is empty or no file matched; check `Settings > Edit Supported Extensions...` |

!!! note "Matching details"
    - Only the top level of the image folder is checked. A cover stored in a subfolder does not count.
    - The lookup tries the sanitized ROM base name first, then the original base name, against every recognized image extension.
    - The comparison is case-insensitive on Windows, where the file system is case-insensitive. On Linux and macOS it is case-sensitive, so `Sonic (USA).ZIP` is matched by `Sonic (USA).png` but not by `sonic (usa).png`.

## Folder persistence

- The ROM folder is not persisted. It is empty again on the next start unless you pass it on the command line.
- The image folder is saved as the last-used folder when you browse for it or when the window closes, but it is **not** restored into the interface on the next start.
- With command-line arguments the boxes are pre-filled, and a scan starts when both paths are valid — see [Command-Line Arguments](../reference/command-line.md).

## Automatic re-scan triggers

The list is refreshed automatically after:

- choosing a folder with a **Browse...** button;
- editing and saving the supported extensions;
- saving a cover through the Local Files or Google API tabs;
- the image folder watcher renaming a new image to the selected game;
- a completed AI Batch Fill run.

Press **F5** or click **Check for Missing Images** for a manual refresh.

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
- [Missing Covers List](missing-covers.md)
- [Command-Line Arguments](../reference/command-line.md)
