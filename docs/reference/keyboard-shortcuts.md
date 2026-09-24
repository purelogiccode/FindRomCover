# Keyboard Shortcuts

FindRomCover keeps the keyboard surface small and predictable.

## Global

| Shortcut | Action |
|----------|--------|
| **F5** | Scan the ROM and image folders again (**Check for Missing Images**) |
| **F8** | Save a screenshot of the active window and show the path in the status bar |
| **Delete** | Remove the selected entry from the missing covers list (key repeat is suppressed) |
| **Escape** | Exit the application |

## Folder boxes

| Shortcut | Action |
|----------|--------|
| **Enter** (ROM Folder box) | Validate the path and start scanning |
| **Enter** (Image Folder box) | Validate the path and start scanning |

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
| `Ignore Bracketed Text in Matching` | Toggle ignoring text inside `()`, `[]`, and `{}` during matching |
| `Set Thumbnail Size` | Set Google API thumbnail size (100–500 px) |
| `Settings > Show/Hide Log Window` | Toggle the live log viewer |

## Notes

- **Escape** exits the application; use the **Exit** menu item or close the window if you prefer the mouse.
- Shortcuts are not configurable.
- If a shortcut does not respond, click the main window first to ensure it has focus.

## Related pages

- [Interface Overview](../user-guide/interface.md)
- [Missing Covers List](../user-guide/missing-covers.md)
- [Command-Line Arguments](command-line.md)
