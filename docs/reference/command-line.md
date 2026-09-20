# Command-Line Arguments

FindRomCover accepts optional command-line arguments to pre-fill the ROM and image folders. This is useful for launching the application from a launcher, a script, or a file manager.

## Usage

```text
FindRomCover.exe [ImageFolder] [RomFolder]
```

| Arguments | Behavior |
|-----------|----------|
| none | Start with the folders from the last session |
| one | The argument is used as the **Image Folder** |
| two | The first argument is the **Image Folder**, the second is the **ROM Folder**; a scan starts automatically |

Only existing directories are accepted. If a path does not exist, it is ignored and the application starts with the stored folders instead.

## Examples

Start with only the image folder:

```text
FindRomCover.exe "D:\Covers"
```

Start with both folders and scan immediately:

```text
FindRomCover.exe "D:\Covers" "D:\ROMs"
```

Use it from a shortcut or batch file:

```bat
@echo off
start "" "C:\Tools\FindRomCover\FindRomCover.exe" "D:\Covers" "D:\ROMs"
```

## Notes

- Quote paths that contain spaces.
- The scan uses the same logic as **Check for Missing Images**; the status bar shows the result.
- Folder paths are stored, so the next start without arguments reuses them.
- There are no other switches; all other behavior is configured through the application menus.

## Related pages

- [Folders & Scanning](../user-guide/folders-and-scanning.md)
- [Interface Overview](../user-guide/interface.md)
