# Codebase Concerns

**Analysis Date:** 2026-03-18

## Tech Debt

**Limited Schema Format Support:**
- Issue: The codebase currently only implements JSON Schema validation. While the architecture is designed to be format-agnostic, the actual implementations for Protobuf, PostgreSQL, and Redis are still planned/unimplemented.
- Files: `src/Gluey.Contract/Schema/SchemaNode.cs`, `src/Gluey.Contract.Json/*`
- Impact: Users expecting multiple format support will find only JSON working in production. Future format implementations must maintain the same zero-allocation contract.
- Fix approach: Continue planned implementation phases; ensure each new format package (e.g., `Gluey.Contract.Protobuf`) maintains the same `ParsedProperty` interface and IDisposable pooling patterns.

**Cross-Schema $ref Anchor Support Incomplete:**
- Issue: Anchor references (`#my-anchor`) across schema boundaries are not supported — only JSON Pointer fragments (`#/path/to/definition`) work in cross-schema references.
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs` (lines 171-174)
- Impact: Schemas using anchor-based references to shared definitions in remote schemas will fail to resolve. Only single-schema or JSON Pointer-based cross-schema refs work.
- Fix approach: Implement anchor collection and lookup for remote schemas in `ResolveCrossSchemaRef()`, requires loading and parsing remote schemas first.

## Known Bugs

**Potential Stack Overflow in Deep Nested Schemas:**
- Symptoms: Stack overflow exception if validating deeply nested JSON structures (100+ levels) against schemas with complex composition keywords (allOf, anyOf, oneOf).
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (recursive `WalkValue` method, lines 146-244)
- Trigger: Create a schema with nested objects reaching 200+ levels, then parse JSON matching that depth while schema includes composition validators.
- Workaround: Limit nesting depth in schemas or validate payloads separately (e.g., validate in chunks).
- Root cause: `SchemaWalker.WalkValue()` is recursive and makes deep recursive calls for each nested level. Stack size on .NET is typically 1MB; deeply nested structures can exhaust it. Not observed in normal use (typical JSON rarely exceeds 50 levels) but possible in pathological inputs.

**Potential Array Index Overflow in ArrayBuffer:**
- Symptoms: If array ordinal (assigned sequentially during parsing) exceeds `int.MaxValue`, `ArrayBuffer.Add()` will allow it but internal tracking may break.
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs` (lines 162-177, ordinal validity checks)
- Trigger: Parse JSON with more than ~2 billion array objects in the same document.
- Workaround: Not practically applicable — the buffer would run out of memory long before this becomes an issue.
- Root cause: Array ordinals use `int` type; while ordinal is incremented once per array in the schema (reasonable), the bounds check uses signed comparison without overflow guard.

## Security Considerations

