# Phase 5: Basic Validation - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Core JSON Schema keyword validators (type, enum, const, required, properties, additionalProperties, items, prefixItems) and the error collection pipeline connecting validators to ErrorCollector. Covers VALD-01 through VALD-05 and VALD-17. Numeric/string/size constraints are Phase 6. Composition/conditionals are Phase 7. The single-pass walker that orchestrates these validators is Phase 9.

</domain>

<decisions>
## Implementation Decisions

### Validator Structure
- Single internal static class (KeywordValidator) with methods like ValidateType(), ValidateRequired(), ValidateEnum() etc.
- Lives in Gluey.Contract.Json package — validators consume JsonByteReader tokens and SchemaNode, they're JSON-specific
- Standalone functions with no recursion — each validator checks its keyword only (e.g., "is this token the right type?"), the walker (Phase 9) handles traversal and decides which validators to call at each node
- Phases 6-8 add more static classes (e.g., NumericValidator, StringValidator) following the same pattern

### Integer vs Number Detection
- Spec-strict: mathematical integer, not lexical — `1.0` is a valid integer per JSON Schema Draft 2020-12
- Use Utf8JsonReader.TryGetInt64() to determine integer-ness — leverages BCL's battle-tested parsing, handles scientific notation (1e2 = 100 = integer)
- Integers beyond Int64 range fail as non-integer — pragmatic limit, covers -9.2x10^18 to 9.2x10^18, virtually all real API payloads fit

### Enum/Const Comparison
- Spec-compliant: JSON value equality applies — `1` equals `1.0` for numeric values
- Byte-first with numeric fallback: try byte-exact match first (fast path, zero-alloc), if no match AND the token is a number, parse both sides as decimal and compare values
- Non-numeric types (strings, booleans, null): byte-exact comparison is always correct per spec
- Full support for structured values (objects/arrays) in enum/const — not just scalars

### Error Pipeline
- Direct push: each ValidateX() method receives the ErrorCollector as a parameter and calls collector.Add() directly when validation fails
- Validators return bool indicating pass/fail — enables short-circuiting (e.g., if type fails, skip type-dependent keywords)
- ErrorCollector.Add() handles overflow internally (no-op when full, sentinel already placed) — validators never check IsFull

### Testing Strategy
- Unit test each validator method directly — pass SchemaNode + token info + ErrorCollector to each ValidateX() method
- No dependency on JsonByteReader or JSON parsing in tests — fast, isolated
- Walker integration tested in Phase 9

### Claude's Discretion
- ValidateX() method signatures (exact parameter types beyond SchemaNode + ErrorCollector)
- How structured enum/const values (objects/arrays) are extracted and compared from the byte stream
- Internal helpers for byte-level comparison and numeric parsing
- Method organization within KeywordValidator class

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ValidationErrorCode.cs`: Full enum already defined with Phase 5 codes — TypeMismatch, EnumMismatch, ConstMismatch, RequiredMissing, AdditionalPropertyNotAllowed, ItemsInvalid, PrefixItemsInvalid
- `ValidationError.cs`: Readonly struct with path, code, static message — ready to construct
- `ValidationErrorMessages.cs`: Static message lookup per error code
- `ErrorCollector.cs`: ArrayPool-backed, Add() method, max capacity with sentinel overflow
- `SchemaNode.cs`: All keyword fields present — Type, Enum (byte[][]), Const (byte[]), Required (string[]), Properties (Dictionary<string, SchemaNode>), AdditionalProperties (SchemaNode?), Items (SchemaNode?), PrefixItems (SchemaNode[]?)
- `SchemaType.cs`: Flags enum with all seven JSON Schema types
- `JsonByteReader.cs`: Internal ref struct with Read() loop, JsonByteTokenType, ByteOffset, ByteLength

### Established Patterns
- Utf8JsonReader with ValueTextEquals and u8 literals for zero-alloc matching (Phase 2 loader)
- readonly struct for public types, internal sealed class for tree nodes (ADR 8)
- Internal visibility for implementation details (JsonByteReader, SchemaNode)
- No external dependencies in core (ADR 7); System.Text.Json is BCL, acceptable in JSON package

### Integration Points
- Phase 9 (Single-Pass Walker) is the primary consumer — calls ValidateX() methods during traversal
- ErrorCollector passed through from walker to validators — same instance for entire parse
- SchemaNode.Path provides precomputed RFC 6901 paths for ValidationError construction
- JsonByteReader provides token type, byte offset, and byte length per token

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

*Phase: 05-basic-validation*
*Context gathered: 2026-03-09*
