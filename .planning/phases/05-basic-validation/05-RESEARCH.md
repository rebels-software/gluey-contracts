# Phase 5: Basic Validation - Research

**Researched:** 2026-03-09
**Domain:** JSON Schema Draft 2020-12 keyword validators (type, enum, const, required, properties, additionalProperties, items, prefixItems) + error collection pipeline
**Confidence:** HIGH

## Summary

Phase 5 implements the core JSON Schema keyword validators as static methods on an internal `KeywordValidator` class in the `Gluey.Contract.Json` namespace. Each method receives a SchemaNode, token information, and an ErrorCollector, validates a single keyword, and pushes errors directly. The validators are standalone -- no recursion, no traversal -- the walker (Phase 9) calls them.

The existing infrastructure is remarkably complete: ValidationErrorCode enum has all needed codes, ValidationErrorMessages has all messages, ErrorCollector handles overflow with sentinel, SchemaNode has all keyword fields populated, and SchemaType is a flags enum enabling efficient bitwise type checking. The primary implementation work is writing the validator methods and their tests.

**Primary recommendation:** Implement KeywordValidator as a single internal static class with one public static method per keyword. Each method takes the minimum parameters needed (SchemaNode, token type, byte span for enum/const, property names for required, etc.) and returns bool for short-circuit support.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Single internal static class (KeywordValidator) with methods like ValidateType(), ValidateRequired(), ValidateEnum() etc.
- Lives in Gluey.Contract.Json package -- validators consume JsonByteReader tokens and SchemaNode, they're JSON-specific
- Standalone functions with no recursion -- each validator checks its keyword only, the walker (Phase 9) handles traversal
- Phases 6-8 add more static classes (e.g., NumericValidator, StringValidator) following the same pattern
- Spec-strict integer detection: mathematical integer, not lexical -- `1.0` is a valid integer per JSON Schema Draft 2020-12
- Use Utf8JsonReader.TryGetInt64() to determine integer-ness -- leverages BCL's battle-tested parsing
- Integers beyond Int64 range fail as non-integer -- pragmatic limit
- Spec-compliant enum/const: JSON value equality applies -- `1` equals `1.0` for numeric values
- Byte-first with numeric fallback for enum/const comparison
- Full support for structured values (objects/arrays) in enum/const -- not just scalars
- Direct push: each ValidateX() method receives ErrorCollector as a parameter and calls collector.Add() directly
- Validators return bool indicating pass/fail -- enables short-circuiting
- ErrorCollector.Add() handles overflow internally -- validators never check IsFull
- Unit test each validator method directly -- pass SchemaNode + token info + ErrorCollector
- No dependency on JsonByteReader or JSON parsing in tests -- fast, isolated

### Claude's Discretion
- ValidateX() method signatures (exact parameter types beyond SchemaNode + ErrorCollector)
- How structured enum/const values (objects/arrays) are extracted and compared from the byte stream
- Internal helpers for byte-level comparison and numeric parsing
- Method organization within KeywordValidator class

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| VALD-01 | type keyword (null, boolean, integer, number, string, array, object) | SchemaType flags enum + JsonByteTokenType mapping; TryGetInt64 for integer detection |
| VALD-02 | enum and const keywords with byte-level comparison | byte[][] Enum and byte[] Const on SchemaNode; byte-first with decimal fallback for numeric equality |
| VALD-03 | required keyword | string[] Required on SchemaNode; track seen properties during object traversal |
| VALD-04 | properties and additionalProperties keywords | Dictionary<string, SchemaNode> Properties + SchemaNode? AdditionalProperties; property name matching |
| VALD-05 | items and prefixItems keywords | SchemaNode? Items + SchemaNode[]? PrefixItems; positional vs uniform array item validation |
| VALD-17 | All errors collected (not fail-fast), configurable max count | ErrorCollector already implements: ArrayPool-backed, Add() with sentinel overflow at capacity (default 64) |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | BCL (.NET 9) | Utf8JsonReader for TryGetInt64 integer detection | Zero-dependency; already used by JsonByteReader |
| NUnit | 4.3.1 | Test framework | Already established in project |
| FluentAssertions | 8.0.1 | Test assertions | Already established in project |

### Supporting
No additional libraries needed. All validation logic uses existing BCL types and project infrastructure.

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract.Json/
    KeywordValidator.cs          # Static class with ValidateType, ValidateEnum, etc.
