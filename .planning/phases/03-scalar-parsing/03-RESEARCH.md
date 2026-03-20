# Phase 3: Scalar Parsing - Research

**Researched:** 2026-03-20
**Domain:** Binary payload parsing, BinaryPrimitives, zero-allocation patterns
**Confidence:** HIGH

## Summary

Phase 3 implements the first end-to-end binary parse pipeline: `BinaryContractSchema.Parse(byte[])` returns a `ParseResult` with scalar fields accessible via `parsed["fieldName"].GetXxx()`. The infrastructure from Phases 1 and 2 is substantial -- `ParsedProperty` already has working binary read paths for int32/int64/double/boolean, `OffsetTable` and `ErrorCollector` are ArrayPool-backed and ready to use, and `BinaryContractSchema` has `OrderedFields`, `TotalFixedSize`, and `NameToOrdinal` pre-resolved at load time.

The main deliverables are: (1) the `Parse()` method on `BinaryContractSchema`, (2) unsigned integer accessors (`GetUInt8`, `GetUInt16`, `GetUInt32`) on `ParsedProperty` since these do not exist yet, (3) truncated numeric handling for non-natural byte widths (e.g., int32 in 3 bytes), and (4) accessor type strictness so that `GetInt32()` on a `uint16` field throws. The parse loop itself is simple -- iterate `OrderedFields`, skip non-scalar or dynamic-offset fields, construct `ParsedProperty` with binary constructor, set into `OffsetTable`.

**Primary recommendation:** Build in three waves: (1) Parse method + scalar type set identification, (2) unsigned accessor additions + truncated numeric paths, (3) accessor type strictness via field type metadata on ParsedProperty.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- Parse() walks all OrderedFields but only populates scalar types (uint8/16/32, int8/16/32, float32/64, boolean) in the OffsetTable
- Non-scalar fields are left as ParsedProperty.Empty -- their ordinal slots exist but are not populated
- The parser still advances past fixed-size non-scalar fields to keep subsequent scalar offsets correct (AbsoluteOffset from chain resolution handles this)
- When IsDynamicOffset is true (fields after a semi-dynamic array), stop parsing -- skip that field and all subsequent ones. Arrays are Phase 5
- Signed truncated integers use MSB sign-extension: read N bytes, check MSB of first byte (big-endian) or last byte (little-endian), fill remaining upper bytes with 0xFF if sign bit set
- Unsigned truncated integers use zero-padding: read N bytes, place in lower bytes of target width
- Example: int32 with size 3, big-endian bytes [0xFF, 0xCF, 0xC7] sign-extends to int32 -12345
- GetInt32() on a uint32 field throws InvalidOperationException -- accessor must match the contract-declared type
- GetUInt16() on a uint16 field works; GetInt32() on a uint16 field throws
- This requires storing the contract-declared field type on ParsedProperty (or accessible from it) so the accessor can validate the call
- Same principle applies to all GetXxx() methods: the accessor must correspond to the field's declared type
- Two overloads: Parse(byte[]) and Parse(ReadOnlySpan<byte>) -- mirrors JsonContractSchema exactly
- Span overload is the real implementation; byte[] delegates to it
- Returns ParseResult? -- null means payload is shorter than the contract's TotalFixedSize (structurally invalid)
- parsed["fieldName"] returns ParsedProperty, consumer calls GetXxx() -- identical to JSON access pattern
- Parse is a simple loop over OrderedFields inside BinaryContractSchema -- no ref struct walker needed (binary is sequential, not recursive like JSON)
- Each ParsedProperty references the original payload byte[] (zero-copy) with (buffer, offset, length, path, format=binary, endianness)
- Consumer must keep payload alive while using ParseResult (same contract as JSON)
- OffsetTable and ErrorCollector are ArrayPool-backed, returned via IDisposable -- same pattern as JSON ParseResult
- No tokenizer, no recursive descent -- just iterate ordered fields and populate OffsetTable slots

