# Building & Running

This page describes how to build FindRomCover from source and produce release archives.

## Prerequisites

| Requirement | Notes |
|-------------|-------|
| Windows 10 or later | The application targets `net10.0-windows` and WPF |
| [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Version pinned by `global.json` with `rollForward: latestMajor` |
| Visual Studio 2022+ or Rider | Optional; the CLI is sufficient |
| Microsoft Edge WebView2 Runtime | Needed at runtime for the web tabs |

## Clone and build

```bash
git clone https://github.com/purelogiccode/FindRomCover.git
cd FindRomCover
dotnet build CSharp_FindRomCover.sln
```

A warning-free build is expected: the projects enable Meziantou, Roslynator, and Microsoft.CodeAnalysis analyzers.

## Run from source

```bash
dotnet run --project FindRomCover
```

The application starts with the folders from your last session. You can pass startup folders as arguments:

```bash
dotnet run --project FindRomCover -- "D:\Covers" "D:\ROMs"
```

## Run tests

```bash
dotnet test CSharp_FindRomCover.sln
```

See [Testing](testing.md) for details and the full test layout.

## Publish

Release builds are framework-dependent and single-file:

```bash
dotnet publish FindRomCover/FindRomCover.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
dotnet publish FindRomCover/FindRomCover.csproj -c Release -r win-arm64 --self-contained false -p:PublishSingleFile=true
```

The output is written under `FindRomCover/bin/Release/net10.0-windows/<rid>/publish`. Zip the contents as `release_<version>_win-<rid>.zip` to match the naming used on the Releases page.

> **Note:** Published builds are framework-dependent. Users need the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## Project settings worth knowing

| Setting | Value |
|---------|-------|
| Target framework | `net10.0-windows` |
| Language version | C# 14 |
| Nullable | Enabled |
| WPF | Enabled |
| Assembly/file version | `3.2.0` (kept in `FindRomCover.csproj`) |
| `mame.dat` | Copied to the output directory on every build |
| `settings.dat` | Never copied to the output directory |

## Documentation build

The docs site uses MkDocs Material:

```bash
pip install -r docs/requirements.txt
mkdocs serve
```

Open `http://127.0.0.1:8000`. To produce the static site:

```bash
mkdocs build --strict
```

The output lands in `site/` (not committed). See [CI/CD](ci-cd.md) for how it is published.

## Wiki generation

The GitHub wiki is generated from the same `docs/` sources:

```powershell
pwsh ./scripts/Sync-Wiki.ps1 -OutputPath wiki-out
```

The script rewrites links for wiki page names and writes `_Sidebar.md` and `_Footer.md`.

## Common build problems

| Problem | Solution |
|---------|----------|
| `NETSDK1045` / SDK not found | Install the .NET 10 SDK; check `global.json` |
| Missing `mame.dat` at runtime | Rebuild; the file is copied automatically, or copy it manually |
| Analyzer warnings fail the build | Fix the warnings; do not suppress them without a good reason |
| WebView2 errors at runtime | Install the WebView2 Runtime |

## Related pages

- [Testing](testing.md)
- [CI/CD](ci-cd.md)
- [Contributing](contributing.md)
