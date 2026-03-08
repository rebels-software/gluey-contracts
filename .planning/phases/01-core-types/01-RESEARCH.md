# Phase 1: Core Types - Research

**Researched:** 2026-03-08
**Domain:** C# readonly struct value types, ArrayPool, zero-allocation patterns, .NET 9 / C# 13
**Confidence:** HIGH

## Summary

Phase 1 defines the foundational readonly struct types that every downstream phase produces and consumes. The types are pure data structures with no external dependencies, built entirely on BCL primitives: `byte[]` with offset/length for buffer references, `System.Buffers.ArrayPool<T>` for pooled storage, `System.Buffers.Text.Utf8Parser` for zero-allocation value materialization, and `System.Text.Encoding.UTF8` for string conversion.

The critical design tension is between the readonly struct constraint (ADR 8 -- no classes, no ref structs, no record structs) and the need for IDisposable on types that hold ArrayPool-rented buffers. A readonly struct CAN implement IDisposable -- the readonly modifier prevents mutation of fields, but Dispose() can still call `ArrayPool.Return()` since the pool reference and array reference are read (not mutated). The array is returned to the pool, not nulled out, so no field mutation occurs.

**Primary recommendation:** Implement all six types as readonly structs in the `Gluey.Contract` namespace. Use `System.Buffers.Text.Utf8Parser` for numeric materialization and `Encoding.UTF8.GetString` for string materialization. Use `ArrayPool<T>.Shared` for both OffsetTable and ErrorCollector backing arrays. Define the full ValidationErrorCode enum upfront (~28 values) covering all JSON Schema Draft 2020-12 keywords.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- ParsedProperty stores a reference to the byte buffer (byte[] + offset + length + path) -- self-contained, no buffer threading
- Type mismatch on GetX() returns default(T) -- zero branching, caller trusts schema validation
- GetString() allocates a new string each call -- allocation happens at materialization, outside the parse path
- No TryGetX() variants -- only GetString(), GetInt32(), GetInt64(), GetDouble(), GetBoolean(), GetDecimal()
- Expose RawBytes as ReadOnlySpan<byte> for advanced users who want to avoid materialization entirely
- One enum value per JSON Schema keyword (~25-30 values): TypeMismatch, RequiredMissing, MinimumExceeded, MaxLengthExceeded, PatternMismatch, etc.
- Full enum defined upfront in Phase 1 covering all keywords in the roadmap -- no breaking changes across phases
- Static compile-time string messages per error code -- no string interpolation, no runtime context in messages
- JSON Pointer path stored as pre-allocated string reference from the schema (invariant 3) -- zero allocation
- Both string indexer (result["name"]) and ordinal indexer (result[0]) for property access
- String indexer for ergonomics, ordinal for perf-critical paths -- both resolve to the same offset table
- Missing/absent property returns an empty ParsedProperty (length 0, HasValue = false) -- no exceptions
- Errors collection always accessible via ParseResult.Errors -- empty on success, populated on failure
- Supports foreach enumeration of all parsed properties via GetEnumerator()
- When ErrorCollector hits max capacity, error #64 (last slot) is replaced with a sentinel TooManyErrors entry
- Max error count configurable per schema (default 64) -- set at schema configuration time
- ErrorCollector uses ArrayPool<ValidationError>.Shared for pre-allocated buffer
- ErrorCollector implements IDisposable to return ArrayPool buffer

