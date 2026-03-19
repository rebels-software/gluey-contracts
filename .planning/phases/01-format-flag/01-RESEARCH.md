# Phase 1: Format Flag - Research

**Researched:** 2026-03-19
**Domain:** Readonly struct modification, format-aware dispatch in C# / .NET 9/10
**Confidence:** HIGH

## Summary

Phase 1 adds a 1-byte `_format` discriminator field to the existing `ParsedProperty` readonly struct in the shared `Gluey.Contract` core package. This enables `GetXxx()` methods to dispatch between UTF-8/JSON parsing (existing behavior) and binary reading (new behavior) based on the format flag. The default value of 0 preserves backward compatibility -- all existing JSON consumers see identical behavior without any code changes.

The scope is deliberately narrow: modify `ParsedProperty` to accept and store a format flag, add branching in the `GetXxx()` methods (binary branches can throw `NotSupportedException` initially since the binary walker does not exist yet), and verify that every existing test in both `Gluey.Contract.Tests` and `Gluey.Contract.Json.Tests` passes unchanged. No new packages, no new project files -- just a surgical modification to the core struct and its constructors.

**Primary recommendation:** Add `_format` as a `byte` field to `ParsedProperty`. Keep all existing constructors unchanged (they implicitly set `_format = 0`). Add new `internal` constructor overloads that accept a `byte format` parameter. Branch in each `GetXxx()` method with `if (_format == 0)` guarding the existing JSON path.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CORE-01 | ParsedProperty has a 1-byte format flag that dispatches GetXxx() between UTF-8 and binary reading | Struct modification pattern, constructor overloads, GetXxx() branching pattern documented below |
| CORE-02 | Adding format flag does not break existing JSON consumers (all JSON tests pass unchanged) | Default value 0 = JSON, existing constructors unchanged, regression test strategy documented below |
</phase_requirements>

## Standard Stack

### Core

No new dependencies are introduced in Phase 1. All work is within the existing `Gluey.Contract` core package.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Buffers.Binary.BinaryPrimitives` | BCL (built-in) | Binary-path GetXxx() reads | Endianness-aware, zero-allocation. Already identified in stack research as THE correct API for binary reads. |
| `System.Buffers.Text.Utf8Parser` | BCL (built-in) | JSON-path GetXxx() reads (existing) | Already used in current ParsedProperty. No change needed. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework | Existing test infrastructure -- verified in project files |
| FluentAssertions | 8.0.1 | Assertion library | Existing test infrastructure -- verified in project files |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| 1-byte format flag | Separate ParsedProperty types per format | Impossible -- readonly struct cannot use inheritance. Would require duplicating all downstream types (OffsetTable, ParseResult, etc.) |
| Branch in GetXxx() | Strategy pattern via delegate | Delegate call is more expensive than a byte comparison branch. Adds allocation for delegate. Violates zero-allocation invariant. |
| Enum for format | Raw byte constant | Enum is cleaner for readability, but adds a type. Use `internal enum PropertyFormat : byte { Utf8Json = 0, Binary = 1 }` for clarity while storing as byte. |

## Architecture Patterns

### Pattern 1: Format Discriminator with Default-Zero Backward Compatibility

**What:** Add a `byte _format` field to ParsedProperty. The value 0 means "UTF-8/JSON" (the existing behavior). Value 1 means "binary". Since C# initializes all struct fields to 0 by default, any ParsedProperty created by existing code automatically gets format = 0 (JSON), preserving all existing behavior.

**When to use:** Always. This is the foundational pattern for Phase 1.

**Example:**
```csharp
// Source: Architecture research + existing ParsedProperty.cs analysis
public readonly struct ParsedProperty
{
    private readonly byte _format;     // 0 = UTF-8/JSON (default), 1 = Binary
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly string _path;
    // ... remaining fields unchanged ...
}
```

### Pattern 2: Existing Constructors Unchanged, New Overloads for Binary

**What:** Keep all three existing `internal` constructors exactly as they are. They implicitly initialize `_format = 0`. Add parallel constructor overloads that accept a `byte format` parameter for binary consumers (used by the BinaryWalker in Phase 3+).

**When to use:** Always. This is how backward compatibility is guaranteed at the constructor level.

**Example:**
```csharp
// Existing constructor -- UNCHANGED, _format defaults to 0
internal ParsedProperty(byte[] buffer, int offset, int length, string path)
{
    _buffer = buffer;
    _offset = offset;
    _length = length;
    _path = path;
    _format = 0; // implicit via default, but can be explicit for clarity
    // ... remaining fields set to default ...
}

