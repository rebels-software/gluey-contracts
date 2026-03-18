# Technology Stack

**Analysis Date:** 2026-03-18

## Languages

**Primary:**
- C# 13 - All source code and projects

**Secondary:**
- None

## Runtime

**Environment:**
- .NET 9.0 and .NET 10.0 (multi-target)
- Language version: C# 13

**Package Manager:**
- NuGet - .NET package management
- Lockfile: Implicit (managed by NuGet's dependency resolution)

## Frameworks

**Core:**
- Microsoft.NET.Sdk - Base .NET library SDK
  - Implicit usings enabled
  - Nullable reference types enabled (strict null checking)

**Testing:**
- NUnit 4.3.1 - Test framework
- NUnit3TestAdapter 4.6.0 - Test runner adapter
- NUnit.Analyzers 4.5.0 - Static analysis for NUnit tests

**Build/Dev:**
- Microsoft.NET.Test.Sdk 17.12.0 - Test runner infrastructure
- BenchmarkDotNet 0.14.0 - Performance benchmarking framework

**Validation & Comparison:**
- JsonSchema.Net 9.1.2 - JSON Schema validation (used in benchmarks for comparison)

## Key Dependencies

**Critical:**
- FluentAssertions 8.0.1 - Readable assertion syntax in tests
- coverlet.collector 6.0.2 - Code coverage collection

**Infrastructure:**
- None (no external service SDKs or third-party integrations)

## Configuration

**Environment:**
- CI/CD detection: `CI` environment variable triggers `ContinuousIntegrationBuild`
- NuGet publishing: Uses `NUGET_API_KEY` secret from GitHub Actions
- Code coverage: Uses `CODE_COV_TOKEN` for codecov.io integration

**Build:**
- Solution file: `Gluey.Contract.sln`
- Project files (.csproj):
  - `src/Gluey.Contract/Gluey.Contract.csproj` - Core library
  - `src/Gluey.Contract.Json/Gluey.Contract.Json.csproj` - JSON parser driver
  - `tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj` - Core library tests
  - `tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj` - JSON tests
  - `benchmarks/Gluey.Contract.Benchmarks/Gluey.Contract.Benchmarks.csproj` - Performance benchmarks

## Platform Requirements

**Development:**
- .NET SDK 9.0 or later
- Windows, macOS, or Linux (cross-platform .NET support)
- Visual Studio, VS Code, or Rider recommended

**Production:**
- .NET 9.0 or .NET 10.0 runtime
- No OS constraints (managed .NET runtime)
- Distributed as NuGet packages:
  - `Gluey.Contract` (main library) - version 1.0.0
  - `Gluey.Contract.Json` (JSON driver) - version 1.0.0

## NuGet Package Configuration

**Package Metadata:**
- License: Apache 2.0
- Repository: GitHub (https://github.com/rebels-software/gluey-contracts)
- Icon: `assets/icon.png`
- README: Embedded README.md
- Source link: Embedded via `PublishRepositoryUrl` and `EmbedUntrackedSources`

**Visibility & Encapsulation:**
- InternalsVisibleTo declarations allow test and implementation assemblies to access internal types:
  - `Gluey.Contract` → visible to `Gluey.Contract.Tests`, `Gluey.Contract.Json`, `Gluey.Contract.Json.Tests`
  - `Gluey.Contract.Json` → visible to `Gluey.Contract.Json.Tests`

---

*Stack analysis: 2026-03-18*
