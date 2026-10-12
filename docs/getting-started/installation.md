# Installation

FindRomCover is distributed as a framework-dependent cross-platform desktop application. This page explains what you need, how to install it, and where it keeps its data.

## Requirements

| Platform | Requirement | Details |
|----------|-------------|---------|
| **Windows 10 or later** | .NET runtime | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — choose **.NET Desktop Runtime** |
| **Windows 10 or later** | Embedded web search | Microsoft Edge WebView2 Runtime — pre-installed on most Windows 10/11 systems; if missing, the app offers the download page and falls back to the system browser |
| **Linux** | .NET runtime | [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Linux** | Embedded web search | WebKitGTK; if it is missing, the app offers the download page and falls back to the system browser |
| **macOS 11 or later** | .NET runtime | [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **macOS 11 or later** | Embedded web search | WKWebView, which is part of macOS |
| All platforms | Google API (optional) | A Google Cloud API key with the **Custom Search JSON API** enabled |
| All platforms | AI Vision Assist (optional) | An API key for your chosen provider, or a local OpenAI-compatible server such as Ollama or LM Studio |

!!! note
    The released builds are framework-dependent. The .NET runtime must be installed separately. If it is missing, the application will not start; install the runtime for your platform and start FindRomCover again.

## Download and install

1. Open the [Releases](https://github.com/purelogiccode/FindRomCover/releases) page.
2. Download the archive for your platform and architecture:

    | File | Platform |
    |------|----------|
    | `release_<version>_win-x64.zip` | Windows x64 |
    | `release_<version>_win-arm64.zip` | Windows ARM64 |
    | `release_<version>_linux-x64.zip` | Linux x64 |
    | `release_<version>_linux-arm64.zip` | Linux ARM64 |
    | `release_<version>_osx-x64.zip` | macOS Intel |
    | `release_<version>_osx-arm64.zip` | macOS Apple Silicon |

    Each archive also contains `LICENSE.txt`, `README.md`, and `WhatsNew.md`.
3. Extract the archive to a folder of your choice. Settings, caches, and logs are written to your per-user data folder, so the application folder does not need to be writable. If you install under a read-only location such as `Program Files`, screenshots automatically fall back to the data folder.
4. Start the application:
   - Windows: run `FindRomCover.exe`.
   - Linux and macOS: make the binary executable once (`chmod +x FindRomCover`) if needed, then run the `FindRomCover` executable from the extracted folder.

No installer is required. To uninstall, delete the extracted folder and, optionally, the data folder described below.

## First run

On first start FindRomCover creates default settings and opens the main window. Recommended first steps:

1. Set your **ROM Folder** and **Image Folder** using the **Browse...** buttons. The scan starts automatically as soon as both paths are valid.
2. Open `Settings > Edit Supported Extensions...` and adjust the list if your collection uses unusual file extensions.
3. Pick a theme under `Theme > Base Theme` if you prefer light mode.
4. If you want to use the Google Custom Search API, configure the key under `Settings > API Settings...` — see [Google Custom Search API](../user-guide/google-api.md).
5. If you want AI-assisted picking, configure a provider under `Settings > AI Settings...` — see [AI Vision Assist](../ai/index.md).

!!! note "Folder persistence"
    The image folder is remembered as the last-used folder when you browse for it or when the window closes, but it is **not** restored into the interface on the next start. The ROM folder is not persisted at all. Pass both folders on the command line to pre-fill the boxes and start a scan — see [Command-Line Arguments](../reference/command-line.md).

## Where FindRomCover stores data

The per-user data folder depends on the platform:

| Platform | Data folder |
|----------|-------------|
| Windows | `%LocalAppData%\FindRomCover` (typically `C:\Users\<you>\AppData\Local\FindRomCover`) |
| Linux | `~/.local/share/FindRomCover` |
| macOS | `~/Library/Application Support/FindRomCover` |

| File | Contents |
|------|----------|
| `Settings.dat` | All settings in a SQLite database, including encrypted API keys |
| `QueryHistory.dat` | SQLite database of covers already queried with AI Assist |
| `ai-cache.json` | Cached AI verdicts (30-day retention) |
| `ai-models.json` | Cached model lists per AI provider (7-day retention) |
| `app<yyyyMMdd>.log` | Rolling daily application log (7 files kept) |
| `ApiLogError.txt` | Error entries sent to the automatic bug-report API |
| `Screenshot/` | Screenshots, when the application folder is not writable (see [Screenshots](../user-guide/screenshots.md)) |

The application folder (next to the executable) only holds `mame.dat` and, after a migration or quarantine, `settings.dat.legacy` / `settings.dat.corrupt`.

See [Data Storage](../configuration/data-storage.md) for retention rules and backup instructions, and [Logs & Diagnostics](../configuration/logs.md) for the full list of diagnostic files.

## Updating

FindRomCover checks GitHub for a newer release at startup and offers to open the release page. To update manually:

1. Download the latest archive for your platform from [Releases](https://github.com/purelogiccode/FindRomCover/releases).
2. Close FindRomCover.
3. Extract the new archive over the existing folder, keeping your data folder untouched.
4. Start FindRomCover again. Settings are preserved because they live in the per-user data folder.

## Building from source

If you prefer to build the application yourself, see [Building & Running](../development/building.md). The short version:

```bash
git clone https://github.com/purelogiccode/FindRomCover.git
cd FindRomCover
dotnet build
```

## Troubleshooting installation issues

| Symptom | Solution |
|---------|----------|
| ".NET runtime not found" on Windows | Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and start the app again |
| ".NET runtime not found" on Linux or macOS | Install the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and start the app again |
| Windows SmartScreen warning | The builds are unsigned; choose **More info** and **Run anyway** if you trust the download |
| "The embedded WebView2 Runtime is not available" (Windows) | Follow the in-app prompt to open the Microsoft download page, install the runtime, then restart FindRomCover. Until then, web searches open in your system browser |
| "The embedded WebKitGTK is not available" (Linux) | Install WebKitGTK from your distribution, or let the app open the WebKitGTK download page. Until then, web searches open in your system browser |
| Embedded web view unavailable on macOS | The alert names WKWebView and offers the Safari support page; web searches open in your system browser |
| Application closes immediately | Check the rolling log in the per-user data folder, and see [Troubleshooting](../troubleshooting.md) |

## Related pages

- [Quick Start](quick-start.md)
- [Interface Overview](../user-guide/interface.md)
- [Screenshots](../user-guide/screenshots.md)
- [Data Storage](../configuration/data-storage.md)
- [Troubleshooting](../troubleshooting.md)
