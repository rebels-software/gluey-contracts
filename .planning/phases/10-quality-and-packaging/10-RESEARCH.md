# Phase 10: Quality and Packaging - Research

**Researched:** 2026-03-10
**Domain:** BenchmarkDotNet, allocation regression testing, NuGet packaging
**Confidence:** HIGH

## Summary

Phase 10 covers three distinct workstreams: (1) BenchmarkDotNet benchmarks proving zero-allocation on the parse path, (2) NUnit allocation regression tests using `GC.GetAllocatedBytesForCurrentThread` that run in CI, and (3) NuGet package metadata configuration for both Gluey.Contract and Gluey.Contract.Json. All three are well-understood .NET patterns with mature tooling.

The project targets .NET 9 with C# 13. BenchmarkDotNet 0.15.8 is the current stable release and fully supports .NET 9. Source Link is built into the .NET 8+ SDK, so no extra NuGet package is needed for deterministic/reproducible builds. The existing csproj files already have partial NuGet metadata (PackageReadmeFile, PackageIcon) that just needs extending.

**Primary recommendation:** Split into three plans: (1) benchmark project setup + benchmark classes, (2) allocation regression tests in existing test project, (3) NuGet metadata additions to both csproj files.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Four benchmark scenarios: simple flat object, nested objects, array-heavy payload, full schema validation
- Three payload sizes per scenario: small (~100B), medium (~5KB), large (~50KB)
- Include System.Text.Json deserialization as comparison baseline
- Include validation-only path (ReadOnlySpan<byte>) alongside full TryParse(byte[]) path
- MemoryDiagnoser enabled on all benchmarks
- Allocation regression tests live in Gluey.Contract.Json.Tests alongside functional tests
- Strict zero tolerance: assert exactly 0 bytes via GC.GetAllocatedBytesForCurrentThread
- Format assertion (SchemaOptions.AssertFormat) gets separate tests allowed to allocate
- Four coverage areas: TryParse(byte[]) full path, TryParse(ReadOnlySpan<byte>) validate-only, property access after parse, Dispose/cleanup path
- Run in CI as normal NUnit tests
- License: Apache 2.0
- Version: 1.0.0-preview.1
- Authors: Gluey Contributors
- Deterministic/reproducible builds enabled (ContinuousIntegrationBuild, EmbedUntrackedSources, SourceLink)
- Both packages need: PackageId, Description, PackageLicenseExpression, RepositoryUrl, PackageTags, RepositoryType
- Benchmark project: benchmarks/Gluey.Contract.Benchmarks/ (new console project)
- Added to solution under "benchmarks" solution folder
- Local-only execution (not in CI)
- Benchmark results committed to docs/benchmarks/ as evidence

### Claude's Discretion
- Exact BenchmarkDotNet configuration (job, runtime, columns)
- JSON payload content for each scenario/size combination
- Schema definitions for each benchmark scenario
- SourceLink package version and configuration details
- Solution folder GUID and project GUID for benchmark project
- Whether to use Directory.Build.props for shared NuGet metadata

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| QUAL-01 | BenchmarkDotNet suite proving zero heap allocation on parse path | BenchmarkDotNet 0.15.8 with MemoryDiagnoser; Job.Default on .NET 9; four scenarios x three sizes; BytesAllocatedPerOperation == 0 verification |
| QUAL-02 | Allocation regression tests using GC.GetAllocatedBytesForCurrentThread | GC.GetAllocatedBytesForCurrentThread delta pattern in NUnit tests; warm-up call required; four coverage areas with zero-tolerance assertions |
| QUAL-03 | NuGet packages configured and ready for publishing | csproj metadata additions; SourceLink built into .NET 9 SDK; ContinuousIntegrationBuild conditional on CI; IsPackable=true (default) |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| BenchmarkDotNet | 0.15.8 | Performance benchmarks with MemoryDiagnoser | Official .NET Foundation benchmark library; only tool that reliably reports per-operation heap allocations |
| NUnit | 4.3.1 | Allocation regression test runner | Already used in project; allocation tests are plain NUnit tests |
| FluentAssertions | 8.0.1 | Test assertions | Already used in project |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.NET.Test.Sdk | 17.12.0 | Test host | Already referenced in test projects |

