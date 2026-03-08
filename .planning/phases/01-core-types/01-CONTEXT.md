# Phase 1: Core Types - Context

**Gathered:** 2026-03-08
**Status:** Ready for planning

<domain>
## Phase Boundary

All foundational readonly struct value types: ParsedProperty, OffsetTable, ValidationError, ErrorCollector, ParseResult, and the dual TryParse/Parse API surface. These types establish the shapes that all downstream phases produce and consume — no layout changes after this phase.

</domain>

<decisions>
## Implementation Decisions

### Value Access API
- ParsedProperty stores a reference to the byte buffer (byte[] + offset + length + path) — self-contained, no buffer threading
- Type mismatch on GetX() returns default(T) — zero branching, caller trusts schema validation
- GetString() allocates a new string each call — allocation happens at materialization, outside the parse path (zero-allocation invariant preserved)
- No TryGetX() variants — only GetString(), GetInt32(), GetInt64(), GetDouble(), GetBoolean(), GetDecimal()
- Expose RawBytes as ReadOnlySpan<byte> for advanced users who want to avoid materialization entirely

### Error Code Design
- One enum value per JSON Schema keyword (~25-30 values): TypeMismatch, RequiredMissing, MinimumExceeded, MaxLengthExceeded, PatternMismatch, etc.
- Full enum defined upfront in Phase 1 covering all keywords in the roadmap — no breaking changes across phases
- Static compile-time string messages per error code — no string interpolation, no runtime context in messages
- JSON Pointer path stored as pre-allocated string reference from the schema (invariant 3) — zero allocation

### ParseResult Consumer Experience
- Both string indexer (result["name"]) and ordinal indexer (result[0]) for property access
- String indexer for ergonomics, ordinal for perf-critical paths — both resolve to the same offset table
- Missing/absent property returns an empty ParsedProperty (length 0, HasValue = false) — no exceptions
- Errors collection always accessible via ParseResult.Errors — empty on success, populated on failure (uniform API)
- Supports foreach enumeration of all parsed properties via GetEnumerator()

### Error Overflow Behavior
- When ErrorCollector hits max capacity, error #64 (last slot) is replaced with a sentinel TooManyErrors entry
- Max error count configurable per schema (default 64) — set at schema configuration time, shared across all parses
- ErrorCollector uses ArrayPool<ValidationError>.Shared for pre-allocated buffer — consistent with OffsetTable approach
- ErrorCollector implements IDisposable to return ArrayPool buffer

### Claude's Discretion
- Exact struct field ordering and padding for cache-line optimization
- Internal implementation of string-to-ordinal lookup in offset table
- GetEnumerator() implementation details (custom struct enumerator vs. other patterns)
- Exact set of ~25-30 error code enum values (named per JSON Schema keyword semantics)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- ParsedProperty.cs: Empty readonly struct stub in Gluey.Contract namespace — ready to implement
- JsonContractSchema.cs: Empty class stub in Gluey.Contract.Json — dual API surface (TryParse/Parse) lives here

### Established Patterns
- C# 13, nullable enabled, implicit usings
- readonly struct for all core value types (ADR 8)
- XML doc comments on public types (established in stubs)
- No external dependencies in core package (ADR 7)

### Integration Points
- ParsedProperty is consumed by all downstream validation and walker phases
- OffsetTable is built by Phase 9 (Single-Pass Walker) but type defined here
- ValidationError + ErrorCollector feed into every validation phase (5-8)
- ParseResult is the public return type from JsonContractSchema.TryParse/Parse (Phase 9 integration)
- Error code enum referenced by all validation keyword implementations

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-core-types*
*Context gathered: 2026-03-08*
