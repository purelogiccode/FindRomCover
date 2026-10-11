# Themes & Appearance

FindRomCover uses the Avalonia Fluent theme with light and dark variants, plus a MahApps-compatible accent color map implemented in `ThemePalette`. You can switch between the two base themes and choose from 23 accent colors. Both selections are stored in the settings database and applied again on startup.

## Base themes

| Theme | Description |
|-------|-------------|
| **Dark** | Default; easier on the eyes during long sessions |
| **Light** | Bright interface for daytime use |

Change the theme under the **Theme** menu. The choice applies to the main window and every dialog immediately.

## Accent colors

The following 23 accents are available under `Theme > Accent Colors`:

| Group | Colors |
|-------|--------|
| Classics | Red, Green, Blue, Purple, Orange |
| Cool | Lime, Emerald, Teal, Cyan, Cobalt, Indigo, Violet |
| Warm | Pink, Magenta, Crimson, Amber, Yellow |
| Earthy | Brown, Olive, Steel, Mauve, Taupe, Sienna |

The accent color is used for highlights, selected items, buttons, and progress indicators. The palette keeps the same color names as the MahApps.Metro accents the application historically used, so existing settings remain valid.

## Where the theme is applied

- **Main window** — theme and accent.
- **Dialogs** — API Settings, AI Settings, batch fill, settings, about, and the log window are themed when opened.
- **Fallback** — an invalid base theme falls back to **Dark** and an invalid accent color falls back to **Blue**. At the palette level, any theme name other than `Dark` is rendered with the light variant.

## Persistence

Theme and accent are saved in `Settings.dat` in the per-user data folder. They survive updates and application restarts.

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| Theme does not change | Try switching again; check the log for theme errors |
| Window looks unstyled | Restart the application; if it persists, reset the theme by deleting `Settings.dat` |
| Accent looks wrong on a dialog | Reopen the dialog; themes are applied when windows open |

## Related pages

- [Settings Overview](index.md)
- [Data Storage](data-storage.md)
- [Logs & Diagnostics](logs.md)