### Claude's Discretion
- Exact struct field ordering and padding for cache-line optimization
- Internal implementation of string-to-ordinal lookup in offset table
- GetEnumerator() implementation details (custom struct enumerator vs. other patterns)
- Exact set of ~25-30 error code enum values (named per JSON Schema keyword semantics)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CORE-01 | ParsedProperty readonly struct with offset, length, and path into byte buffer | Readonly struct pattern with byte[] + int offset + int length + string path fields; RawBytes via slicing |
| CORE-02 | On-demand value materialization via GetString(), GetInt32(), GetInt64(), GetDouble(), GetBoolean(), GetDecimal() | Utf8Parser.TryParse for numerics, Encoding.UTF8.GetString for strings, manual boolean byte comparison |
| CORE-03 | Offset table mapping schema property ordinals to byte positions (ArrayPool-backed) | ArrayPool<ParsedProperty>.Shared rental; Dictionary or FrozenDictionary for name-to-ordinal mapping |
| CORE-04 | ValidationError readonly struct with RFC 6901 path, error code enum, and static message | Simple readonly struct with string Path, ValidationErrorCode Code, string Message fields |
| CORE-05 | ErrorCollector with pre-allocated buffer, max 64 errors default | ArrayPool<ValidationError>.Shared rental; count tracking; sentinel overflow pattern |
| CORE-06 | ParseResult readonly struct with success/failure and parsed data access, IDisposable for buffer return | Readonly struct wrapping OffsetTable + ErrorCollector; cascading Dispose; indexers |
| CORE-07 | Dual API surface: TryParse (bool + out) and Parse (returns nullable, never throws) | Method signatures on JsonContractSchema; shared internal parse logic; Parse returns ParseResult? |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Buffers | (BCL) | ArrayPool<T>.Shared for buffer pooling | Built-in, zero-dependency, standard .NET pooling mechanism |
| System.Buffers.Text | (BCL) | Utf8Parser for zero-allocation numeric parsing from byte spans | The official .NET API for parsing UTF-8 bytes without string conversion |
| System.Text.Encoding | (BCL) | UTF8.GetString for string materialization | Standard .NET UTF-8 decoding |
| System.Runtime.CompilerServices | (BCL) | MethodImpl(AggressiveInlining) for hot-path methods | Performance attribute for small struct methods |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework | Already configured in test projects |
| FluentAssertions | 8.0.1 | Assertion library | Already configured, use for readable test assertions |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Utf8Parser | int.Parse with string conversion | Would allocate a string -- violates zero-allocation invariant |
| ArrayPool<T>.Shared | stackalloc | Cannot escape method scope, incompatible with readonly struct fields |
| Dictionary<string,int> for name lookup | FrozenDictionary<string,int> | FrozenDictionary is faster for read-only lookups but requires .NET 8+; both work on .NET 9 |

**Installation:**
```bash
# No additional packages needed -- all types are BCL
# Test packages already configured
```

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract/
    ParsedProperty.cs          # CORE-01, CORE-02
    OffsetTable.cs             # CORE-03
    ValidationError.cs         # CORE-04
    ValidationErrorCode.cs     # CORE-04 (enum)
    ValidationErrorMessages.cs # CORE-04 (static message lookup)
    ErrorCollector.cs          # CORE-05
    ParseResult.cs             # CORE-06
src/Gluey.Contract.Json/
    JsonContractSchema.cs      # CORE-07 (dual API signatures only)
tests/Gluey.Contract.Tests/
    ParsedPropertyTests.cs
    OffsetTableTests.cs
    ValidationErrorTests.cs
    ErrorCollectorTests.cs
    ParseResultTests.cs
```

### Pattern 1: Readonly Struct with ArrayPool Disposal
**What:** A readonly struct that rents from ArrayPool and returns the buffer on Dispose.
**When to use:** OffsetTable, ErrorCollector, ParseResult -- any type holding pooled arrays.
**Example:**
```csharp
// Readonly struct CAN implement IDisposable -- Dispose reads fields but does not mutate them
public readonly struct OffsetTable : IDisposable
{
    private readonly ParsedProperty[] _entries;
    private readonly int _count;

    internal OffsetTable(ParsedProperty[] entries, int count)
    {
        _entries = entries;
        _count = count;
    }

    public void Dispose()
    {
        if (_entries is not null)
        {
            ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true);
        }
    }
}
```

### Pattern 2: Zero-Allocation Value Materialization
**What:** Parse values directly from UTF-8 bytes without intermediate string allocation.
**When to use:** All GetX() methods except GetString().
**Example:**
```csharp
public readonly struct ParsedProperty
{
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly string _path;

    public int GetInt32()
    {
        if (_length == 0) return default;
        var span = _buffer.AsSpan(_offset, _length);
        Utf8Parser.TryParse(span, out int value, out _);
        return value;
    }

    public string GetString()
    {
        if (_length == 0) return string.Empty;
        // Allocation happens here intentionally -- outside parse path
        return Encoding.UTF8.GetString(_buffer, _offset, _length);
    }

    public ReadOnlySpan<byte> RawBytes => _buffer.AsSpan(_offset, _length);
    public bool HasValue => _length > 0;
}
```

### Pattern 3: Custom Struct Enumerator (Zero-Allocation Foreach)
**What:** A struct-based enumerator that allows `foreach` without boxing or allocation.
**When to use:** ParseResult.GetEnumerator() for iterating parsed properties.
**Example:**
```csharp
// Struct enumerator avoids boxing -- foreach calls duck-typed GetEnumerator()
public struct Enumerator
{
    private readonly ParsedProperty[] _entries;
    private readonly int _count;
    private int _index;

