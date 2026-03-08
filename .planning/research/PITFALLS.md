# Domain Pitfalls

**Domain:** Zero-allocation, schema-driven JSON byte parser and validator (.NET)
**Researched:** 2026-03-08

## Critical Pitfalls

Mistakes that cause rewrites or violate core invariants.

### Pitfall 1: Hidden Allocations from Escaped JSON Strings

**What goes wrong:** The zero-allocation invariant silently breaks when the parser encounters escaped strings (e.g., `\"hello\\nworld\"`). `Utf8JsonReader.GetString()` allocates a new `string` on the heap every time it unescapes. Even `ValueSpan` is empty when `HasValueSequence` is true (multi-segment input), forcing you into allocation territory. There is currently no way in `System.Text.Json` to get an unescaped string view without allocating, per [dotnet/runtime#54410](https://github.com/dotnet/runtime/issues/54410).

**Why it happens:** Developers test with clean JSON (no escapes), benchmarks pass, then production payloads with `\n`, `\t`, `\"`, `\uXXXX` silently allocate. The `ValueIsEscaped` property (.NET 8+) tells you escapes are present but does not help you avoid the allocation.

**Consequences:** Zero-allocation invariant violated in production. Allocation regression tests pass in CI but fail on real-world data. GC pressure returns under load.

**Prevention:**
- During validation, compare escaped property names using `Utf8JsonReader.ValueTextEquals()` which handles unescaping internally without allocating a string.
- For the offset table: store raw byte offsets into the original buffer (including escape sequences). Defer unescaping to the `GetString()` materialization call, which is explicitly outside the parse path.
- For property name matching against schema: precompute both the UTF-8 bytes and the escaped UTF-8 bytes of each schema property name at schema construction time. Match using `ValueTextEquals()` or direct byte comparison against `ValueSpan`.
- Add allocation regression tests that include JSON payloads with escaped strings, Unicode escapes (`\u0041`), and surrogate pairs.

**Detection:** BenchmarkDotNet `[MemoryDiagnoser]` shows non-zero `Allocated` column. `GC.GetAllocatedBytesForCurrentThread()` delta > 0 in unit tests with escaped input.

**Phase:** Must be addressed in the very first parsing phase -- design the offset table to store raw byte positions, not materialized strings.

---

### Pitfall 2: Closure and Lambda Allocations in Validation Logic

**What goes wrong:** Every lambda that captures a local variable generates a hidden "display class" on the heap. A validation function like `properties.Any(p => p.Name == currentName)` allocates a closure object on every invocation because `currentName` is captured. LINQ methods (`Where`, `Any`, `Select`, `FirstOrDefault`) also allocate enumerator objects.

**Why it happens:** C# makes closures and LINQ feel free. The allocation is invisible in the source code -- the compiler generates it. Code reviews miss it because the pattern is idiomatic.

**Consequences:** A schema with 20 properties validated using LINQ generates 20+ closure allocations per parse call. The zero-allocation invariant is violated by "clean-looking" code.

**Prevention:**
- Ban LINQ and lambda expressions on the parse path entirely. Use `for`/`foreach` loops with direct index access.
- Use `static` lambdas (C# 9+) where delegates are unavoidable -- the compiler will error if you accidentally capture.
- Use `Span<T>`-based iteration instead of `IEnumerable<T>`.
- Pre-allocate any delegate instances at schema construction time (outside the parse path).
- Consider an analyzer or `.editorconfig` rule to flag LINQ usage in the parsing namespace.

**Detection:** JetBrains dotMemory or Rider heap allocation viewer. Roslyn analyzer `HAA0301` (closure allocation), `HAA0302` (display class), `HAA0303` (lambda delegate). BenchmarkDotNet `[MemoryDiagnoser]`.

**Phase:** Establish the coding convention in Phase 1. Every contributor must know: no LINQ, no lambdas on the parse path.

---

### Pitfall 3: Boxing Through Interface Dispatch on Structs

**What goes wrong:** Casting a `readonly struct` to an interface (e.g., `IValidationError error = new ValidationError(...)`) boxes it -- allocating a heap object to wrap the value type. This also happens implicitly when calling `object.ToString()`, `object.Equals()`, or `object.GetHashCode()` if the struct does not override them, and when using non-generic collections like `ArrayList` or `IList`.

**Why it happens:** ADR 8 already warns about this, but the temptation is strong: interfaces enable polymorphism and testability. A single `IComparable` constraint or `IEnumerable<ValidationError>` in the wrong place triggers boxing.

**Consequences:** Each boxing allocates 16 bytes of object header plus the struct payload. With the "collect all errors" design (up to 64 errors), this could mean 64 boxing allocations per parse.

**Prevention:**
- Use generic constraints (`where T : struct, IValidationError`) instead of interface references. The JIT specializes the code per value type, avoiding boxing.
- Override `ToString()`, `Equals()`, and `GetHashCode()` on every `readonly struct` to prevent fallback to `object` methods.
- Implement `IEquatable<T>` on structs used as dictionary keys or in equality comparisons.
- Never store structs in `object`-typed fields, parameters, or collections. Use typed arrays or `Span<T>`.
- Use `[Obsolete("Use typed overload", error: true)]` on any accidentally-exposed `object` overloads.

**Detection:** Roslyn analyzer `HAA0601` (boxing allocation). IL inspection showing `box` instructions. Allocation benchmarks.

**Phase:** Phase 1 (core type design). Once `ParsedProperty` and `ValidationError` are defined with boxing, downstream code locks in the pattern.

---

### Pitfall 4: $ref and $dynamicRef Resolution Breaks Single-Pass

**What goes wrong:** JSON Schema 2020-12's `$ref` and `$dynamicRef` allow schemas to reference other schemas, including recursively. Naive implementations resolve references during validation, which either (a) requires random access to schema nodes (breaking forward-only parsing) or (b) causes infinite recursion on circular schemas. `$dynamicRef` is particularly dangerous because it resolves based on the evaluation path at runtime, not the lexical position.

**Why it happens:** Simple schemas don't use `$ref`. Developers build a working validator, then encounter real-world schemas with deep `$ref` chains, circular references, and `$dynamicRef`/`$dynamicAnchor` pairs. The architecture doesn't accommodate them.

**Consequences:** Infinite loops on circular schemas. Stack overflows on deeply nested `$ref` chains. Incorrect validation when `$dynamicRef` resolution is implemented as static lookup. Complete rewrite of the schema compilation phase.

**Prevention:**
- Resolve ALL `$ref` and `$dynamicRef` references at schema construction time (not at validation time). Compile the schema into a flattened, self-referential graph of schema nodes.
- Detect circular references during schema compilation using a visited-set. Mark recursive schema nodes so the validator can apply depth limits.
- For `$dynamicRef`: precompute the dynamic scope during schema compilation. If full 2020-12 dynamic scoping is needed, maintain an evaluation stack during validation (allocate it once from a pool, not per-node).
- Consider deferring `$dynamicRef`/`$dynamicAnchor` to a later phase. Most real-world schemas use only static `$ref`. Ship with `$ref` support first.
- Set a max `$ref` resolution depth (e.g., 64) to prevent stack overflow on malicious schemas.

**Detection:** JSON Schema Test Suite (official, from `json-schema-org/JSON-Schema-Test-Suite`) contains specific test cases for recursive `$ref`, `$dynamicRef`, and circular schemas.

**Phase:** Schema compilation phase (early). The internal schema representation must be designed from the start to support pre-resolved references.

---

### Pitfall 5: Offset Table Allocation Strategy

**What goes wrong:** The offset table maps property names to byte positions. If implemented as a `Dictionary<string, ParsedProperty>`, every entry allocates: the string key is heap-allocated, and the dictionary itself allocates buckets and entries arrays. Even if you use `Dictionary<int, ParsedProperty>` with hashed keys, the dictionary's internal arrays are heap-allocated.

**Why it happens:** Dictionary is the obvious choice for key-value lookup. Developers reach for it without considering that the schema constrains the problem: the maximum number of properties is known at schema construction time.

**Consequences:** The offset table becomes the single largest source of allocations. A 20-property JSON object allocates 20 strings for keys plus dictionary internals.

**Prevention:**
- Since the schema is known, the maximum property count is deterministic. Pre-allocate a fixed-size array or `Span<T>` at schema construction time.
- Use a schema-aware indexing strategy: assign each schema property a sequential index (0..N-1) at schema construction time. The offset table becomes `Span<ParsedProperty>` or a fixed-size `stackalloc` array indexed by property index.
- For property name lookup during parsing, use `Utf8JsonReader.ValueTextEquals()` against precomputed UTF-8 byte arrays for each schema property, then map to the property index.
- For nested objects, compute the total property count across all levels at schema construction time and allocate one flat buffer.
- Use `ArrayPool<ParsedProperty>.Shared.Rent()` for the backing store if `stackalloc` exceeds safe stack limits (keep stackalloc under ~1KB).

**Detection:** `GC.GetAllocatedBytesForCurrentThread()` delta test. BenchmarkDotNet showing `Gen0` collections.

**Phase:** Phase 1 (core data structures). The offset table design is foundational -- everything else builds on it.

---

### Pitfall 6: Error Collection Without Allocation

**What goes wrong:** The requirement to collect ALL validation errors (up to 64) while maintaining zero allocation is a tension point. Naive approaches use `List<ValidationError>` which allocates on the heap, resizes dynamically, and stores references to heap-allocated error objects.

**Why it happens:** "Collect all errors" and "zero allocation" are in direct tension. Most validation libraries pick one or the other.

**Consequences:** Either you allocate per error (breaking invariant 1) or you fail fast on first error (breaking invariant 5). Neither is acceptable.

**Prevention:**
- Pre-allocate a fixed-capacity error buffer. Since the maximum is 64 (configurable), use `stackalloc ValidationError[64]` if the struct is small enough, or rent from `ArrayPool<ValidationError>`.
- `ValidationError` must be a small `readonly struct`. Keep it to: error code (enum/int), schema property index (int), and a precomputed path reference (index into schema's path table). No strings.
- Error messages should be computed lazily: store the error code and property index during parsing, format the human-readable message only when the caller iterates the errors after parsing completes.
- The path (RFC 6901 JSON Pointer) should be precomputed from the schema at construction time (invariant 3 already specifies this). During parsing, reference the precomputed path by index, not by building a string.

**Detection:** Allocation benchmarks with payloads that produce many validation errors (invalid payloads with 64+ problems).

**Phase:** Phase 1 (error types) and Phase 2 (validation integration). The `ValidationError` struct shape must be designed allocation-free from the start.

---

## Moderate Pitfalls

### Pitfall 7: Unicode Edge Cases in JSON

**What goes wrong:** JSON allows `\uXXXX` escape sequences including surrogate pairs (`\uD800\uDC00` for characters outside the Basic Multilingual Plane). Incorrect handling causes: (a) accepting invalid surrogates as valid JSON, (b) miscalculating byte offsets when the parser encounters multi-byte UTF-8 sequences, (c) off-by-one errors in `ParsedProperty` offset/length values.

**Prevention:**
- Rely on `Utf8JsonReader` for all JSON tokenization -- it handles Unicode validation correctly per RFC 8259.
- When storing byte offsets, store the raw byte positions as reported by `Utf8JsonReader.TokenStartIndex` and `Utf8JsonReader.BytesConsumed`. Do not attempt manual byte arithmetic on UTF-8 sequences.
- Test with the Unicode edge cases: BOM (must be stripped before passing to `Utf8JsonReader`), surrogates, multi-byte sequences, null bytes in strings.

**Phase:** Parsing phase. Add specific Unicode test cases to the test suite early.

---

### Pitfall 8: allOf/anyOf/oneOf Breaks Simple Single-Pass Assumptions

**What goes wrong:** `allOf` requires validating one value against multiple subschemas. `oneOf` requires knowing that exactly one subschema matches (must try all). `anyOf` can short-circuit but must still track which subschemas matched for annotation collection. These composition keywords mean the parser can't just "check and move on" -- it may need to evaluate the same JSON value against multiple schemas.

**Prevention:**
- For `allOf`: merge constraints at schema compilation time where possible. For example, `allOf: [{type: "object", properties: {a: ...}}, {required: ["a"]}]` can be flattened into a single schema node with merged constraints.
- For `oneOf`: during validation, evaluate each subschema and count matches. This is O(N) per value where N is the number of subschemas. Accept this cost but avoid allocating per-subschema evaluation.
- For `anyOf`: short-circuit on first match when not collecting annotations.
- Pre-flatten composition keywords during schema compilation to reduce runtime branching.
- Use a pre-allocated `Span<bool>` (one slot per subschema) to track match results during `oneOf`/`anyOf` evaluation.

**Phase:** Schema compilation and validation phases. Design schema nodes to support merged constraints from the start.

---

### Pitfall 9: Buffer Lifetime and Use-After-Free

**What goes wrong:** `ParsedProperty` holds a `byte[]` reference with offset/length into the caller-owned buffer. If the caller reuses or releases the buffer (e.g., returns it to `ArrayPool`), all `ParsedProperty` values become dangling references. Calling `GetString()` on a property whose underlying buffer has been recycled returns garbage or throws.

**Prevention:**
- Document buffer lifetime requirements prominently in the API surface: "The byte buffer must outlive all `ParsedProperty` instances."
- Consider a `ParsedData` wrapper that holds the buffer reference and implements `IDisposable`, making ownership explicit.
- In Debug builds, consider adding a disposed flag that `GetString()` checks, throwing a clear error instead of returning garbage.
- Do NOT copy the buffer to "solve" this -- that violates the zero-allocation design. Buffer ownership is the caller's responsibility (invariant 2).

**Phase:** Phase 1 (API design). The ownership model must be clear in the public API.

---

### Pitfall 10: stackalloc Size Limits and Stack Overflow

**What goes wrong:** `stackalloc` allocates on the stack, which is limited to ~1MB (default on 64-bit .NET). A deeply nested JSON schema could require a large offset table. Allocating too much with `stackalloc` causes `StackOverflowException`, which is unrecoverable -- the process terminates.

**Prevention:**
- Use a threshold pattern: `stackalloc` for small schemas (e.g., <= 32 properties), `ArrayPool<T>.Shared.Rent()` for larger ones.
  ```csharp
  Span<ParsedProperty> table = propertyCount <= 32
      ? stackalloc ParsedProperty[propertyCount]
      : (rentedArray = ArrayPool<ParsedProperty>.Shared.Rent(propertyCount));
  ```
- Account for recursion depth. Each nested object may stackalloc its own buffer. A JSON document nested 20 levels deep with 32 properties each could use 20 * 32 * sizeof(ParsedProperty) of stack space.
- Set a maximum nesting depth (e.g., 64, matching `Utf8JsonReader`'s default `MaxDepth`).
- Always return rented arrays in a `finally` block.

**Phase:** Parsing phase. Choose the threshold based on benchmarking sizeof(ParsedProperty) * max nesting.

---

### Pitfall 11: Utf8JsonReader Is a ref struct

**What goes wrong:** `Utf8JsonReader` is a `ref struct` (stack-only). It cannot be stored in fields, passed to async methods, used in iterators, or boxed. This constrains the parser architecture significantly. You cannot, for example, create a `class JsonValidator` with a `Utf8JsonReader` field, or use `async` anywhere in the parse pipeline.

**Why it happens:** The project already rejected `ref struct` for its own types (ADR 8) due to async incompatibility. But `Utf8JsonReader` is the best zero-allocation JSON tokenizer in .NET, and it IS a `ref struct`. You must use it within its constraints.

**Prevention:**
- The entire parse method must be synchronous and stack-based. Accept this: parsing raw bytes should not need to be async.
- Pass `Utf8JsonReader` by `ref` through the call stack. Do not try to abstract it behind an interface.
- If the public API needs to support `async` callers (ASP.NET), make parsing synchronous internally and let callers `await Task.Run()` if needed. Do not make parsing itself async.
- Structure the validator as static methods or methods on a `readonly struct` that accept `ref Utf8JsonReader` -- not as a class with a reader field.

**Phase:** Phase 1 (architecture). This constraint shapes the entire parser design.

---

## Minor Pitfalls

### Pitfall 12: record struct ToString() Allocation

**What goes wrong:** `record struct` auto-generates a `ToString()` that allocates strings via interpolation/concatenation. Debuggers and logging frameworks call `ToString()` implicitly. ADR 8 already rejected `record struct` for this reason, but a developer might use one for a "helper" type on the parse path.

**Prevention:** Enforce `readonly struct` (not `record struct`) for all types on the parse path. Custom `ToString()` overrides that return a string literal or use `stackalloc` + `string.Create()`.

**Phase:** Coding convention. Establish in Phase 1.

---

### Pitfall 13: String Interning Trap

**What goes wrong:** Developers may attempt `string.Intern()` for property names to avoid repeated allocations. While interning prevents duplicate allocations, the initial `string.Intern()` call still allocates, and interned strings live forever (never garbage collected), causing memory leaks proportional to schema diversity.

**Prevention:** Do not intern strings. Use precomputed UTF-8 byte arrays for property names (allocated once at schema construction, outside the parse path). Compare using `Utf8JsonReader.ValueTextEquals(ReadOnlySpan<byte>)`.

**Phase:** Phase 1 (property name matching design).

---

### Pitfall 14: JSON Number Precision Loss

**What goes wrong:** JSON allows arbitrary-precision numbers. `double` loses precision beyond 15-17 significant digits. `decimal` handles 28-29 digits but is slow. JavaScript's `Number.MAX_SAFE_INTEGER` (2^53) is a common practical limit, but JSON Schema imposes no such limit. A validator that parses numbers as `double` may incorrectly validate `minimum`/`maximum` constraints for large integers.

**Prevention:**
- For validation constraints (`minimum`, `maximum`, `multipleOf`): compare as raw bytes when possible (integer comparison via byte parsing). Use `Utf8JsonReader.TryGetInt64()` first, fall back to `TryGetDouble()`, and document precision limitations.
- Store the raw byte offset/length in `ParsedProperty`. Let the caller choose precision at materialization time (`GetInt32()`, `GetInt64()`, `GetDouble()`, `GetDecimal()`).
- For `multipleOf` validation with fractional values, be aware of floating-point rounding. Consider integer arithmetic where the multiplier is a whole number.

**Phase:** Validation phase. Design number validation to operate on raw bytes where possible.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Core type design (ParsedProperty, ValidationError) | Boxing through interfaces (Pitfall 3), record struct ToString (Pitfall 12) | Generic constraints, override ToString/Equals/GetHashCode |
| Offset table design | Dictionary allocations (Pitfall 5), stackalloc overflow (Pitfall 10) | Schema-indexed fixed arrays, threshold pattern |
| Property name matching | String allocations (Pitfall 1), string interning (Pitfall 13) | Precomputed UTF-8 byte arrays, ValueTextEquals() |
| Schema compilation | $ref infinite loops (Pitfall 4), allOf/oneOf merging (Pitfall 8) | Pre-resolve at construction, circular detection, flatten where possible |
| Validation logic | Closure/LINQ allocations (Pitfall 2), error collection (Pitfall 6) | Ban LINQ on parse path, pre-allocated error buffer |
| Parser architecture | Utf8JsonReader ref struct constraint (Pitfall 11), buffer lifetime (Pitfall 9) | Synchronous parse, pass reader by ref, document ownership |
| Number validation | Precision loss (Pitfall 14) | Raw byte comparison, TryGetInt64 before TryGetDouble |
| Unicode handling | Surrogate pairs, BOM, multi-byte offsets (Pitfall 7) | Rely on Utf8JsonReader, store raw TokenStartIndex |

## Sources

- [dotnet/runtime#54410: Non-allocating string view for Utf8JsonReader](https://github.com/dotnet/runtime/issues/54410) -- confirms no zero-alloc unescaping exists
- [Utf8JsonReader documentation (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader) -- ValueTextEquals, ValueIsEscaped, MaxDepth
- [Utf8JsonReader.ValueSpan (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader.valuespan) -- ValueSpan vs ValueSequence behavior
- [Building Zero-Allocation Parsers in C# .NET (Medium)](https://jordansrowles.medium.com/building-zero-allocation-parsers-in-c-net-348ce6d124f1)
- [Hidden Performance Traps in C# (Medium)](https://medium.com/@orbens/the-hidden-performance-traps-in-c-youre-probably-ignoring-8d7c24f5519a) -- closure and boxing allocations
- [Dos and Don'ts of stackalloc (vcsjones.dev)](https://vcsjones.dev/stackalloc/) -- stack size limits, initialization
- [JSON Schema Structuring (json-schema.org)](https://json-schema.org/understanding-json-schema/structuring) -- $ref resolution
- [JSON Schema 2020-12 Core Spec](https://json-schema.org/draft/2020-12/json-schema-core) -- $dynamicRef, $dynamicAnchor
- [Validation of Modern JSON Schema: Formalization (HAL)](https://hal.science/hal-04042629/document) -- PSPACE-hardness of $dynamicRef
- [Optimising .NET code: Hunting for allocations (endjin)](https://endjin.com/blog/2023/09/optimising-dotnet-code-2-hunting-for-allocations)
- [Pipeline and closure allocations (Particular Software)](https://particular.net/blog/pipeline-and-closure-allocations) -- 5x improvement by removing closures
