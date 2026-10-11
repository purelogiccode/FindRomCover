# Testing

FindRomCover ships with a comprehensive automated test suite. The test project targets `net10.0-windows` and is part of continuous integration on Windows.

## Frameworks

| Framework | Role |
|-----------|------|
| **xUnit** | Test framework |
| **FluentAssertions** | Readable assertions |
| **Moq** | Mocking for service and HTTP tests |
| **coverlet** | Code coverage collection |

The suite uses xUnit `2.9.3`, FluentAssertions `8.11.0`, Moq `4.21.0`, and `coverlet.collector` for coverage.

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
├── TestBootstrap.cs      # Temp settings dir and API seam applied at module load
└── xunit.runner.json     # Disables collection parallelization
```

Coverage highlights:

- **AI subsystem** — each vision client (OpenAI-compatible, Anthropic, Gemini) is tested for request building, response parsing, empty-response diagnostics, and error mapping; the factory, model catalog, image preparer, verdict cache, query history, assist service, and batch fill service have dedicated suites.
- **Settings** — defaults, clamping, persistence round-trips, secret encryption, SQLite migration from the legacy encrypted format, and corrupt-file recovery.
- **Similarity** — all three algorithms, edge cases such as empty strings, and n-gram indexing.
- **Services** — image processing and saving, screenshot saving, query cleaning, URL generation, update checks, logging, and bug-report suppression.

The suite currently contains **879 test cases** (685 `[Fact]` plus 194 `[InlineData]` theory cases) across 68 files, all expected to pass.

## Test isolation

Settings tests never touch your real configuration. `TestBootstrap` runs as a `[ModuleInitializer]` and sets `SettingsManager.DefaultSettingsDirectoryOverride` to a unique temporary directory, so:

- `%LocalAppData%\FindRomCover` is not modified by test runs;
- every run uses its own unique temporary directory under the system temp path;
- `ErrorLogger.SendToApiOverride` replaces the real bug-report API call, so tests never contact the production endpoint.

Tests that write corrupt or empty files operate inside those temporary directories.

## Parallelization

`xunit.runner.json` sets `parallelizeTestCollections` to `false` and is copied to the output directory, so test collections run sequentially. New tests should not depend on collection-level parallelism; keep shared static state isolated as the existing fixtures do.

## Conventions for new tests

- One test class per production type, named `<Type>Tests`; additional edge-case files use suffixes such as `EdgeCaseTests` or `AdditionalTests`.
- Use FluentAssertions for assertions.
- Keep tests deterministic and independent of collection parallelization: no real network calls, no dependence on user settings or execution order.
- Mock HTTP with `HttpMessageHandler` stubs where possible.
- Prefer theories for input matrices.
- Every bug fix should come with a regression test.
- Production changes should follow the same conventions as the app: `Async` suffix on asynchronous methods and XML documentation on public and internal members.

## Related pages

- [Building & Running](building.md)
- [CI/CD](ci-cd.md)
- [Contributing](contributing.md)
