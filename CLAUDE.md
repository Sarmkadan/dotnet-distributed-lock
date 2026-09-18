# CLAUDE.md

Distributed lock library for .NET (`SarmKadan.DistributedLock`) with pluggable backends: in-memory, Redis, PostgreSQL, SQLite.

## Build

- `dotnet restore && dotnet build -c Release` (or `make build`)
- `./build.sh` - restore, build, test in one go
- `dotnet pack -c Release -o ./packages` (or `make pack`)
- Target framework: net10.0, `Nullable` + `ImplicitUsings` enabled, `LangVersion latest`
- Root csproj is the library; it excludes `src/Api`, `src/Integration`, `src/Formatters`, `tests`, `examples`, `benchmarks` from compilation
- `Lock` is aliased to `SarmKadan.DistributedLock.Models.Lock` (global using) to avoid clash with `System.Threading.Lock`

## Test

- `dotnet test -c Release` (or `make test`)
- Single test: `dotnet test --filter "FullyQualifiedName~LockServiceTests"`
- Stack: xUnit 2.9, FluentAssertions 8, Moq
- Test project: `tests/dotnet-distributed-lock.Tests/`
- Benchmarks: `dotnet run -c Release --project benchmarks/dotnet-distributed-lock.Benchmarks` (BenchmarkDotNet)
- Local Redis/Postgres for integration tests: `docker-compose up -d` (or `make run-docker-compose`)

## Lint / format

- `dotnet format` (or `make format`)
- `make lint` = build with `/p:TreatWarningsAsErrors=true`
- Style is driven by `.editorconfig`: 4-space indent, LF, max line 120, `I`-prefixed interfaces, PascalCase members

## Key directories

- `src/Core/` - library proper: `Models`, `Services` (`LockService`, `ILockService`, `FencingTokenService`, `DeadlockDetector`, `LockMonitor`, `LockRetryPolicy`), `Repository` (`ILockRepository`, `InMemoryLockRepository`), `Configuration` (`DistributedLockOptions`, `ServiceCollectionExtensions` - DI entry point), `Exceptions`, `Enums`, `Constants`
- `src/Backends/{Redis,PostgreSQL,SQLite}/` - `ILockRepository` implementations
- `src/Workers/` - hosted services: renewal, cleanup, health monitoring, metrics collection
- `src/Events/` - in-process lock event bus/publisher/subscriber
- `src/Caching/`, `src/Utilities/` - cache helpers, extensions, `ValidationHelper`, `RetryPolicyHelper`
- `src/Api/`, `src/Integration/`, `src/Formatters/` - controllers/middleware, webhooks/HTTP client, CSV/JSON/XML serializers (not compiled into the NuGet package)
- `examples/` - standalone usage samples
- `docs/` - `ARCHITECTURE.md`, `DEPLOYMENT.md`, `FAQ.md`, per-class notes
- `.github/workflows/` - CI (build + test on .NET 8/10), CodeQL, Docker, NuGet publish

## Conventions

- Root namespace `SarmKadan.DistributedLock`; sub-namespaces follow folder names (`.Models`, `.Services`, `.Repository`, `.Enums`)
- `ILockRepository` is the single seam between lock semantics and storage; keep everything above it backend-agnostic
- `TryAcquireAsync` = single non-blocking attempt returning a tuple; `AcquireAsync` = retry loop with backoff, throws `LockAcquisitionException`
- Fencing tokens guard renew/release; custom exceptions derive from `DistributedLockException`
- Files start with `#nullable enable` and an author header comment; public members carry XML doc comments (`GenerateDocumentationFile` is on)
- Extra behavior lives in `*Extensions.cs`, `*JsonExtensions.cs`, `*Validation.cs` partials/static classes next to the main type
- Tests: one class per type named `<Type>Tests`, mocks via `Moq`, assertions via FluentAssertions
- Commits: conventional prefixes (`docs:`, `chore:`, `feat:`, `fix:`)
