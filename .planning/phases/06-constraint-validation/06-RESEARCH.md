# Phase 6: Constraint Validation - Research

**Researched:** 2026-03-09
**Domain:** JSON Schema Draft 2020-12 constraint validation (numeric, string, collection size)
**Confidence:** HIGH

## Summary

Phase 6 implements value-level constraint validators for three keyword groups: numeric (minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf), string (minLength, maxLength, pattern), and collection size (minItems, maxItems, minProperties, maxProperties). All infrastructure is already in place -- SchemaNode stores all constraint fields, ValidationErrorCode/Messages have all entries, ErrorCollector is ready, and JsonSchemaLoader already parses all relevant keywords from schema JSON.

The work is creating 4 new `internal static class` validator files plus a `CompiledPattern` field on SchemaNode with regex compilation in JsonSchemaLoader. Each validator follows the established Phase 5 pattern: static methods returning bool, pushing errors to ErrorCollector. The validators are pure check functions -- they receive already-parsed values (decimal for numerics, string for pattern, int for counts) and compare against schema constraints.

**Primary recommendation:** Split into two plans: (1) NumericValidator + StringValidator + CompiledPattern on SchemaNode + loader integration, (2) ArrayValidator + ObjectValidator. This keeps plan scope small and separates the more complex work (decimal parsing, Rune counting, regex compilation) from the simpler count-based validators.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Decimal arithmetic throughout -- SchemaNode already stores constraints as `decimal?`, validators parse token bytes to `decimal` via `Utf8JsonReader.TryGetDecimal()`
- Parsing: wrap raw UTF-8 bytes in `Utf8JsonReader`, call `TryGetDecimal()` -- same pattern as existing `IsInteger()` and `TryNumericEqual()` in KeywordValidator
- multipleOf uses decimal modulo: `value % multipleOf == 0m`
- Overflow handling: if `TryGetDecimal()` fails (number exceeds ~+/-7.9x10^28), skip constraint and pass validation
- minLength/maxLength count Unicode codepoints using `System.Text.Rune` enumeration over UTF-8 span -- zero-alloc, handles multi-byte sequences correctly
- Pattern regex compiled at schema load time with `RegexOptions.Compiled` -- schema loading is not on the zero-alloc parse path, validation calls `regex.IsMatch()` which is zero-alloc for simple patterns
- Compiled `Regex` stored as a new `internal Regex? CompiledPattern` field on `SchemaNode` -- co-located with the pattern string, no external cache
- Invalid regex patterns reported as schema loading error -- fail-fast, caught at load time not validation time
- Pattern validator accepts a `string` parameter -- walker (Phase 9) materializes the string once and passes it
- Walker (Phase 9) tracks item/property counts during traversal; after processing an array/object, passes the count to validators
- Validators are pure check functions: `ValidateMinItems(int count, int? minItems, ...)` -- consistent with Phase 5's stateless validator pattern
- 4 new internal static classes: NumericValidator, StringValidator, ArrayValidator, ObjectValidator
- Each follows the Phase 5 pattern: `internal static bool ValidateX(..., ErrorCollector collector)`
- KeywordValidator (Phase 5) stays focused on type/enum/const/required/additionalProperties -- not expanded

### Claude's Discretion
- Exact method signatures beyond the established pattern (parameter ordering, whether to combine min/max into single methods)
- Internal helpers for UTF-8 byte parsing and Rune counting
- Test organization and test helper utilities
- Whether to move GetItemSchema from KeywordValidator to ArrayValidator as a cleanup

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| VALD-06 | minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf | NumericValidator class with 5 methods; decimal arithmetic via Utf8JsonReader.TryGetDecimal(); multipleOf via decimal modulo; overflow passthrough |
| VALD-07 | minLength, maxLength (Unicode codepoint counting), pattern | StringValidator class with 3 methods; Rune.DecodeFromUtf8 loop for codepoint counting; CompiledPattern field on SchemaNode; regex compilation in JsonSchemaLoader |
| VALD-08 | minItems, maxItems, minProperties, maxProperties | ArrayValidator (2 methods) and ObjectValidator (2 methods); pure int comparison; counts provided by walker in Phase 9 |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | .NET 9.0 built-in | Utf8JsonReader for parsing numeric bytes to decimal | Already used throughout project for zero-alloc JSON parsing |
| System.Text.Rune | .NET 9.0 built-in | Unicode codepoint enumeration from UTF-8 bytes | Zero-alloc codepoint counting via DecodeFromUtf8 static method |
| System.Text.RegularExpressions | .NET 9.0 built-in | Compiled regex for pattern validation | Standard .NET regex; RegexOptions.Compiled for schema-load-time compilation |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework | All test classes |
| FluentAssertions | 8.0.1 | Test assertions | All test assertions via `.Should()` |