### Claude's Discretion
- Internal method organization within BinaryContractSchema.Parse()
- How to propagate field type metadata to ParsedProperty for accessor validation (new byte field, enum, or lookup table)
- Exact error message text for InvalidOperationException on type mismatch
- Test contract JSON structure (pure-scalar contracts, not the full battery contract)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SCLR-01 | Parser reads uint8, uint16, uint32 with correct endianness via BinaryPrimitives | Need new GetUInt8/GetUInt16/GetUInt32 accessors on ParsedProperty; binary constructor already stores endianness |
| SCLR-02 | Parser reads int8, int16, int32 with correct endianness via BinaryPrimitives | GetInt32 binary path already handles 1/2/4 bytes; GetInt64 handles 1/2/4/8 bytes |
| SCLR-03 | Parser reads float32 and float64 with correct endianness | GetDouble binary path handles 4 and 8 bytes via ReadSingle/ReadDouble; no separate GetFloat needed since GetDouble widens float32 |
| SCLR-04 | Parser reads boolean (0 = false, non-zero = true) | GetBoolean binary path already implemented: `_buffer[_offset] != 0` |
| SCLR-05 | Truncated numerics: int32 in fewer bytes with correct sign extension | Current GetInt32 only handles 1/2/4 bytes -- need to add 3-byte path with sign extension |
| SCLR-06 | Truncated numerics: uint32 in fewer bytes with zero-padding | Need GetUInt32 with 1/2/3 byte paths using zero-padding |
| CORE-04 | BinaryContractSchema.Parse(byte[]) returns ParseResult? (null for structurally invalid) | Parse method body is the main deliverable; mirrors JsonContractSchema.Parse pattern |
| CORE-05 | Zero-allocation parse path using ArrayPool, OffsetTable, ErrorCollector, ArrayBuffer | OffsetTable and ErrorCollector already ArrayPool-backed; ParsedProperty uses zero-copy buffer reference |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Buffers.Binary.BinaryPrimitives | .NET 9/10 built-in | Endianness-aware integer/float reads | Zero-allocation, hardware-optimized, already used in Phase 1 |
| System.Buffers.ArrayPool | .NET 9/10 built-in | Pool-backed buffers for OffsetTable/ErrorCollector | Avoids GC pressure on hot parse path |
| NUnit | 4.3.1 | Test framework | Already in test project |
| FluentAssertions | 8.0.1 | Test assertions | Already in test project |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Runtime.CompilerServices.Unsafe | .NET built-in | MethodImplOptions.AggressiveInlining | GetXxx() accessor hot paths |

No external packages needed. Everything required is in the .NET BCL.

## Architecture Patterns

### Parse Method Structure
```
BinaryContractSchema.Parse(ReadOnlySpan<byte>)
  1. Check payload.Length < TotalFixedSize (if TotalFixedSize != -1) -> return null
  2. Rent OffsetTable(OrderedFields.Length)
  3. Rent ErrorCollector()
  4. Loop: for each node in OrderedFields
     a. if node.IsDynamicOffset -> break (stop parsing)
     b. if not IsScalarType(node.Type) -> continue (skip, offset already computed)
     c. Create ParsedProperty(buffer, node.AbsoluteOffset, node.Size, "/" + node.Name, format=1, endianness=node.ResolvedEndianness)
     d. offsetTable.Set(ordinal, property)
  5. Return new ParseResult(offsetTable, errors, NameToOrdinal)
```

### Scalar Type Set
The following types are scalar for Phase 3:
- `uint8`, `uint16`, `uint32`
- `int8`, `int16`, `int32`
- `float32`, `float64`
- `boolean`

Non-scalar types to skip: `string`, `enum`, `bits`, `array`, `struct`, `padding`

### ParsedProperty Field Type Metadata

**Recommendation:** Add a `_fieldType` byte to ParsedProperty.

The accessor type strictness requirement means ParsedProperty must know the contract-declared type to validate GetXxx() calls. Options considered:

| Approach | Pros | Cons |
|----------|------|------|
| New `_fieldType` byte on ParsedProperty | Simple, zero overhead, direct comparison | Struct size increases by 1 byte (padded anyway) |
| Lookup table on ParseResult | No struct change | Extra indirection on every GetXxx() call |
| Encode in _format byte (upper bits) | No new field | Limits future format expansion, mixing concerns |

**Use a `_fieldType` byte.** Define an internal enum or constants: `FieldType.UInt8 = 1, UInt16 = 2, UInt32 = 3, Int8 = 4, Int16 = 5, Int32 = 6, Float32 = 7, Float64 = 8, Boolean = 9, String = 10, ...`. Each GetXxx() accessor checks `_fieldType` matches before reading. JSON format (_format == 0) bypasses the check entirely since JSON does not have declared types.