// New overload for binary format consumers
internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format)
{
    _buffer = buffer;
    _offset = offset;
    _length = length;
    _path = path;
    _format = format;
    // ... remaining fields set to default ...
}
```

### Pattern 3: Guard-Clause Branching in GetXxx() Methods

**What:** Each `GetXxx()` method checks `_format == 0` first (the JSON path), then falls through to the binary path. The JSON path code is IDENTICAL to the current implementation -- do not refactor, reorganize, or "improve" it. The binary path can initially throw `NotSupportedException` since the binary infrastructure does not exist yet, OR it can contain the actual `BinaryPrimitives` read logic (since the API is known and stable).

**When to use:** In every GetXxx() method: GetString, GetInt32, GetInt64, GetDouble, GetBoolean, GetDecimal.

**Example:**
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public int GetInt32()
{
    if (_length == 0) return default;
    if (_format == 0)
    {
        // JSON path: existing behavior, unchanged
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
        return value;
    }
    // Binary path: read raw bytes
    return _length switch
    {
        1 => (sbyte)_buffer[_offset],
        2 => BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(_offset, _length)),
        4 => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_offset, _length)),
        _ => throw new InvalidOperationException(
            $"Cannot read Int32 from {_length} bytes at path '{_path}'")
    };
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool GetBoolean()
{
    if (_length == 0) return default;
    if (_format == 0)
    {
        // JSON path: existing behavior, unchanged
        return _length == 4 && _buffer[_offset] == (byte)'t';
    }
    // Binary path: 0 = false, non-zero = true
    return _buffer[_offset] != 0;
}
```

### Pattern 4: Endianness Decision -- Defer to Phase 3

**What:** The binary `GetXxx()` methods need to know endianness. Two options were identified in architecture research: (a) store `_endianness` as another byte on ParsedProperty, or (b) normalize bytes to host order at parse time. For Phase 1, the binary path implementations can use a fixed endianness (little-endian, matching the most common case) or throw `NotSupportedException`. The endianness storage decision can be finalized in Phase 1 or deferred to Phase 3 when the binary walker is built.

**Recommendation:** Add the `_endianness` byte NOW in Phase 1 alongside `_format`. The cost is one more byte of struct padding (which is likely free due to alignment -- see struct layout analysis below). Doing it now avoids a second breaking struct change in Phase 3.

**Example:**
```csharp
private readonly byte _format;      // 0 = JSON, 1 = Binary
private readonly byte _endianness;   // 0 = little-endian, 1 = big-endian (only meaningful when _format == 1)
```

### Anti-Patterns to Avoid

- **Refactoring the JSON path:** Do NOT rewrite or reorganize the existing GetXxx() JSON logic when adding the format branch. The code must remain identical to minimize risk. Line-for-line same.
- **Making format a public property:** The format flag is an internal implementation detail. Do not expose it in the public API. Consumers should not know or care whether the backing data is JSON or binary.
- **Using a bool instead of byte:** A bool limits future extensibility to two formats. A byte allows up to 256 formats at zero cost.
- **Adding format to the public constructor surface:** All constructors with format parameter should be `internal`. External consumers never set the format.

## Struct Layout Analysis

### Current Layout (64-bit runtime)

```
ParsedProperty (current):
  byte[] _buffer          : 8 bytes (reference)
  int    _offset          : 4 bytes
  int    _length          : 4 bytes
  string _path            : 8 bytes (reference)
  OffsetTable _childTable : 16 bytes (struct: ParsedProperty[]? ref + int capacity + padding)
  Dictionary? _childOrdinals : 8 bytes (reference, nullable)
  Dictionary? _directChildren : 8 bytes (reference, nullable)
  ArrayBuffer? _arrayBuffer  : 8 bytes (reference, nullable)
  int    _arrayOrdinal    : 4 bytes
  ---
  Estimated: ~68 bytes + alignment padding
```

### Proposed Layout (with _format and _endianness)

Adding 2 bytes (`_format` + `_endianness`) to the struct. On 64-bit runtime, struct fields are laid out to minimize padding. Two extra `byte` fields can often fit into existing alignment gaps. Even in the worst case, the struct grows by at most 8 bytes (one alignment unit).

**Key insight:** The `_arrayOrdinal` (int, 4 bytes) is the last field and likely has 4 bytes of trailing padding to reach the next 8-byte boundary. Two bytes fit in that gap for free.

