# Phase 4: Leaf Types - Research

**Researched:** 2026-03-20
**Domain:** Binary parsing of non-composite field types (string, enum, bits, padding) in C# / .NET 9+
**Confidence:** HIGH

## Summary

Phase 4 extends the existing binary Parse() loop (which currently handles scalars only) to support four additional field types: strings (ASCII/UTF-8 with trimming modes), enums (dual-access numeric + string label), bit containers (with sub-field extraction), and padding (cursor advance, empty entry). All contract model properties needed for these types (BinaryContractNode.Encoding, EnumValues, EnumPrimitive, BitFields) are already populated by the Phase 2 loader. The Parse() loop currently returns `fieldType == 0` for these types and skips them with `continue`.

The primary challenge is the ParsedProperty struct design -- it is a readonly struct with many constructor overloads but no mechanism to store auxiliary data (enum values dictionary, string encoding byte, string trim mode). New constructors or fields on ParsedProperty will be needed. The OffsetTable and NameToOrdinal must also be expanded to accommodate synthetic entries (enum "s" suffix fields, bit sub-field path entries).

**Primary recommendation:** Extend ParsedProperty with additional constructor parameters for encoding/mode metadata and enum dictionary reference. Extend GetFieldType() to map string/enum/bits/padding. Add parse-time extraction logic in the Parse() loop for each type.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Honor the contract's `encoding` field: use `Encoding.ASCII` for `"ASCII"` contracts, `Encoding.UTF8` for `"UTF-8"`. Catches invalid bytes in ASCII fields
- **D-02:** Add a `mode` field to the string type in ADR-16. Supported modes: `plain` (as-is), `trimStart`, `trimEnd`, `trim`. Default when omitted: `trimEnd` (trim trailing null bytes and whitespace)
- **D-03:** ADR-16 must be updated in this phase to include the string `mode` field specification
- **D-04:** Encoding type stored as an extra byte on ParsedProperty (0=UTF-8, 1=ASCII) resolved at load time from `BinaryContractNode.Encoding`
- **D-05:** Two ParsedProperty entries per enum field: base name (e.g., `"mode"`) for raw numeric access, base + "s" (e.g., `"modes"`) for mapped string label
- **D-06:** This is INVERTED from current ADR-16 text -- `parsed["mode"].GetUInt8()` returns raw byte value, `parsed["modes"].GetString()` returns mapped label. ADR-16 must be updated
- **D-07:** Enum string resolution is deferred to GetString() call -- ParsedProperty stores a reference to the values dictionary, lookup happens lazily. Preserves zero-alloc parse path
- **D-08:** Unmapped enum values: GetString() returns the numeric value as a string (e.g., `"42"`). No exception, no null
- **D-09:** Bit sub-fields use path-based nested access: `parsed["status/isCharging"].GetBoolean()`, `parsed["status/errorCode"].GetUInt8()`
- **D-10:** Container field itself is also accessible: `parsed["status"].GetUInt8()` returns raw container byte(s)
- **D-11:** Bit sub-field values are pre-extracted at parse time -- Parse() reads container byte(s), extracts each sub-field immediately, stores as small byte values on individual ParsedProperty entries
- **D-12:** Multi-byte (16-bit) bit containers respect endianness when reading the container value before sub-field extraction
- **D-13:** Padding fields create ParsedProperty.Empty entries in OffsetTable -- field exists in NameToOrdinal but returns empty
- **D-14:** Padding fields are named (current ADR-16 behavior) -- e.g., `"reserved1"`, `"gap"`
- **D-15:** Parse loop recognizes FieldTypes.Padding, creates Empty entry, advances cursor by field size

