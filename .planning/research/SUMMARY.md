# Project Research Summary

**Project:** Gluey.Contract - Zero-Allocation JSON Schema Validator/Indexer
**Domain:** High-performance .NET library (zero-allocation byte parsing, JSON Schema validation)
**Researched:** 2026-03-08
**Confidence:** HIGH

## Executive Summary

Gluey.Contract is a zero-allocation, schema-driven JSON byte validator and indexer for .NET 9. The library occupies a unique niche: no existing .NET JSON Schema validator operates directly on raw bytes without intermediate object models. The recommended approach, informed by established parser architectures (simdjson's tape structure, Blaze's compiled schema instructions, jsmn's token-offset design), is a two-phase architecture: a **compile phase** that parses JSON Schema into an immutable, pre-optimized schema graph (allocations acceptable), and a **parse phase** that validates and indexes raw UTF-8 bytes in a single pass with zero heap allocations. The entire stack is in-box .NET 9 -- no external production dependencies.

The core technical approach is sound and well-supported by prior art. `Utf8JsonReader` provides the zero-allocation JSON tokenizer foundation. `ArrayPool<T>` handles variable-size buffer rental. Pre-computed UTF-8 property names and JSON Pointer paths eliminate string allocation during validation. The schema-driven sizing strategy (the schema tells you the maximum property count, so all buffers are pre-sized) is the key insight that makes zero-allocation feasible for a validator that must collect all errors and build an offset table simultaneously.

The primary risks are: (1) hidden allocations from escaped JSON strings, closures, LINQ, and boxing -- each requiring strict coding discipline enforced by analyzers and allocation regression tests; (2) JSON Schema composition keywords (`allOf`/`anyOf`/`oneOf`) complicating single-pass validation and requiring careful schema flattening at compile time; and (3) `$ref` resolution creating circular references or breaking forward-only parsing if not fully resolved during schema compilation. All three are mitigatable with the patterns identified in research, but they demand attention from the earliest phases.

## Key Findings

### Recommended Stack

The stack is entirely in-box .NET 9 with zero external production dependencies (per ADR 7). The only external dependency is BenchmarkDotNet for the benchmark project.

**Core technologies:**
- **.NET 9 / C# 13**: Target framework, already decided. Provides `Utf8JsonReader` improvements, `InlineArray`, `params ReadOnlySpan<T>`, and superior JIT for struct-heavy code.
- **System.Text.Json.Utf8JsonReader**: The only zero-allocation JSON reader in .NET. Ref struct reading directly from `ReadOnlySpan<byte>`. Used as the foundation tokenizer (or as reference for a custom `JsonByteReader`).
- **ArrayPool\<T\>.Shared**: Zero-allocation buffer rental for offset tables and error collectors. ~44ns per rent, thread-safe.
- **stackalloc / InlineArray**: Stack-allocated buffers for small, bounded collections (offset tables for small schemas, temporary scratch space).
- **Custom JSON Schema model**: No existing library (Corvus, NJsonSchema, JsonSchema.Net, Json.NET Schema) achieves zero-allocation validation. All allocate during the validation path. A custom model is the only viable approach.
- **BenchmarkDotNet 0.15.8**: Allocation verification via `[MemoryDiagnoser]`. Paired with `GC.GetAllocatedBytesForCurrentThread()` for CI regression tests.

### Expected Features

**Must have (table stakes -- v1 launch):**
- JSON byte walker (tokenize raw UTF-8 bytes, zero allocation)
- Schema loading with `$ref`/`$defs` resolution at load time
- Pre-computed JSON Pointer paths from schema (invariant 3)
- Core validation keywords: `type`, `enum`, `const`, `required`, `properties`, `additionalProperties`, `items`, `prefixItems`
- Numeric constraints (`minimum`, `maximum`, `multipleOf`), string constraints (`minLength`, `maxLength`, `pattern`), size constraints
- Composition: `allOf`, `anyOf`, `oneOf`, `not`
- Error collection with RFC 6901 paths, error codes, static messages (up to 64 errors)
- Offset table construction + `ParsedProperty` with on-demand `GetString()`, `GetInt32()`, etc.
- Dual API: `TryParse` (bool) + `Result<T>` (ADR 4)
- BenchmarkDotNet suite proving zero allocation

**Should have (v1.x):**
- `if`/`then`/`else` conditional validation
- `dependentRequired`/`dependentSchemas`
- `patternProperties`/`propertyNames`
- `contains`/`minContains`/`maxContains`
- `uniqueItems` (needs zero-alloc hashing strategy)
- Schema registry for multi-schema `$ref` resolution
- Format annotation + opt-in format assertion

**Defer (v2+):**
- `unevaluatedProperties`/`unevaluatedItems` (requires full annotation tracking -- hardest part of spec)
- `$dynamicRef`/`$dynamicAnchor` (rare, complex runtime resolution)
- Custom keyword extension API, JSON Schema output format, Protobuf driver

### Architecture Approach

The architecture follows a two-phase model proven by simdjson, Blaze, and FlatBuffers. The compile phase (once per schema lifetime) parses JSON Schema into an immutable `CompiledSchema` graph with pre-computed paths, property indices, buffer sizing metadata, and validation rules. The parse phase (per request) uses a `SchemaWalker` to drive a `JsonByteReader` forward through raw bytes in lockstep with the compiled schema, populating an `OffsetTable` and `ErrorCollector` -- both pre-sized from schema metadata and rented from `ArrayPool`. The result is a `ParseResult` readonly struct that provides indexed access into the original caller-owned byte buffer.

**Major components:**
1. **CompiledSchema / SchemaNode** (Gluey.Contract) -- Immutable schema graph with precomputed paths, property indices, validation rules, and buffer sizing metadata
2. **OffsetTable / OffsetEntry** (Gluey.Contract) -- Pre-sized flat array mapping schema property ordinals to byte positions; rented from ArrayPool
3. **ErrorCollector / ValidationError** (Gluey.Contract) -- Fixed-capacity error buffer with enum codes, precomputed paths, static messages
4. **ParseResult / ParsedProperty** (Gluey.Contract) -- Readonly struct API surface; offset-based on-demand value materialization from original buffer
5. **JsonByteReader** (Gluey.Contract.Json) -- Purpose-built UTF-8 tokenizer with native byte offset tracking
6. **JsonSchemaCompiler** (Gluey.Contract.Json) -- Parses JSON Schema, resolves $ref, builds CompiledSchema
7. **SchemaWalker** (Gluey.Contract.Json) -- Orchestrates single-pass validation + indexing

**Package boundary:** `Gluey.Contract` (core) defines shapes with zero format knowledge. `Gluey.Contract.Json` provides JSON-specific implementations. Future format drivers reuse core types.

### Critical Pitfalls

1. **Hidden allocations from escaped JSON strings** -- `Utf8JsonReader.GetString()` allocates on every call with escapes. Store raw byte offsets in the offset table; defer unescaping to `GetString()` materialization. Test with escaped payloads.
2. **Closure/LINQ allocations on parse path** -- Every captured lambda generates a hidden heap-allocated display class. Ban LINQ and lambdas entirely on the parse path; use `for` loops and `Span<T>` iteration. Enforce with Roslyn analyzers.
3. **Boxing through interface dispatch on structs** -- Casting `readonly struct` to interface boxes it. Use generic constraints (`where T : struct`); override `ToString`/`Equals`/`GetHashCode` on every struct.
4. **$ref circular references breaking single-pass** -- Resolve ALL references at schema compile time. Detect cycles with visited-set. Set max resolution depth. Defer `$dynamicRef` to v2+.
5. **Offset table as Dictionary = allocation disaster** -- Use flat array indexed by compile-time property ordinals. Schema knows max property count; pre-size from ArrayPool.

## Implications for Roadmap

Based on research, the build order follows clear dependency chains identified in ARCHITECTURE.md. Components form a strict DAG where each phase produces types consumed by the next.

### Phase 1: Core Types and Contracts
**Rationale:** Every other component produces or consumes these types. They are the contract between core and format drivers. Getting the struct shapes right here prevents boxing and allocation issues downstream.
**Delivers:** `ValidationErrorCode` (enum), `ValidationError` (readonly struct), `OffsetEntry` (readonly struct), `ParsedProperty` (readonly struct), `OffsetTable` (struct with ArrayPool rental), `ErrorCollector` (struct with ArrayPool rental), `ParseResult` (readonly struct, IDisposable).
**Addresses:** Core type definitions, dual API surface (TryParse + Result), buffer ownership model
**Avoids:** Boxing through interfaces (Pitfall 3), record struct ToString (Pitfall 12), Dictionary-based offset table (Pitfall 5)

### Phase 2: Schema Model and Compilation
**Rationale:** The schema model defines the "shape" that both the compiler and walker operate on. It must be stable before either can be built. Schema compilation is where $ref resolution, path precomputation, and buffer sizing happen -- all critical for zero-allocation parse.
**Delivers:** `JsonType` (enum), `ValidationRule` (readonly struct), `SchemaNode` (sealed class), `JsonSchemaCompiler`, `$ref`/`$defs` resolution, path precomputation, property index assignment, MaxPropertyCount/MaxDepth calculation.
**Addresses:** Schema loading, $ref resolution, precomputed JSON Pointer paths (invariant 3), schema-driven buffer sizing
**Avoids:** $ref circular references (Pitfall 4), allOf/oneOf complexity (Pitfall 8)

### Phase 3: JSON Byte Reader
**Rationale:** The walker needs the reader to traverse bytes, but the reader has no dependency on schema or offset tables. It is a pure tokenizer that can be built and tested independently.
**Delivers:** `JsonTokenType` (enum), `JsonByteReader` (ref struct) with byte-offset tracking, structural JSON validation.
**Addresses:** JSON byte walker feature, UTF-8 tokenization
**Avoids:** Hidden allocations from escaped strings (Pitfall 1), Unicode edge cases (Pitfall 7), Utf8JsonReader ref struct constraints (Pitfall 11)

### Phase 4: Validation Engine
**Rationale:** With core types, schema model, and byte reader in place, the validation engine wires them together. This is where individual keyword validators are implemented and zero-allocation guarantees are verified end-to-end.
**Delivers:** Type/enum/const validators, numeric validators, string validators, array validators, object validators (properties/required/additionalProperties), composition validators (allOf/anyOf/oneOf/not), error collection with RFC 6901 paths.
**Addresses:** All P1 validation keywords, error reporting, single-pass validation
**Avoids:** Closure/LINQ allocations (Pitfall 2), error collection allocation (Pitfall 6), number precision loss (Pitfall 14)

### Phase 5: Single-Pass Walker and Integration
**Rationale:** The SchemaWalker is the integration point that orchestrates reader + schema + offset table + error collector. This is where the "validate and index in one pass" differentiator is realized. Integration tests and benchmarks prove the zero-allocation invariant.
**Delivers:** `SchemaWalker`, public `JsonContractSchema` API, offset table construction during validation, `ParsedProperty` value materialization, BenchmarkDotNet zero-allocation proof, allocation regression tests.
**Addresses:** Single-pass validation + indexing (core differentiator), offset-based value access, dual API (TryParse + Result), benchmark suite
**Avoids:** Buffer lifetime issues (Pitfall 9), stackalloc overflow (Pitfall 10)

### Phase 6: Extended Keywords (v1.x)
**Rationale:** Once the core is working and proven zero-allocation, extend to P2 keywords that increase spec coverage and adoption.
**Delivers:** `if`/`then`/`else`, `dependentRequired`/`dependentSchemas`, `patternProperties`/`propertyNames`, `contains`/`minContains`/`maxContains`, `uniqueItems`, `$anchor`, schema registry, format annotation/assertion.
**Addresses:** P2 features from FEATURES.md
**Avoids:** Format assertion allocation conflicts (document as opt-in)

### Phase Ordering Rationale

- Phases 1-2 establish the type contracts and schema model that all downstream code depends on. Changing struct layouts after Phase 3+ would cascade through everything.
- Phase 3 (byte reader) is isolated from schema concerns, enabling parallel development if needed.
- Phases 4-5 are where the zero-allocation invariant is most at risk. By this point, all foundational types are stable, so the focus is purely on validation logic and integration.
- Phase 6 extends an already-working system. Each P2 keyword can be added independently without architectural changes.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Schema Compilation):** $ref resolution strategy, circular reference detection, allOf/anyOf/oneOf flattening, and minimal perfect hash for large property sets need detailed design work. The Blaze paper provides a starting point but .NET-specific implementation requires validation.
- **Phase 4 (Validation Engine):** `pattern` keyword requires regex execution which may allocate. `uniqueItems` hashing strategy for zero-allocation array element comparison needs investigation. Number precision handling for `multipleOf` with fractional values needs careful design.
- **Phase 3 (JSON Byte Reader):** Decision between wrapping `Utf8JsonReader` versus building a custom `JsonByteReader`. ARCHITECTURE.md recommends custom, STACK.md recommends `Utf8JsonReader`. This tension must be resolved -- likely by prototyping both approaches and benchmarking.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Core Types):** Readonly struct design, ArrayPool rental patterns, and IDisposable implementation are well-documented .NET patterns.
- **Phase 5 (Integration):** The walker is the composition of already-researched components. BenchmarkDotNet setup is straightforward.
- **Phase 6 (Extended Keywords):** Individual keywords are well-specified in JSON Schema Draft 2020-12. Implementation follows established patterns from Phase 4.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All technologies are in-box .NET 9 with official Microsoft documentation. No external dependencies to evaluate. Well-established patterns. |
| Features | HIGH | JSON Schema Draft 2020-12 is a stable spec. Feature landscape is well-defined by the spec itself. Competitor analysis based on public NuGet packages and GitHub repos. |
| Architecture | HIGH | Two-phase compile/parse architecture proven by simdjson, Blaze, and FlatBuffers. Component boundaries derived from project ADRs and invariants. ArrayPool strategy is standard .NET. |
| Pitfalls | HIGH | Pitfalls sourced from official .NET documentation, known runtime issues (dotnet/runtime#54410), and established .NET performance guidance. All are well-documented and verifiable. |

**Overall confidence:** HIGH

### Gaps to Address

- **Custom JsonByteReader vs. Utf8JsonReader wrapper:** ARCHITECTURE.md recommends a custom byte reader for native offset tracking. STACK.md recommends Utf8JsonReader as the foundation. Both arguments have merit. Resolve by prototyping in Phase 3 -- build a thin wrapper around Utf8JsonReader first, evaluate whether offset tracking works cleanly, and fall back to custom reader only if needed.
- **Minimal perfect hash for large property sets:** Mentioned in ARCHITECTURE.md as needed for objects with >8 properties. The Blaze paper demonstrates the concept, but a .NET implementation needs to be validated. Defer to Phase 2 implementation; linear scan is fine for v1 MVP if most real-world schemas have <8 properties per object.
- **`uniqueItems` zero-allocation hashing:** Comparing array elements for uniqueness without deserialization is an unsolved design problem in this codebase. Possible approaches: hash raw bytes (fast but incorrect for semantically equivalent values like `1.0` vs `1`), or defer to v1.x. Recommend deferring.
- **Format assertion allocation budget:** Some format validators (regex for `pattern`, email parsing) may need to allocate. The project must decide whether format assertion is allowed a small allocation budget or must be truly zero-alloc. Recommend: format assertion is opt-in and explicitly outside the zero-allocation guarantee.
- **Array path precomputation depth:** For arrays, precomputing JSON Pointer paths for indices 0-N requires choosing N. ARCHITECTURE.md suggests 64 as default with pooled fallback. Needs validation against real-world schema usage patterns.

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: Utf8JsonReader](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader) -- core API documentation
- [Microsoft Learn: Utf8JsonReader.ValueTextEquals](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader.valuetextequals) -- zero-alloc string comparison
- [Microsoft Learn: InlineArray](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/inline-arrays) -- C# 12 fixed-size buffers
- [Microsoft Learn: GC.GetAllocatedBytesForCurrentThread](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread) -- allocation measurement
- [.NET Blog: System.Text.Json in .NET 9](https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/) -- .NET 9 improvements
- [JSON Schema Draft 2020-12 Core](https://json-schema.org/draft/2020-12/json-schema-core) -- authoritative spec
- [JSON Schema Draft 2020-12 Validation](https://json-schema.org/draft/2020-12/json-schema-validation) -- validation keywords
- [BenchmarkDotNet Diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html) -- memory diagnostics

### Secondary (MEDIUM confidence)
- [Blaze: Compiling JSON Schema for 10x Faster Validation](https://arxiv.org/html/2503.02770v1) -- compiled schema architecture, semi-perfect hashing
- [simdjson tape structure](https://github.com/simdjson/simdjson/blob/master/doc/tape.md) -- tape-based parser design
- [Adam Sitnik: ArrayPool](https://adamsitnik.com/Array-Pool/) -- .NET team member guidance on buffer pooling
- [vcsjones: Dos and Don'ts of stackalloc](https://vcsjones.dev/stackalloc/) -- stack allocation limits
- [dotnet/runtime#54410](https://github.com/dotnet/runtime/issues/54410) -- non-allocating string view limitation
- [endjin: .NET 9 JSON Schema performance](https://endjin.com/blog/2024/11/how-dotnet-9-boosted-json-schema-performance-by-32-percent) -- Corvus performance context
- [endjin: Hunting for allocations](https://endjin.com/blog/2023/09/optimising-dotnet-code-2-hunting-for-allocations) -- allocation detection patterns

### Tertiary (LOW confidence)
- [HAL: Validation of Modern JSON Schema](https://hal.science/hal-04042629/document) -- PSPACE-hardness of $dynamicRef (theoretical, informs deferral decision)

---
*Research completed: 2026-03-08*
*Ready for roadmap: yes*