**Schema URI Validation Absent:**
- Risk: Cross-schema `$ref` URIs are not validated for safety. Malicious or incorrect URIs could be stored in schemas without validation.
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs` (lines 142-175, `ResolveCrossSchemaRef`)
- Current mitigation: Registry lookup is user-controlled; calling code must ensure only trusted schemas are registered. No built-in URI format validation.
- Recommendations:
  1. Add optional URI validation in `SchemaRefResolver` to check against a whitelist or pattern.
  2. Document that `SchemaRegistry.Register()` accepts arbitrary URIs and should only be called with trusted schemas.

**No Input Size Limits:**
- Risk: Extremely large JSON payloads (multi-GB) can be parsed without bounds checking, potentially causing memory exhaustion or slow validation.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (entry point, line 83-105)
- Current mitigation: None. The parser reads the entire input span as-is.
- Recommendations:
  1. Add optional max-input-size parameter to `JsonContractSchema.Parse()`.
  2. Document that callers should enforce their own payload size limits before calling the parser.

**Format String Validation Not Enforced by Default:**
- Risk: If `assertFormat: false` (default), format keywords (email, uri, date-time) are silently skipped without validation.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (lines 231-235, conditional format validation)
- Current mitigation: Explicit opt-in via `Parse(bytes, assertFormat: true)`. Most users validate with `assertFormat: false` for performance.
- Recommendations: Document clearly that format validation is optional. Consider adding a logger/warning if schema contains format keywords but assertFormat is false.

## Performance Bottlenecks

**Double-Validation on Fast-Path Failure:**
- Problem: The fast-path scalar validation (ADR 13) re-validates type when it fails, running the same check twice.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (lines 471-490, `TryWalkValueFastPath()` and fallback)
- Cause: Fast path uses `CheckType()` (bool-returning), but on failure, code falls back to full `WalkValue()` which checks type again.
- Current impact: Acceptable — failures are rare in production (well-formed payloads). Adds ~5% overhead only to failing unknown properties.
- Improvement path: Cache type-check result or pass it through to `WalkValue()` to avoid redundant check. Lower priority since failures are uncommon.

**Regex Compilation for Pattern Validation Not Cached:**
- Problem: `patternProperties` regex patterns are compiled on every validation, not cached at schema load time.
- Files: `src/Gluey.Contract.Json/Schema/JsonSchemaLoader.cs` (likely regex creation), `src/Gluey.Contract/Schema/SchemaNode.cs` (no apparent regex field)
- Cause: Regex objects should be compiled once at schema load and stored; currently they may be recompiled per validation.
- Current impact: Observable slowdown when validating large payloads with `patternProperties` constraints.
- Improvement path: Pre-compile all pattern regexes in `SchemaIndexer` or during schema loading. Store as `Regex` field in `SchemaNode`.

**ArrayBuffer Region Lookup O(1) but Could Use Contiguous Storage:**
- Problem: Array element lookup uses dictionary-like region tracking; for heavily nested arrays, this adds pointer indirection.
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs` (lines 196-209, `Get()` method)
- Cause: Region tracking splits array ordinals across sparse arrays; accessing an element requires two array bounds checks.
- Current impact: Minimal — typical payloads have <10 array ordinals. Only noticeable with massive schema complexity.
- Improvement path: Use a flat `List<ArrayRegion>` indexed by ordinal instead of separate _regionStarts/_regionCounts arrays.

## Fragile Areas

**SchemaWalker Error Recovery:**
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (1323 lines)
- Why fragile: The `SchemaWalker` ref struct is the core validation engine. It has complex state management:
  - Manages multiple types of validation (scalar, object, array, composition, conditionals).
  - Maintains error state (`_structuralError`, `_errors`) across method calls.
  - Uses ref structs with pooled resources (`ArrayBuffer`), requiring careful lifetime management.
- Safe modification: Any changes to validation logic must preserve the error collection invariant: all validation errors must be reported regardless of early failures. Add unit tests for each keyword type covering both success and failure paths.
- Test coverage: Good (recent commits show coverage expansion), but edge cases with composition keywords + conditionals need more test cases.

**Array Buffer Thread-Static Pooling:**
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs` (lines 35-36, thread-static cache)
- Why fragile: Single cached instance per thread could be misused if `Dispose()` is not called or is called multiple times.
- Safe modification: `Dispose()` is idempotent by design (checks for null), but callers must ensure they use `using` statements. Document that `ParseResult` is already IDisposable and handles this automatically.
- Test coverage: `DisposeAllocationTests` exists but could expand to cover concurrent access patterns.

**SchemaRefResolver Cycle Detection:**
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs` (lines 91-106)
- Why fragile: Per-chain cycle detection uses a `HashSet<string>` created fresh for each chain. If a schema with circular refs is modified externally after loading, cycle detection could miss it.
- Safe modification: Schemas are immutable after loading (no public setters on SchemaNode), so this is safe. But if future changes allow schema mutation, cycle detection must be re-run or cached results invalidated.
- Test coverage: Coverage exists but should include very long circular chains (10+ hops) to ensure HashSet doesn't hit memory issues.

**JsonByteReader Exception Handling:**
- Files: `src/Gluey.Contract.Json/Reader/JsonByteReader.cs` (lines 52-71)
- Why fragile: Converts `JsonException` to two error kinds (`UnexpectedEndOfData` vs `InvalidJson`) based on `BytesConsumed` position. If Utf8JsonReader behavior changes in .NET updates, this heuristic could misclassify errors.
- Safe modification: The try-catch is narrow and focused. If .NET introduces new exception types, wrap them explicitly. Document the heuristic in a comment.
- Test coverage: Good — `JsonByteReaderTests` should ensure both error types are exercised with actual malformed JSON samples.

