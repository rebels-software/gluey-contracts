# Phase 4: JSON Byte Reader - Research

**Researched:** 2026-03-09
**Domain:** JSON tokenization, byte offset tracking, Utf8JsonReader wrapping
**Confidence:** HIGH

## Summary

Phase 4 implements a `JsonByteReader` ref struct that wraps `System.Text.Json.Utf8JsonReader` to provide forward-only JSON tokenization with native byte offset tracking. The reader exposes per-token type, byte offset, and byte length, where string/property name offsets point to content inside quotes (matching the `ParsedProperty` contract from Phase 1). Structural JSON errors are detected and reported through a separate error type distinct from schema validation errors.

The implementation is straightforward because `Utf8JsonReader` already provides `TokenStartIndex` (long), `ValueSpan` (content without quotes), and `CurrentDepth`. The wrapper's job is to translate BCL token types to our own `JsonByteTokenType` enum, compute content-inside-quotes offsets for strings/property names, and catch `JsonException` to produce `JsonReadError` values. Since we always construct from `ReadOnlySpan<byte>`, `HasValueSequence` is always false and `ValueSpan` is always valid.

**Primary recommendation:** Wrap `Utf8JsonReader` via composition in a ref struct. Use `TokenStartIndex + 1` for string/property content offset and `ValueSpan.Length` for content length. Keep the reader internal -- it is consumed only by the Phase 9 walker.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- Wrap System.Text.Json's Utf8JsonReader -- battle-tested, zero-alloc, already used in Phase 2's schema loader
- JsonByteReader is a ref struct -- natural fit since Utf8JsonReader is itself a ref struct
- Forward-only Read()-in-a-loop design -- mirrors Utf8JsonReader pattern, zero allocation
- Lives in Gluey.Contract.Json package -- JSON-specific implementation, consistent with ADR 5
- Minimal per-token info: token type + byte offset + byte length -- depth/parent tracking is the walker's job (Phase 9)
- Own JsonByteTokenType enum -- decoupled from BCL's JsonTokenType
- Separate PropertyName token type distinct from String value tokens
- ByteOffset/ByteLength points to content inside quotes for string/property name tokens -- matches ParsedProperty contract
- Fail on first structural error -- malformed JSON is unrecoverable
- Separate error type (not ValidationErrorCode/ErrorCollector) -- structural errors are fundamentally different from schema validation errors
- Read() returns false when hitting invalid JSON; caller checks reader.Error property for details
- Error type lives in Gluey.Contract.Json -- JSON structural errors are format-specific
- Span-primary: core implementation takes ReadOnlySpan<byte>; byte[] and ReadOnlyMemory<byte> overloads implicitly convert
- Internal visibility -- reader is an implementation detail consumed by the walker (Phase 9)
- Type named JsonByteReader

### Claude's Discretion
- JsonByteTokenType enum values and whether to split Number into Integer/Number
- JsonReadError readonly struct field layout and error kind enum values
- How the Utf8JsonReader wrapper extracts byte offsets (TokenStartIndex, BytesConsumed, etc.)
- Internal helper methods for offset calculation around quoted strings
- Whether to expose CurrentDepth from the underlying Utf8JsonReader for convenience

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| READ-01 | JSON byte tokenizer with native byte offset tracking | Utf8JsonReader.TokenStartIndex provides token start; ValueSpan.Length gives content length; wrapper translates to JsonByteTokenType + offset + length |
| READ-02 | Accept byte[], ReadOnlySpan<byte>, and ReadOnlyMemory<byte> inputs | ReadOnlySpan<byte> is primary; byte[] and ReadOnlyMemory<byte> implicitly convert to Span; static factory methods provide typed entry points |
| READ-03 | Structural JSON validation (well-formedness) | Utf8JsonReader throws JsonException on malformed JSON; wrapper catches and translates to JsonReadError with byte offset |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | BCL (net9.0) | Utf8JsonReader for JSON tokenization | Battle-tested, zero-alloc, already used in Phase 2 loader |

### Supporting
No additional libraries needed. All types are BCL or project-defined.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Utf8JsonReader wrapper | Custom byte-level tokenizer | Massive effort, re-invents well-tested parser, no benefit |
| Own JsonByteTokenType | BCL JsonTokenType directly | Creates coupling to BCL enum, can't tailor to library needs |

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract.Json/
    JsonByteReader.cs        # ref struct wrapping Utf8JsonReader
    JsonByteTokenType.cs     # Token type enum (decoupled from BCL)
    JsonReadError.cs         # Structural error readonly struct
    JsonReadErrorKind.cs     # Error kind enum
