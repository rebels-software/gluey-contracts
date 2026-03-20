# Phase 3: Scalar Parsing - Context

**Gathered:** 2026-03-20
**Status:** Ready for planning

<domain>
## Phase Boundary

First end-to-end binary parse pipeline. `BinaryContractSchema.Parse(byte[])` returns `ParseResult` with scalar fields (uint8/16/32, int8/16/32, float32/64, boolean) accessible via `parsed["fieldName"].GetXxx()`. Non-scalar fields (strings, enums, bits, arrays, structs, padding) are skipped — those are Phase 4+. Validation is Phase 6. This phase proves the full round-trip: load contract, parse payload, access values.

</domain>

<decisions>
## Implementation Decisions

### Non-scalar field handling
- Parse() walks all OrderedFields but only populates scalar types (uint8/16/32, int8/16/32, float32/64, boolean) in the OffsetTable
- Non-scalar fields are left as ParsedProperty.Empty — their ordinal slots exist but are not populated
- The parser still advances past fixed-size non-scalar fields to keep subsequent scalar offsets correct (AbsoluteOffset from chain resolution handles this)
- When IsDynamicOffset is true (fields after a semi-dynamic array), stop parsing — skip that field and all subsequent ones. Arrays are Phase 5

### Truncated numeric semantics
- Signed truncated integers use MSB sign-extension: read N bytes, check MSB of first byte (big-endian) or last byte (little-endian), fill remaining upper bytes with 0xFF if sign bit set
- Unsigned truncated integers use zero-padding: read N bytes, place in lower bytes of target width
- Example: int32 with size 3, big-endian bytes [0xFF, 0xCF, 0xC7] sign-extends to int32 -12345

### Accessor type strictness
- GetInt32() on a uint32 field throws InvalidOperationException — accessor must match the contract-declared type
- GetUInt16() on a uint16 field works; GetInt32() on a uint16 field throws
- This requires storing the contract-declared field type on ParsedProperty (or accessible from it) so the accessor can validate the call
- Same principle applies to all GetXxx() methods: the accessor must correspond to the field's declared type

### Parse API surface
- Two overloads: Parse(byte[]) and Parse(ReadOnlySpan<byte>) — mirrors JsonContractSchema exactly
- Span overload is the real implementation; byte[] delegates to it
- Returns ParseResult? — null means payload is shorter than the contract's TotalFixedSize (structurally invalid)
- parsed["fieldName"] returns ParsedProperty, consumer calls GetXxx() — identical to JSON access pattern

### Zero-allocation parse path
- Parse is a simple loop over OrderedFields inside BinaryContractSchema — no ref struct walker needed (binary is sequential, not recursive like JSON)
- Each ParsedProperty references the original payload byte[] (zero-copy) with (buffer, offset, length, path, format=binary, endianness)
- Consumer must keep payload alive while using ParseResult (same contract as JSON)
- OffsetTable and ErrorCollector are ArrayPool-backed, returned via IDisposable — same pattern as JSON ParseResult
- No tokenizer, no recursive descent — just iterate ordered fields and populate OffsetTable slots

### Claude's Discretion
- Internal method organization within BinaryContractSchema.Parse()
- How to propagate field type metadata to ParsedProperty for accessor validation (new byte field, enum, or lookup table)
- Exact error message text for InvalidOperationException on type mismatch
- Test contract JSON structure (pure-scalar contracts, not the full battery contract)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Binary contract specification
- `docs/adr/16-binary-format-contract.md` — Full contract JSON format, supported types (see "Supported types" table), truncated numeric semantics, endianness rules, dependency chain model

### Reference implementation (JSON side)
- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — Parse() method pattern to mirror (lines 163-202), overload structure, null return for structural errors
- `src/Gluey.Contract/Parsing/ParseResult.cs` — Return type, IDisposable pattern, OffsetTable + ErrorCollector + nameToOrdinal wrapping
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` — Binary read paths already implemented (GetInt32 lines 227-253, GetInt64 lines 260-288, GetDouble lines 295-319, GetBoolean lines 327-333), binary constructor (line 118)

### Binary contract model (Phase 2 output)
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` — TryLoad/Load API, OrderedFields array, TotalFixedSize, NameToOrdinal dictionary. Parse() method to be added here
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` — Field descriptor with AbsoluteOffset, ResolvedEndianness, IsDynamicOffset, Type, Size, Name

### Shared infrastructure
- `src/Gluey.Contract/Parsing/OffsetTable.cs` — ArrayPool-backed ordinal-indexed property storage, Set() and indexer
- `src/Gluey.Contract/Validation/ErrorCollector.cs` — ArrayPool-backed error collection (needed for Phase 6, but rent/dispose pattern needed now)

### Phase 2 context (prior decisions)
- `.planning/phases/02-contract-model/02-CONTEXT.md` — Contract loading API decisions, validation error reporting, internal model shape

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ParsedProperty` binary constructor and GetXxx() methods: Already handle endianness-aware reads via BinaryPrimitives for 1/2/4/8-byte integers and floats
- `OffsetTable`: Ready to use — Set(ordinal, property) and indexer access
- `ErrorCollector`: Rent/dispose pattern established — needed even if no validation errors in Phase 3
- `BinaryContractSchema.OrderedFields`: Pre-resolved field array with AbsoluteOffset, ResolvedEndianness, Size per field
- `BinaryContractSchema.NameToOrdinal`: Maps field names to ordinal indices for ParseResult construction
- `BinaryContractSchema.TotalFixedSize`: Precomputed contract byte size (-1 if dynamic) — direct payload length check

### Established Patterns
- Factory method on schema: `schema.Parse(data)` — Parse is an instance method on the loaded schema object
- Null return for structural error: JSON returns null for malformed input, binary returns null for too-short payload
- IDisposable ParseResult: Consumer wraps in `using` to return pooled buffers
- ParsedProperty as readonly struct with deferred materialization: Values read only when GetXxx() called

### Integration Points
- `BinaryContractSchema` already has all infrastructure except Parse() — the method body is the main deliverable
- `ParseResult` constructor: `internal ParseResult(OffsetTable, ErrorCollector, Dictionary<string, int>)` — binary uses same constructor
- Test project `Gluey.Contract.Binary.Tests` exists with ContractLoadingTests — add parsing tests alongside

</code_context>

<specifics>
## Specific Ideas

- Test contracts should be pure-scalar only (no strings, enums, bits, arrays) — clean isolation for Phase 3
- The battery contract from ADR-16 will be used as an integration test in later phases, not Phase 3
- Accessor type strictness is a deliberate design choice: the contract is the source of truth for what type a field is, and the consumer must respect it

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-scalar-parsing*
*Context gathered: 2026-03-20*
