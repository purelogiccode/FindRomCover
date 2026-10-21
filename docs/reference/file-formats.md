# File Formats

This page summarizes the file types FindRomCover reads, writes, and manages.

## ROM files

Any file whose extension appears in the [supported extensions](../configuration/extensions.md) list is treated as a ROM. The list is fully configurable and covers most cartridge, disk, tape, and archive formats used by retro platforms.

Default extensions include (short excerpt):

```text
nes, sfc, smc, n64, z64, gb, gbc, gba, nds, 3ds, nsp, xci,
sms, md, gen, 32x, gg, gdi, cue, iso, bin, pbp, cso, chd,
zip, 7z, rar, gz, d64, t64, a78, wad, wbfs, ...
```

The full list is maintained in the application and can be edited under `Settings > Edit Supported Extensions...`.

## Cover images

### Recognized input formats

| Family | Extensions |
|--------|------------|
| Web and common | `.png`, `.apng`, `.jpg`, `.jpeg`, `.jfif`, `.jpe`, `.bmp`, `.gif`, `.webp` |
| High efficiency | `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2` |
| TIFF | `.tiff`, `.tif` |
| Vector and icons | `.svg`, `.ico` |

All of these are recognized when checking whether a ROM already has a cover. Images are converted to PNG when they are saved through FindRomCover or picked up by the watcher. A file with a misleading `.png` extension (for example WebP bytes saved as `.png`) is normalized to a real PNG.

### Output format

| Property | Value |
|----------|-------|
| Format | PNG |
| Name | `<rom base name>.png` |
| Location | Top level of the image folder |

Examples:

| ROM | Cover |
|-----|-------|
| `Sonic the Hedgehog (USA).zip` | `Sonic the Hedgehog (USA).png` |
| `Super Mario World.sfc` | `Super Mario World.png` |

## Screenshots

Pressing **F8** saves a PNG screenshot of the main window:

| Property | Value |
|----------|-------|
| Format | PNG |
| Name | `Screenshot_yyyyMMdd_HHmmss_fff.png` (a `_1`, `_2`, ... suffix is added if the name already exists) |
| Location | `Screenshot` folder next to the executable; when that folder is not writable, the `Screenshot` folder inside the per-user data folder is used |

## Application files

| File | Format | Purpose |
|------|--------|---------|
| `Settings.dat` | SQLite | All settings, secrets encrypted |
| `QueryHistory.dat` | SQLite | AI query history |
| `QueryHistory.dat.corrupt` | SQLite | Quarantined query-history database |
| `ai-cache.json` | JSON | AI verdict cache |
| `ai-models.json` | JSON | Cached model lists |
| `mame.dat` | MAME database | Arcade game descriptions |
| `app<yyyyMMdd>.log`, `ApiLogError.txt`, `UserLogError.txt`, `InternalLog.txt` | Text | Diagnostics |
| `settings.dat.legacy` | Encrypted legacy | Backup of the old settings file |
| `settings.dat.corrupt` | Quarantined | Unreadable settings database |
| `settings.xml.migrated` | XML | Legacy settings file moved aside after a successful migration |

See [Data Storage](../configuration/data-storage.md) for details.

## Media used by the application

| Asset | Format | Purpose |
|-------|--------|---------|
| `click.mp3` | MP3 | UI sound effect |
| Icons and images | PNG | Application UI |

## Related pages

- [Supported Extensions](../configuration/extensions.md)
- [Image Handling](../user-guide/image-handling.md)
- [Data Storage](../configuration/data-storage.md)