**Impact on OffsetTable:** `OffsetTable` stores `ParsedProperty[]` rented from `ArrayPool`. Larger struct = each array slot uses more memory. For typical schemas with 10-50 properties, this is negligible (50 * 8 bytes = 400 extra bytes worst case). The ArrayPool granularity is much coarser.

**Recommendation:** Place `_format` and `_endianness` as the FIRST fields in the struct declaration. This is a readability choice, not a performance one -- the CLR optimizes layout regardless of declaration order for reference types, but readonly structs follow declaration order (sequential layout).

**Correction:** Actually, C# structs use `LayoutKind.Sequential` by default. The CLR respects declaration order for structs. Place the two byte fields adjacent to each other (together they consume 2 bytes, which the CLR can pack efficiently). Placing them near other small fields (the ints) allows better packing.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Format dispatch | Visitor pattern, strategy delegates, polymorphism | Simple byte-comparison branch | ParsedProperty is a readonly struct -- no polymorphism possible. A byte comparison is a single CPU instruction. |
| Binary integer reads | Manual byte shifting for standard sizes | `BinaryPrimitives.ReadInt32LittleEndian/BigEndian` | Correct, optimized, handles endianness. Hand-rolling is a bug factory. |
| Struct size measurement | Manual counting | `System.Runtime.InteropServices.Marshal.SizeOf<ParsedProperty>()` or `Unsafe.SizeOf<ParsedProperty>()` | Use this in a test to verify struct size did not grow more than expected. |

## Common Pitfalls

### Pitfall 1: Binary Boolean Evaluated as JSON Boolean

**What goes wrong:** If a ParsedProperty has `_format = 1` (binary) but `GetBoolean()` executes the JSON path (checking for `_length == 4 && _buffer[_offset] == (byte)'t'`), a binary boolean (single byte: 0x01 = true) would return `false` because its length is 1, not 4.

**Why it happens:** Default `_format` is 0. If the binary walker forgets to pass `format: 1` to the constructor, all binary properties silently use JSON parsing logic.

**How to avoid:** The binary path constructors MUST require the format parameter explicitly (not optional, not defaulted). A test should construct a binary boolean and verify it returns `true` for `0x01`.

**Warning signs:** Any ParsedProperty constructor call in binary walker code that does NOT pass the format parameter.

### Pitfall 2: Existing Tests Break Due to Constructor Signature Change

**What goes wrong:** If existing constructors are modified (rather than keeping them and adding overloads), all existing test code and JSON walker code that calls those constructors will fail to compile.

**Why it happens:** Temptation to add `format` as a parameter to existing constructors with a default value. While C# supports default parameters, this changes the IL signature and breaks binary compatibility.

**How to avoid:** Keep ALL existing constructors exactly as they are. Add NEW overloads with the format parameter. The existing constructors implicitly set `_format = 0` via default struct initialization.

**Warning signs:** Any modification to existing constructor parameter lists.

### Pitfall 3: Branch Prediction Penalty Overestimated

**What goes wrong:** Developer avoids the format branch because of perceived performance cost, leading to over-engineered solutions (separate code paths, duplicated types, etc.).

**Why it happens:** Misunderstanding of branch prediction. Within a single ParseResult, ALL properties have the same format (either all JSON or all binary). The branch predictor learns the pattern after the first call and predicts correctly for all subsequent calls. The cost is ~0 ns for the predicted path.

**How to avoid:** Use the simple `if (_format == 0)` branch. Do not try to avoid it with delegates, vtables, or code duplication. Profile only if performance regression is measured.

### Pitfall 4: Forgetting to Update All GetXxx() Methods

**What goes wrong:** Developer adds the binary branch to `GetInt32()` and `GetBoolean()` but forgets `GetInt64()`, `GetDouble()`, `GetDecimal()`, or `GetString()`. Binary consumers call these methods and get JSON parsing of binary bytes, producing wrong results.

**How to avoid:** Update ALL six GetXxx() methods in a single pass. The binary path for each:
- `GetString()`: Use `Encoding.UTF8.GetString` or `Encoding.ASCII.GetString` (encoding TBD -- may need another byte or may be handled at parse time)
- `GetInt32()`: `BinaryPrimitives.ReadInt32LittleEndian/BigEndian` based on endianness
- `GetInt64()`: `BinaryPrimitives.ReadInt64LittleEndian/BigEndian`
- `GetDouble()`: `BinaryPrimitives.ReadDoubleLittleEndian/BigEndian`
- `GetBoolean()`: `_buffer[_offset] != 0`
- `GetDecimal()`: Binary path can throw `NotSupportedException` (no binary decimal type in ADR-16)

## Code Examples