### Not Needed (Built-in)
| Feature | Provided By | Notes |
|---------|------------|-------|
| Source Link | .NET 9 SDK | Built into SDK since .NET 8; no Microsoft.SourceLink.GitHub package needed |
| Deterministic builds | .NET SDK | `<Deterministic>true</Deterministic>` is default in SDK-style projects |

**Installation (benchmark project only):**
```bash
dotnet add benchmarks/Gluey.Contract.Benchmarks/Gluey.Contract.Benchmarks.csproj package BenchmarkDotNet --version 0.15.8
```

## Architecture Patterns

### Recommended Project Structure
```
benchmarks/
  Gluey.Contract.Benchmarks/
    Gluey.Contract.Benchmarks.csproj
    Program.cs
    Scenarios/
      FlatObjectBenchmark.cs
      NestedObjectBenchmark.cs
      ArrayPayloadBenchmark.cs
      FullSchemaBenchmark.cs
    Payloads/
      PayloadGenerator.cs          # Static helper generating byte[] payloads
docs/
  benchmarks/
    results.md                     # Committed benchmark output as evidence
tests/
  Gluey.Contract.Json.Tests/
    AllocationTests/
      TryParseAllocationTests.cs
      ValidateOnlyAllocationTests.cs
      PropertyAccessAllocationTests.cs
      DisposeAllocationTests.cs
      FormatAssertionAllocationTests.cs
```

### Pattern 1: BenchmarkDotNet Benchmark Class
**What:** Each benchmark class covers one scenario with GlobalSetup loading schema and pre-generating payloads.
**When to use:** All four benchmark scenarios.
**Example:**
```csharp
// Source: BenchmarkDotNet official docs
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class FlatObjectBenchmark
{
    private JsonContractSchema _schema = null!;
    private byte[] _smallPayload = null!;
    private byte[] _mediumPayload = null!;
    private byte[] _largePayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Load schema once (schema loading allocates, that's fine)
        var schemaJson = """{ "type": "object", "properties": { ... } }""";
        _schema = JsonContractSchema.Load(schemaJson)!;

        // Pre-generate payloads as byte[]
        _smallPayload = GenerateFlatPayload(size: 100);
        _mediumPayload = GenerateFlatPayload(size: 5_000);
        _largePayload = GenerateFlatPayload(size: 50_000);
    }

    [Benchmark]
    public bool TryParse_Small()
    {
        var ok = _schema.TryParse(_smallPayload, out var result);
        result.Dispose();
        return ok;
    }

    [Benchmark]
    public bool ValidateOnly_Small()
    {
        ReadOnlySpan<byte> span = _smallPayload;
        var ok = _schema.TryParse(span, out var result);
        result.Dispose();
        return ok;
    }

    // ... Medium, Large variants
}
```

### Pattern 2: System.Text.Json Comparison Baseline
**What:** Include STJ deserialization benchmark alongside each scenario for comparison.
**When to use:** Each scenario class includes a baseline method.
**Example:**
```csharp
[Benchmark(Baseline = true)]
public JsonDocument StjDeserialize_Small()
{
    var doc = JsonDocument.Parse(_smallPayload);
    doc.Dispose();
    return doc;
}
```

