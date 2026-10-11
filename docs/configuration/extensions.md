# Supported Extensions

FindRomCover scans the ROM folder for files whose extension is in the supported-extensions list. The default list contains **120 extensions** and covers cartridge, disk, tape, and archive formats from most retro platforms.

## Editing the list

1. Open `Settings > Edit Supported Extensions...`.
2. Click **Add...** to open the input dialog, type the extension (without the dot), and confirm.
3. Remove an extension by selecting it and clicking **Remove**.
4. Click **Save**.

Extensions may contain letters, digits, and hyphens only. Dots are stripped from the input, duplicates are rejected case-insensitively, and valid entries are normalized to lower case and sorted alphabetically. Changing the list applies immediately: the settings are saved and the ROM folder is rescanned so the missing covers list stays accurate.

## Default extensions

```text
2hd, 3ds, 7z, 88d, a78, arc, bat, bin, bs, cas, ccd, cdi, cdt, chd, cht,
ciso, cmd, col, cpr, cso, cue, cv, d64, d71, d81, d88, dim, dol, dsk, dup,
dummy, elf, exe, fdi, fds, fig, g64, gb, gcm, gcz, gdi, gg, gz, hdf, hdm,
img, int, ipf, iso, lnk, lnx, m3u, mdf, mds, ms1, msa, mx1, mx2, n64, nbz,
nca, ndd, nds, nes, nib, nrg, nro, nso, nsp, o, pbp, pce, prg, prx, rar, ri,
rom, rvz, sc, scl, sda, sf, sfc, sfx, sg, smc, sms, sna, st, stx, swc, t64,
tap, tgc, toc, trd, tzx, u1, unf, unif, url, v64, voc, wad, wbfs, wua, xci,
xdf, z64, z80, zip, zso, gba, gbc, snes, md, smd, gen, 32x, sgg
```

| Platform family | Example extensions |
|-----------------|--------------------|
| Nintendo (NES/SNES/N64/GB/GBA/DS/3DS/Switch) | `nes`, `sfc`, `smc`, `n64`, `z64`, `gb`, `gbc`, `gba`, `nds`, `3ds`, `nsp`, `xci` |
| Sega (Master System/Genesis/Game Gear/CD) | `sms`, `md`, `gen`, `32x`, `gg`, `gdi`, `cue` |
| Sony (PlayStation/PSP) | `bin`, `cue`, `iso`, `pbp`, `cso`, `chd` |
| Commodore (C64/Amiga) | `d64`, `d71`, `d81`, `t64`, `adf`-style disk images |
| Atari | `a78`, `atr`-style disk images, `xex` |
| Arcade | `zip`, `7z`, `chd` |
| Archives | `zip`, `7z`, `rar`, `gz` |

> **Tip:** Add console-specific extensions you use, such as `adf`, `xex`, `atr`, or `d64`, to catch formats that are not in the default list.

## How matching works

- Extensions are compared case-insensitively, so `.ZIP` matches `zip`.
- Subfolders are scanned recursively.
- A ROM counts as covered only when an image with the same base name exists in the top level of the image folder **and uses one of the recognized image extensions** (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp`, `.tiff`, `.tif`, `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2`, `.ico`, `.svg`). An unrelated file with a non-image extension does not count.
- Files with extensions outside the list are ignored entirely.

## Common problems

| Problem | Cause | Fix |
|---------|-------|-----|
| No ROM files found | Extension list does not match your files | Add your extensions and save |
| Unrelated files appear | Very broad extensions such as `bin` or `exe` | Remove broad extensions if your ROM folder contains other files |
| A known ROM is missing from the list | Unsupported extension | Add it under `Settings > Edit Supported Extensions...` |

## Related pages

- [Folders & Scanning](../user-guide/folders-and-scanning.md)
- [File Formats](../reference/file-formats.md)
- [Missing Covers List](../user-guide/missing-covers.md)
