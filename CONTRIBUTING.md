# Contributing to dotnet-distributed-lock

Thank you for considering contributing to dotnet-distributed-lock!

## Development Requirements

- **.NET SDK 10.0** or later — download from [dot.net](https://dotnet.microsoft.com/download)
- A supported IDE: Visual Studio 2022+, Rider, or VS Code with the C# Dev Kit extension
- Docker (optional) — needed for Redis/PostgreSQL integration tests

## Building Locally

```bash
# Clone your fork
git clone https://github.com/<your-username>/dotnet-distributed-lock.git
cd dotnet-distributed-lock

# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build --configuration Release

# Build in Debug mode (default)
dotnet build
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run tests and produce a TRX report
dotnet test --verbosity normal --logger "trx;LogFileName=test-results.trx"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~LockServiceTests"
```

## Packing the NuGet Package

```bash
dotnet pack --configuration Release --output ./nupkg
```

## Code Style

This project uses `.editorconfig` for consistent formatting. Most IDEs will pick it up automatically. Key conventions:

- **Indentation**: 4 spaces for C# files, 2 spaces for JSON/YAML/XML
- **Naming**: PascalCase for types and public members; interfaces prefixed with `I`
- **Braces**: always use braces for control flow blocks
- **Nullable**: nullable reference types are enabled — handle nulls explicitly
- **XML docs**: required for all public API surface

Run the formatter to ensure compliance before committing:

```bash
dotnet format
```

## How to Contribute

### 1. Fork and Clone

Fork the repository on GitHub, then clone your fork:

```bash
git clone https://github.com/<your-username>/dotnet-distributed-lock.git
```

### 2. Create a Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/your-bug-fix
```

### 3. Make Changes

- Keep changes focused — one logical change per pull request.
- Add or update tests to cover your changes.
- Include XML doc comments on any new public API.
- Run `dotnet format` before committing.

### 4. Run Tests

Ensure all tests pass:

```bash
dotnet test --configuration Release
```

### 5. Submit a Pull Request

- Push your branch: `git push origin your-branch-name`
- Open a Pull Request against the `main` branch.
- Fill in the PR template, describing what changed and why.
- Link any related issues with `Fixes #<issue-number>`.

## Reporting Issues

Use GitHub Issues to report bugs or request features.

When reporting a bug, include:
- A clear, descriptive title
- Steps to reproduce
- Expected vs. actual behaviour
- Library version and .NET runtime version

## License

By contributing you agree that your contributions will be licensed under the MIT License.
