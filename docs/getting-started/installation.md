# Installation

FindRomCover is distributed as a framework-dependent Windows application. This page explains what you need, how to install it, and where it keeps its data.

## Requirements

| Requirement | Details |
|-------------|---------|
| Operating system | Windows 10 or later (x64 or ARM64) |
| Runtime | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — choose **.NET Desktop Runtime** |
| Web search | Microsoft Edge WebView2 Runtime — required for the Google Web and Bing Web tabs; pre-installed on most Windows 10/11 systems |
| Google API (optional) | A Google Cloud API key with the **Custom Search JSON API** enabled |
| AI Vision Assist (optional) | An API key for your chosen provider, or a local OpenAI-compatible server such as Ollama or LM Studio |

> **Note:** The released builds are framework-dependent. The .NET Desktop Runtime must be installed separately. If it is missing, Windows will show a prompt with a download link when you start the application.

## Download and install

1. Open the [Releases](https://github.com/purelogiccode/FindRomCover/releases) page.
2. Download the archive for your architecture:
   - `release_x.y.z_win-x64.zip` for standard Intel/AMD PCs
   - `release_x.y.z_win-arm64.zip` for Windows on ARM devices
3. Extract the archive to a folder of your choice. Avoid extracting into `Program Files` unless you plan to run the application as administrator, because it writes logs next to the executable.
4. Run `FindRomCover.exe`.

No installer is required. To uninstall, delete the extracted folder and, optionally, the data folder described below.

## First run

On first start FindRomCover creates default settings and opens the main window. Recommended first steps:

1. Set your **ROM Folder** and **Image Folder** — the scan starts automatically as soon as both are set.
2. Open `Settings > Edit Supported Extensions...` and adjust the list if your collection uses unusual file extensions.
3. Pick a theme under the **Theme** menu if you prefer light mode.
4. If you want to use the Google Custom Search API, configure the key under `Settings > API Settings...` — see [Google Custom Search API](../user-guide/google-api.md).
5. If you want AI-assisted picking, configure a provider under `Settings > AI Settings...` — see [AI Vision Assist](../ai/index.md).

## Where FindRomCover stores data

| Path | Contents |
|------|----------|
| `%LocalAppData%\FindRomCover\Settings.dat` | All settings in a SQLite database, including encrypted API keys |
| `%LocalAppData%\FindRomCover\QueryHistory.dat` | SQLite database of covers already queried with AI Assist |
| `%LocalAppData%\FindRomCover\ai-cache.json` | Cached AI verdicts (30-day retention) |
| `%LocalAppData%\FindRomCover\ai-models.json` | Cached model lists per AI provider (7-day retention) |
| Application folder | `app.log`, `error.log`, `error_user.log`, `mame.dat`, `settings.dat.legacy` (if migrated) |

See [Data Storage](../configuration/data-storage.md) for retention rules and backup instructions.

## Updating

FindRomCover checks GitHub for a newer release at startup and offers to open the download page. To update manually:

1. Download the latest archive from [Releases](https://github.com/purelogiccode/FindRomCover/releases).
2. Close FindRomCover.
3. Extract the new archive over the existing folder, keeping your data folder untouched.
4. Start FindRomCover again. Settings are preserved because they live in `%LocalAppData%\FindRomCover`.

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
| "You must install .NET Desktop Runtime" | Install the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and start the app again |
| Windows SmartScreen warning | The builds are unsigned; choose **More info** and **Run anyway** if you trust the download |
| WebView2 runtime missing | Follow the in-app prompt to download the runtime from Microsoft, then restart FindRomCover |
| Application closes immediately | Check `error.log` in the application folder, and see [Troubleshooting](../troubleshooting.md) |

## Next steps

- [Quick Start](quick-start.md) — find your first cover in five minutes
- [Interface Overview](../user-guide/interface.md) — learn the main window