No new packages needed -- everything is in the .NET BCL.

## Architecture Patterns

### New Files
```
src/Gluey.Contract.Json/
    NumericValidator.cs        # 5 validate methods + TryParseDecimal helper
    StringValidator.cs         # 3 validate methods + CountCodepoints helper
    ArrayValidator.cs          # 2 validate methods (ValidateMinItems, ValidateMaxItems)
    ObjectValidator.cs         # 2 validate methods (ValidateMinProperties, ValidateMaxProperties)

tests/Gluey.Contract.Json.Tests/
    NumericValidatorTests.cs   # Tests for all 5 numeric constraint methods
    StringValidatorTests.cs    # Tests for minLength, maxLength, pattern
    ArrayValidatorTests.cs     # Tests for minItems, maxItems
    ObjectValidatorTests.cs    # Tests for minProperties, maxProperties
```

### Modified Files
```
src/Gluey.Contract/SchemaNode.cs           # Add CompiledPattern field
src/Gluey.Contract.Json/JsonSchemaLoader.cs # Compile regex at load time
```

### Pattern: Stateless Validator Method
**What:** Each validator is an `internal static` method on an `internal static class` that takes constraint value(s), the actual value, a path string, and an ErrorCollector. Returns bool (true = valid).
**When to use:** Every constraint validation method in this phase.
**Example:**
```csharp
// Follows KeywordValidator.ValidateType pattern exactly
internal static class NumericValidator
{
    internal static bool ValidateMinimum(
        decimal value,
        decimal minimum,
        string path,
        ErrorCollector collector)
    {
        if (value >= minimum)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinimumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinimumExceeded)));
        return false;
    }
}
```

### Pattern: UTF-8 Byte to Decimal Parsing
**What:** Wrap raw UTF-8 number bytes in Utf8JsonReader to parse as decimal. Same pattern as `IsInteger()` and `TryNumericEqual()`.
**When to use:** All numeric constraint validators need to parse the incoming token bytes.
**Example:**
```csharp
// Reusable helper within NumericValidator
internal static bool TryParseDecimal(ReadOnlySpan<byte> numberBytes, out decimal value)
{
    value = 0m;
    var reader = new Utf8JsonReader(numberBytes);
    if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
        return false;
    return reader.TryGetDecimal(out value);
}
```

### Pattern: Zero-Alloc Codepoint Counting from UTF-8 Bytes
**What:** Use `Rune.DecodeFromUtf8` in a loop over raw UTF-8 bytes to count Unicode codepoints without allocating.
**When to use:** StringValidator.ValidateMinLength and ValidateMaxLength.
**Example:**
```csharp
// Source: Microsoft Learn - Rune.DecodeFromUtf8
internal static int CountCodepoints(ReadOnlySpan<byte> utf8Bytes)
{
    int count = 0;
    while (!utf8Bytes.IsEmpty)
    {
        Rune.DecodeFromUtf8(utf8Bytes, out _, out int bytesConsumed);
        utf8Bytes = utf8Bytes.Slice(bytesConsumed);
        count++;
    }
    return count;
}
```

### Pattern: Schema-Load-Time Regex Compilation
**What:** Compile the pattern regex when loading the schema, store on SchemaNode. Invalid patterns fail at load time.
**When to use:** When JsonSchemaLoader encounters a `pattern` keyword.
**Example:**
```csharp
// In SchemaNode -- new field
internal Regex? CompiledPattern { get; }

// In JsonSchemaLoader -- after constructing SchemaNode, or in constructor
// Option: compile in loader, pass to SchemaNode constructor
// Or: add a post-construction step
```

### Anti-Patterns to Avoid
- **Parsing numeric bytes on every constraint check:** Parse once in a shared helper, call all applicable numeric validators with the parsed decimal value.
- **Using string.Length for codepoint counting:** string.Length counts UTF-16 code units, not codepoints. Surrogate pairs (emoji, CJK extensions) would be miscounted.
- **Runtime regex compilation:** Pattern should be compiled at schema load time, not on each validation call. `RegexOptions.Compiled` is acceptable at load time since it is not on the hot path.
- **Allocating strings for length checking:** Use `Rune.DecodeFromUtf8` on raw UTF-8 bytes to count codepoints without materializing a string. The string is only materialized for pattern matching (where `Regex.IsMatch` requires it).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Decimal parsing from UTF-8 | Custom number parser | `Utf8JsonReader.TryGetDecimal()` | Handles all JSON number formats (scientific notation, leading zeros, etc.) |
| Unicode codepoint counting | Manual UTF-8 byte scanning | `Rune.DecodeFromUtf8` | Correctly handles all multi-byte sequences, surrogate handling, BOM |
| Regex compilation | Custom pattern matching | `Regex` with `RegexOptions.Compiled` | Full ECMA-262 compatible regex (JSON Schema spec says ECMA-262 dialect) |
| Error message lookup | Inline strings | `ValidationErrorMessages.Get()` | Pre-allocated static strings, consistent with existing pattern |

