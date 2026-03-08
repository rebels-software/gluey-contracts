# Technology Stack

**Project:** Gluey.Contract - Zero-Allocation JSON Schema Validator/Indexer
**Researched:** 2026-03-08

## Recommended Stack

### Core Framework

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| .NET 9.0 | 9.0 | Target framework | Already decided. .NET 9 gives us Utf8JsonReader improvements (AllowMultipleValues), better JIT for struct-heavy code, and improved Span optimizations. No reason to target older. | HIGH |
| C# 13 | 13 | Language version | Already decided. Gives us `params ReadOnlySpan<T>`, improved pattern matching, and all C# 12 features (InlineArray, collection expressions). | HIGH |

### JSON Parsing Layer (Gluey.Contract.Json)

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `System.Text.Json.Utf8JsonReader` | In-box (.NET 9) | Forward-only JSON token reader | The only zero-allocation JSON reader in .NET. It is a `ref struct` that reads directly from `ReadOnlySpan<byte>` without any heap allocation. It handles UTF-8 natively, unescaping, and multi-segment buffers. This is not optional -- it is the only viable choice for zero-alloc JSON reading in .NET. | HIGH |

### Zero-Allocation Primitives (Gluey.Contract core)

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `System.Span<T>` / `ReadOnlySpan<T>` | In-box | Stack-bound slicing of byte buffers | Zero-copy views into the caller's buffer. Used extensively in the parse path for slicing without allocation. Cannot be stored in fields of `readonly struct` (ref struct limitation), but used as local variables and method parameters throughout parsing. | HIGH |
| `System.ReadOnlyMemory<byte>` | In-box | Heap-safe buffer reference | For API surface that accepts memory from callers. Can be stored in fields, passed to async code. Slice with `.Span` property in hot path. | HIGH |
| `System.Buffers.ArrayPool<T>` | In-box | Pooled arrays for variable-size collections | For the offset table when property count exceeds stackalloc threshold. Rent/return pattern avoids allocation. ~44ns per rent with zero GC. Use `ArrayPool<T>.Shared` for simplicity. | HIGH |
| `stackalloc` | Language feature | Stack-allocated temporary buffers | For small, bounded buffers (offset tables for schemas with few properties, temp scratch space). Use when size is known at compile time or bounded by a small constant (typically < 512 bytes). | HIGH |
| `[InlineArray(N)]` | C# 12+ | Fixed-size value-type buffers without unsafe | For fixed-capacity collections embedded in structs (e.g., a validation error buffer of 64 entries). Replaces `unsafe fixed` buffers with type-safe alternative. Lives inline in the struct -- no heap allocation. | HIGH |
| `System.Runtime.InteropServices.MemoryMarshal` | In-box | Low-level span reinterpretation | For casting `ReadOnlySpan<byte>` to `ReadOnlySpan<char>` when comparing property names encoded as UTF-8 bytes. Avoids string allocation during comparisons. | MEDIUM |

### JSON Schema Implementation

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Custom schema model | N/A | Schema representation | Build a custom schema model (`JsonContractSchema`) because: (1) ADR 7 mandates zero external dependencies in core, (2) existing libraries (Corvus.JsonSchema, NJsonSchema, Json.NET Schema) all allocate heavily during validation, (3) the schema model must pre-compute JSON Pointer paths (invariant 3), (4) the schema is loaded once and reused -- allocation during schema loading is acceptable, only the parse path must be zero-alloc. | HIGH |
| `System.Text.Json.JsonDocument` | In-box | Schema JSON parsing (load-time only) | For parsing the JSON Schema document itself at load time. `JsonDocument` pools its memory internally and is the standard way to parse JSON into a traversable DOM. Allocation during schema loading is acceptable. | HIGH |

### Benchmarking and Allocation Verification

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| BenchmarkDotNet | 0.15.8 | Performance benchmarking | Industry standard for .NET micro-benchmarks. `[MemoryDiagnoser]` attribute reports Gen0/1/2 collections and allocated bytes. A dash in the Allocated column = zero allocation. 99.5% accurate for allocation measurement. | HIGH |
| `GC.GetAllocatedBytesForCurrentThread()` | In-box | Allocation regression tests | Call before/after parse, assert difference is 0. Cheaper than BenchmarkDotNet for CI. Caveat: non-deterministic on first call due to JIT; warm up first. Run assertion in a loop (e.g., 3 iterations, assert last iteration is 0). | HIGH |

### Test Infrastructure (already chosen)

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| NUnit | 4.3.1 (current: 4.5.1) | Test framework | Already in project. Consider bumping to 4.5.x for latest fixes but not required. | HIGH |
| FluentAssertions | 8.0.1 | Assertion library | Already in project. Provides readable assertions. | HIGH |
| coverlet | 6.0.2 | Code coverage | Already in project. | HIGH |

## Key API Patterns for Zero-Allocation Parsing