    internal Enumerator(ParsedProperty[] entries, int count)
    {
        _entries = entries;
        _count = count;
        _index = -1;
    }

    public ParsedProperty Current => _entries[_index];

    public bool MoveNext()
    {
        _index++;
        return _index < _count;
    }
}

// On the containing type:
public Enumerator GetEnumerator() => new Enumerator(_entries, _count);
```

### Pattern 4: Sentinel Overflow for Error Collection
**What:** When error buffer is full, replace the last slot with a TooManyErrors sentinel.
**When to use:** ErrorCollector when count reaches capacity.
**Example:**
```csharp
public void Add(ValidationError error)
{
    if (_count < _capacity - 1)
    {
        _errors[_count++] = error;
    }
    else if (_count == _capacity - 1)
    {
        // Last slot becomes sentinel
        _errors[_count] = new ValidationError(
            string.Empty,
            ValidationErrorCode.TooManyErrors,
            ValidationErrorMessages.Get(ValidationErrorCode.TooManyErrors));
        _count = _capacity;
    }
    // If _count >= _capacity, silently drop
}
```

### Anti-Patterns to Avoid
- **Boxing readonly structs via interface casts:** Never cast ParsedProperty to `object` or an interface variable. Use generic constraints (`where T : struct, ISomeInterface`) to avoid boxing.
- **Storing Span<byte> in readonly struct fields:** `Span<T>` is a ref struct and cannot be stored in a regular struct field. Use `byte[]` + offset + length instead; expose `ReadOnlySpan<byte>` only as a computed property.
- **Forgetting to return ArrayPool buffers:** Every `ArrayPool.Rent()` must pair with a `Return()`. Wrap in IDisposable and document that consumers must use `using`.
- **String interpolation in error messages:** Violates zero-allocation invariant. Use static pre-computed message strings per error code.
- **Using record struct:** Generates ToString() that allocates, GetHashCode/Equals that may not be needed. Explicit readonly struct is leaner and more predictable.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| UTF-8 numeric parsing | Manual byte-by-byte integer/decimal parser | `System.Buffers.Text.Utf8Parser.TryParse` | Handles edge cases (overflow, sign, leading zeros, exponents), heavily optimized in .NET runtime |
| UTF-8 string decoding | Manual byte-to-char conversion | `System.Text.Encoding.UTF8.GetString` | Handles multi-byte sequences, surrogate pairs, BOM, malformed sequences |
| Buffer pooling | Custom free-list or pre-allocated arrays | `System.Buffers.ArrayPool<T>.Shared` | Thread-safe, GC-aware, size-bucketed, battle-tested in ASP.NET Core |
| Boolean parsing from bytes | Manual comparison logic | Direct byte comparison (`true` = 4 bytes: 0x74,0x72,0x75,0x65) | Only two valid values; simple enough to inline but must match JSON spec exactly |

**Key insight:** The BCL provides all the low-level parsing primitives needed. The value of this phase is in struct layout, ownership semantics, and API shape -- not in parsing algorithms.

## Common Pitfalls

### Pitfall 1: Defensive Copies of Readonly Structs
**What goes wrong:** Calling methods on a readonly struct through an `in` parameter or readonly field creates a hidden defensive copy if the method is not marked readonly.
**Why it happens:** The compiler cannot prove the method won't mutate the struct, so it copies first.
**How to avoid:** In C# 8+, all members of a `readonly struct` are implicitly readonly. Ensure the struct IS declared `readonly struct`, not just a struct with readonly fields.
**Warning signs:** Unexpected allocations in BenchmarkDotNet when passing structs by `in`.

### Pitfall 2: ArrayPool Buffer Size Mismatch
**What goes wrong:** `ArrayPool.Rent(64)` may return an array of length 128 (power-of-2 bucketing). Code that iterates `array.Length` instead of the requested count processes garbage data.
**Why it happens:** ArrayPool returns the nearest bucket size, not the exact requested size.
**How to avoid:** Always track the logical count separately from the array. Never iterate `_entries.Length` -- iterate `_count`.
**Warning signs:** Tests see unexpected default values or stale data at the end of arrays.

### Pitfall 3: Double Dispose of ArrayPool Buffers
**What goes wrong:** Returning the same array to ArrayPool twice corrupts pool state and causes mysterious bugs.
**Why it happens:** Struct copying -- if a struct implementing IDisposable is copied (e.g., passed by value), both copies may call Dispose.
**How to avoid:** Document that these types must be used with `using` statements and not copied after construction. Consider a `_disposed` boolean field (though this adds a field to the struct). At minimum, null-check the array before returning.
**Warning signs:** `ArgumentException` from ArrayPool, or data corruption in unrelated code.

### Pitfall 4: GetString() on JSON String Values Includes Quotes
**What goes wrong:** The byte buffer contains the raw JSON including quote characters. `GetString()` returns `"hello"` (with quotes) instead of `hello`.
**Why it happens:** The offset/length from the tokenizer may or may not include the surrounding quotes depending on tokenizer design.
**How to avoid:** This is a Phase 1 / Phase 4 interface contract. Decide now: ParsedProperty offset/length should point to the VALUE content (inside quotes), not the raw JSON token. Document this contract clearly.
**Warning signs:** String values come back with escaped characters or surrounding quotes.

### Pitfall 5: Decimal Parsing Precision
**What goes wrong:** `Utf8Parser.TryParse` for decimal may lose precision for very large or very precise numbers.
**Why it happens:** Decimal has 28-29 significant digits; JSON allows arbitrary precision.
**How to avoid:** For JSON Schema validation purposes, decimal precision is sufficient. Document that GetDecimal() follows .NET decimal semantics (28-29 digits). Users needing arbitrary precision should use RawBytes.
**Warning signs:** Round-trip failures on numbers with more than 28 digits.

## Code Examples

### ParsedProperty -- Complete Materialization Pattern
```csharp
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gluey.Contract;