**Key insight:** All the complex parsing/encoding work is handled by .NET BCL types. The validators themselves are trivial comparison logic.

## Common Pitfalls

### Pitfall 1: multipleOf with Decimal Edge Cases
**What goes wrong:** `value % multipleOf` can produce unexpected results if multipleOf is very small or value is at decimal precision limits.
**Why it happens:** Decimal has ~28-29 significant digits; extremely precise fractional multipleOf values could hit precision boundaries.
**How to avoid:** Use `value % multipleOf == 0m` as decided. Decimal modulo is exact for all reasonable multipleOf values (0.01, 0.1, 0.5, etc.) unlike float/double. Edge case: multipleOf of 0 is invalid per JSON Schema spec (schema loading should reject or ignore).
**Warning signs:** Tests with very large numbers combined with very small multipleOf values.

### Pitfall 2: String Length vs Codepoint Count vs Grapheme Cluster Count
**What goes wrong:** JSON Schema spec says minLength/maxLength count Unicode codepoints, not bytes, not UTF-16 code units, not grapheme clusters.
**Why it happens:** `string.Length` in C# counts UTF-16 code units. An emoji like U+1F600 is 2 code units (surrogate pair) but 1 codepoint. Conversely, a combining character sequence like "e" + combining acute = 2 codepoints but visually 1 character.
**How to avoid:** Use `Rune.DecodeFromUtf8` which correctly identifies individual codepoints. JSON Schema counts codepoints, not grapheme clusters, so combining sequences count as multiple characters per spec.
**Warning signs:** Tests with emoji, CJK characters, combining marks, and supplementary plane characters.

### Pitfall 3: Pattern Must Match Against Full String Content (Not Raw Bytes)
**What goes wrong:** Attempting to regex-match against raw UTF-8 bytes or escaped JSON string content instead of the unescaped string value.
**Why it happens:** The token bytes in the buffer include JSON escape sequences (e.g., `\u0041` for "A"). Regex must match against the logical string value.
**How to avoid:** Pattern validator accepts a `string` parameter (as decided). The walker (Phase 9) materializes the string once for pattern matching. This is the one place where a string allocation is acceptable.
**Warning signs:** Pattern tests with Unicode escapes in JSON strings.

### Pitfall 4: exclusiveMinimum/exclusiveMaximum Semantics
**What goes wrong:** Confusing inclusive vs exclusive boundary semantics.
**Why it happens:** Draft 2020-12 uses numeric `exclusiveMinimum`/`exclusiveMaximum` (the value itself is the boundary, and it is exclusive). Older drafts used boolean flags on minimum/maximum.
**How to avoid:** `exclusiveMinimum`: value must be strictly greater than the boundary (`value > exclusiveMinimum`). `exclusiveMaximum`: value must be strictly less than the boundary (`value < exclusiveMaximum`).
**Warning signs:** Boundary value tests (value exactly equal to exclusive boundary should fail).

### Pitfall 5: CompiledPattern Field Placement on SchemaNode
**What goes wrong:** SchemaNode is in Gluey.Contract (core library) but Regex is in System.Text.RegularExpressions. Adding CompiledPattern to SchemaNode couples the core to regex.
**Why it happens:** SchemaNode was designed as a pure data model.
**How to avoid:** This is acceptable -- `System.Text.RegularExpressions` is a BCL namespace with no external dependency. SchemaNode already has `string? Pattern`, adding `Regex? CompiledPattern` alongside is natural. The alternative (external cache/dictionary) adds complexity for no benefit.
**Warning signs:** None -- this is the decided approach.

### Pitfall 6: TryGetDecimal Overflow Behavior
**What goes wrong:** Numbers exceeding decimal range (~+/-7.9x10^28) cause TryGetDecimal to return false.
**Why it happens:** JSON allows arbitrarily large numbers but C# decimal has finite range.
**How to avoid:** Per decision: if TryGetDecimal fails, skip the constraint and pass validation. This is pragmatic and consistent with Phase 5's Int64 approach.
**Warning signs:** Tests with extremely large numbers (10^30+).

