# Web Search (Google Web & Bing Web)

The **Google Web** and **Bing Web** tabs embed a full browser (Microsoft Edge WebView2) and open an image search for the selected game. No API key is required for either tab.

## Workflow

1. Select a game in the missing covers list.
2. Switch to **Google Web** or **Bing Web**. The search loads automatically.
3. Refine the search with the **Extra Query** box if needed, for example:
   - `box art`
   - `front cover`
   - `game cover`
   - a platform name such as `snes`
4. Right-click an image in the embedded browser and choose **Save image as...**.
5. Save the image anywhere inside your image folder, using any filename.

FindRomCover's file-system watcher notices the new file, renames it to the selected game, converts it to PNG, and removes the game from the missing list. You do not have to name the file correctly yourself.

> **Note:** Automatic one-click saving from the embedded browser is not available because browser security rules prevent the application from reading image data from the page. Saving manually through the browser's context menu is the supported workflow.

## How automatic matching works

The image folder watcher runs in the background and:

1. detects newly created image files in the image folder (including subfolders);
2. converts them to PNG if they use another supported format;
3. renames the file to match the currently selected missing ROM;
4. removes the entry from the missing covers list;
5. optionally asks AI Vision Assist to verify the image first (see below).

If a file with the target name already exists, you are asked whether to overwrite it.

Because of this watcher, drag-and-drop or copy-paste into the image folder works the same way as saving from the browser.

## Verify with AI before renaming

When **Verify images with AI before renaming or saving** is enabled in [AI Settings](../ai/settings.md), the watcher sends the new image to your configured vision model and asks whether it matches the selected game.

- **Match** — the image is renamed and converted as usual.
- **Mismatch** — the image is left untouched and a message is logged so you can review it.

This prevents a wrongly saved image (for example a screenshot or a different game) from silently becoming the cover.

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
| Reuse the browser session | Both tabs keep their page until you select another game |

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| Blank browser or "WebView2 component is not ready" | Install the Microsoft Edge WebView2 Runtime when prompted, then restart the app |
| Search shows unrelated images | Refine the Extra Query or enable MAME descriptions |
| Saved image is not renamed | Make sure you saved it inside the image folder and that a game is selected in the missing list; check the log for watcher errors |
| Wrong image was renamed | Enable **Verify images with AI** or check the log; rename the file manually and refresh with **F5** |

## Related pages

- [Google Custom Search API](google-api.md)
- [Image Handling](image-handling.md)
- [AI Vision Assist](../ai/index.md)