/// <summary>
/// A zero-allocation accessor into parsed byte data.
/// Holds an offset and length into the original byte buffer.
/// Values are materialized only when accessed via GetString(), GetInt32(), etc.
/// </summary>
public readonly struct ParsedProperty
{
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly string _path;

    internal ParsedProperty(byte[] buffer, int offset, int length, string path)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
        _path = path;
    }

    /// <summary>The RFC 6901 JSON Pointer path for this property.</summary>
    public string Path => _path ?? string.Empty;

    /// <summary>Whether this property has a value (was present in the parsed data).</summary>
    public bool HasValue => _length > 0;

    /// <summary>The raw bytes of this property's value.</summary>
    public ReadOnlySpan<byte> RawBytes =>
        _buffer is not null ? _buffer.AsSpan(_offset, _length) : ReadOnlySpan<byte>.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString()
    {
        if (_length == 0) return string.Empty;
        return Encoding.UTF8.GetString(_buffer, _offset, _length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInt64()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out long value, out _);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out double value, out _);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBoolean()
    {
        if (_length == 0) return default;
        // JSON: "true" = 4 bytes, "false" = 5 bytes
        return _length == 4
            && _buffer[_offset] == (byte)'t';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal GetDecimal()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out decimal value, out _);
        return value;
    }

    /// <summary>Returns a default ParsedProperty with no value.</summary>
    public static ParsedProperty Empty => default;
}
```

### ValidationErrorCode -- Complete Enum
```csharp
namespace Gluey.Contract;

/// <summary>
/// Machine-readable error codes for JSON Schema validation failures.
/// One value per JSON Schema keyword.
/// </summary>
public enum ValidationErrorCode : byte
{
    // -- General --
    None = 0,

    // -- Type (VALD-01) --
    TypeMismatch,

    // -- Enum/Const (VALD-02) --
    EnumMismatch,
    ConstMismatch,

    // -- Object keywords (VALD-03, VALD-04) --
    RequiredMissing,
    AdditionalPropertyNotAllowed,

    // -- Array keywords (VALD-05) --
    ItemsInvalid,
    PrefixItemsInvalid,

    // -- Numeric constraints (VALD-06) --
    MinimumExceeded,
    MaximumExceeded,
    ExclusiveMinimumExceeded,
    ExclusiveMaximumExceeded,
    MultipleOfInvalid,

    // -- String constraints (VALD-07) --
    MinLengthExceeded,
    MaxLengthExceeded,
    PatternMismatch,