The binary constructor already has a `format` and `endianness` byte. Add `fieldType` as a third byte parameter to the binary constructor. The struct is already 8 bytes aligned so this likely fits in existing padding.

### Truncated Numeric Implementation

For truncated numerics (e.g., int32 with size=3), BinaryPrimitives cannot be used directly since it requires exact-width spans. Manual byte assembly is needed:

```csharp
// Signed truncated read (big-endian, 3 bytes -> int32)
// bytes: [0xFF, 0xCF, 0xC7]
// MSB = bytes[0] = 0xFF, sign bit set -> extend with 0xFF
// result = (0xFF << 24) | (0xFF << 16) | (0xCF << 8) | 0xC7 = -12345

// Unsigned truncated read (big-endian, 3 bytes -> uint32)
// bytes: [0xFF, 0xCF, 0xC7]
// No sign extension -> zero-pad upper byte
// result = (0x00 << 24) | (0xFF << 16) | (0xCF << 8) | 0xC7 = 16764871
```

Key insight: the truncated numeric logic belongs in the GetXxx() accessors on ParsedProperty, not in the parse loop. The parse loop just records (offset, length) -- the accessor reads the bytes and assembles the value. The existing `_ => throw` default case in the switch expressions needs to be replaced with truncated-read logic.

### Anti-Patterns to Avoid
- **Copying payload bytes into ParsedProperty:** The whole point is zero-copy. ParsedProperty holds (buffer, offset, length) and reads on demand.
- **Building a separate parser class:** Binary parsing is a simple sequential loop. No walker/tokenizer/visitor pattern needed.
- **Normalizing endianness at parse time:** Endianness is resolved per-field at load time and stored on ParsedProperty. The accessor handles it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Endianness-aware reads | Manual bit shifting for standard widths | BinaryPrimitives.ReadInt32BigEndian etc. | Hardware-optimized, handles edge cases |
| Buffer pooling | Custom pool | ArrayPool<T>.Shared | Battle-tested, avoids LOH fragmentation |
| Property storage | Dictionary<string, object> | OffsetTable (ArrayPool-backed ordinal array) | Zero allocation, O(1) lookup by ordinal |

**Key insight:** The truncated numeric case (non-standard widths like 3 bytes) IS the one place where manual byte assembly is necessary. BinaryPrimitives only handles 1/2/4/8 byte widths.

## Common Pitfalls

### Pitfall 1: Span Overload Cannot Reference byte[] for ParsedProperty
**What goes wrong:** `Parse(ReadOnlySpan<byte>)` receives a span, but ParsedProperty needs a `byte[]` reference for deferred materialization.
**Why it happens:** Spans cannot be stored on the heap or in structs that outlive the stack frame.
**How to avoid:** The `Parse(byte[])` overload passes the original array. The `Parse(ReadOnlySpan<byte>)` overload must `ToArray()` the span first (or require the caller to keep the array alive). Check how JsonContractSchema handles this -- it has the same constraint.
**Warning signs:** ParsedProperty storing a null buffer.

### Pitfall 2: Off-by-One in Sign Extension
**What goes wrong:** Sign bit checked on wrong byte, or fill byte applied incorrectly.
**Why it happens:** Big-endian MSB is bytes[0], little-endian MSB is bytes[length-1]. Easy to confuse.
**How to avoid:** Explicit tests for both endianness with known values. The CONTEXT.md example (int32/3 bytes/big-endian [0xFF,0xCF,0xC7] = -12345) serves as a reference test case.
**Warning signs:** Positive values appearing negative or vice versa.

### Pitfall 3: Missing Accessor Methods for Unsigned Types
**What goes wrong:** Consumer calls `GetUInt16()` but it doesn't exist on ParsedProperty.
**Why it happens:** ParsedProperty was built for JSON first; JSON uses GetInt32/GetInt64/GetDouble which cover all JSON numeric needs.
**How to avoid:** Add GetUInt8, GetUInt16, GetUInt32 methods. The binary format requires unsigned types that JSON never needed.
**Warning signs:** No way to read a uint16 field without casting from int.

### Pitfall 4: Payload Buffer Lifetime
**What goes wrong:** Consumer disposes or loses reference to the byte[] payload, then calls GetXxx() on ParsedProperty and gets garbage or an exception.
**Why it happens:** ParsedProperty holds a reference to the original buffer, not a copy.
**How to avoid:** Document the contract: "ParseResult is only valid while the source byte[] is alive." Same pattern as JSON -- ParsedProperty references the original UTF-8 buffer.
**Warning signs:** Tests pass in isolation but fail when buffer is reused.