### Claude's Discretion
- How to store the enum values dictionary reference on ParsedProperty (additional field, lookup by ordinal, or external table)
- How to store the string mode on ParsedProperty or BinaryContractNode
- Internal method organization for bit extraction helpers
- Test contract JSON structure for leaf type tests

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| STRE-01 | Parser reads fixed-length ASCII strings | GetFieldType() maps "string" to FieldTypes.String; Parse() reads bytes at offset/size, creates ParsedProperty with encoding=1 (ASCII); GetString() uses Encoding.ASCII |
| STRE-02 | Parser reads fixed-length UTF-8 strings | Same as STRE-01 but encoding=0 (UTF-8); GetString() uses Encoding.UTF8 (already the default path) |
| STRE-03 | Enum field maps byte value to string via contract values table | Parse() creates two OffsetTable entries: one for raw numeric (base name, fieldType=Enum's primitive), one for string label (suffixed name, fieldType=Enum); GetString() on enum entry does lazy dictionary lookup |
| STRE-04 | Enum dual-access: parsed["name"] returns raw numeric, parsed["names"] returns mapped string | NameToOrdinal gets two entries per enum; Parse() stores raw value at base ordinal with primitive's fieldType, stores enum entry at suffixed ordinal with Enum fieldType + dictionary reference |
| BITS-01 | Bit container reads 1-2 bytes and extracts sub-fields at specified bit positions and widths | Parse() reads container bytes, then iterates BinaryContractNode.BitFields; for each sub-field, extracts value with bit shift/mask, stores as individual ParsedProperty |
| BITS-02 | Boolean sub-fields (1-bit width) return true/false | Sub-field ParsedProperty with fieldType=Boolean, single byte value (0 or 1) stored at parse time |
| BITS-03 | Numeric sub-fields extract correct unsigned value across bit positions | Bit extraction: `(containerValue >> bit) & ((1 << bits) - 1)`, stored as byte(s) in ParsedProperty with appropriate fieldType |
| BITS-04 | Multi-byte bit containers (16 bits) work correctly with endianness | Container value read via BinaryPrimitives with endianness before bit extraction |
| COMP-04 | Padding fields: parser skips specified number of bytes, not exposed in ParseResult | Parse() sets ParsedProperty.Empty at padding ordinal; NameToOrdinal includes padding name but HasValue returns false |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Encoding | .NET 9+ built-in | ASCII and UTF-8 string decoding | Standard BCL; Encoding.ASCII and Encoding.UTF8 are the canonical APIs |
| System.Buffers.Binary.BinaryPrimitives | .NET 9+ built-in | Endianness-aware integer reading for bit containers | Already used in Phase 3 scalar parsing |
| NUnit | 4.3.1 | Test framework | Already established in project |
| FluentAssertions | 8.0.1 | Test assertion library | Already established in project |

No external packages needed for this phase -- all functionality uses BCL types already referenced.

## Architecture Patterns

### Parse Loop Extension Pattern
**What:** The existing Parse() loop in BinaryContractSchema.cs currently skips non-scalar types (GetFieldType returns 0). Phase 4 extends this by adding cases to GetFieldType() and branching logic in the parse loop.

**Current code (line 218-229):**
```csharp
private static byte GetFieldType(string type) => type switch
{
    "uint8" => FieldTypes.UInt8,
    // ... other scalars ...
    "boolean" => FieldTypes.Boolean,
    _ => 0 // non-scalar: string, enum, bits, array, struct, padding
};
```

**After Phase 4:**
```csharp
private static byte GetFieldType(string type) => type switch
{
    // ... existing scalars ...
    "string" => FieldTypes.String,
    "enum" => FieldTypes.Enum,
    "bits" => FieldTypes.Bits,
    "padding" => FieldTypes.Padding,
    _ => 0 // array, struct (Phase 5)
};
```

### ParsedProperty Extension for String Encoding + Mode
**What:** ParsedProperty needs to carry encoding (ASCII vs UTF-8) and trim mode for string fields. Since ParsedProperty is a readonly struct, a new constructor overload with an extra byte for encoding metadata is needed per D-04.

**Recommendation for Claude's Discretion -- encoding storage:**
Add a `_encoding` byte field to ParsedProperty (0=UTF-8, 1=ASCII). This is resolved at load time from `BinaryContractNode.Encoding`. The `_fieldType` already distinguishes String fields.

**Recommendation for Claude's Discretion -- mode storage:**
Store the mode as a byte on BinaryContractNode (not on ParsedProperty). The mode is needed only in GetString() which can read it from... wait, ParsedProperty does not reference the node. Better option: encode mode in a second byte alongside encoding on ParsedProperty, or use bits within the existing `_encoding` byte field. Since there are only 4 modes (plain=0, trimStart=1, trimEnd=2, trim=3), pack encoding in low 2 bits and mode in next 2 bits of a single byte. This keeps ParsedProperty's struct size unchanged.

Alternatively, the simplest approach: add `_encoding` byte to ParsedProperty (D-04 already decided this), and add `_stringMode` as a second byte. The struct already has 3 metadata bytes (_format, _endianness, _fieldType). Two more bytes still keeps alignment reasonable.

**Recommended approach:** Store string mode on BinaryContractNode (add `StringMode` property to the node class, populated during contract loading with default `trimEnd`). At parse time, apply the trim mode when creating the ParsedProperty. Encode the mode into the `_encoding` byte using bit packing: bits 0-1 = encoding (0=UTF-8, 1=ASCII), bits 2-3 = mode (0=plain, 1=trimStart, 2=trimEnd, 3=trim). GetString() unpacks and applies.

### Enum Dual-Entry Pattern
**What:** Each enum field produces TWO entries in OffsetTable and NameToOrdinal:
1. Base name (e.g., `"mode"`) -- ordinal points to ParsedProperty with the raw numeric value and the enum primitive's fieldType (e.g., UInt8)
2. Suffixed name (e.g., `"modes"`) -- ordinal points to ParsedProperty with fieldType=Enum that stores a reference to the values dictionary for lazy lookup

**Challenge:** ParsedProperty is a readonly struct -- it cannot store a `Dictionary<string, string>?` reference without adding a new field. Options:
1. Add a `Dictionary<string, string>?` field to ParsedProperty (adds 8 bytes to struct size on 64-bit)
2. Store an index into an external dictionary array on BinaryContractSchema, with ParsedProperty holding the index
3. Reuse the `_buffer` + `_offset` + `_length` to point at the raw bytes and store the dictionary reference in a separate field

**Recommendation:** Option 1 -- add a nullable `_enumValues` field to ParsedProperty. This is the simplest approach. The struct is already large (multiple reference fields like `_buffer`, `_childTable`, `_childOrdinals`, etc.). One more reference field is negligible. GetString() checks `_fieldType == FieldTypes.Enum`, reads the raw byte value from `_buffer`, looks up in `_enumValues`, returns label or numeric string per D-08.

### Bit Field Extraction Pattern
**What:** At parse time, read the container's raw bytes (1-2 bytes, endianness-aware), then for each sub-field in `BinaryContractNode.BitFields`, extract the value using bit manipulation.

**Extraction formula:**
```csharp
uint containerValue = ReadContainerValue(data, offset, size, endianness);
foreach (var (name, info) in node.BitFields)
{
    uint mask = (1u << info.Bits) - 1;
    uint extracted = (containerValue >> info.Bit) & mask;
    // Store as ParsedProperty at path "containerName/subFieldName"
}
```

**OffsetTable sizing:** The OffsetTable capacity must account for synthetic entries. Currently `new OffsetTable(OrderedFields.Length)`. Must increase to include: +1 per enum field (for "s" suffix entry), +N per bits container (for N sub-fields). This requires a pre-scan of OrderedFields or computing the total during chain resolution.

### Padding Skip Pattern
**What:** The simplest case -- Parse() recognizes `FieldTypes.Padding`, does NOT create a meaningful ParsedProperty, but the field still occupies its ordinal in NameToOrdinal. The cursor advances by `node.Size` (which happens implicitly since AbsoluteOffset is precomputed).

**Implementation:** Since offsets are precomputed at load time, there is actually nothing to "skip" at parse time. The padding field's ordinal simply gets `ParsedProperty.Empty` (default), and the next field's AbsoluteOffset already accounts for the padding bytes. NameToOrdinal includes the padding name so `parsed["reserved1"]` returns Empty with `HasValue == false`.

### NameToOrdinal Expansion
**What:** Currently, NameToOrdinal is built from `orderedFields[i].Name`. Phase 4 needs additional entries:
- Enum suffix: `"modes"` -> ordinal for the string-label entry
- Bit sub-field paths: `"flags/isCharging"` -> ordinal for the extracted boolean

**Approach:** During the TryLoad chain resolution (or a post-resolution step), scan OrderedFields for enum and bits types. Assign additional ordinals beyond `OrderedFields.Length`. Update the OffsetTable capacity accordingly.

### Anti-Patterns to Avoid
- **Allocating strings at parse time for enum labels:** D-07 requires lazy resolution. Store dictionary reference, do lookup in GetString() only.
- **Creating intermediate byte arrays for bit sub-fields:** Extract bit values directly into small numeric ParsedProperty entries pointing at existing buffer data or storing the extracted value inline.
- **Modifying ParsedProperty to be a class:** It must remain a readonly struct for zero-allocation patterns established in Phase 3.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ASCII string decoding | Custom byte-to-char loop | `Encoding.ASCII.GetString()` | Handles all edge cases, throwable for invalid bytes |
| UTF-8 string decoding | Manual multi-byte UTF-8 parsing | `Encoding.UTF8.GetString()` | Already used in Phase 3's GetString() path |
| Endianness-aware uint16 read | Manual byte swapping | `BinaryPrimitives.ReadUInt16LittleEndian/BigEndian` | Already used in Phase 3; correct and fast |
| Null-terminated string trimming | Character-by-character scan | `ReadOnlySpan<byte>.TrimEnd()` or `AsSpan().LastIndexOfAnyExcept()` | BCL span methods are optimized and vectorized |

## Common Pitfalls

### Pitfall 1: OffsetTable Capacity Too Small
**What goes wrong:** OffsetTable is created with `OrderedFields.Length` capacity. Enum and bits fields add synthetic entries beyond this count, causing out-of-bounds writes.
**Why it happens:** Phase 3 only stored one entry per field. Phase 4 introduces 1:N field-to-entry mappings.
**How to avoid:** Pre-compute total entry count: `OrderedFields.Length + enumFieldCount + totalBitSubFieldCount`. Or compute during chain resolution.
**Warning signs:** IndexOutOfRangeException or silent data corruption in OffsetTable.

### Pitfall 2: String Trim Mode Applied to Wrong Encoding
**What goes wrong:** Trimming null bytes (\0) and whitespace must work correctly for both ASCII and UTF-8. In UTF-8, multi-byte sequences could theoretically end with bytes that look like null or space in ASCII context.
**Why it happens:** Applying ASCII trim logic to UTF-8 strings.
**How to avoid:** For trimEnd (default), trim trailing 0x00 bytes first, then trailing ASCII whitespace. Both ASCII and UTF-8 share the same representation for \0 and space (0x20), so byte-level trimming is safe for both encodings.

### Pitfall 3: Enum GetString() on Raw Numeric Entry
**What goes wrong:** User calls `parsed["mode"].GetString()` (the raw numeric entry) instead of `parsed["modes"].GetString()`. If fieldType is UInt8 (not Enum), GetString() would try to decode the raw byte as a UTF-8 string, returning garbage.
**Why it happens:** The raw numeric entry has fieldType=UInt8, and the current GetString() binary path just does `Encoding.UTF8.GetString()`.
**How to avoid:** Type strictness -- GetString() on a UInt8 field type should throw InvalidOperationException (matching the pattern from Phase 3 where GetUInt8() on a String field throws). Alternatively, return the numeric value as string, but that diverges from type strictness.
**Recommendation:** Follow type strictness pattern: GetString() throws for non-string, non-enum field types in binary format.

### Pitfall 4: Bit Extraction on Big-Endian Containers
**What goes wrong:** Bit positions in the contract are defined relative to LSB (bit 0 = least significant). For big-endian multi-byte containers, the bytes must be read in big-endian order FIRST, converting to a native integer, THEN bit extraction uses the same LSB-relative positions.
**Why it happens:** Extracting bits directly from bytes without endianness conversion first.
**How to avoid:** Always read the container value as an integer (using BinaryPrimitives with correct endianness) before applying bit shifts.

### Pitfall 5: ParsedProperty Struct Size Inflation
**What goes wrong:** Adding too many fields to ParsedProperty inflates the struct size, increasing copy cost for every OffsetTable read and method return.
**Why it happens:** Struct is copied by value on every access.
**How to avoid:** Be strategic about new fields. Reuse existing fields where possible (e.g., bit-pack encoding+mode into one byte). Consider that the struct already has ~3 reference fields (8 bytes each on 64-bit) plus several value fields.

## Code Examples

### String Field Parsing (Parse Loop)
```csharp
// In BinaryContractSchema.Parse(), after scalar handling:
case FieldTypes.String:
{
    byte encodingByte = node.Encoding == "ASCII" ? (byte)1 : (byte)0;
    // mode from node.StringMode (0=plain, 1=trimStart, 2=trimEnd, 3=trim)
    var prop = new ParsedProperty(
        data, node.AbsoluteOffset, node.Size,
        "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness,
        FieldTypes.String, encodingByte, node.StringMode);
    offsetTable.Set(i, prop);
    break;
}
```

### String GetString() with Encoding and Trim
```csharp
// In ParsedProperty.GetString():
if (_fieldType == FieldTypes.String)
{
    var span = _buffer.AsSpan(_offset, _length);
    // Apply trim mode before decoding
    byte mode = (_encoding >> 2) & 0x03; // or separate _stringMode field
    span = mode switch
    {
        1 => span.TrimStart((byte)0), // trimStart
        2 => TrimEndNullsAndWhitespace(span), // trimEnd (default)
        3 => TrimEndNullsAndWhitespace(span.TrimStart((byte)0)), // trim
        _ => span // plain
    };
    return (_encoding & 0x01) == 1
        ? Encoding.ASCII.GetString(span)
        : Encoding.UTF8.GetString(span);
}
```

### Bit Field Extraction
```csharp
// Read container value with endianness
uint containerValue = node.Size switch
{
    1 => data[node.AbsoluteOffset],
    2 => node.ResolvedEndianness == 0
        ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(node.AbsoluteOffset, 2))
        : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(node.AbsoluteOffset, 2)),
    _ => throw new InvalidOperationException($"Unsupported bit container size: {node.Size}")
};

// Container itself accessible
offsetTable.Set(containerOrdinal, new ParsedProperty(
    data, node.AbsoluteOffset, node.Size,
    "/" + node.Name, 1, node.ResolvedEndianness, FieldTypes.Bits));

// Extract each sub-field
foreach (var (subName, info) in node.BitFields)
{
    uint mask = (1u << info.Bits) - 1;
    byte extracted = (byte)((containerValue >> info.Bit) & mask);
    byte subFieldType = GetFieldType(info.Type);
    // Store extracted byte in a small buffer or inline
    string path = "/" + node.Name + "/" + subName;
    // Create ParsedProperty with extracted value
    offsetTable.Set(subFieldOrdinal, /* ... */);
}
```

### Enum Dual-Entry Setup
```csharp
// Raw numeric entry at base ordinal
byte enumPrimitiveType = GetFieldType(node.EnumPrimitive!);
offsetTable.Set(baseOrdinal, new ParsedProperty(
    data, node.AbsoluteOffset, node.Size,
    "/" + node.Name, 1, node.ResolvedEndianness, enumPrimitiveType));

// String label entry at suffixed ordinal
offsetTable.Set(suffixedOrdinal, new ParsedProperty(
    data, node.AbsoluteOffset, node.Size,
    "/" + node.Name + "s", 1, node.ResolvedEndianness,
    FieldTypes.Enum, node.EnumValues));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| GetFieldType returns 0 for leaf types | Must return FieldTypes.String/Enum/Bits/Padding | Phase 4 | Enables parse loop to handle these types |
| OffsetTable sized to OrderedFields.Length | Must be sized to include synthetic entries | Phase 4 | More slots needed for enum suffixes and bit sub-fields |
| NameToOrdinal has 1:1 field:ordinal mapping | Must include enum suffix and bit sub-field path entries | Phase 4 | Enables parsed["modes"] and parsed["flags/isCharging"] access |
| GetString() only does UTF-8 decode | Must handle ASCII, trimming, and enum label lookup | Phase 4 | String and enum GetString() paths diverge |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-build --filter "ClassName~LeafType"` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| STRE-01 | ASCII string parsing | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~StringParsing"` | No - Wave 0 |
| STRE-02 | UTF-8 string parsing | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~StringParsing"` | No - Wave 0 |
| STRE-03 | Enum maps byte to string | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~EnumParsing"` | No - Wave 0 |
| STRE-04 | Enum dual-access | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~EnumParsing"` | No - Wave 0 |
| BITS-01 | Bit container extraction | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~BitFieldParsing"` | No - Wave 0 |
| BITS-02 | Boolean sub-field | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~BitFieldParsing"` | No - Wave 0 |
| BITS-03 | Numeric sub-field extraction | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~BitFieldParsing"` | No - Wave 0 |
| BITS-04 | 16-bit container endianness | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~BitFieldParsing"` | No - Wave 0 |
| COMP-04 | Padding skip + not exposed | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~PaddingParsing"` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-build`
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Binary.Tests/LeafTypeParsingTests.cs` -- covers STRE-01 through COMP-04 (or split into StringParsingTests, EnumParsingTests, BitFieldParsingTests, PaddingParsingTests)
- [ ] No new framework install needed -- NUnit + FluentAssertions already configured

## Open Questions

1. **Bit sub-field extracted value storage**
   - What we know: Extracted bit values are small (1-8 bits typically). ParsedProperty needs buffer+offset+length to read values.
   - What's unclear: Where to store the extracted byte(s). Options: (a) allocate a small byte[] per parse call to hold extracted values, (b) write extracted values back into the payload buffer at unused positions (unsafe), (c) store inline in ParsedProperty if possible.
   - Recommendation: Allocate a single small byte[] per parse call (pooled from ArrayPool) to hold all extracted bit values. This is a minor allocation vs. the complexity of inline storage. The number of bit sub-fields across all containers is known at load time, so the buffer size is predictable.

2. **ADR-16 update scope**
   - What we know: D-03 requires updating ADR-16 for string mode field. D-06 requires updating enum accessor convention.
   - What's unclear: Whether to do ADR updates as a separate task or inline with implementation.
   - Recommendation: ADR update should be the first task in the plan, before implementation begins, since it serves as the spec.

## Sources

### Primary (HIGH confidence)
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` -- current struct layout, all constructors, GetString() stub
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` -- Parse() loop, GetFieldType() mapper, OffsetTable creation
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` -- BitFields, EnumValues, EnumPrimitive, Encoding properties
- `docs/adr/16-binary-format-contract.md` -- full contract spec including enum and bit field semantics
- `.planning/phases/04-leaf-types/04-CONTEXT.md` -- all locked decisions D-01 through D-15

### Secondary (MEDIUM confidence)
- `tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs` -- established test patterns for binary parsing tests
- `.planning/STATE.md` -- project history and accumulated decisions

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all BCL types, no external dependencies needed
- Architecture: HIGH -- patterns directly extend existing Parse() loop and ParsedProperty with clear decisions from CONTEXT.md
- Pitfalls: HIGH -- derived from concrete code analysis of existing struct layout and parse patterns

**Research date:** 2026-03-20
**Valid until:** 2026-04-20 (stable .NET BCL, no moving targets)
