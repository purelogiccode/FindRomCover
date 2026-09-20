# Themes & Appearance

FindRomCover uses the MahApps.Metro theme engine. You can switch between light and dark base themes and choose from more than twenty accent colors. Both selections are stored in the settings database and restored on startup.

## Base themes

| Theme | Description |
|-------|-------------|
| **Dark** | Default; easier on the eyes during long sessions |
| **Light** | Bright interface for daytime use |

Change the theme under the **Theme** menu. The choice applies to the main window and every dialog immediately.

## Accent colors

The following accents are available under `Theme > Accent Colors`:

| Group | Colors |
|-------|--------|
| Classics | Red, Green, Blue, Purple, Orange |
| Cool | Lime, Emerald, Teal, Cyan, Cobalt, Indigo, Violet |
| Warm | Pink, Magenta, Crimson, Amber, Yellow |
| Earthy | Brown, Olive, Steel, Mauve, Taupe, Sienna |

The accent color is used for highlights, selected items, buttons, and progress indicators.

## Where the theme is applied

- **Main window** — theme and accent.
- **Dialogs** — API Settings, AI Settings, batch fill, settings, about, and the log window are themed when opened.
- **Fallback** — if a theme fails to apply (for example after an unusual configuration), FindRomCover falls back to `Light.Blue` and logs the problem.

## Persistence

Theme and accent are saved in `%LocalAppData%\FindRomCover\Settings.dat`. They survive updates and application restarts.

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| Theme does not change | Try switching again; check the log for theme errors |
| Window looks unstyled | Restart the application; if it persists, reset the theme by deleting `Settings.dat` |
| Accent looks wrong on a dialog | Reopen the dialog; themes are applied when windows open |

## Related pages

- [Settings Overview](index.md)
- [Logs & Diagnostics](logs.md)