tests/Gluey.Contract.Json.Tests/
    KeywordValidatorTests.cs     # Unit tests for all validator methods
```

### Pattern 1: Static Validator Method
**What:** Each keyword validator is a static method returning bool, receiving only the data it needs.
**When to use:** Every keyword in Phase 5 (and subsequent phases for NumericValidator, StringValidator).

```csharp
internal static class KeywordValidator
{
    /// <summary>
    /// Validates the "type" keyword against a token type.
    /// </summary>
    /// <returns>true if valid, false if error was added.</returns>
    internal static bool ValidateType(
        SchemaType expected,
        JsonByteTokenType tokenType,
        bool isInteger,
        string path,
        ErrorCollector collector)
    {
        SchemaType actual = MapTokenToSchemaType(tokenType, isInteger);
        if ((expected & actual) != 0)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.TypeMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.TypeMismatch)));
        return false;
    }
}
```

### Pattern 2: Token-to-SchemaType Mapping
**What:** Convert JsonByteTokenType + isInteger flag to SchemaType for bitwise comparison against the schema's type constraint.
**When to use:** ValidateType method.

```csharp
private static SchemaType MapTokenToSchemaType(JsonByteTokenType tokenType, bool isInteger)
{
    return tokenType switch
    {
        JsonByteTokenType.Null => SchemaType.Null,
        JsonByteTokenType.True => SchemaType.Boolean,
        JsonByteTokenType.False => SchemaType.Boolean,
        JsonByteTokenType.String => SchemaType.String,
        JsonByteTokenType.StartObject => SchemaType.Object,
        JsonByteTokenType.StartArray => SchemaType.Array,
        // Number: integer type is a subset of number type per spec
        JsonByteTokenType.Number => isInteger
            ? SchemaType.Integer | SchemaType.Number
            : SchemaType.Number,
        _ => SchemaType.None,
    };
}
```

**Critical spec detail:** An integer value satisfies BOTH `"type": "integer"` AND `"type": "number"`. The flags enum makes this natural: map integer tokens to `SchemaType.Integer | SchemaType.Number`, then bitwise AND with the schema's expected type.

### Pattern 3: Byte-First Enum/Const Comparison with Numeric Fallback
**What:** Compare token bytes against stored enum/const byte arrays. For numbers, if byte-exact fails, parse both as decimal and compare values.
**When to use:** ValidateEnum and ValidateConst.

```csharp
internal static bool ValidateConst(
    byte[] expected,
    ReadOnlySpan<byte> tokenBytes,
    bool tokenIsNumber,
    string path,
    ErrorCollector collector)
{
    if (tokenBytes.SequenceEqual(expected))
        return true;

    // Numeric fallback: 1 == 1.0 == 1.00 per JSON Schema value equality
    if (tokenIsNumber && TryNumericEqual(tokenBytes, expected, out bool equal) && equal)
        return true;

    collector.Add(new ValidationError(
        path,
        ValidationErrorCode.ConstMismatch,
        ValidationErrorMessages.Get(ValidationErrorCode.ConstMismatch)));
    return false;
}