    // -- Array/Object size constraints (VALD-08) --
    MinItemsExceeded,
    MaxItemsExceeded,
    MinPropertiesExceeded,
    MaxPropertiesExceeded,

    // -- Composition (VALD-09) --
    AllOfInvalid,
    AnyOfInvalid,
    OneOfInvalid,
    NotInvalid,

    // -- Conditionals (VALD-10, VALD-11) --
    IfThenInvalid,
    IfElseInvalid,
    DependentRequiredMissing,
    DependentSchemaInvalid,

    // -- Advanced (VALD-12, VALD-13, VALD-14) --
    PatternPropertyInvalid,
    PropertyNameInvalid,
    ContainsInvalid,
    MinContainsExceeded,
    MaxContainsExceeded,
    UniqueItemsViolation,

    // -- Format (VALD-15, VALD-16) --
    FormatInvalid,

    // -- Sentinel --
    TooManyErrors,
}
```

### ValidationError -- Readonly Struct
```csharp
namespace Gluey.Contract;

/// <summary>
/// A validation error with RFC 6901 JSON Pointer path, error code, and static message.
/// </summary>
public readonly struct ValidationError
{
    /// <summary>RFC 6901 JSON Pointer path to the failing property.</summary>
    public readonly string Path;

    /// <summary>Machine-readable error code.</summary>
    public readonly ValidationErrorCode Code;

    /// <summary>Human-readable static error message.</summary>
    public readonly string Message;

    public ValidationError(string path, ValidationErrorCode code, string message)
    {
        Path = path;
        Code = code;
        Message = message;
    }
}
```

### Static Error Messages Pattern
```csharp
namespace Gluey.Contract;

internal static class ValidationErrorMessages
{
    // Pre-allocated static strings -- zero allocation at runtime
    private static readonly string[] Messages = new string[(int)ValidationErrorCode.TooManyErrors + 1];

    static ValidationErrorMessages()
    {
        Messages[(int)ValidationErrorCode.TypeMismatch] = "Value does not match the expected type.";
        Messages[(int)ValidationErrorCode.RequiredMissing] = "Required property is missing.";
        Messages[(int)ValidationErrorCode.TooManyErrors] = "Too many validation errors; remaining errors truncated.";
        // ... all other codes
    }

