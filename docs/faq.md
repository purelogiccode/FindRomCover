# Frequently Asked Questions

## General

### Is FindRomCover free?

Yes. FindRomCover is free software licensed under the GNU General Public License v3.0. See [License](license.md).

### Which platforms are supported?

Windows 10 and later, Linux, and macOS. Version 3.3.1 is built with Avalonia 12.1.4 on .NET 10, so the same application runs on all three platforms. The embedded browser uses the native web view of each platform: WebView2 on Windows, WKWebView on macOS, and WebKitGTK on Linux.

### Does it download ROMs?

No. FindRomCover only works with ROM files you already have and only downloads cover images.

### Does it send my data anywhere?

Only through the requests below:

- an anonymous startup statistics hit with the application id and version is sent at every launch;
- the update check contacts the public GitHub Releases API;
- the Google Custom Search API is called only when you use that tab;
- AI providers are contacted only when AI Assist is enabled and used, and then the candidate images are sent to your configured provider;
- automatic bug reports are sent for Warning-level events and above and contain environment details and log messages, which can include local file paths and URLs.

Images are never uploaded except to your configured AI provider when AI Assist runs. See [Privacy](configuration/privacy.md) for the full list.

### Can I use it offline?

Yes, with Local Files search and the embedded browser. Google API, cloud AI providers, and the update check require internet access and simply fail quietly when offline. Local AI models (Ollama, LM Studio) work offline. The startup statistics and automatic bug reports are also skipped while offline.

## Setup

### Do I need an API key?

Only for the **Google API** tab and for cloud **AI providers**. Local Files, Google Web, Bing Web, and local AI servers do not need any key.

### Where are my settings stored?

In `Settings.dat` (SQLite) inside the per-user data folder:

| Platform | Folder |
|----------|--------|
| Windows | `%LocalAppData%\FindRomCover` |
| Linux | `~/.local/share/FindRomCover` |
| macOS | `~/Library/Application Support/FindRomCover` |

See [Data Storage](configuration/data-storage.md).

### How do I move FindRomCover to a new PC?

Copy the application folder and the per-user data folder. The settings encryption key is derived from the machine name, so copying `Settings.dat` to another user account on the same machine still decrypts your API keys, but on a different machine the secrets cannot be decrypted and must be entered again.

## Usage

### Why is a game listed as missing when I have a cover?

The cover name must match the ROM base name and use a recognized image extension. On Windows the check is case-insensitive; on Linux and macOS the file system is case-sensitive, so `sonic (usa).png` does not cover `Sonic (USA).zip`. Rename the image to match the ROM, or let the watcher rename it while the game is selected, then press **F5**.

### How do I stop a game from reappearing in the list?

Right-click it and choose **Remove Item from the list**. It returns after a full scan unless a cover exists or the ROM is deleted.

### Can I search with a different name?

Yes — edit the **Extra Query** field, or enable `Settings > Use MAME Descriptions` for arcade titles.

### How do I delete a ROM from disk?

Right-click the entry and choose **Delete corresponding ROM or ISO**. This is permanent and asks for confirmation first.

### How do I take a screenshot?

Press **F8**. The screenshot captures the **main window** (not a dialog or the embedded browser) and is saved as `Screenshot_yyyyMMdd_HHmmss_fff.png` in the `Screenshot` folder next to the executable, falling back to the `Screenshot` folder in the per-user data folder when the application folder is not writable. The status bar shows the saved path. See [Screenshots](user-guide/screenshots.md).

### What happens when I minimize the window?

Minimizing hides the window to the system tray. Click the tray icon or choose **Show FindRomCover** to bring it back, and use **Exit** to quit. Some Linux desktop environments do not display tray icons; in that case use the taskbar or close the window to exit.

## AI Assist

### Is AI Assist required?

No. It is optional and disabled by default.

### How much does it cost?

With the default OpenRouter model, a pick costs a fraction of a cent — about $0.21 per 1,000 picks. See [Recommended Models](ai/recommended-models.md).

### Can I use a local model?

Yes. Run Ollama or LM Studio and select the **Local** provider. See [Providers & Models](ai/providers.md#local-ollama-lm-studio).

### Why does the AI sometimes return no answer?

Reasoning models can exhaust their output budget before writing an answer. FindRomCover allows 4,096 output tokens by default and reports a clear error if that still happens; switching to a non-reasoning model usually solves it, or you can raise the budget with the custom request parameters. See [AI Troubleshooting](ai/troubleshooting.md).

### Does AI ever delete or overwrite my files?

No. AI can only choose an image. Saving and renaming follow your settings and the normal overwrite confirmation.

### How do I stop it from asking about the same game again?

Keep **Skip missing covers already queried in previous sessions** enabled. Use **Clear query history** to start over. See [Query History](ai/query-history.md).

## Troubleshooting

### The embedded browser is blank.

Install the web view runtime for your platform — Microsoft Edge WebView2 on Windows, WebKitGTK on Linux (WKWebView is part of macOS) — then restart the application. If the runtime is unavailable, FindRomCover explains the problem and can open the runtime download page or the system browser instead.

### Google API says the key is invalid.

Verify the key in `Settings > API Settings...`, ensure the Custom Search JSON API is enabled in Google Cloud, and check your quota.

### Where are the logs?

In the per-user data folder (`app<yyyyMMdd>.log`, `ApiLogError.txt`, `UserLogError.txt`, `InternalLog.txt`) and in the live log window under `Settings > Show/Hide Log Window`. See [Logs & Diagnostics](configuration/logs.md).

### How do I reset all settings?

Close FindRomCover and delete `Settings.dat` from the per-user data folder. Defaults are recreated on the next start. Stored API keys are removed too.

## Related pages

- [Troubleshooting](troubleshooting.md)
- [AI Troubleshooting](ai/troubleshooting.md)
- [Bug Reports](configuration/bug-reports.md)
- [Privacy](configuration/privacy.md)
- [GitHub Issues](https://github.com/purelogiccode/FindRomCover/issues)
- [GitHub Discussions](https://github.com/purelogiccode/FindRomCover/discussions)
