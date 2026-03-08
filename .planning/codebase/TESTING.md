# Testing

## Framework

| Component | Tool |
|-----------|------|
| Test framework | NUnit 4.3.1 |
| Assertions | FluentAssertions 8.0.1 |
| Test SDK | Microsoft.NET.Test.Sdk 17.12.0 |
| Coverage | coverlet.collector 6.0.2 |
| Analyzers | NUnit.Analyzers 4.5.0 |

## Test Projects

| Project | Tests | Scope |
|---------|-------|-------|
| `tests/Gluey.Contract.Tests/` | `Gluey.Contract.Tests.csproj` | Core library |
| `tests/Gluey.Contract.Json.Tests/` | `Gluey.Contract.Json.Tests.csproj` | JSON format driver |

## Current State

**No test files exist yet** — only `GlobalUsings.cs` with:
```csharp
global using NUnit.Framework;
global using FluentAssertions;
```

## Test Conventions (from project setup)

- Test project mirrors source project: `Gluey.Contract` -> `Gluey.Contract.Tests`
- `InternalsVisibleTo` grants test projects access to internal members
- FluentAssertions preferred for readable assertions
- Codecov integration for coverage reporting (badge in README)

## Benchmarks

- `benchmarks/` directory exists but is empty
- Performance testing will be critical given zero-allocation invariant
- BenchmarkDotNet expected (common .NET choice) but not yet added

## Running Tests

```bash
dotnet test Gluey.Contract.sln
```