### Utf8JsonReader Usage Pattern

```csharp
// Utf8JsonReader is a ref struct -- stack only, zero allocation
public static bool TryParse(
    ReadOnlySpan<byte> utf8Json,
    JsonContractSchema schema,
    out ParseResult result)
{
    var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    });

    // Key APIs for zero-alloc reading:
    // reader.Read()              -- advance to next token
    // reader.TokenType           -- what kind of token
    // reader.ValueSpan           -- raw bytes of current value (no alloc)
    // reader.ValueTextEquals()   -- compare property name without allocating string
    // reader.GetInt32()          -- parse number without string intermediate
    // reader.HasValueSequence    -- true if value spans multiple segments
    // reader.CopyString()        -- AVOID: allocates a string
    // reader.GetString()         -- AVOID in hot path: allocates a string
}
```

### Offset Table Pattern

```csharp
// For small schemas: stackalloc
Span<ParsedProperty> entries = stackalloc ParsedProperty[schema.PropertyCount];

// For larger schemas: ArrayPool
ParsedProperty[]? rented = null;
Span<ParsedProperty> entries = schema.PropertyCount <= 32
    ? stackalloc ParsedProperty[32]
    : (rented = ArrayPool<ParsedProperty>.Shared.Rent(schema.PropertyCount));

try
{
    // ... fill entries during parse ...
}
finally
{
    if (rented is not null)
        ArrayPool<ParsedProperty>.Shared.Return(rented);
}
```

### InlineArray for Validation Errors

```csharp
// Fixed-capacity error buffer -- no heap allocation
[InlineArray(64)]
internal struct ValidationErrorBuffer
{
    private ValidationError _element;
}

// Usage in parse method:
var errors = new ValidationErrorBuffer();
int errorCount = 0;
// errors[errorCount++] = new ValidationError(...);
```

### Pre-computed UTF-8 Property Names

```csharp
// Schema loading (allocation OK here):
public sealed class SchemaNode
{
    // Pre-encode property names as UTF-8 bytes at schema load time
    // so Utf8JsonReader.ValueTextEquals() can compare without allocation
    public byte[] Utf8PropertyName { get; }
    public string JsonPointerPath { get; }  // pre-computed RFC 6901 path
}

// Parse path (zero allocation):
if (reader.ValueTextEquals(schemaNode.Utf8PropertyName))
{
    // matched -- no string allocated
}
```

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| JSON Reader | `Utf8JsonReader` (in-box) | SimdJsonSharp | External dependency violates ADR 7 for core. Utf8JsonReader is fast enough and zero-alloc. SimdJson could be explored as a future alternative driver if perf demands it. |
| JSON Schema Validation | Custom model | Corvus.JsonSchema | Corvus generates types at build time via code gen -- fundamentally different paradigm from runtime schema-driven validation. It validates against generated types, not against a schema loaded at runtime. Wrong approach for this use case. |
| JSON Schema Validation | Custom model | NJsonSchema | Allocates heavily (reflection-based, POCO generation). Not designed for zero-allocation scenarios. Good for tooling, wrong for hot-path validation. |
| JSON Schema Validation | Custom model | Json.NET Schema | Depends on Newtonsoft.Json which allocates strings for every property. Fundamentally incompatible with zero-allocation goals. |
| JSON Schema Validation | Custom model | json-everything (JsonSchema.Net) | Good spec compliance but allocates during validation. Uses JsonElement/JsonNode internally. Not zero-alloc. |
| Struct approach | `readonly struct` | `ref struct` | Cannot be stored in fields, collections, or used in async methods. ADR 8 already decided this. |
| Struct approach | `readonly struct` | `record struct` | Generated ToString() allocates. ADR 8 already decided this. |
| Buffer pooling | `ArrayPool<T>.Shared` | Custom pool | Premature optimization. ArrayPool.Shared is thread-safe, well-tuned, and zero-alloc at ~44ns. Only build custom if benchmarks show contention. |
| Benchmark tool | BenchmarkDotNet | Manual Stopwatch | BenchmarkDotNet handles warmup, JIT, statistical analysis, memory diagnostics. Manual timing is error-prone and misses allocation data. |
| Test framework | NUnit 4.x | xUnit | Already chosen. No reason to switch. Both work fine. |

## What NOT to Use (and Why)

