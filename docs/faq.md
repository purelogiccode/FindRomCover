# Frequently Asked Questions

## General

### Is FindRomCover free?

Yes. FindRomCover is free software licensed under the GNU General Public License v3.0. See [License](license.md).

### Which platforms are supported?

Windows 10 and later, both x64 and ARM64. The application is built with WPF on .NET 10 and does not run on Linux or macOS.

### Does it download ROMs?

No. FindRomCover only works with ROM files you already have and only downloads cover images.

### Does it send my data anywhere?

Only when you use features that require it:

- the update check contacts GitHub;
- the Google Custom Search API is called only when you use that tab;
- AI providers are contacted only when AI Assist is enabled and used;
- anonymous, best-effort usage statistics are recorded at startup;
- error-level events may be sent to the bug-report API.

Filenames and image contents are never uploaded except to your configured AI provider when AI Assist runs.

### Can I use it offline?

Yes, with Local Files search and the embedded browser. Google API, cloud AI providers, and update checks require internet access. Local AI models (Ollama, LM Studio) work offline.

## Setup

### Do I need an API key?

Only for the **Google API** tab and for cloud **AI providers**. Local Files, Google Web, Bing Web, and local AI servers do not need any key.

### Where are my settings stored?

In `%LocalAppData%\FindRomCover\Settings.dat` (SQLite). See [Data Storage](configuration/data-storage.md).

### How do I move FindRomCover to a new PC?

Copy the application folder and the `%LocalAppData%\FindRomCover` folder. API keys may need to be re-entered because encryption is tied to the Windows user and machine.

## Usage

### Why is a game listed as missing when I have a cover?

The cover name must match the ROM base name (case-insensitive, any recognized image extension). Rename the image to match the ROM, or let the watcher rename it while the game is selected, then press **F5**.

### How do I stop a game from reappearing in the list?

Right-click it and choose **Remove Item from the list**. It returns after a full scan unless a cover exists or the ROM is deleted.

### Can I search with a different name?

Yes — edit the **Extra Query** field, or enable `Settings > Use MAME Descriptions` for arcade titles.

### How do I delete a ROM from disk?

Right-click the entry and choose **Delete corresponding ROM or ISO**. This is permanent and asks for confirmation first.

## AI Assist

### Is AI Assist required?

No. It is optional and disabled by default.

### How much does it cost?

With the default OpenRouter model, a pick costs a fraction of a cent — about $0.21 per 1,000 picks. See [Recommended Models](ai/recommended-models.md).

### Can I use a local model?

Yes. Run Ollama or LM Studio and select the **Local** provider. See [Providers & Models](ai/providers.md#local-ollama-lm-studio).

### Why does the AI sometimes return no answer?

Reasoning models can exhaust their output budget before writing an answer. FindRomCover now allows 4,096 output tokens and reports a clear error if that still happens; switching to a non-reasoning model usually solves it. See [AI Troubleshooting](ai/troubleshooting.md).

### Does AI ever delete or overwrite my files?

No. AI can only choose an image. Saving and renaming follow your settings and the normal overwrite confirmation.

### How do I stop it from asking about the same game again?

Keep **Skip missing covers already queried in previous sessions** enabled. Use **Clear query history** to start over. See [Query History](ai/query-history.md).

## Troubleshooting

### The embedded browser is blank.

Install the Microsoft Edge WebView2 Runtime when prompted, then restart the application.

### Google API says the key is invalid.

Verify the key in `Settings > API Settings...`, ensure the Custom Search JSON API is enabled in Google Cloud, and check your quota.

### Where are the logs?

In the application folder (`app.log`, `error.log`, `error_user.log`) and in the live log window under `Settings > Show/Hide Log Window`. See [Logs & Diagnostics](configuration/logs.md).

### How do I reset all settings?

Close FindRomCover and delete `%LocalAppData%\FindRomCover\Settings.dat`. Defaults are recreated on the next start. Stored API keys are removed too.

## Still stuck?

- [Troubleshooting](troubleshooting.md)
- [AI Troubleshooting](ai/troubleshooting.md)
- [GitHub Issues](https://github.com/purelogiccode/FindRomCover/issues)
- [GitHub Discussions](https://github.com/purelogiccode/FindRomCover/discussions)