### Pitfall 5: IsDynamicOffset Stop Condition
**What goes wrong:** Parser tries to read fields after a semi-dynamic array, getting wrong offsets.
**Why it happens:** Fields after a semi-dynamic array have IsDynamicOffset=true; their AbsoluteOffset is meaningless.
**How to avoid:** When `node.IsDynamicOffset` is true, stop populating fields. Either `break` from the loop or `continue` (depends on whether there are any non-dynamic fields after dynamic ones -- with the chain model, once dynamic, all subsequent are dynamic too, so `break` is correct).
**Warning signs:** ArrayIndexOutOfRangeException when parsing payloads with semi-dynamic arrays.

## Code Examples

### Parse Method Implementation Pattern
```csharp
// Source: mirrors JsonContractSchema.Parse (lines 163-202)
public ParseResult? Parse(ReadOnlySpan<byte> data)
{
    // Structural check: payload too short
    if (TotalFixedSize >= 0 && data.Length < TotalFixedSize)
        return null;

    // Need byte[] for ParsedProperty storage (span can't be stored)
    byte[] buffer = data.ToArray();

    var offsetTable = new OffsetTable(OrderedFields.Length);
    var errors = new ErrorCollector();

    for (int i = 0; i < OrderedFields.Length; i++)
    {
        var node = OrderedFields[i];

        if (node.IsDynamicOffset)
            break;

        if (!IsScalarType(node.Type))
            continue;

        var prop = new ParsedProperty(
            buffer, node.AbsoluteOffset, node.Size,
            "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness);

        offsetTable.Set(i, prop);
    }

    return new ParseResult(offsetTable, errors, NameToOrdinal);
}

public ParseResult? Parse(byte[] data)
{
    if (TotalFixedSize >= 0 && data.Length < TotalFixedSize)
        return null;

    var offsetTable = new OffsetTable(OrderedFields.Length);
    var errors = new ErrorCollector();

    for (int i = 0; i < OrderedFields.Length; i++)
    {
        var node = OrderedFields[i];

        if (node.IsDynamicOffset)
            break;

        if (!IsScalarType(node.Type))
            continue;

        var prop = new ParsedProperty(
            data, node.AbsoluteOffset, node.Size,
            "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness);

        offsetTable.Set(i, prop);
    }

    return new ParseResult(offsetTable, errors, NameToOrdinal);
}
```

### Truncated Signed Integer Read (3 bytes, big-endian)
```csharp
// Manual assembly for non-standard width
// int32 with size=3, big-endian: [0xFF, 0xCF, 0xC7]
var span = _buffer.AsSpan(_offset, _length);
bool signBit = (span[0] & 0x80) != 0; // big-endian: MSB is first byte
byte fill = signBit ? (byte)0xFF : (byte)0x00;
int result = (fill << 24) | (span[0] << 16) | (span[1] << 8) | span[2];
// result = -12345
```

### Truncated Unsigned Integer Read (3 bytes, big-endian)
```csharp
// uint32 with size=3, big-endian: [0xFF, 0xCF, 0xC7]
var span = _buffer.AsSpan(_offset, _length);
uint result = ((uint)span[0] << 16) | ((uint)span[1] << 8) | span[2];
// result = 16764871
```

### Accessor Type Strictness
```csharp
// In GetInt32():
if (_format == 1 && _fieldType != FieldType.Int32 && _fieldType != FieldType.Int16 && _fieldType != FieldType.Int8)
    throw new InvalidOperationException(
        $"Cannot read Int32 from field of type '{GetFieldTypeName()}' at path '{_path}'. " +
        "Use the accessor matching the contract-declared type.");
```

Note: the strictness decision says accessor MUST match declared type exactly. `GetInt32()` on int16 also throws. Only `GetInt16()` works on an int16 field.

