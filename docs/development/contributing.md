# Contributing

Thanks for considering a contribution to FindRomCover. This page explains how to report issues, propose changes, and get your work merged.

## Ways to contribute

- **Report bugs** with clear reproduction steps and log excerpts.
- **Suggest features** through GitHub Issues or Discussions.
- **Improve documentation** — every page is a Markdown file in `docs/`.
- **Submit code** for fixes, performance, or new features.

## Reporting issues

Include:

1. FindRomCover version (visible in `About`).
2. Windows version and architecture (x64/ARM64).
3. Steps to reproduce.
4. Expected and actual behavior.
5. Relevant log entries (`app<yyyyMMdd>.log` in `%LocalAppData%\FindRomCover` or the log window). Remove anything you do not want to share.
6. Whether AI Assist or the Google API was involved.

## Development workflow

1. Fork the repository and create a branch:

   ```bash
   git checkout -b fix/short-description
   ```

2. Build and test:

   ```bash
   dotnet build CSharp_FindRomCover.sln
   dotnet test CSharp_FindRomCover.sln
   ```

3. Keep changes focused: one logical change per pull request.
4. Add or update tests for behavior changes.
5. Update documentation when user-visible behavior changes.
6. Open a pull request against `master` with a clear description.

## Code style

- Follow `.editorconfig` and the existing formatting.
- Keep the build **warning-free**; the projects enable Meziantou, Roslynator, and Microsoft.CodeAnalysis analyzers.
- One top-level type per file.
- Prefer small, focused methods and clear names over comments.
- Use nullable reference types correctly; avoid suppressions.
- Never log secrets or personal data.

## Commit messages

Use short, descriptive messages in the imperative mood, optionally with a conventional prefix:

```text
fix: handle missing WebView2 runtime on startup
feat(ai): add Gemini provider
docs: document batch fill outcomes
```

## Pull request checklist

- [ ] Build is warning-free.
- [ ] All tests pass.
- [ ] New behavior is covered by tests.
- [ ] User-facing changes are reflected in `docs/` and, where relevant, `README.md` and `WhatsNew.md`.
- [ ] No secrets, tokens, or personal paths are committed.

## Documentation contributions

The docs are built with MkDocs Material from `docs/` and mirrored to the GitHub wiki by `scripts/Sync-Wiki.ps1`.

Preview locally:

```bash
pip install -r docs/requirements.txt
mkdocs serve
```

Guidelines:

- Use plain Markdown that renders both on the site and in the wiki (tables, lists, code fences, blockquotes).
- Link between pages with relative `.md` links; the wiki script rewrites them.
- Keep each page focused; add it to the `nav` in `mkdocs.yml` and to the sidebar structure in the sync script.
- Run `mkdocs build --strict` before submitting.

## License

By contributing, you agree that your contributions are licensed under the **GNU General Public License v3.0**, the same license as the project. See [License](../license.md).

## Related pages

- [Building & Running](building.md)
- [Testing](testing.md)
- [CI/CD](ci-cd.md)
