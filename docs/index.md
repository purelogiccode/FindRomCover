# FindRomCover

**Find and download missing cover art for your retro gaming ROM collection.**

[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64%20%7C%20ARM64-blue)](https://github.com/purelogiccode/FindRomCover/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://github.com/purelogiccode/FindRomCover/blob/master/LICENSE.txt)

FindRomCover is a Windows desktop application that scans your ROM folder, detects games that do not have a cover image yet, and helps you find and save the right artwork — from your own image collection, from Google or Bing image search, from the Google Custom Search API, or with the help of a vision-capable AI model.

![FindRomCover main window](assets/images/screenshot.png)

## Highlights

| Feature | Description |
|---------|-------------|
| Local similarity search | Matches ROM filenames against images already in your image folder using Jaccard, Jaro-Winkler, or Levenshtein similarity |
| Web image search | Embedded Google and Bing search in a WebView2 browser |
| Google Custom Search API | Programmatic image results as clickable thumbnails |
| Automatic matching | A file-system watcher renames newly saved images to the selected game and converts them to PNG |
| AI Vision Assist | OpenRouter, OpenAI, Anthropic, Gemini, GLM, local (Ollama/LM Studio), or custom endpoints pick the best cover |
| AI Batch Fill | Fills the entire missing-covers list automatically with confidence thresholds and query history |
| Themes | Light and dark base themes with 20+ accent colors |
| Detailed diagnostics | Built-in log viewer plus a rolling `app<yyyyMMdd>.log` file |

## How it works

1. Point FindRomCover at your **ROM folder** and your **image folder**.
2. It scans the ROM folder for files with the [supported extensions](configuration/extensions.md) and compares them against the cover images in the image folder.
3. Every ROM without a cover appears in the **Missing Covers** list.
4. Select a game and use one of the four search tabs to find a cover:
   - **Local Files** — similar filenames in your image folder
   - **Google Web** / **Bing Web** — embedded browser search
   - **Google API** — Google Custom Search results as thumbnails
5. Click an image to save it, or save it from the embedded browser — the watcher renames and converts it automatically.
6. Optionally enable [AI Vision Assist](ai/index.md) to let a vision model rank the candidates, verify images before saving, or fill the whole list in one batch.

## Documentation

| Section | What you will find |
|---------|--------------------|
| [Installation](getting-started/installation.md) | Requirements, download, first run |
| [Quick Start](getting-started/quick-start.md) | A five-minute walkthrough |
| [User Guide](user-guide/interface.md) | Every window, tab, and workflow explained |
| [AI Vision Assist](ai/index.md) | Providers, models, settings, batch fill, costs |
| [Configuration](configuration/index.md) | Themes, similarity, extensions, storage, logs |
| [Reference](reference/command-line.md) | Command line, shortcuts, file formats |
| [Development](development/architecture.md) | Architecture, building, testing, CI/CD |
| [Release Notes](release-notes.md) | What changed in every version |

## Requirements

- Windows 10 or later (x64 or ARM64)
- [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Microsoft Edge WebView2 Runtime (pre-installed on most Windows 10/11 systems)
- Optional: a Google API key for the Google Custom Search API, and an API key for the AI provider of your choice

See [Installation](getting-started/installation.md) for details.

## Support

- **Issues**: [github.com/purelogiccode/FindRomCover/issues](https://github.com/purelogiccode/FindRomCover/issues)
- **Discussions**: [github.com/purelogiccode/FindRomCover/discussions](https://github.com/purelogiccode/FindRomCover/discussions)
- **Donations**: [purelogiccode.com/donate](https://www.purelogiccode.com/donate)

---

Made with care by [Pure Logic Code](https://www.purelogiccode.com).
