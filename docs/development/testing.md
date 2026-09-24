# Testing

FindRomCover ships with a comprehensive automated test suite. The suite runs on Windows and is part of continuous integration.

## Frameworks

| Framework | Role |
|-----------|------|
| **xUnit** | Test framework |
| **FluentAssertions** | Readable assertions |
| **Moq** | Mocking for service and HTTP tests |
| **coverlet** | Code coverage collection |

## Running the tests

```bash
dotnet test CSharp_FindRomCover.sln
```

Faster inner loop (build once, then run):

```bash
dotnet build CSharp_FindRomCover.sln
dotnet test CSharp_FindRomCover.sln --no-build
```

Run a single area:

```bash
dotnet test CSharp_FindRomCover.sln --filter "FullyQualifiedName~SettingsManager"
```

## Test layout

```text
FindRomCover.Tests/
├── ApiProvider/          # Google Custom Search parsing and error handling
├── Managers/             # SettingsManager (defaults, persistence, edge cases, properties)
├── Models/               # DTO behavior, equality, formatting
├── Services/             # Image, similarity, query helper, logging, updates, ...
│   └── Ai/               # AI clients, factory, catalog, caches, batch fill
├── AppConstantsTests.cs
└── TestBootstrap.cs
```

Coverage highlights:

- **AI subsystem** — each vision client (OpenAI-compatible, Anthropic, Gemini) is tested for request building, response parsing, empty-response diagnostics, and error mapping; the factory, model catalog, image preparer, verdict cache, query history, assist service, and batch fill service have dedicated suites.
- **Settings** — defaults, clamping, persistence round-trips, secret encryption, SQLite migration from the legacy encrypted format, and corrupt-file recovery.
- **Similarity** — all three algorithms, edge cases such as empty strings, and n-gram indexing.
- **Services** — image processing and saving, query cleaning, URL generation, update checks, logging, and error reporting.

The suite currently contains **888 tests**, all expected to pass.

## Test isolation

Settings tests never touch your real configuration. `SettingsManager` accepts an optional settings directory, and the test fixtures create a unique temporary directory per test class, so:

- tests run in parallel without interfering with each other;
- `%LocalAppData%\FindRomCover` is not modified by test runs;
- temporary directories are deleted after each test class finishes.

Tests that write corrupt or empty files operate inside those temporary directories.

## Conventions for new tests

- One test class per production type, named `<Type>Tests`; additional edge-case files use suffixes such as `EdgeCaseTests` or `AdditionalTests`.
- Use FluentAssertions for assertions.
- Keep tests deterministic: no real network calls, no dependence on user settings.
- Mock HTTP with `HttpMessageHandler` stubs where possible.
- Prefer theories for input matrices.
- Every bug fix should come with a regression test.

## Related pages

- [Building & Running](building.md)
- [CI/CD](ci-cd.md)
- [Contributing](contributing.md)