### Pattern 3: Allocation Regression Test with GC.GetAllocatedBytesForCurrentThread
**What:** Measure allocated bytes before and after, assert delta is zero.
**When to use:** All four coverage areas.
**Example:**
```csharp
[Test]
public void TryParse_ByteArray_AllocatesZeroBytes()
{
    // Arrange: schema and payload pre-loaded in [SetUp]
    // Warm up: one parse to JIT everything
    _schema.TryParse(_payload, out var warmup);
    warmup.Dispose();

    // Act
    long before = GC.GetAllocatedBytesForCurrentThread();
    var ok = _schema.TryParse(_payload, out var result);
    result.Dispose();
    long after = GC.GetAllocatedBytesForCurrentThread();

    // Assert
    long allocated = after - before;
    allocated.Should().Be(0, "parse path must not allocate on the managed heap");
}
```

### Pattern 4: NuGet Package Metadata in csproj
**What:** Complete package metadata in each csproj.
**When to use:** Both src projects.
**Example:**
```xml
<PropertyGroup>
  <PackageId>Gluey.Contract</PackageId>
  <Version>1.0.0-preview.1</Version>
  <Authors>Gluey Contributors</Authors>
  <Description>Zero-allocation, single-pass validation and indexing of raw bytes against a schema</Description>
  <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
  <PackageTags>json;schema;validation;zero-allocation;high-performance</PackageTags>
  <RepositoryUrl>https://github.com/gluey/Gluey.Contracts</RepositoryUrl>
  <RepositoryType>git</RepositoryType>

  <!-- Reproducible builds -->
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>

  <!-- NuGet pack assets (already present) -->
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>

<!-- ContinuousIntegrationBuild only on CI (prevents broken local debug) -->
<PropertyGroup Condition="'$(CI)' == 'true'">
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
</PropertyGroup>
```

### Anti-Patterns to Avoid
- **Enabling ContinuousIntegrationBuild unconditionally:** Breaks local debugging because paths are normalized. Must be conditional on CI environment variable.
- **Measuring allocations without warm-up:** First call JITs methods and allocates. Always do a warm-up call before measuring.
- **Using `GC.Collect()` in allocation tests:** The method measures total bytes allocated on the thread, not bytes currently alive. GC.Collect is irrelevant and adds noise.
- **Putting BenchmarkDotNet in CI:** Benchmarks are noisy, slow, and non-deterministic. Allocation regression tests (via GC.GetAllocatedBytesForCurrentThread in NUnit) are the CI mechanism.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Heap allocation measurement | Custom GC hooks or profiler integration | BenchmarkDotNet MemoryDiagnoser | Handles edge cases: EpsilonGC noise, Gen0/1/2 breakdown, cross-platform |
| Micro-benchmark infrastructure | Custom Stopwatch loops | BenchmarkDotNet | Warm-up, outlier removal, statistical analysis, multi-runtime support |
| Source Link configuration | Manual PDB embedding | Built-in .NET SDK Source Link | Already integrated; just needs PublishRepositoryUrl + EmbedUntrackedSources |
| NuGet metadata validation | Manual pack testing | `dotnet pack --no-build` + inspect .nupkg | Standard workflow; catches missing metadata early |

**Key insight:** BenchmarkDotNet MemoryDiagnoser uses `GC.GetAllocatedBytesForCurrentThread` internally but adds statistical rigor (multiple iterations, outlier removal). For CI regression tests, direct `GC.GetAllocatedBytesForCurrentThread` is simpler and deterministic (zero or not-zero is binary).

## Common Pitfalls

### Pitfall 1: Warm-up Allocation Leaking into Measurement
**What goes wrong:** First call to TryParse triggers JIT compilation which allocates. The test reports false positives.
**Why it happens:** .NET JIT compiles methods on first use, allocating on the managed heap.
**How to avoid:** Always call the method under test once before measuring. Dispose the result.
**Warning signs:** Inconsistent test failures that pass on retry.

### Pitfall 2: Test Framework Allocations in Measurement Window
**What goes wrong:** Assertions or logging inside the measurement window allocate, causing false failures.
**Why it happens:** FluentAssertions, string interpolation, or NUnit infrastructure allocates.
**How to avoid:** Capture `before` and `after` around ONLY the code under test. Do assertions after `after`.
**Warning signs:** Tests fail with small (< 100 byte) allocations that vary.

