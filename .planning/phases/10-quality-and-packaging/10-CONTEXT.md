# Phase 10: Quality and Packaging - Context

**Gathered:** 2026-03-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Prove zero-allocation guarantees via BenchmarkDotNet benchmarks, enforce them with GC.GetAllocatedBytesForCurrentThread regression tests, and configure both NuGet packages (Gluey.Contract + Gluey.Contract.Json) with full metadata for publishing. Covers QUAL-01, QUAL-02, QUAL-03.

</domain>

<decisions>
## Implementation Decisions

### Benchmark Scenarios
- Four scenarios: simple flat object (~5-10 props), nested objects (2-3 levels), array-heavy payload (100 items), full schema validation (composition + conditionals + patterns)
- Three payload sizes per scenario: small (~100B), medium (~5KB), large (~50KB)
- Include System.Text.Json deserialization as comparison baseline for each scenario
- Include validation-only path (ReadOnlySpan<byte>, no OffsetTable) alongside full TryParse(byte[]) path
- MemoryDiagnoser enabled on all benchmarks to report heap allocations

### Allocation Regression Tests
- Live in existing test projects (Gluey.Contract.Json.Tests) alongside functional tests
- Strict zero tolerance: assert exactly 0 bytes allocated via GC.GetAllocatedBytesForCurrentThread
- Format assertion (opt-in via SchemaOptions.AssertFormat) gets separate tests that are allowed to allocate
- Four coverage areas:
  1. TryParse(byte[]) full path (parse + validate + index)
  2. TryParse(ReadOnlySpan<byte>) validate-only path
  3. Property access after parse (result["name"].GetString(), chained indexers)
  4. Dispose/cleanup path (buffer return to ArrayPool)
- Run in CI as normal NUnit tests — catches regressions on every PR

### NuGet Package Metadata
- License: Apache 2.0 (matches existing NOTICE file convention)
- Version: 1.0.0-preview.1 (SemVer pre-release, iterate to preview.2+ before stable 1.0.0)
- Authors: Gluey Contributors
- Deterministic/reproducible builds: enabled (ContinuousIntegrationBuild, EmbedUntrackedSources, SourceLink)
- Both packages need: PackageId, Description, PackageLicenseExpression, RepositoryUrl, PackageTags, RepositoryType
- Gluey.Contract description: zero-allocation, single-pass validation and indexing of raw bytes
- Gluey.Contract.Json description: JSON Schema Draft 2020-12 driver for Gluey.Contract

### Benchmark Project Setup
- New console project: benchmarks/Gluey.Contract.Benchmarks/
- Added to solution under a "benchmarks" solution folder
- References Gluey.Contract.Json (and transitively Gluey.Contract)
- Local-only execution (not in CI) — BenchmarkDotNet runs are slow and noisy
- Benchmark results committed to docs/benchmarks/ as evidence of zero-alloc claim

### Claude's Discretion
- Exact BenchmarkDotNet configuration (job, runtime, columns)
- JSON payload content for each scenario/size combination
- Schema definitions for each benchmark scenario
- SourceLink package version and configuration details
- Solution folder GUID and project GUID for benchmark project
- Whether to use Directory.Build.props for shared NuGet metadata

</decisions>

<specifics>
## Specific Ideas

- Benchmarks serve as proof of the zero-allocation claim — results committed to repo for credibility
- System.Text.Json comparison shows the performance advantage of zero-alloc indexing vs full deserialization
- Allocation regression tests are the CI safety net — benchmarks prove it once, regression tests enforce it forever

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `JsonContractSchema.TryParse(byte[])` / `TryParse(ReadOnlySpan<byte>)`: the two parse entry points to benchmark
- `ParseResult`: wraps OffsetTable + ErrorCollector + indexers — disposal path to test
- `SchemaOptions.AssertFormat`: opt-in format assertion flag — separate allocation budget
- `assets/icon.png`: already exists for PackageIcon
- Both README.md files already configured for pack (PackageReadmeFile)

### Established Patterns
- NUnit 4.3.1 + FluentAssertions 8.0.1 test stack
- Test projects reference src projects directly
- InternalsVisibleTo configured for test assemblies
- .NET 9.0, C# 13, nullable enabled across all projects

### Integration Points
- Both .csproj files need metadata additions (PackageId, Description, License, etc.)
- Solution file needs benchmark project entry under new solution folder
- Benchmark project references Gluey.Contract.Json.csproj

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 10-quality-and-packaging*
*Context gathered: 2026-03-10*