## Code Examples

### NumericValidator - Complete Method Set
```csharp
// Source: Established pattern from KeywordValidator.cs
internal static class NumericValidator
{
    internal static bool TryParseDecimal(ReadOnlySpan<byte> numberBytes, out decimal value)
    {
        value = 0m;
        var reader = new Utf8JsonReader(numberBytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
            return false;
        return reader.TryGetDecimal(out value);
    }

    internal static bool ValidateMinimum(decimal value, decimal minimum, string path, ErrorCollector collector)
    {
        if (value >= minimum) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MinimumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinimumExceeded)));
        return false;
    }

    internal static bool ValidateMaximum(decimal value, decimal maximum, string path, ErrorCollector collector)
    {
        if (value <= maximum) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MaximumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaximumExceeded)));
        return false;
    }

    internal static bool ValidateExclusiveMinimum(decimal value, decimal exclusiveMinimum, string path, ErrorCollector collector)
    {
        if (value > exclusiveMinimum) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.ExclusiveMinimumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.ExclusiveMinimumExceeded)));
        return false;
    }

    internal static bool ValidateExclusiveMaximum(decimal value, decimal exclusiveMaximum, string path, ErrorCollector collector)
    {
        if (value < exclusiveMaximum) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.ExclusiveMaximumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.ExclusiveMaximumExceeded)));
        return false;
    }

    internal static bool ValidateMultipleOf(decimal value, decimal multipleOf, string path, ErrorCollector collector)
    {
        if (value % multipleOf == 0m) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MultipleOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.MultipleOfInvalid)));
        return false;
    }
}
```

### StringValidator - Codepoint Counting from UTF-8
```csharp
// Source: Microsoft Learn - Rune.DecodeFromUtf8
internal static class StringValidator
{
    internal static int CountCodepoints(ReadOnlySpan<byte> utf8Bytes)
    {
        int count = 0;
        while (!utf8Bytes.IsEmpty)
        {
            Rune.DecodeFromUtf8(utf8Bytes, out _, out int bytesConsumed);
            utf8Bytes = utf8Bytes.Slice(bytesConsumed);
            count++;
        }
        return count;
    }

    internal static bool ValidateMinLength(int codepointCount, int minLength, string path, ErrorCollector collector)
    {
        if (codepointCount >= minLength) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MinLengthExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinLengthExceeded)));
        return false;
    }

    internal static bool ValidateMaxLength(int codepointCount, int maxLength, string path, ErrorCollector collector)
    {
        if (codepointCount <= maxLength) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MaxLengthExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxLengthExceeded)));
        return false;
    }

    internal static bool ValidatePattern(string value, Regex compiledPattern, string path, ErrorCollector collector)
    {
        if (compiledPattern.IsMatch(value)) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.PatternMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.PatternMismatch)));
        return false;
    }
}
```

### Collection Size Validators
```csharp
internal static class ArrayValidator
{
    internal static bool ValidateMinItems(int itemCount, int minItems, string path, ErrorCollector collector)
    {
        if (itemCount >= minItems) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MinItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinItemsExceeded)));
        return false;
    }

    internal static bool ValidateMaxItems(int itemCount, int maxItems, string path, ErrorCollector collector)
    {
        if (itemCount <= maxItems) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MaxItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxItemsExceeded)));
        return false;
    }
}

internal static class ObjectValidator
{
    internal static bool ValidateMinProperties(int propertyCount, int minProperties, string path, ErrorCollector collector)
    {
        if (propertyCount >= minProperties) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MinPropertiesExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinPropertiesExceeded)));
        return false;
    }

    internal static bool ValidateMaxProperties(int propertyCount, int maxProperties, string path, ErrorCollector collector)
    {
        if (propertyCount <= maxProperties) return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.MaxPropertiesExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxPropertiesExceeded)));
        return false;
    }
}
```

### SchemaNode CompiledPattern Addition
```csharp
// New field on SchemaNode, alongside existing Pattern field
internal Regex? CompiledPattern { get; }

// In constructor, add parameter:
// System.Text.RegularExpressions.Regex? compiledPattern = null
// Assignment: CompiledPattern = compiledPattern;
```

