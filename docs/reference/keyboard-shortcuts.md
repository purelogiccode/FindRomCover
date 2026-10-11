# Keyboard Shortcuts

FindRomCover keeps the keyboard surface small and predictable.

## Global

| Shortcut | Action |
|----------|--------|
| **F5** | Scan the ROM and image folders again (**Check for Missing Images**) |
| **F8** | Save a screenshot of the main window and show the path in the status bar |
| **Delete** | Remove the selected entry from the missing covers list |
| **Escape** | Exit the application |

## Folder boxes

| Shortcut | Action |
|----------|--------|
| **Enter** (ROM Folder box) | Validate the path only; does not start a scan |
| **Enter** (Image Folder box) | Validate the path only; does not start a scan |

When both paths are valid, use **Check for Missing Images** or **F5** to run the scan.

## Screenshots

**F8** captures the **main window**, not the OS-active window, so a dialog or the embedded browser does not end up in the image. The screenshot is saved as a PNG:

| Property | Value |
|----------|-------|
| Folder | `Screenshot` next to the executable; the per-user data folder's `Screenshot` folder is used when the application folder is not writable |
| File name | `Screenshot_yyyyMMdd_HHmmss_fff.png` (a `_1`, `_2`, ... suffix is added if the name already exists) |
| Feedback | The status bar shows the full path, or a failure message if saving was not possible |

See [Screenshots](../user-guide/screenshots.md) for details.

## Mouse

| Action | Result |
|--------|--------|
| Left-click a missing cover | Select the game and run the search for the active tab |
| Left-click a Local Files result | Save that image as the cover |
| Left-click a Google API thumbnail | Download and save that image as the cover |
| Right-click a missing cover | Context menu: remove, copy filename, delete ROM/ISO |
| Right-click a Local Files result | Context menu with copy/use actions |
| Right-click in the embedded browser | Browser context menu, including **Save image as...** |

## Menus

| Menu shortcut | Action |
|---------------|--------|
| `Theme > Light` / `Dark` | Switch base theme |
| `Set Similarity Algorithm` | Choose Jaccard, Jaro-Winkler, or Levenshtein |
| `Set Similarity Threshold` | Set the local match threshold (10–90%) |
| `Settings > Ignore Bracketed Text in Matching` | Toggle ignoring text inside `()`, `[]`, and `{}` during matching |
| `Set Thumbnail Size` | Set the preview size (100–500 px) for Google API thumbnails and Local Files results |
| `Settings > Show/Hide Log Window` | Toggle the live log viewer |

## Notes

- **Escape** exits the application; use the **Exit** menu item or close the window if you prefer the mouse.
- Shortcuts are not configurable.
- If a shortcut does not respond, click the main window first to ensure it has focus.

## Related pages

- [Interface Overview](../user-guide/interface.md)
- [Screenshots](../user-guide/screenshots.md)
- [Missing Covers List](../user-guide/missing-covers.md)
- [Command-Line Arguments](command-line.md)
