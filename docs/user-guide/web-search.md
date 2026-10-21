# Web Search (Google Web & Bing Web)

The **Google Web** and **Bing Web** tabs embed a native browser and open an image search for the selected game. Avalonia's web view uses WebView2 on Windows, WKWebView on macOS, and WebKitGTK on Linux. No API key is required for either tab.

## Workflow

1. Select a game in the missing covers list.
2. Switch to **Google Web** or **Bing Web**. The search loads automatically.
3. Refine the search with the **Extra Query** box if needed, for example:
   - `box art`
   - `front cover`
   - `game cover`
   - a platform name such as `snes`
4. Right-click an image in the embedded browser and choose **Save image as...**.
5. Save the image in the top level of your image folder, using any filename.

FindRomCover's file-system watcher notices the new file, renames it to the selected game, converts it to PNG, and removes the game from the missing list. You do not have to name the file correctly yourself.

!!! note
    Automatic one-click saving from the embedded browser is not available because browser security rules prevent the application from reading image data from the page. Saving manually through the browser's context menu is the supported workflow. Every time you switch to a web tab, the search is loaded again for the currently selected game.

## How automatic matching works

The image folder watcher runs in the background and:

1. detects newly created image files in the **top level** of the image folder (subfolders are not watched);
2. waits until the browser has finished writing the file;
3. renames the file to match the currently selected missing ROM;
4. converts it to PNG with the shared Magick.NET pipeline, including files whose `.png` name hides JPEG/WebP bytes or a truncated download;
5. removes the entry from the missing covers list;
6. optionally asks AI Vision Assist to verify the image first (see below).

The watcher never overwrites an existing cover:

- If a file with the target name already exists, the new file is renamed to a free name such as `Game (1).png` instead.
- For non-PNG sources, if the PNG cover already exists, the conversion is refused and the dropped file is kept under its current name. The action is reported as "kept as-is" so the existing cover is protected.

There is no overwrite prompt; the existing cover always wins.

Because of this watcher, drag-and-drop or copy-paste into the top level of the image folder works the same way as saving from the browser.

## Verify with AI before renaming

When **Verify images with AI before renaming or saving** is enabled in [AI Settings](../ai/settings.md), the watcher sends the new image to your configured vision model and asks whether it matches the selected game.

- **Match** — the image is renamed and converted as usual.
- **Mismatch** — the image is left untouched and a message is logged so you can review it.

This prevents a wrongly saved image (for example a screenshot or a different game) from silently becoming the cover.

## When the embedded web view is unavailable

If the platform web view runtime cannot be used, FindRomCover shows a dialog that names the missing component and asks whether to open its download page:

- Windows: **WebView2 Runtime** — [developer.microsoft.com/microsoft-edge/webview2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- macOS: **WebKit** — Apple's Safari support page
- Linux: **WebKitGTK** — [webkitgtk.org/releases](https://webkitgtk.org/releases/)

The web search is then opened in your **system browser** instead, the status bar shows `Web view component is not ready.`, and web searches continue to use the system browser for the rest of the session. All other features keep working normally.

## Where the embedded browser stores its data

On Windows, the WebView2 profile (cache, cookies, and local storage) lives in the per-user data folder — `%LocalAppData%\FindRomCover\WebView2` — with a separate profile per web tab, instead of next to the executable. This keeps the embedded browser working when FindRomCover is installed in a launcher-managed or read-only folder. Deleting the `WebView2` folder resets the embedded browser. macOS and Linux use their native web view's own storage.

## MAME data

MAME arcade titles often have cryptic filenames. When `Settings > Use MAME Descriptions` is enabled, FindRomCover looks up the game in `mame.dat` (shipped with the application) and uses the full description for the search query instead of the cleaned filename.

- `mame.dat` must be present in the application folder.
- If the file is missing or corrupt, a warning is logged and filename-based queries are used instead.
- MAME descriptions can also be shown in the missing covers list.

## Tips

| Goal | Suggestion |
|------|------------|
| Better box-art results | Add `box art` or `front cover` to the Extra Query |
| Avoid fan art or logos | Add the platform name and `cover` |
| Search a specific region | Add the region, for example `USA` |
| Load the current search again | Switch away and back to the tab; the search re-runs for the selected game |

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| Blank browser or "Web view component is not ready" | Install the runtime named in the dialog (WebView2 on Windows, WebKitGTK on Linux; WKWebView ships with macOS), or use the system-browser fallback, then restart the app |
| Search shows unrelated images | Refine the Extra Query or enable MAME descriptions |
| Saved image is not renamed | Make sure you saved it in the top level of the image folder and that a game is selected in the missing list; check the log for watcher errors |
| Wrong image was renamed | Enable **Verify images with AI** or check the log; rename the file manually and refresh with **F5** |
| A dropped file got a `(1)` suffix | A cover with the target name already exists; the existing cover was protected and the new file was kept under a free name |
| Non-PNG file was "kept as-is" | The PNG cover for this game already exists; delete or replace it first if you want the new image to become the cover |

## Related pages

- [Google Custom Search API](google-api.md)
- [Image Handling](image-handling.md)
- [Missing Covers List](missing-covers.md)
- [AI Vision Assist](../ai/index.md)