### Pitfall 3: ParseResult Constructor Allocates int[1] for Dispose Guard
**What goes wrong:** `ParseResult` constructor allocates `new int[1]` for the `_disposedHolder` field. This is an allocation on every parse.
**Why it happens:** The readonly struct needs a mutable reference type for Interlocked.Exchange dispose guard.
**How to avoid:** This is a known allocation in the current code. Either (a) the allocation test must account for this (assert == 32 bytes or whatever int[1] costs), or (b) the code must be refactored to eliminate this allocation before benchmarks prove zero-alloc. This is a CRITICAL finding that must be addressed.
**Warning signs:** Consistent ~32 byte allocation on every TryParse call.

### Pitfall 4: ArrayBuffer Class Allocation
**What goes wrong:** `ArrayBuffer` is a class (not struct, per Phase 9 decision). Creating it allocates.
**Why it happens:** Design decision to avoid copy semantics when shared across ParsedProperty instances.
**How to avoid:** ArrayBuffer is only created when array elements exist. For schemas without arrays, this is zero. For schemas with arrays, this is a known allocation. Tests must be designed accordingly -- use non-array schemas for zero-alloc proof, or acknowledge array scenarios have a fixed allocation budget.
**Warning signs:** Allocations only in array-containing payloads.

### Pitfall 5: BenchmarkDotNet Console App Configuration
**What goes wrong:** Running benchmarks in Debug mode or without `--configuration Release` produces meaningless results.
**Why it happens:** Debug builds disable optimizations, inlining, etc.
**How to avoid:** Always build and run in Release. BenchmarkDotNet warns about this but the project should enforce it.
**Warning signs:** BenchmarkDotNet warning about non-optimized build.

## Code Examples

### Benchmark Project csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.15.8" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Gluey.Contract.Json\Gluey.Contract.Json.csproj" />
  </ItemGroup>
</Project>
```

### Benchmark Program.cs
```csharp
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
```

### Allocation Regression Test Helper Pattern
```csharp
private static long MeasureAllocations(Action action)
{
    // Warm up
    action();

    // Measure
    long before = GC.GetAllocatedBytesForCurrentThread();
    action();
    long after = GC.GetAllocatedBytesForCurrentThread();

    return after - before;
}
```

### NuGet Pack Verification Command
```bash
dotnet pack src/Gluey.Contract/Gluey.Contract.csproj --configuration Release --no-build --output ./nupkg
dotnet pack src/Gluey.Contract.Json/Gluey.Contract.Json.csproj --configuration Release --no-build --output ./nupkg
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Microsoft.SourceLink.GitHub NuGet package | Built into .NET SDK | .NET 8 (Nov 2023) | No package reference needed; just set PublishRepositoryUrl + EmbedUntrackedSources |
| BenchmarkDotNet 0.13.x | BenchmarkDotNet 0.15.8 | 2025 | Better .NET 9 support, improved MemoryDiagnoser accuracy |
| Manual allocation tracking | GC.GetAllocatedBytesForCurrentThread | .NET Core 1.1+ | Cross-platform, thread-specific, no external tooling needed |

## Critical Finding: Known Allocations in Parse Path

The current `ParseResult` implementation allocates `new int[1]` for the `_disposedHolder` field on every construction. This means `TryParse` will always allocate at least ~32 bytes (int[1] object overhead on 64-bit). Additionally, `ArrayBuffer` is a class that allocates when array elements are present.

**Options:**
1. **Refactor _disposedHolder out:** Use a different disposal tracking mechanism (e.g., a sentinel value in the OffsetTable itself, or accept that double-dispose is benign and remove the guard)
2. **Accept known allocation budget:** Allocation tests assert <= 32 bytes for the dispose guard, zero for everything else
3. **Document as known:** The zero-allocation claim covers the validation/indexing path, not the result wrapper overhead

