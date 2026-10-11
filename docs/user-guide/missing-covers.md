# Missing Covers List

The missing covers list is the heart of FindRomCover. It shows every ROM file that does not have a matching cover image yet and drives the search workflow.

## What appears in the list

Every file under the ROM folder whose extension is in the [supported extensions](../configuration/extensions.md) list is checked for a matching cover in the top level of the image folder. A ROM is listed when no image with the same base name exists. The list is sorted alphabetically, and the header shows the current count, for example `MISSING COVERS: 87`.

## Working with the list

| Action | Result |
|--------|--------|
| Click an entry | Selects the game, runs the search for the active tab, and copies the filename to the clipboard |
| Press **Delete** | Removes the selected entry from the list |
| Click **Check for Missing Images** (or press **F5**) | Re-scans both folders and rebuilds the list |
| Click **Remove Selected Item from List** | Removes the selected entry from the list without touching any files |
| Click **AI Fill Missing Covers...** | Runs AI Batch Fill over the whole list (requires AI Assist) |
| Right-click > **Remove Item from the list** | Removes the entry from the list without touching any files |
| Right-click > **Copy FileName** | Copies the ROM filename to the clipboard |
| Right-click > **Delete corresponding ROM or ISO** | Permanently deletes the ROM file from disk after confirmation |

Removing an entry from the list does not change anything on disk. It reappears after the next full scan unless a cover exists or the ROM file is deleted.

!!! warning
    **Delete corresponding ROM or ISO** removes the actual game file. The action asks for confirmation and cannot be undone.

## Refreshing the list

The list refreshes automatically when:

- a folder is chosen with a **Browse...** button, or the extensions list is edited and saved;
- a cover is saved from the Local Files or Google API tabs;
- the watcher renames a new image to the selected game;
- an AI Batch Fill run completes.

Press **F5** or click **Check for Missing Images** for a manual refresh.

## Keyboard and clipboard

- Selecting an entry copies the filename to the clipboard automatically, so you can paste it into a search engine or a file manager.
- The **Delete** key removes the selected entry from the list.

## AI Batch Fill

`AI Fill Missing Covers...` processes the list automatically: local candidates first, optional Google API fallback, with confidence thresholds and query history. See [Batch Fill](../ai/batch-fill.md).

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| List is empty but games exist | Verify the ROM folder and the supported extensions; check the status bar message |
| A game stays in the list after saving a cover | Ensure the image is in the top level of the image folder and named exactly like the ROM base name, then press **F5** |
| Deleted ROM still appears | Refresh with **F5** |
| Entries removed by mistake | Refresh with **F5** to rebuild the list |

## Related pages

- [Folders & Scanning](folders-and-scanning.md)
- [Image Handling](image-handling.md)
- [Local Files Search](local-files.md)
- [Batch Fill](../ai/batch-fill.md)