```

### Pattern 1: Ref Struct Wrapper with Composition
**What:** `JsonByteReader` is a `ref struct` that holds a `Utf8JsonReader` field and translates each `Read()` call into token type + offset + length.
**When to use:** When wrapping another ref struct and need zero-allocation forwarding.
**Example:**
```csharp
// Source: Microsoft Docs - Utf8JsonReader API
internal ref struct JsonByteReader
{
    private Utf8JsonReader _reader;
    private JsonReadError _error;

    public JsonByteReader(ReadOnlySpan<byte> utf8Json)
    {
        _reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        _error = default;
    }

    public JsonByteTokenType TokenType { get; private set; }
    public int ByteOffset { get; private set; }
    public int ByteLength { get; private set; }
    public JsonReadError Error => _error;
    public bool HasError => _error.Kind != JsonReadErrorKind.None;

    public bool Read()
    {
        try
        {
            if (!_reader.Read())
                return false;

            TokenType = MapTokenType(_reader.TokenType);
            ComputeOffsets();
            return true;
        }
        catch (JsonException ex)
        {
            _error = new JsonReadError(
                JsonReadErrorKind.InvalidJson,
                (int)_reader.BytesConsumed,
                ex.Message);
            return false;
        }
    }
}
```

### Pattern 2: Content-Inside-Quotes Offset Calculation
**What:** For String and PropertyName tokens, offset/length must point to content inside quotes to match ParsedProperty contract.
**When to use:** Every time a string or property name token is encountered.
**Critical detail from official docs:**
- `TokenStartIndex` points to **before the start quote** for strings/property names
- `ValueSpan` contains content **without quotes** (raw bytes between quotes, still with escape sequences)
- Therefore: content offset = `TokenStartIndex + 1`, content length = `ValueSpan.Length`
**Example:**
```csharp
// Source: Microsoft Docs - Utf8JsonReader.TokenStartIndex
// "For JSON strings (including property names), this value points to before the start quote."
private void ComputeOffsets()
{
    if (TokenType == JsonByteTokenType.String || TokenType == JsonByteTokenType.PropertyName)
    {
        // TokenStartIndex points to the opening quote
        // Content starts one byte after the quote
        ByteOffset = (int)_reader.TokenStartIndex + 1;
        ByteLength = _reader.ValueSpan.Length;
    }
    else
    {
        // For all other tokens (numbers, bools, null, structural),
        // TokenStartIndex points to the first byte of the token
        ByteOffset = (int)_reader.TokenStartIndex;
        ByteLength = _reader.ValueSpan.Length;
    }
}
```

### Pattern 3: Static Factory Methods for Multi-Input
**What:** Static `Create` methods or constructor overloads for byte[], ReadOnlySpan<byte>, ReadOnlyMemory<byte>.
**When to use:** READ-02 requires accepting all three input types.
**Example:**
```csharp
// byte[] and ReadOnlyMemory<byte> implicitly convert to ReadOnlySpan<byte>
// The constructor taking ReadOnlySpan<byte> covers all three cases.
// However, since ref structs can't have interface constraints,
// separate static factory methods or overloaded constructors are cleanest.

internal ref struct JsonByteReader
{
    // Primary constructor -- Span is the core implementation
    public JsonByteReader(ReadOnlySpan<byte> utf8Json) { ... }

    // byte[] implicitly converts to ReadOnlySpan<byte> -- no extra overload needed
    // ReadOnlyMemory<byte>.Span property provides the span -- caller does .Span
    // But for API clarity, explicit overloads are fine since the conversion is trivial
}
```

### Anti-Patterns to Avoid
- **Storing ReadOnlySpan<byte> in a field:** Ref structs can hold spans, but be careful about lifetime. The `Utf8JsonReader` already handles this correctly since it stores the span internally.
- **Using int for TokenStartIndex without cast:** `Utf8JsonReader.TokenStartIndex` is `long`. Cast to `int` is safe because we operate on contiguous buffers (no streaming), but the cast must be explicit.
- **Allocating strings for error messages:** Error messages should be static string constants or compile-time interpolated strings, not dynamically constructed.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON tokenization | Custom byte-by-byte parser | Utf8JsonReader | Handles UTF-8, escapes, numbers, structural validation, edge cases |
| Structural validation | Manual brace/bracket matching | Utf8JsonReader throws JsonException | Already validates well-formedness during tokenization |
| UTF-8 string boundary detection | Manual quote scanning | TokenStartIndex + ValueSpan | BCL already computes these precisely |

**Key insight:** Utf8JsonReader does 99% of the work. The wrapper's value is translating BCL types to project-specific types and computing content-inside-quotes offsets.

## Common Pitfalls

### Pitfall 1: TokenStartIndex Points to Quote, Not Content
**What goes wrong:** Using `TokenStartIndex` directly as the content offset for strings gives an offset that includes the opening quote character.
**Why it happens:** The official docs state "For JSON strings (including property names), this value points to before the start quote."
**How to avoid:** Always add 1 to `TokenStartIndex` for String and PropertyName tokens.
**Warning signs:** ParsedProperty.GetString() returns a leading quote character.

### Pitfall 2: ValueSpan Length vs Raw Byte Length for Escaped Strings
**What goes wrong:** `ValueSpan.Length` gives the length of the raw (still-escaped) content between quotes, which is correct for our use case. However, if `ValueIsEscaped` is true, the raw bytes in the buffer contain escape sequences (e.g., `\"` takes 2 bytes but represents 1 character).
**Why it happens:** ParsedProperty stores raw byte offset/length into the original buffer. The reader should report raw byte positions, not unescaped positions.
**How to avoid:** Use `ValueSpan.Length` directly -- it gives the raw byte count between quotes, which is exactly what ParsedProperty needs. The `ValueIsEscaped` property is informational but doesn't affect offset/length calculation for our purposes.
**Warning signs:** Escaped strings cause off-by-N errors in subsequent token offsets.

