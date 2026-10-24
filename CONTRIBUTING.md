# Contributing

Bug reports, documentation fixes and pull requests are welcome.

## Prerequisites

- **.NET SDK 10.0** (`net10.0` target). There is no `global.json`; any 10.0.x SDK works. An 8.0 SDK cannot build these projects.
- No database or cache server is needed to build or test. The SQLite tests use a temp file, and the Redis and PostgreSQL repository tests do not connect to a server.

## Build

The repository root contains both a `.csproj` and a `.sln`, so pass the solution explicitly. A bare `dotnet build` fails with `MSB1011`.

```bash
dotnet restore dotnet-distributed-lock.sln
dotnet build dotnet-distributed-lock.sln -c Release
```

Or run `./build.sh`, which restores, builds and tests.

## Test

```bash
dotnet test dotnet-distributed-lock.sln -c Release

# Single class
dotnet test dotnet-distributed-lock.sln -c Release --filter "FullyQualifiedName~LockServiceTests"
```

Tests live in `tests/dotnet-distributed-lock.Tests/`, with one `<Type>Tests` class per type. They use xUnit, FluentAssertions and Moq.

## Coding conventions

- **`.editorconfig` is authoritative**: 4-space indentation, LF line endings, 120-character maximum line length.
- **Nullable reference types** are enabled. Source files start with `#nullable enable` (nearly all do).
- **File-scoped namespaces** (`namespace SarmKadan.DistributedLock.Services;`) are used throughout `src/`.
- **Async methods** that return `Task` or `ValueTask` end in `Async`.
- **Public members** have XML documentation comments (`GenerateDocumentationFile` is on).
- **Warnings**: new code should not add warnings. Check with `dotnet build dotnet-distributed-lock.sln -c Release /p:TreatWarningsAsErrors=true`.
- **Backend-agnostic core**: keep everything above `ILockRepository` free of storage-specific code.
- Run `dotnet format dotnet-distributed-lock.sln` before committing.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/) with a lowercase type and an optional scope, for example:

```
feat(events): add ArgumentNullException guards to lock event subscribers
fix(services): guard FencingTokenService public methods against null arguments
```

Common types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Write the subject in the imperative mood.

## Pull requests

1. Fork the repository (external contributors) and create a branch from `main`, named `feature/<short-name>` or `fix/<short-name>`.
2. Keep each PR to one logical change.
3. Add or update tests for any behavior change. Bug fixes should include a regression test.
4. Make sure CI is green. The `ci.yml` workflow runs restore, build and test on .NET 10 for pushes and pull requests to `main`.
5. Fill in the [pull request template](.github/PULL_REQUEST_TEMPLATE.md).

## Reporting issues

Include your .NET version, OS, reproduction steps, expected and actual behavior, and relevant logs. For security issues, follow [SECURITY.md](SECURITY.md) instead of opening a public issue.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
