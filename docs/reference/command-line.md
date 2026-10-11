# Command-Line Arguments

FindRomCover accepts optional command-line arguments to pre-fill the ROM and image folders. This is useful for launching the application from a launcher, a script, or a file manager.

## Usage

```text
FindRomCover [ImageFolder] [RomFolder]
```

On Windows the executable is `FindRomCover.exe`; on Linux and macOS use the `FindRomCover` binary.

| Arguments | Behavior |
|-----------|----------|
| none | Start with empty folder boxes; the previous session is not restored |
| one | The argument is used as the **Image Folder** |
| two | The first argument is the **Image Folder**, the second is the **ROM Folder** |

Only existing directories are accepted. A path that does not exist is ignored. With one or two valid arguments the folder boxes are prefilled; when both folders are valid (the usual two-argument case), the scan starts automatically.

## Examples

Start with only the image folder (Windows):

```text
FindRomCover.exe "D:\Covers"
```

Start with both folders and scan immediately (Windows):

```text
FindRomCover.exe "D:\Covers" "D:\ROMs"
```

The same on Linux or macOS:

```bash
./FindRomCover ~/covers ~/roms
```

Use it from a shortcut or batch file:

```bat
@echo off
start "" "C:\Tools\FindRomCover\FindRomCover.exe" "D:\Covers" "D:\ROMs"
```

## Notes

- Quote paths that contain spaces.
- The scan uses the same logic as **Check for Missing Images**; the status bar shows the result.
- Folders passed on the command line are not restored on the next start. The ROM folder is not persisted at all, and the remembered image folder is used only for temporary-file cleanup at startup — see [Data Storage](../configuration/data-storage.md).
- There are no other switches; all other behavior is configured through the application menus.

## Related pages

- [Folders & Scanning](../user-guide/folders-and-scanning.md)
- [Interface Overview](../user-guide/interface.md)
- [Data Storage](../configuration/data-storage.md)
