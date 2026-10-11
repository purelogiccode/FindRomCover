# Screenshots

FindRomCover can capture the main window to a PNG file without any third-party tool. Screenshots are handy for bug reports, forum posts, and documentation.

## Capturing a screenshot

1. Bring the main window into the state you want to capture (for example, after a failed scan or with a result list on screen).
2. Press **F8**.

FindRomCover renders the main window to a PNG file and shows the result in the status bar:

- `Screenshot saved: <path>` — the file was written; the full path tells you exactly where it is.
- `Screenshot failed. Check the log for details.` — no candidate folder could be written, or the capture itself failed; see [Logs & Diagnostics](../configuration/logs.md).

## Where screenshots are saved

FindRomCover tries two folders in order and writes to the first one that works:

| Priority | Folder | Notes |
|----------|--------|-------|
| 1 | `<application folder>/Screenshot` | Preferred. Created automatically when it does not exist. This keeps screenshots together with a portable installation |
| 2 | `<data folder>/Screenshot` | Fallback. Used when the application folder is not writable, for example under `Program Files` |

The data folder depends on the platform:

| Platform | Data folder |
|----------|-------------|
| Windows | `%LocalAppData%\FindRomCover` |
| Linux | `~/.local/share/FindRomCover` |
| macOS | `~/Library/Application Support/FindRomCover` |

!!! note
    Screenshots are the only files FindRomCover prefers to write next to the executable. Settings, caches, and logs always go to the per-user data folder. See [Data Storage](../configuration/data-storage.md).

## File naming

- File names use the pattern `Screenshot_yyyyMMdd_HHmmss_fff.png`, based on the local time at capture: year, month, day, hour, minute, second, and milliseconds.
- If a file with that exact name already exists (for example when capturing twice within the same millisecond), a numeric suffix is added: `Screenshot_20261011_143012_481_1.png`, then `_2`, and so on. Nothing is overwritten.

## Using screenshots in bug reports

1. Reproduce the problem and press **F8** while the relevant window state is visible.
2. Open the screenshots folder from the status-bar path.
3. Attach the PNG to your GitHub issue together with the related log excerpt — see [Reporting a Bug](../configuration/bug-reports.md) and [Logs & Diagnostics](../configuration/logs.md).
4. Review the image before posting. Screenshots can show folder paths, ROM filenames, and search queries; crop or redact anything you do not want to share.

!!! tip
    **F8** captures the main window only; open dialogs are not included. For problems involving a dialog, note its exact wording in the bug report, close it, and then capture the main window state (list, status bar, and log window) that shows the problem.

## Related pages

- [Interface Overview](interface.md)
- [Keyboard Shortcuts](../reference/keyboard-shortcuts.md)
- [Reporting a Bug](../configuration/bug-reports.md)
- [Logs & Diagnostics](../configuration/logs.md)