### Scalar Type Check
```csharp
private static bool IsScalarType(string type) => type switch
{
    "uint8" or "uint16" or "uint32" or
    "int8" or "int16" or "int32" or
    "float32" or "float64" or "boolean" => true,
    _ => false
};
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| BinaryReader with streams | BinaryPrimitives on Span<byte> | .NET Core 2.1+ | Zero-alloc, no stream overhead |
| BitConverter.ToInt32 | BinaryPrimitives.ReadInt32BigEndian | .NET Core 2.1+ | Explicit endianness, no array copy |
| Manual IEEE 754 decode | BinaryPrimitives.ReadSingleBigEndian | .NET 5+ | Correct NaN handling, hardware-optimized |

## Open Questions

1. **Span overload and byte[] requirement**
   - What we know: ParsedProperty requires a `byte[]` reference for deferred value materialization. `ReadOnlySpan<byte>` cannot be stored.
   - What's unclear: Should `Parse(ReadOnlySpan<byte>)` call `ToArray()` internally (allocates), or should it be removed in favor of byte[] only?
   - Recommendation: Keep both overloads for API parity with JsonContractSchema. The Span overload calls `ToArray()` internally -- the allocation is unavoidable since ParsedProperty holds a reference. Document this.

2. **Unsigned accessor naming**
   - What we know: Need GetUInt8, GetUInt16, GetUInt32 methods. Return types are `byte`, `ushort`, `uint`.
   - What's unclear: Should `GetUInt8()` return `byte` or `int`? C# convention is `byte` for uint8.
   - Recommendation: `byte GetUInt8()`, `ushort GetUInt16()`, `uint GetUInt32()`. Follow .NET type conventions.

3. **Field type metadata propagation**
   - What we know: ParsedProperty needs to know the contract-declared type for accessor validation.
   - What's unclear: Exact encoding -- byte enum vs string lookup.
   - Recommendation: Internal `byte _fieldType` field with constants. Minimal overhead, no string allocations. Add to existing binary constructor as additional parameter.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| Quick run command | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-restore -v q` |
| Full suite command | `dotnet test --no-restore -v q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SCLR-01 | uint8/16/32 read with endianness | unit | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~ScalarParsing" --no-restore -v q` | No - Wave 0 |
| SCLR-02 | int8/16/32 read with endianness | unit | same filter | No - Wave 0 |
| SCLR-03 | float32/64 read with endianness | unit | same filter | No - Wave 0 |
| SCLR-04 | boolean read (0=false, nonzero=true) | unit | same filter | No - Wave 0 |
| SCLR-05 | Truncated signed int sign-extension | unit | same filter | No - Wave 0 |
| SCLR-06 | Truncated unsigned int zero-padding | unit | same filter | No - Wave 0 |
| CORE-04 | Parse returns ParseResult? (null for short payload) | unit | same filter | No - Wave 0 |
| CORE-05 | Zero-alloc parse (ArrayPool, no heap on hot path) | unit | same filter | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-restore -v q`
- **Per wave merge:** `dotnet test --no-restore -v q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs` -- covers SCLR-01 through SCLR-06, CORE-04
- [ ] Test contract JSON files (pure-scalar contracts for big-endian, little-endian, mixed, truncated cases)
- [ ] No framework install needed -- NUnit + FluentAssertions already configured

## Sources

### Primary (HIGH confidence)
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` -- Existing binary constructors and GetXxx() binary paths (verified line by line)
- `src/Gluey.Contract/Parsing/ParseResult.cs` -- Constructor signatures, IDisposable pattern
- `src/Gluey.Contract/Parsing/OffsetTable.cs` -- ArrayPool rent/return, Set/indexer API
- `src/Gluey.Contract/Validation/ErrorCollector.cs` -- ArrayPool rent/return, Add/Dispose API
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` -- OrderedFields, TotalFixedSize, NameToOrdinal, ComputeTotalFixedSize
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` -- AbsoluteOffset, ResolvedEndianness, IsDynamicOffset, Type, Size
- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` -- Parse() pattern to mirror (lines 163-202)
- `docs/adr/16-binary-format-contract.md` -- Supported types table, truncated numeric semantics, endianness rules

### Secondary (MEDIUM confidence)
- .NET BinaryPrimitives documentation -- ReadSingleBigEndian/ReadDoubleBigEndian availability (.NET 5+)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in project, .NET BCL primitives
- Architecture: HIGH -- existing patterns (JsonContractSchema.Parse, OffsetTable, ParsedProperty binary paths) are well-understood from code review
- Pitfalls: HIGH -- identified from direct code analysis (missing unsigned accessors, span storage limitation, truncated numeric edge cases)

**Research date:** 2026-03-20
**Valid until:** 2026-04-20 (stable domain -- binary parsing fundamentals don't change)