### Complete GetXxx() Binary Branching Pattern

```csharp
// Source: Existing ParsedProperty.cs + Architecture research + ADR-16 type mapping

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public int GetInt32()
{
    if (_length == 0) return default;
    if (_format == 0)
    {
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
        return value;
    }
    // Binary: read raw bytes with endianness awareness
    var span = _buffer.AsSpan(_offset, _length);
    if (_endianness == 0) // little-endian
    {
        return _length switch
        {
            1 => (sbyte)span[0],
            2 => BinaryPrimitives.ReadInt16LittleEndian(span),
            4 => BinaryPrimitives.ReadInt32LittleEndian(span),
            _ => ThrowInvalidLength<int>(_length, _path)
        };
    }
    return _length switch
    {
        1 => (sbyte)span[0],
        2 => BinaryPrimitives.ReadInt16BigEndian(span),
        4 => BinaryPrimitives.ReadInt32BigEndian(span),
        _ => ThrowInvalidLength<int>(_length, _path)
    };
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public double GetDouble()
{
    if (_length == 0) return default;
    if (_format == 0)
    {
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out double value, out _);
        return value;
    }
    var span = _buffer.AsSpan(_offset, _length);
    if (_endianness == 0)
        return BinaryPrimitives.ReadDoubleLittleEndian(span);
    return BinaryPrimitives.ReadDoubleBigEndian(span);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool GetBoolean()
{
    if (_length == 0) return default;
    if (_format == 0)
        return _length == 4 && _buffer[_offset] == (byte)'t';
    return _buffer[_offset] != 0;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public string GetString()
{
    if (_length == 0) return string.Empty;
    if (_format == 0)
        return Encoding.UTF8.GetString(_buffer, _offset, _length);
    // Binary: UTF-8 decode (encoding-specific logic deferred to Phase 4)
    return Encoding.UTF8.GetString(_buffer, _offset, _length);
}
```

### Constructor Overload Pattern

```csharp
// New overload for binary properties (leaf/scalar)
internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format, byte endianness)
{
    _format = format;
    _endianness = endianness;
    _buffer = buffer;
    _offset = offset;
    _length = length;
    _path = path;
    _childTable = default;
    _childOrdinals = null;
    _directChildren = null;
    _arrayBuffer = null;
    _arrayOrdinal = -1;
}

// New overload for binary properties (with children)
internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format, byte endianness,
    OffsetTable childTable, Dictionary<string, int>? childOrdinals,
    ArrayBuffer? arrayBuffer, int arrayOrdinal)
{
    _format = format;
    _endianness = endianness;
    _buffer = buffer;
    _offset = offset;
    _length = length;
    _path = path;
    _childTable = childTable;
    _childOrdinals = childOrdinals;
    _directChildren = null;
    _arrayBuffer = arrayBuffer;
    _arrayOrdinal = arrayOrdinal;
}
```

### Format Constants