    public static string Get(ValidationErrorCode code) => Messages[(int)code] ?? string.Empty;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Span<byte>` in struct fields | `byte[]` + offset + length in readonly struct | C# 7.2+ | ref struct restriction makes Span unusable in async / collections |
| Manual buffer management | `ArrayPool<T>.Shared` | .NET Core 2.1+ | Thread-safe pooling, standard across ecosystem |
| `int.Parse(string)` from bytes | `Utf8Parser.TryParse(ReadOnlySpan<byte>)` | .NET Core 2.1+ | Zero-allocation numeric parsing directly from UTF-8 bytes |
| `record struct` for value types | `readonly struct` for perf-critical types | C# 10 added record struct | record struct generates ToString/Equals which allocate; explicit readonly struct is leaner |
| `FrozenDictionary` not available | `FrozenDictionary<TKey,TValue>` for read-only lookups | .NET 8+ | Faster than Dictionary for read-only scenarios; good for name-to-ordinal maps |

**Deprecated/outdated:**
- `Utf8Parser` is still the recommended API for .NET 9; no replacement planned. The newer `IUtf8SpanParsable<T>` interface (introduced .NET 8) is an alternative but requires generic constraints on the caller side.

## Open Questions

1. **GetString() for JSON string values -- quote handling**
   - What we know: The byte buffer contains raw JSON. String values are surrounded by quotes and may contain escape sequences (`\"`, `\n`, `\\`, `\uXXXX`).
   - What's unclear: Should ParsedProperty's offset/length point inside the quotes (content only) or include quotes? Should GetString() handle JSON unescaping?
   - Recommendation: Define the contract now that offset/length point to content INSIDE quotes (no quotes). JSON unescape handling belongs to the tokenizer (Phase 4). GetString() simply calls UTF8.GetString on the raw bytes. Document this as an interface contract between Phase 1 and Phase 4.

2. **OffsetTable name-to-ordinal lookup implementation**
   - What we know: Need both string indexer and ordinal indexer. String indexer must map property names to ordinals.
   - What's unclear: The mapping is schema-level (shared across parses), not per-parse. Should the lookup table live in OffsetTable or in the schema?
   - Recommendation: The name-to-ordinal dictionary belongs to the schema (it is immutable per schema). OffsetTable only needs ordinal-indexed storage. The string indexer on ParseResult delegates to the schema's name-to-ordinal map, then indexes into OffsetTable by ordinal. This keeps OffsetTable simple (just an array) and the mapping reusable.

3. **ParseResult nullable return from Parse()**
   - What we know: CORE-07 says Parse returns nullable and never throws. CONTEXT.md says Parse returns nullable.
   - What's unclear: The ADR 4 originally described a Result<T> pattern; PROJECT.md still mentions Result<T>. The CONTEXT.md decisions override this -- Parse() returns `ParseResult?` (null on failure or non-null with error state?).
   - Recommendation: Follow CONTEXT.md strictly. Parse() returns `ParseResult?`. On well-formed input (even with validation errors), return a non-null ParseResult with errors populated. Return null only for catastrophic failures (malformed JSON that cannot be parsed at all). This aligns with "never throws" invariant while giving callers null-check ergonomics.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Tests --filter "Category!=Slow" --no-build -q` |
| Full suite command | `dotnet test tests/Gluey.Contract.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CORE-01 | ParsedProperty holds offset, length, path; HasValue correct | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ParsedPropertyTests -q` | No -- Wave 0 |
| CORE-02 | GetString/GetInt32/GetInt64/GetDouble/GetBoolean/GetDecimal materialize correctly from bytes | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ParsedPropertyTests -q` | No -- Wave 0 |
| CORE-03 | OffsetTable maps ordinals to ParsedProperty; ArrayPool rental and return | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~OffsetTableTests -q` | No -- Wave 0 |
| CORE-04 | ValidationError carries path, code, message | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ValidationErrorTests -q` | No -- Wave 0 |
| CORE-05 | ErrorCollector pre-allocates, collects, overflows with sentinel | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ErrorCollectorTests -q` | No -- Wave 0 |
| CORE-06 | ParseResult exposes success/failure, indexers, IDisposable | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ParseResultTests -q` | No -- Wave 0 |
| CORE-07 | TryParse returns bool+out, Parse returns nullable; neither throws | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter FullyQualifiedName~JsonContractSchemaTests -q` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Tests --no-build -q`
- **Per wave merge:** `dotnet test` (full solution)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Tests/ParsedPropertyTests.cs` -- covers CORE-01, CORE-02
- [ ] `tests/Gluey.Contract.Tests/OffsetTableTests.cs` -- covers CORE-03
- [ ] `tests/Gluey.Contract.Tests/ValidationErrorTests.cs` -- covers CORE-04
- [ ] `tests/Gluey.Contract.Tests/ErrorCollectorTests.cs` -- covers CORE-05
- [ ] `tests/Gluey.Contract.Tests/ParseResultTests.cs` -- covers CORE-06
- [ ] `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` -- covers CORE-07 (API signatures compile)

## Sources

### Primary (HIGH confidence)
- ADR 2 (zero-allocation design), ADR 4 (dual API), ADR 6 (JSON Pointer paths), ADR 8 (readonly structs) -- project docs
- Invariants 1, 3, 5, 6 -- project docs
- CONTEXT.md -- locked user decisions
- [Utf8Parser API Reference](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.text.utf8parser?view=net-9.0) -- BCL API verification
- Existing csproj files -- .NET 9, C# 13, NUnit 4.3.1, FluentAssertions 8.0.1 confirmed

### Secondary (MEDIUM confidence)
- [What's new in C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13) -- C# 13 features confirmed
- [JSON Schema Draft 2020-12 Validation](https://json-schema.org/draft/2020-12/json-schema-validation) -- keyword list for enum values
- [ref struct IDisposable in C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct) -- confirms readonly struct IDisposable is valid

### Tertiary (LOW confidence)
- None -- all findings verified against official sources

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all BCL types, no external dependencies, verified against .NET 9 docs
- Architecture: HIGH -- patterns derived directly from ADRs and locked decisions in CONTEXT.md
- Pitfalls: HIGH -- well-known patterns in .NET struct/ArrayPool ecosystem, verified by experience and documentation
- Error code enum: MEDIUM -- keyword list derived from JSON Schema Draft 2020-12 spec; exact naming is Claude's discretion per CONTEXT.md

**Research date:** 2026-03-08
**Valid until:** 2026-04-08 (stable domain -- .NET 9 BCL and C# 13 are released and stable)
