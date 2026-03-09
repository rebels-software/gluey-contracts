# Phase 2: Schema Model - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

JSON Schema documents can be loaded and compiled into an immutable, indexed schema tree with precomputed RFC 6901 paths and property index assignment. Covers SCHM-01 (loading), SCHM-02 (immutable tree with paths), SCHM-05 (property index assignment). Reference resolution ($ref/$defs/$anchor) is Phase 3.

</domain>

<decisions>
## Implementation Decisions

### Schema Loading API
- Static factory methods on JsonContractSchema: `TryLoad` (bool + out) and `Load` (returns nullable)
- Dual API mirrors TryParse/Parse pattern from Phase 1 — consistent philosophy across the library
- Accept `ReadOnlySpan<byte>` for UTF-8 bytes and `string` for JSON text — two overloads per method
- Use System.Text.Json's Utf8JsonReader internally to parse the JSON Schema document — BCL dependency, no external packages
- Schema loading is a one-time setup cost; zero-allocation invariant applies to parse path, not schema loading

### SchemaNode Design
- Internal sealed class — not part of public API, free to refactor internals
- Lives in `Gluey.Contract` (core package) — schema model is format-agnostic per ADR 5
- JSON Schema loader lives in `Gluey.Contract.Json` — parses JSON Schema format into the core SchemaNode tree
- Immutable by design — all properties set in constructor, never mutated after construction
- Class (not readonly struct) because tree nodes reference children; allocated once at load time, not on parse path

### Property Index Assignment
- Depth-first traversal order for ordinal assignment — natural for single-pass walker descent
- JSON Pointer paths as dictionary keys for name-to-ordinal mapping (e.g., `"/address/street"` → ordinal 2)
- Only named object properties get ordinals — arrays tracked as a whole, individual elements resolved at parse time by walker (Phase 9)
- JsonContractSchema exposes a `PropertyCount` for pre-sizing the offset table

### Keyword Modeling Scope
- All JSON Schema Draft 2020-12 keywords defined as fields on SchemaNode upfront (~30 nullable fields)
- Loader parses ALL keywords from JSON during Load — validation phases just consume what's already on the node
- $ref string and $defs map read and stored in Phase 2; resolution logic (cycle detection, pointer following) deferred to Phase 3
- Unknown/unrecognized keywords silently ignored — standard JSON Schema behavior

### Claude's Discretion
- Exact SchemaNode field types and nullability strategy (nullable value types vs sentinel values)
- Internal organization of the JSON Schema loader (single class vs per-section helpers)
- SchemaType enum values and how multi-type schemas are represented
- How precomputed path strings are allocated and shared across nodes

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `JsonContractSchema.cs`: Existing class stub with TryParse/Parse methods — TryLoad/Load factory methods added here
- `ParseResult.cs`: Uses `Dictionary<string, int>` nameToOrdinal — schema must produce this mapping
- `OffsetTable.cs`: ArrayPool-backed storage sized by schema's PropertyCount
- `ValidationErrorCode.cs`: Full enum already defined — schema loading errors may need codes

### Established Patterns
- C# 13, nullable enabled, implicit usings
- readonly struct for core value types (ADR 8) — but SchemaNode is internal sealed class (not on parse path)
- XML doc comments on public types
- No external dependencies in core package (ADR 7) — System.Text.Json is BCL, acceptable in JSON driver

### Integration Points
- SchemaNode tree consumed by validation phases (5-8) — keyword fields read during validation
- PropertyCount used by Phase 9 walker to pre-size OffsetTable
- Name-to-ordinal Dictionary<string, int> passed to ParseResult constructor
- Precomputed RFC 6901 path strings referenced by ValidationError during parse
- $ref/$defs fields on SchemaNode consumed by Phase 3 reference resolution

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

*Phase: 02-schema-model*
*Context gathered: 2026-03-09*
