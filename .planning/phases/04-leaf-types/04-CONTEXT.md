# Phase 4: Leaf Types - Context

**Gathered:** 2026-03-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Parse all non-composite field types: strings (ASCII/UTF-8), enums (dual-access), bit fields (container + sub-fields), and padding. After this phase, only arrays and structs remain unparsed. Validation is Phase 6 — this phase focuses on correct reading.

</domain>

<decisions>
## Implementation Decisions

### String encoding handling
- **D-01:** Honor the contract's `encoding` field: use `Encoding.ASCII` for `"ASCII"` contracts, `Encoding.UTF8` for `"UTF-8"`. Catches invalid bytes in ASCII fields
- **D-02:** Add a `mode` field to the string type in ADR-16. Supported modes: `plain` (as-is), `trimStart`, `trimEnd`, `trim`. Default when omitted: `trimEnd` (trim trailing null bytes and whitespace)
- **D-03:** ADR-16 must be updated in this phase to include the string `mode` field specification
- **D-04:** Encoding type stored as an extra byte on ParsedProperty (0=UTF-8, 1=ASCII) resolved at load time from `BinaryContractNode.Encoding`

### Enum accessor design
- **D-05:** Two ParsedProperty entries per enum field: base name (e.g., `"mode"`) for raw numeric access, base + "s" (e.g., `"modes"`) for mapped string label
- **D-06:** This is INVERTED from current ADR-16 text — `parsed["mode"].GetUInt8()` returns raw byte value, `parsed["modes"].GetString()` returns mapped label. ADR-16 must be updated
- **D-07:** Enum string resolution is deferred to GetString() call — ParsedProperty stores a reference to the values dictionary, lookup happens lazily. Preserves zero-alloc parse path
- **D-08:** Unmapped enum values: GetString() returns the numeric value as a string (e.g., `"42"`). No exception, no null

### Bit field extraction
- **D-09:** Bit sub-fields use path-based nested access: `parsed["status/isCharging"].GetBoolean()`, `parsed["status/errorCode"].GetUInt8()`
- **D-10:** Container field itself is also accessible: `parsed["status"].GetUInt8()` returns raw container byte(s)
- **D-11:** Bit sub-field values are pre-extracted at parse time — Parse() reads container byte(s), extracts each sub-field immediately, stores as small byte values on individual ParsedProperty entries
- **D-12:** Multi-byte (16-bit) bit containers respect endianness when reading the container value before sub-field extraction

### Padding behavior
- **D-13:** Padding fields create ParsedProperty.Empty entries in OffsetTable — field exists in NameToOrdinal but returns empty
- **D-14:** Padding fields are named (current ADR-16 behavior) — e.g., `"reserved1"`, `"gap"`
- **D-15:** Parse loop recognizes FieldTypes.Padding, creates Empty entry, advances cursor by field size

### Claude's Discretion
- How to store the enum values dictionary reference on ParsedProperty (additional field, lookup by ordinal, or external table)
- How to store the string mode on ParsedProperty or BinaryContractNode
- Internal method organization for bit extraction helpers
- Test contract JSON structure for leaf type tests

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Binary contract specification
- `docs/adr/16-binary-format-contract.md` — Full contract JSON format, string encoding/mode spec (to be updated), enum values table, bit field semantics, padding behavior. **This file must be updated in Phase 4 to add string mode field and correct enum accessor convention**

### Core parsing types
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` — GetString() binary stub (line ~281-288), FieldTypes constants (Enum, Bits, Padding already defined), type strictness pattern from Phase 3
- `src/Gluey.Contract/Parsing/ParseResult.cs` — IDisposable pattern, OffsetTable + ErrorCollector wrapping

### Binary schema (Parse loop to extend)
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` — Parse() method (line ~170-215), GetFieldType() mapper (line ~218-228, returns 0 for non-scalar types — must add string/enum/bits/padding cases)
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` — BitFields dictionary, EnumValues dictionary, EnumPrimitive, Encoding property — all already populated by Phase 2 contract loader

### Phase 3 context (prior decisions)
- `.planning/phases/03-scalar-parsing/03-CONTEXT.md` — Parse API surface, accessor type strictness, zero-allocation patterns, non-scalar field handling (currently skipped with continue)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FieldTypes.Enum`, `FieldTypes.Bits`, `FieldTypes.Padding`, `FieldTypes.String`: Constants already defined in ParsedProperty.cs
- `BinaryContractNode.BitFields`: Dictionary<string, BitFieldInfo> with Bit, Bits, Type per sub-field — already populated by Phase 2 loader
- `BinaryContractNode.EnumValues`: Dictionary<string, string> mapping integer keys to string labels — already populated
- `BinaryContractNode.EnumPrimitive`: The underlying integer type (e.g., "uint8") for the enum
- `BinaryContractNode.Encoding`: String encoding type (e.g., "ASCII", "UTF-8") — already populated
- `GetFieldType()`: Mapper in BinaryContractSchema that currently returns 0 for non-scalar types — extend to return correct FieldTypes for string/enum/bits/padding

### Established Patterns
- Accessor type strictness: GetXxx() checks _fieldType matches expected type, throws InvalidOperationException on mismatch
- Deferred materialization: ParsedProperty stores buffer reference, values read only when GetXxx() called
- Two OffsetTable entries per special field: Enum needs "mode" + "modes" entries (similar to how arrays will need element entries in Phase 5)

### Integration Points
- `BinaryContractSchema.Parse()`: The main parse loop — add cases for string, enum, bits, padding alongside existing scalar handling
- `BinaryContractSchema.NameToOrdinal`: Must include entries for enum "s" suffix fields and bit sub-field paths
- `OffsetTable`: Must have enough capacity for synthetic entries (enum "s" fields, bit sub-fields)

</code_context>

<specifics>
## Specific Ideas

- String mode field in ADR-16 is a scope extension decided during discussion — update the ADR document itself, not just the code
- Enum accessor convention is inverted from ADR-16 text — update ADR-16 to match: base name = numeric, base + "s" = string label
- Bit sub-fields use "/" path separator matching JSON Pointer style already established for arrays in ADR-16

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-leaf-types*
*Context gathered: 2026-03-20*