```csharp
// Internal constants for format values -- avoids magic numbers
internal static class PropertyFormat
{
    public const byte Utf8Json = 0;
    public const byte Binary = 1;
}

// Internal constants for endianness
internal static class PropertyEndianness
{
    public const byte Little = 0;
    public const byte Big = 1;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate type per format | Discriminated readonly struct | ADR-16 decision (2026) | Single ParsedProperty type serves all formats; consumers are format-agnostic |
| Runtime type checking | Byte-flag dispatch | Standard pattern in .NET value types | Near-zero cost branching due to branch prediction |

## Open Questions

1. **Should _endianness be added in Phase 1 or deferred to Phase 3?**
   - What we know: Adding it now costs ~0 bytes (alignment padding) and avoids a second struct layout change
   - What's unclear: Whether the binary path implementations should use endianness now (stub) or defer
   - Recommendation: Add the field NOW. Binary GetXxx() methods can use it immediately for the standard-size cases. Truncated-size cases (3-byte int32) are Phase 3 scope.

2. **Should binary GetXxx() paths throw or contain real implementations?**
   - What we know: The BinaryPrimitives API is stable and well-understood. The implementation for standard sizes is trivial.
   - What's unclear: Whether implementing now adds unnecessary risk to the backward-compatibility goal
   - Recommendation: Implement real binary paths for standard sizes (1/2/4/8 byte reads). They are straightforward and testable. Truncated sizes (3-byte int32) can throw for now.

3. **Should GetDecimal() have a binary path?**
   - What we know: ADR-16 does not define a decimal type. Only float32/float64.
   - What's unclear: Whether GetDecimal() should throw for binary format or convert from float64
   - Recommendation: Throw `NotSupportedException` for binary format in GetDecimal(). No binary decimal type exists in the spec.

4. **InternalsVisibleTo for future Gluey.Contract.Binary.Tests**
   - What we know: `Gluey.Contract.csproj` already has InternalsVisibleTo for `Gluey.Contract.Tests`, `Gluey.Contract.Json`, and `Gluey.Contract.Json.Tests`
   - What's unclear: Whether to add `Gluey.Contract.Binary` and `Gluey.Contract.Binary.Tests` now
   - Recommendation: Add them now. It is a one-line change with zero runtime impact and prevents forgetting later.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | `tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj` |
| Quick run command | `dotnet test tests/Gluey.Contract.Tests --no-restore -f net10.0` |
| Full suite command | `dotnet test --no-restore` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CORE-01 | ParsedProperty has format flag that dispatches GetXxx() | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~ParsedPropertyFormatTests" --no-restore -f net10.0` | No -- Wave 0 |
| CORE-01 | Binary GetBoolean returns true for 0x01, false for 0x00 | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~ParsedPropertyFormatTests" --no-restore -f net10.0` | No -- Wave 0 |
| CORE-01 | Binary GetInt32 reads little-endian and big-endian correctly | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~ParsedPropertyFormatTests" --no-restore -f net10.0` | No -- Wave 0 |
| CORE-01 | Binary GetDouble reads IEEE 754 bytes correctly | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~ParsedPropertyFormatTests" --no-restore -f net10.0` | No -- Wave 0 |
| CORE-02 | All existing ParsedProperty JSON tests pass unchanged | regression | `dotnet test tests/Gluey.Contract.Tests --no-restore` | Yes -- `ParsedPropertyTests.cs` (22 tests) |
| CORE-02 | All existing JSON contract tests pass unchanged | regression | `dotnet test tests/Gluey.Contract.Json.Tests --no-restore` | Yes -- 27 test files |

### Sampling Rate

- **Per task commit:** `dotnet test tests/Gluey.Contract.Tests --no-restore -f net10.0`
- **Per wave merge:** `dotnet test --no-restore` (all test projects, all target frameworks)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs` -- covers CORE-01 (binary format dispatch in GetXxx methods)
- [ ] No framework install needed -- NUnit 4.3.1 + FluentAssertions 8.0.1 already in place
- [ ] No shared fixtures needed -- tests use direct struct construction

## Sources

### Primary (HIGH confidence)
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` -- current struct layout, all GetXxx() implementations, all constructors
- `src/Gluey.Contract/Parsing/ParseResult.cs` -- return type, unchanged by this phase
- `src/Gluey.Contract/Parsing/OffsetTable.cs` -- ParsedProperty[] storage, ArrayPool usage
- `src/Gluey.Contract/Gluey.Contract.csproj` -- targets net9.0+net10.0, LangVersion 13, InternalsVisibleTo list
- `docs/adr/16-binary-format-contract.md` -- binary format specification, type mapping, endianness rules
- `docs/adr/2-zero-allocation-design.md` -- zero-allocation invariant, ParsedProperty is readonly struct
- `docs/adr/8-readonly-structs-over-classes.md` -- why readonly struct, no ref struct, no record struct
- `tests/Gluey.Contract.Tests/ParsedPropertyTests.cs` -- 22 existing tests that MUST pass unchanged
- `.planning/research/STACK.md` -- BinaryPrimitives API mapping for all ADR-16 types
- `.planning/research/PITFALLS.md` -- Pitfall 3 (breaking JSON consumers) directly relevant
- `.planning/research/ARCHITECTURE.md` -- Format flag integration pattern, constructor overload strategy

### Secondary (MEDIUM confidence)
- `.planning/research/ARCHITECTURE.md` Pattern 4 -- endianness storage analysis (two options with tradeoffs)

### Tertiary (LOW confidence)
- Struct padding/alignment analysis -- based on general .NET runtime knowledge, not measured for this specific struct. Recommend measuring with `Unsafe.SizeOf<ParsedProperty>()` before and after modification.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all BCL APIs verified in stack research
- Architecture: HIGH -- struct modification is well-understood; all patterns derive from existing codebase analysis
- Pitfalls: HIGH -- Pitfall 3 from domain research directly applies; mitigation is clear (default 0, keep existing constructors)
- Struct layout: MEDIUM -- alignment analysis is theoretical; should be measured empirically

**Research date:** 2026-03-19
**Valid until:** 2026-04-19 (stable domain -- readonly struct patterns do not change)