private static bool TryNumericEqual(
    ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, out bool equal)
{
    equal = false;
    var readerA = new Utf8JsonReader(a);
    var readerB = new Utf8JsonReader(b);
    if (!readerA.Read() || !readerB.Read()) return false;
    if (!readerA.TryGetDecimal(out var valA)) return false;
    if (!readerB.TryGetDecimal(out var valB)) return false;
    equal = valA == valB;
    return true;
}
```

### Pattern 4: Required Property Tracking
**What:** ValidateRequired receives the set of property names seen during object traversal and checks against schema.Required[].
**When to use:** After the walker finishes processing an object's properties.

```csharp
internal static bool ValidateRequired(
    string[] required,
    HashSet<string> seenProperties,  // or ReadOnlySpan<string> sorted
    string path,
    ErrorCollector collector)
{
    bool valid = true;
    for (int i = 0; i < required.Length; i++)
    {
        if (!seenProperties.Contains(required[i]))
        {
            collector.Add(new ValidationError(
                SchemaNode.BuildChildPath(path, required[i]),
                ValidationErrorCode.RequiredMissing,
                ValidationErrorMessages.Get(ValidationErrorCode.RequiredMissing)));
            valid = false;
        }
    }
    return valid;
}
```

### Pattern 5: Properties / AdditionalProperties Validation
**What:** ValidatePropertyAllowed checks if a property name is in the schema's Properties dictionary; if not and AdditionalProperties is SchemaNode.False, emit error.
**When to use:** Called per-property during object traversal.

```csharp
internal static bool ValidateAdditionalProperty(
    string propertyName,
    Dictionary<string, SchemaNode>? properties,
    SchemaNode? additionalProperties,
    string path,
    ErrorCollector collector)
{
    // If property is declared in properties, it's always allowed
    if (properties is not null && properties.ContainsKey(propertyName))
        return true;

    // If additionalProperties is not set, additional props are allowed by default
    if (additionalProperties is null)
        return true;

    // If additionalProperties is boolean false schema, reject
    if (additionalProperties.BooleanSchema == false)
    {
        collector.Add(new ValidationError(
            SchemaNode.BuildChildPath(path, propertyName),
            ValidationErrorCode.AdditionalPropertyNotAllowed,
            ValidationErrorMessages.Get(ValidationErrorCode.AdditionalPropertyNotAllowed)));
        return false;
    }

    // additionalProperties is a schema -- Phase 9 walker validates the value
    return true;
}
```

### Pattern 6: Items / PrefixItems Validation
**What:** These are applicator keywords. Phase 5 implements the schema lookup logic -- determining WHICH schema applies to a given array index. The actual recursive validation of element values happens in Phase 9.
**When to use:** During array element traversal.

```csharp
/// <summary>
/// Determines the schema that applies to an array element at the given index.
/// Returns null if no schema constrains this element.
/// </summary>
internal static SchemaNode? GetItemSchema(
    int index,
    SchemaNode[]? prefixItems,
    SchemaNode? items)
{
    // prefixItems takes priority for positional elements
    if (prefixItems is not null && index < prefixItems.Length)
        return prefixItems[index];

    // items applies to all elements beyond prefixItems (or all if no prefixItems)
    return items;
}
```

### Anti-Patterns to Avoid
- **Validators doing traversal:** Validators must NOT iterate into child objects/arrays. They validate a single token or keyword at the current level. The walker (Phase 9) handles depth.
- **Checking IsFull on ErrorCollector:** ErrorCollector.Add() handles overflow internally. Validators should never check capacity -- just call Add().
- **Allocating strings for error messages:** Use ValidationErrorMessages.Get() which returns pre-allocated static strings.
- **Using JsonTokenType directly:** Always use JsonByteTokenType from the project's own enum to avoid BCL coupling.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Integer detection | Custom regex/parsing of number text | Utf8JsonReader.TryGetInt64() | Handles scientific notation, leading zeros, edge cases; BCL-tested |
| Numeric value equality | Custom decimal parser | Utf8JsonReader.TryGetDecimal() | Correct parsing of all JSON number forms |
| JSON Pointer paths | String concatenation | SchemaNode.BuildChildPath() | Already handles RFC 6901 escaping (~0, ~1) |
| Error message lookup | Inline string literals | ValidationErrorMessages.Get() | Pre-allocated, consistent messages |
| Error overflow management | Manual count checking | ErrorCollector.Add() | Sentinel logic already implemented |

## Common Pitfalls

### Pitfall 1: Integer vs Number Type Semantics
**What goes wrong:** Treating integer as a lexical concept (no decimal point) rather than mathematical.
**Why it happens:** In JSON the token `1.0` looks like a float, but per JSON Schema Draft 2020-12, it IS an integer because its mathematical value is an integer.
**How to avoid:** Use TryGetInt64() -- if it succeeds, the value is a mathematical integer regardless of its lexical form. `1.0`, `1e0`, `10e-1` all parse as integer 1.
**Warning signs:** Tests pass for `1` but fail for `1.0` with `"type": "integer"`.

### Pitfall 2: Number Satisfies Both Integer and Number Types
**What goes wrong:** A value that IS an integer only matching `"type": "integer"`, not `"type": "number"`.
**Why it happens:** Forgetting that integer is a subset of number per JSON Schema spec.
**How to avoid:** Map integer tokens to `SchemaType.Integer | SchemaType.Number` so bitwise AND matches either constraint.
**Warning signs:** Schema `{"type": "number"}` rejects value `42`.

### Pitfall 3: Enum Numeric Value Equality
**What goes wrong:** `1` not matching enum value `[1.0]` because their byte representations differ.
**Why it happens:** Byte-exact comparison fails for numerically equal but lexically different values.
**How to avoid:** Byte-first comparison, then numeric fallback using decimal parsing for number tokens.
**Warning signs:** Enum validation fails for `1` vs `1.0`, or `100` vs `1e2`.

### Pitfall 4: Structured Enum/Const Values
**What goes wrong:** Enum/const only works for scalars, fails for `{"const": {"key": "value"}}`.
**Why it happens:** Not considering that enum/const can contain objects and arrays.
**How to avoid:** For structured values, the byte[] in SchemaNode.Const/Enum represents the full JSON text of the structure. The walker must extract the corresponding byte range from the input and pass it for comparison.
**Warning signs:** Tests only cover scalar enum/const values.

### Pitfall 5: AdditionalProperties Default
**What goes wrong:** Rejecting unknown properties when additionalProperties is not specified.
**Why it happens:** Assuming additionalProperties defaults to false.
**How to avoid:** Per JSON Schema spec, additionalProperties defaults to the empty schema (equivalent to `true` -- allows everything). Only reject when explicitly set to `false` or a failing schema.
**Warning signs:** Objects with extra properties fail validation when schema doesn't mention additionalProperties.

### Pitfall 6: Required Error Path
**What goes wrong:** Using the object's path for required errors instead of the missing property's path.
**Why it happens:** The property doesn't exist, so there's no "current token" path.
**How to avoid:** Construct the path as `parentPath + "/" + escapedPropertyName` using SchemaNode.BuildChildPath().
**Warning signs:** Error path is `/address` instead of `/address/street` for a missing `street` property.

### Pitfall 7: PrefixItems and Items Interaction
**What goes wrong:** Applying `items` schema to elements that should be covered by `prefixItems`.
**Why it happens:** Misunderstanding the spec: `prefixItems` covers indices 0..N-1, `items` covers indices N+ (where N is prefixItems.Length).
**How to avoid:** Check prefixItems first by index; only fall through to items for indices beyond prefixItems array length.
**Warning signs:** First array element validated against items schema instead of prefixItems[0].

## Code Examples

### Integer Detection via Utf8JsonReader
```csharp
// Source: System.Text.Json BCL documentation
// Determine if a JSON number token is a mathematical integer
internal static bool IsInteger(ReadOnlySpan<byte> numberBytes)
{
    var reader = new Utf8JsonReader(numberBytes);
    if (!reader.Read() || reader.TokenType != System.Text.Json.JsonTokenType.Number)
        return false;
    return reader.TryGetInt64(out _);
}
```

### Creating ValidationError
```csharp
// Source: existing project pattern
collector.Add(new ValidationError(
    path,
    ValidationErrorCode.TypeMismatch,
    ValidationErrorMessages.Get(ValidationErrorCode.TypeMismatch)));