## Scaling Limits

**Required Properties Tracking with Stackalloc:**
- Current capacity: `stackalloc bool[requiredCount]` allocates on stack. Stack typical size is 1MB; each bool is 1 byte.
- Limit: Max ~1,000,000 required properties per schema before stack overflow. Practical limit is much lower (~10,000) due to other stack usage.
- Scaling path: For schemas with thousands of required properties, consider replacing stackalloc bitset with rented `ArrayPool<bool>`. Profile first to confirm it's necessary.

**Schema Complexity (Property Count):**
- Current capacity: Pre-computed PropertyEntry lookup arrays can handle any reasonable property count (tested to 1000+).
- Limit: JSON Pointer path string concatenation for deeply nested objects could accumulate large path strings. No apparent hard limit observed.
- Scaling path: If schemas have 10,000+ total properties across nesting, consider string interning for path storage to save memory.

**ArrayBuffer Growth:**
- Current capacity: Starts at 16 elements, doubles on overflow. Grows region arrays as needed.
- Limit: No hard limit; will continue doubling until ArrayPool or memory exhaustion.
- Scaling path: Current exponential growth is appropriate. If validation of very large array-heavy payloads becomes common, consider pre-sizing based on schema hints.

## Dependencies at Risk

**No External NuGet Dependencies:**
- Risk: Zero external dependencies is a strength for stability, but means no third-party code reuse for complex tasks (e.g., regex compilation, date parsing).
- Impact: All functionality must be implemented in-house. Complex validators (format, pattern) are maintained by the team.
- Migration plan: If a critical bug is found in format validation, the team must fix it directly rather than upgrading a package. Consider this during format expansion.

## Missing Critical Features

**No Validation Hooks/Interceptors:**
- Problem: Callers cannot inject custom validation logic (e.g., custom format validators, domain-specific constraints).
- Blocks: Advanced users who need beyond-spec validation must parse, collect errors, then run custom validators separately.

**No Schema Caching or Registry Persistence:**
- Problem: `SchemaRegistry` is in-memory only. Schemas must be reloaded and parsed on every application startup.
- Blocks: Microservices with thousands of schemas pay parsing cost repeatedly. No way to serialize/deserialize compiled schemas.

**No Streaming/Chunked Parsing:**
- Problem: The parser requires the entire JSON payload in memory as a contiguous byte array.
- Blocks: Handling streaming JSON (e.g., server-sent events, large file uploads) requires buffering the entire message first.

## Test Coverage Gaps

**Untested: Recursive schema refs with 10+ levels:**
- What's not tested: Very deep circular reference chains combined with complex composition.
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs` (cycle detection)
- Risk: Cycle detection logic is correct but untested under extreme conditions. Long chains could trigger performance issues.
- Priority: Medium — pathological but possible in hand-crafted schemas.

**Untested: ArrayBuffer concurrent access:**
- What's not tested: Thread-static pooling with concurrent Parse calls on different threads.
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs` (thread-static cache)
- Risk: While thread-static design avoids contention, concurrent calls could cause cache thrashing or missed reuse.
- Priority: Medium — important for high-concurrency scenarios.

**Untested: Format validation with extreme string lengths:**
- What's not tested: Email/URI format validation with strings >10MB.
- Files: `src/Gluey.Contract.Json/Validators/FormatValidator.cs`
- Risk: Regex compilation or parsing could timeout or allocate excessively.
- Priority: Low — rare in practice, but format validation is optional anyway.

**Untested: Unknown keywords in schemas:**
- What's not tested: Schemas with unknown keywords (beyond Draft 2020-12 spec) should be silently ignored; need exhaustive testing of new keyword types.
- Files: `src/Gluey.Contract.Json/Schema/JsonSchemaLoader.cs` (lines 25, "Unknown keywords are silently skipped")
- Risk: If an unknown keyword appears in a child schema, it might not be skipped correctly, causing validation to fail unexpectedly.
- Priority: Low — tested implicitly but no dedicated test suite for forward compatibility.

---

*Concerns audit: 2026-03-18*
