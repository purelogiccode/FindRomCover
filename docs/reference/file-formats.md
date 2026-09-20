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
| Web and common | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp` |
| High efficiency | `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2` |
| TIFF | `.tiff`, `.tif` |

All of these are recognized when checking whether a ROM already has a cover. Non-PNG images are converted to PNG when they are saved through FindRomCover or picked up by the watcher.

### Output format

| Property | Value |
|----------|-------|
| Format | PNG |
| Name | `<rom base name>.png` |
| Location | Image folder |

Examples:

| ROM | Cover |
|-----|-------|
| `Sonic the Hedgehog (USA).zip` | `Sonic the Hedgehog (USA).png` |
| `Super Mario World.sfc` | `Super Mario World.png` |

## Application files

| File | Format | Purpose |
|------|--------|---------|
| `Settings.dat` | SQLite | All settings, secrets encrypted |
| `QueryHistory.dat` | SQLite | AI query history |
| `ai-cache.json` | JSON | AI verdict cache |
| `ai-models.json` | JSON | Cached model lists |
| `mame.dat` | MAME database | Arcade game descriptions |
| `app.log`, `error.log`, `error_user.log` | Text | Diagnostics |
| `settings.dat.legacy` | Encrypted legacy | Backup of the old settings file |
| `settings.dat.corrupt` | Quarantined | Unreadable settings database |

See [Data Storage](../configuration/data-storage.md) for details.

## Media used by the application

| Asset | Format | Purpose |
|-------|--------|---------|
| `click.mp3` | MP3 | UI sound effect |
| Icons and images | PNG/ICO | Application UI |

## Related pages

- [Supported Extensions](../configuration/extensions.md)
- [Image Handling](../user-guide/image-handling.md)
- [Data Storage](../configuration/data-storage.md)