### Pitfall 3: Casting TokenStartIndex from long to int
**What goes wrong:** `TokenStartIndex` is `long` but our reader uses `int` for offsets (matching ParsedProperty's int offset/length).
**Why it happens:** Utf8JsonReader supports multi-segment sequences that could exceed int range.
**How to avoid:** Since we always operate on contiguous `ReadOnlySpan<byte>` (max ~2GB), casting to int is safe. But the cast must be explicit and documented.
**Warning signs:** Compilation errors if implicit conversion is assumed.

### Pitfall 4: Structural Tokens Have Zero-Length ValueSpan
**What goes wrong:** StartObject `{`, EndObject `}`, StartArray `[`, EndArray `]` tokens have `ValueSpan.Length` of 1 (the delimiter character). But True/False/Null tokens have lengths 4/5/4.
**Why it happens:** ValueSpan contains the raw token bytes.
**How to avoid:** For structural delimiters, `ValueSpan.Length` correctly returns the delimiter byte count. This is the correct behavior -- no special handling needed.
**Warning signs:** None, as long as you use ValueSpan.Length consistently.

### Pitfall 5: JsonException Message Contains Line/Column, Not Byte Offset
**What goes wrong:** `JsonException.Message` includes line number and byte position on line, not absolute byte offset.
**Why it happens:** JsonException is designed for human-readable error messages.
**How to avoid:** Use `_reader.BytesConsumed` at the time of the exception to get the approximate byte offset of the error. The `JsonException.BytePositionInLine` and `JsonException.LineNumber` properties provide more detail if needed.
**Warning signs:** Error byte offsets are wrong when input has newlines.

## Code Examples

### Complete Read Loop Pattern
```csharp
// Source: Established pattern from JsonSchemaLoader.cs in this project
var reader = new JsonByteReader(utf8Json);
while (reader.Read())
{
    // reader.TokenType  -- JsonByteTokenType
    // reader.ByteOffset -- int, content start (inside quotes for strings)
    // reader.ByteLength -- int, content length (inside quotes for strings)
}

if (reader.HasError)
{
    // reader.Error.Kind       -- JsonReadErrorKind
    // reader.Error.ByteOffset -- int, approximate position of error
    // reader.Error.Message    -- string, descriptive error message
}
```

### JsonByteTokenType Enum (Recommended)
```csharp
// Decoupled from BCL's JsonTokenType; tailored to library needs
internal enum JsonByteTokenType : byte
{
    None = 0,
    StartObject,      // {
    EndObject,        // }
    StartArray,       // [
    EndArray,         // ]
    PropertyName,     // Distinct from String -- walker can trivially distinguish keys from values
    String,           // String value
    Number,           // All numeric values (no Integer/Number split recommended -- schema validation handles type checking)
    True,             // Boolean true
    False,            // Boolean false
    Null,             // null
}
```

**Recommendation on Integer/Number split:** Do NOT split. The reader's job is tokenization, not type interpretation. Whether a JSON number `42` is an "integer" or "number" for schema validation purposes is the validator's responsibility (Phase 5, VALD-01). The reader should report all numeric tokens as `Number`.

### JsonReadError Readonly Struct (Recommended)
```csharp
// Structural error -- fundamentally different from ValidationError
// No JSON Pointer path, no schema context
internal readonly struct JsonReadError
{
    public JsonReadErrorKind Kind { get; }
    public int ByteOffset { get; }
    public string Message { get; }

    public JsonReadError(JsonReadErrorKind kind, int byteOffset, string message)
    {
        Kind = kind;
        ByteOffset = byteOffset;
        Message = message;
    }
}

internal enum JsonReadErrorKind : byte
{
    None = 0,
    InvalidJson,         // Catch-all for JsonException from Utf8JsonReader
    UnexpectedEndOfData, // Truncated input
    MaxDepthExceeded,    // Nesting too deep (Utf8JsonReader enforces default 64)
}
```

### CurrentDepth Recommendation
**Recommendation:** Do NOT expose CurrentDepth from the underlying reader. Per the locked decision, "depth/parent tracking is the walker's job (Phase 9)." Exposing it would blur the boundary between reader and walker responsibilities. The walker in Phase 9 can track depth itself if needed, or access the underlying Utf8JsonReader's depth through its own reader instance.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| JsonDocument for parsing | Utf8JsonReader for zero-alloc | .NET Core 3.0 (2019) | No DOM allocation needed |
| Custom tokenizers | Wrap BCL Utf8JsonReader | Stable since .NET Core 3.0 | Leverage battle-tested parser |
| JsonTokenType direct use | Custom enum mapping | Project decision | Decouples from BCL versioning |

**Deprecated/outdated:**
- Newtonsoft.Json's JsonTextReader: Allocates strings, not suitable for zero-alloc path
- JsonDocument.Parse: Creates a DOM, defeats zero-allocation purpose

## Open Questions

1. **ValueSpan for escaped strings contains raw escape sequences**
   - What we know: `ValueSpan` gives raw bytes including `\` escape characters. `ValueIsEscaped` indicates when escapes exist. `TokenStartIndex + 1` still correctly points to content start.
   - What's unclear: Whether ParsedProperty consumers expect raw (escaped) or unescaped content. Since ParsedProperty.GetString() calls `Encoding.UTF8.GetString()`, it would return the escaped form (e.g., `hello\"world` instead of `hello"world`).
   - Recommendation: For Phase 4, store raw byte offset/length (escaped). This is consistent with zero-allocation goals. If unescaping is needed, it's a Phase 9 walker concern. Flag this for Phase 9 design review.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~JsonByteReader" --no-build -q` |
| Full suite command | `dotnet test --no-build -q` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| READ-01 | Tokenizer reports correct type, offset, length for each token | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~JsonByteReaderTests" -q` | No -- Wave 0 |
| READ-01 | String/PropertyName offsets point to content inside quotes | unit | same as above | No -- Wave 0 |
| READ-01 | Number, boolean, null tokens report correct offsets | unit | same as above | No -- Wave 0 |
| READ-02 | byte[] input works via implicit Span conversion | unit | same as above | No -- Wave 0 |
| READ-02 | ReadOnlyMemory<byte> input works via .Span | unit | same as above | No -- Wave 0 |
| READ-03 | Mismatched braces detected (e.g., `{"a": 1]`) | unit | same as above | No -- Wave 0 |
| READ-03 | Invalid tokens detected (e.g., `{nope}`) | unit | same as above | No -- Wave 0 |
| READ-03 | Truncated input detected (e.g., `{"a":`) | unit | same as above | No -- Wave 0 |
| READ-03 | Error includes byte offset and descriptive kind | unit | same as above | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~JsonByteReader" --no-build -q`
- **Per wave merge:** `dotnet test --no-build -q`
- **Phase gate:** Full suite green before /gsd:verify-work

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/JsonByteReaderTests.cs` -- covers READ-01, READ-02, READ-03
- [ ] No new test fixtures or framework install needed -- existing test infrastructure covers all needs

## Sources

### Primary (HIGH confidence)
- [Microsoft Docs - Utf8JsonReader.TokenStartIndex](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader.tokenstartindex?view=net-9.0) -- "For JSON strings (including property names), this value points to before the start quote"
- [Microsoft Docs - Utf8JsonReader.ValueSpan](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader.valuespan?view=net-9.0) -- Raw content without quotes, may contain escape sequences
- [Microsoft Docs - Utf8JsonReader Struct](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader?view=net-9.0) -- Full API surface, properties, methods
- [Microsoft Docs - How to use Utf8JsonReader](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader) -- Usage patterns and best practices
- Existing codebase: `JsonSchemaLoader.cs` -- Established Utf8JsonReader wrapping pattern in this project

### Secondary (MEDIUM confidence)
- [dotnet/runtime Utf8JsonReader source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.cs) -- Implementation details
- [dotnet/runtime Issue #28131](https://github.com/dotnet/runtime/issues/28131) -- TokenStartIndex API discussion

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - BCL System.Text.Json, already used in project
- Architecture: HIGH - Wrapping pattern proven in JsonSchemaLoader, official docs confirm offset semantics
- Pitfalls: HIGH - TokenStartIndex quote behavior verified via official docs, ValueSpan semantics confirmed

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable BCL API, no breaking changes expected)