### JsonSchemaLoader Regex Compilation
```csharp
// After constructing the SchemaNode, or as part of construction:
// In the pattern keyword branch, compile regex alongside storing the string
Regex? compiledPattern = null;
if (pattern is not null)
{
    try
    {
        compiledPattern = new Regex(pattern, RegexOptions.Compiled);
    }
    catch (ArgumentException)
    {
        // Invalid regex -- fail fast at schema load time
        // Return null to indicate invalid schema, or store without compiled pattern
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Draft-04 exclusiveMinimum/Maximum as boolean flags on minimum/maximum | Draft 2020-12 exclusiveMinimum/Maximum as standalone numeric values | JSON Schema Draft-06 (2017) | Simpler implementation -- just compare value against boundary |
| string.Length for JSON Schema length | Unicode codepoint counting | Always in spec, but commonly misimplemented | Must use Rune-based counting for correctness |
| Runtime regex compilation | Pre-compiled regex at schema load | Performance optimization | RegexOptions.Compiled at load time; validation is zero-alloc |

## Open Questions

1. **multipleOf with value 0**
   - What we know: JSON Schema spec says multipleOf must be strictly greater than 0
   - What's unclear: Whether to validate this at schema load time or ignore
   - Recommendation: Silently ignore multipleOf of 0 (avoid DivideByZeroException). Schema load validation is a separate concern.

2. **ECMA-262 vs .NET regex dialect**
   - What we know: JSON Schema spec says pattern should follow ECMA-262 regex dialect. .NET Regex is Perl-compatible, which is a superset.
   - What's unclear: Edge cases where .NET regex behavior differs from ECMA-262
   - Recommendation: Use .NET Regex as-is. The differences are minimal and in practice schemas use common regex features. Full ECMA-262 compliance is a v2 concern if needed.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidator\|ClassName~StringValidator\|ClassName~ArrayValidator\|ClassName~ObjectValidator" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VALD-06 | minimum rejects values below bound | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" -x` | No - Wave 0 |
| VALD-06 | maximum rejects values above bound | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" -x` | No - Wave 0 |
| VALD-06 | exclusiveMinimum rejects values at/below bound | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" -x` | No - Wave 0 |
| VALD-06 | exclusiveMaximum rejects values at/above bound | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" -x` | No - Wave 0 |
| VALD-06 | multipleOf validates divisibility | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" -x` | No - Wave 0 |
| VALD-06 | TryParseDecimal handles overflow gracefully | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" -x` | No - Wave 0 |
| VALD-07 | minLength counts codepoints correctly | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~StringValidatorTests" -x` | No - Wave 0 |
| VALD-07 | maxLength counts codepoints correctly | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~StringValidatorTests" -x` | No - Wave 0 |
| VALD-07 | pattern matches against string values | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~StringValidatorTests" -x` | No - Wave 0 |
| VALD-07 | CompiledPattern compiled at schema load | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~JsonSchemaLoadingTests" -x` | Exists (needs new tests) |
| VALD-08 | minItems/maxItems enforce array size | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ArrayValidatorTests" -x` | No - Wave 0 |
| VALD-08 | minProperties/maxProperties enforce object size | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ObjectValidatorTests" -x` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --no-build`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/NumericValidatorTests.cs` -- covers VALD-06
- [ ] `tests/Gluey.Contract.Json.Tests/StringValidatorTests.cs` -- covers VALD-07
- [ ] `tests/Gluey.Contract.Json.Tests/ArrayValidatorTests.cs` -- covers VALD-08 (array)
- [ ] `tests/Gluey.Contract.Json.Tests/ObjectValidatorTests.cs` -- covers VALD-08 (object)

## Sources

### Primary (HIGH confidence)
- Project codebase: KeywordValidator.cs, SchemaNode.cs, ValidationErrorCode.cs, ValidationErrorMessages.cs, ErrorCollector.cs, JsonSchemaLoader.cs -- established patterns
- [Microsoft Learn - Rune.DecodeFromUtf8](https://learn.microsoft.com/en-us/dotnet/api/system.text.rune.decodefromutf8?view=net-9.0) -- UTF-8 codepoint decoding API
- [Microsoft Learn - Decimal Modulus Operator](https://learn.microsoft.com/en-us/dotnet/api/system.decimal.op_modulus?view=net-8.0) -- decimal remainder behavior
- [Microsoft Learn - SpanRuneEnumerator](https://learn.microsoft.com/en-us/dotnet/api/system.text.spanruneenumerator?view=net-9.0) -- Rune enumeration over spans

### Secondary (MEDIUM confidence)
- [Microsoft Learn - Arithmetic Operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators) -- decimal modulo precision characteristics

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all BCL types, no external dependencies, same patterns as Phase 5
- Architecture: HIGH - 4 new static classes following exact established pattern, all infrastructure exists
- Pitfalls: HIGH - well-understood domain; decimal precision, codepoint counting, and exclusive boundary semantics are documented

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable .NET 9 APIs, no changes expected)