**Recommendation:** The planner must address this before allocation tests can assert exact zero. Option 1 (refactor) is cleanest but may require careful review. Option 2 is pragmatic. The user's locked decision says "strict zero tolerance: assert exactly 0 bytes" so option 1 (refactoring away the int[1]) is likely required.

## Open Questions

1. **ParseResult int[1] allocation**
   - What we know: `_disposedHolder = new int[1]` allocates on every TryParse
   - What's unclear: Whether the user considers this within the zero-alloc scope or an acceptable wrapper cost
   - Recommendation: Plan a task to refactor this (e.g., make double-dispose benign without a guard, or use a static sentinel)

2. **ArrayBuffer allocation for array-containing schemas**
   - What we know: ArrayBuffer is a class, allocates when arrays are present
   - What's unclear: Whether array scenarios should have a separate allocation budget or if zero-alloc claim excludes array scenarios
   - Recommendation: Test array scenarios separately with explicit allocation budget; non-array scenarios assert strict zero

3. **Repository URL for NuGet metadata**
   - What we know: Context says RepositoryUrl is needed
   - What's unclear: The actual GitHub URL for this repository
   - Recommendation: Use a placeholder that the planner can fill, or derive from git remote

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Allocation" --no-build` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| QUAL-01 | BenchmarkDotNet reports zero heap allocations | manual (benchmark run) | `dotnet run --project benchmarks/Gluey.Contract.Benchmarks -c Release` | No -- Wave 0 |
| QUAL-02 | Allocation regression via GC.GetAllocatedBytesForCurrentThread | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Allocation" --no-build` | No -- Wave 0 |
| QUAL-03 | NuGet packages packable with correct metadata | smoke | `dotnet pack src/Gluey.Contract/Gluey.Contract.csproj -c Release && dotnet pack src/Gluey.Contract.Json/Gluey.Contract.Json.csproj -c Release` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --no-build --filter "FullyQualifiedName~Allocation"`
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** Full suite green + both packages pack successfully

### Wave 0 Gaps
- [ ] `benchmarks/Gluey.Contract.Benchmarks/` -- entire benchmark project
- [ ] `tests/Gluey.Contract.Json.Tests/AllocationTests/` -- allocation regression test files
- [ ] NuGet metadata additions to both csproj files

## Sources

### Primary (HIGH confidence)
- [BenchmarkDotNet NuGet 0.15.8](https://www.nuget.org/packages/benchmarkdotnet/) - current version verified
- [BenchmarkDotNet MemoryDiagnoser docs](https://benchmarkdotnet.org/articles/configs/diagnosers.html) - configuration and accuracy
- [GC.GetAllocatedBytesForCurrentThread API](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread) - official API reference
- [dotnet/sourcelink GitHub](https://github.com/dotnet/sourcelink) - confirms built-in to .NET 8+ SDK
- [Microsoft.SourceLink.GitHub NuGet](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) - version reference
- Project source code (ParseResult.cs, JsonContractSchema.cs, csproj files) - existing API surface and metadata

### Secondary (MEDIUM confidence)
- [Adam Sitnik - MemoryDiagnoser blog](https://adamsitnik.com/the-new-Memory-Diagnoser/) - 99.5% accuracy claim
- [Deterministic builds guide](https://github.com/clairernovotny/DeterministicBuilds) - ContinuousIntegrationBuild best practices
- [.NET Blog - Source Link packages](https://devblogs.microsoft.com/dotnet/producing-packages-with-source-link/) - packaging guidance

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - BenchmarkDotNet version verified on NuGet; NUnit/FA already in project
- Architecture: HIGH - Well-known patterns for all three workstreams; verified against project code
- Pitfalls: HIGH - ParseResult int[1] allocation identified from source code review; warm-up pattern is well-documented

**Research date:** 2026-03-10
**Valid until:** 2026-04-10 (stable domain, no rapid changes expected)