| Avoid | Why | What to Use Instead |
|-------|-----|---------------------|
| `reader.GetString()` in parse path | Allocates a new `string` on every call | `reader.ValueTextEquals()` for comparison, `reader.ValueSpan` for raw bytes |
| `reader.CopyString(dest)` in parse path | Allocates when value contains escapes | Store offset+length into original buffer instead |
| `JsonDocument.Parse()` in parse path | Allocates pooled memory, creates JsonElement tree | `Utf8JsonReader` for single-pass token reading |
| `JsonNode` / `JsonObject` | Full object graph with heap allocations per node | `Utf8JsonReader` tokens + offset table |
| `JsonSerializer.Deserialize<T>()` | Allocates target object, strings, arrays | Not applicable -- the whole point is to NOT deserialize |
| `Newtonsoft.Json` (anything) | String-based (UTF-16), allocates heavily, external dep | `System.Text.Json` in-box APIs |
| `string` in core data types | Every string is a heap allocation | `byte[]` + offset + length, or pre-computed `byte[]` for known strings |
| `List<T>` for error collection | Allocates backing array, resizes | `InlineArray` or `stackalloc` + count |
| `Dictionary<string, T>` for offset table | Allocates entries, buckets, string keys | Flat array/span indexed by schema property ordinal |
| LINQ in parse path | Allocates iterators, closures, intermediate collections | Manual loops with span indexing |
| `async` in parse path | State machine allocation, prevents `Span<T>` usage | Synchronous parse over `ReadOnlySpan<byte>` |
| Boxing via interface cast | Allocates boxed copy of struct on heap | Generic constraints: `where T : struct, IMyInterface` |
| `string.Format` / string interpolation in parse path | Allocates formatted string | Pre-computed error messages or deferred formatting |

## Installation

### Production Packages

```xml
<!-- Gluey.Contract: ZERO external dependencies (ADR 7) -->
<!-- Only uses in-box .NET 9 APIs -->

<!-- Gluey.Contract.Json: references core, uses in-box System.Text.Json -->
<!-- System.Text.Json is part of the .NET 9 shared framework, not a NuGet dependency -->
```

### Benchmark Project (to create)

```bash
# Create benchmark project
dotnet new console -n Gluey.Contract.Json.Benchmarks -o benchmarks/Gluey.Contract.Json.Benchmarks
cd benchmarks/Gluey.Contract.Json.Benchmarks
dotnet add package BenchmarkDotNet --version 0.15.8
dotnet add reference ../../src/Gluey.Contract/Gluey.Contract.csproj
dotnet add reference ../../src/Gluey.Contract.Json/Gluey.Contract.Json.csproj
```

### Test Projects (already exist, consider updates)

```bash
# Optional: bump test dependencies to latest
dotnet add tests/Gluey.Contract.Tests package NUnit --version 4.5.1
dotnet add tests/Gluey.Contract.Json.Tests package NUnit --version 4.5.1
```

## Version Compatibility Matrix

| Component | Min Version | Recommended | Notes |
|-----------|-------------|-------------|-------|
| .NET SDK | 9.0.100 | 9.0.x latest | Required for C# 13, InlineArray |
| C# | 13 | 13 | For params Span, improved patterns |
| System.Text.Json | 9.0.x (in-box) | In-box | Do NOT add as NuGet package -- use shared framework |
| BenchmarkDotNet | 0.14.0 | 0.15.8 | .NET 9 support, MemoryDiagnoser |
| NUnit | 4.3.1 | 4.5.1 | Already in project, minor bump optional |

## Sources

- [Utf8JsonReader documentation - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader) (HIGH confidence - official docs)
- [What's new in System.Text.Json in .NET 9 - .NET Blog](https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/) (HIGH confidence - official blog)
- [Utf8JsonReader.ValueTextEquals - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader.valuetextequals) (HIGH confidence - official API docs)
- [InlineArray specification - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/inline-arrays) (HIGH confidence - official spec)
- [C# 12 Inline Arrays - endjin](https://endjin.com/blog/2024/11/csharp-12-inline-arrays) (MEDIUM confidence - reputable blog)
- [ArrayPool - Adam Sitnik](https://adamsitnik.com/Array-Pool/) (MEDIUM confidence - .NET team member blog)
- [Dos and Don'ts of stackalloc](https://vcsjones.dev/stackalloc/) (MEDIUM confidence - well-known .NET author)
- [BenchmarkDotNet Diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html) (HIGH confidence - official docs)
- [BenchmarkDotNet NuGet 0.15.8](https://www.nuget.org/packages/benchmarkdotnet/) (HIGH confidence - NuGet)
- [GC.GetAllocatedBytesForCurrentThread - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread) (HIGH confidence - official API docs)
- [Corvus.JsonSchema - GitHub](https://github.com/corvus-dotnet/Corvus.JsonSchema) (MEDIUM confidence - reviewed but not recommended for this use case)
- [NJsonSchema - GitHub](https://github.com/RicoSuter/NJsonSchema) (MEDIUM confidence - reviewed but not recommended)
- [How .NET 9 boosted JSON Schema performance by 32% - endjin](https://endjin.com/blog/2024/11/how-dotnet-9-boosted-json-schema-performance-by-32-percent) (MEDIUM confidence - Corvus team blog, useful for perf context)
- [Non-allocating string view issue - dotnet/runtime](https://github.com/dotnet/runtime/issues/54410) (MEDIUM confidence - open issue tracking limitations)