```

### Test Pattern (No JsonByteReader dependency)
```csharp
[Test]
public void ValidateType_NullToken_PassesForNullType()
{
    var schema = new SchemaNode("", type: SchemaType.Null);
    using var collector = new ErrorCollector();

    bool result = KeywordValidator.ValidateType(
        schema.Type!.Value,
        JsonByteTokenType.Null,
        isInteger: false,
        "",
        collector);

    result.Should().BeTrue();
    collector.HasErrors.Should().BeFalse();
}

[Test]
public void ValidateType_StringToken_FailsForIntegerType()
{
    var schema = new SchemaNode("", type: SchemaType.Integer);
    using var collector = new ErrorCollector();

    bool result = KeywordValidator.ValidateType(
        schema.Type!.Value,
        JsonByteTokenType.String,
        isInteger: false,
        "/name",
        collector);

    result.Should().BeFalse();
    collector.Count.Should().Be(1);
    collector[0].Code.Should().Be(ValidationErrorCode.TypeMismatch);
    collector[0].Path.Should().Be("/name");
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Draft-07 `items` as array | Draft 2020-12 split into `prefixItems` (positional) + `items` (remainder) | 2020-12 | `items` as array is gone; `prefixItems` replaces it |
| `additionalItems` keyword | `items` keyword (in 2020-12 context) | 2020-12 | `additionalItems` replaced by `items` when `prefixItems` is present |

**Spec version:** JSON Schema Draft 2020-12 (the project target).

## Open Questions

1. **Structured enum/const byte extraction**
   - What we know: SchemaNode.Enum stores byte[][] where each element is the raw JSON of an enum value (could be `[1,2,3]` or `{"a":1}`). SchemaNode.Const stores byte[].
   - What's unclear: How the walker will extract the corresponding byte range from the input for structured values (objects/arrays span multiple tokens).
   - Recommendation: For Phase 5, implement scalar comparison fully. For structured values, the validator accepts a `ReadOnlySpan<byte>` representing the full JSON text of the value being compared. The walker (Phase 9) is responsible for extracting this span using ByteOffset of the start token through to the matching end token. Phase 5 validators should handle the comparison side assuming the span is provided.

2. **ValidateRequired parameter type**
   - What we know: Need to track which properties were seen during object traversal.
   - What's unclear: Whether to use `HashSet<string>` (allocation) or a different structure.
   - Recommendation: Accept `HashSet<string>` for now -- this is called once per object after traversal. The walker (Phase 9) manages the HashSet lifecycle. For small objects, the overhead is negligible; for large objects with many required fields, HashSet O(1) lookup is optimal.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~KeywordValidator" --no-build -q` |
| Full suite command | `dotnet test --no-build -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VALD-01 | type validates all 7 JSON Schema types (null, boolean, integer, number, string, array, object) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateType" -q` | No -- Wave 0 |
| VALD-01 | integer type: 1.0 accepted as integer (mathematical, not lexical) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateType" -q` | No -- Wave 0 |
| VALD-01 | number type: integer values also satisfy number type | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateType" -q` | No -- Wave 0 |
| VALD-02 | enum: byte-exact match for non-numeric types | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateEnum" -q` | No -- Wave 0 |
| VALD-02 | enum: numeric fallback (1 matches 1.0) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateEnum" -q` | No -- Wave 0 |
| VALD-02 | const: byte-exact match with numeric fallback | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateConst" -q` | No -- Wave 0 |
| VALD-02 | enum/const: structured values (objects/arrays) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Validate" -q` | No -- Wave 0 |
| VALD-03 | required: missing properties reported with correct JSON Pointer | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateRequired" -q` | No -- Wave 0 |
| VALD-03 | required: all present properties pass | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateRequired" -q` | No -- Wave 0 |
| VALD-04 | properties: known property is accepted | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~AdditionalProperty" -q` | No -- Wave 0 |
| VALD-04 | additionalProperties: false rejects unknown properties | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~AdditionalProperty" -q` | No -- Wave 0 |
| VALD-04 | additionalProperties: not set allows all properties | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~AdditionalProperty" -q` | No -- Wave 0 |
| VALD-05 | items: uniform schema for all array elements | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ItemSchema" -q` | No -- Wave 0 |
| VALD-05 | prefixItems: positional schemas for first N elements | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ItemSchema" -q` | No -- Wave 0 |
| VALD-05 | prefixItems + items: items applies to indices beyond prefixItems | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ItemSchema" -q` | No -- Wave 0 |
| VALD-17 | errors collected not fail-fast | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Keyword" -q` | No -- Wave 0 |
| VALD-17 | ErrorCollector sentinel at max capacity | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~ErrorCollector" -q` | Existing (Phase 1) |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~KeywordValidator" --no-build -q`
- **Per wave merge:** `dotnet test --no-build -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/KeywordValidatorTests.cs` -- covers VALD-01 through VALD-05, VALD-17
- No framework install needed -- NUnit 4.3.1 + FluentAssertions 8.0.1 already configured
- No new conftest/fixtures needed -- tests construct SchemaNode and ErrorCollector directly

## Sources

### Primary (HIGH confidence)
- Project source code: SchemaNode.cs, ErrorCollector.cs, ValidationError.cs, ValidationErrorCode.cs, ValidationErrorMessages.cs, SchemaType.cs, JsonByteReader.cs, JsonByteTokenType.cs
- Project CONTEXT.md: locked implementation decisions from user discussion
- System.Text.Json BCL: Utf8JsonReader.TryGetInt64(), TryGetDecimal() -- stable .NET 9 APIs

### Secondary (MEDIUM confidence)
- JSON Schema Draft 2020-12 specification: type/enum/const/required/properties/additionalProperties/items/prefixItems semantics
- JSON Schema value equality rules for numeric comparison

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- using only existing project libraries and BCL
- Architecture: HIGH -- locked by user decisions in CONTEXT.md; clear static class pattern
- Pitfalls: HIGH -- well-known JSON Schema spec edge cases documented in official spec and test suites
- Code examples: HIGH -- derived directly from existing project code patterns

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable domain, no moving parts)
