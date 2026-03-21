# Phase 6: Validation - Research

**Researched:** 2026-03-22
**Domain:** Binary contract parse-time field validation (numeric range, string constraints, pattern matching)
**Confidence:** HIGH

## Summary

Phase 6 adds parse-time validation of field values against contract-defined constraints. The work is almost entirely additive -- validation calls are inserted into the existing `Parse()` method after each field's `ParsedProperty` is created. All infrastructure already exists: `ErrorCollector`, `ValidationError`, `ValidationErrorCode` enum values, `ValidationErrorMessages`, and `ValidationRules` on `BinaryContractNode`. The JSON validators (`NumericValidator`, `StringValidator`) serve as the reference pattern.

The scope is narrow: five constraint types across two field categories (numeric min/max, string pattern/minLength/maxLength). Error collection is already wired into `ParseResult` via `ErrorCollector`. The payload-too-short null return is already implemented. The only non-trivial design decision is Regex compilation strategy for pattern validation.

**Primary recommendation:** Add a `CompiledPattern` property (compiled `Regex`) to `BinaryContractNode` at load time in `BinaryContractLoader`. Then add inline validation calls in `Parse()` after each field's `ParsedProperty` is created, checking `node.Validation != null` before calling static validator helpers.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Validate inline during Parse() -- immediately after reading each field's value. Error added to ErrorCollector in the same loop iteration. No separate validation pass
- **D-02:** Validation errors do NOT prevent the field from appearing in ParseResult. The parsed value is still in OffsetTable. Consumer can access the "invalid" value and decide what to do. Matches JSON behavior
- **D-03:** Reuse existing ValidationErrorCode enum values (MinimumExceeded, MaximumExceeded, MinLengthExceeded, MaxLengthExceeded, PatternMismatch). Same codes as JSON validation -- consistent consumer experience
- **D-04:** Error Path uses full RFC 6901 JSON Pointer path: "/fieldName" for top-level, "/arrayName/0/subField" for nested. Matches existing ValidationError.Path convention
- **D-05:** Per-element validation -- each array element is individually validated against its type's constraints. Error paths include index: "/readings/2" for scalar arrays, "/errors/0/code" for struct sub-fields
- **D-06:** Struct sub-fields within arrays have their own validation rules from the contract. Validation applied per-element per-sub-field

### Claude's Discretion
- Where in the Parse() switch cases to add validation calls (after ParsedProperty creation vs after OffsetTable.Set)
- Helper method organization for numeric vs string validation
- How to access the parsed value for validation (read from ParsedProperty or from raw bytes)
- Regex compilation strategy for pattern validation (compile once at load time vs per-parse)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| VALD-01 | Numeric fields validated against min/max from contract | Existing `ValidationRules.Min/Max` on `BinaryContractNode`, error codes `MinimumExceeded`/`MaximumExceeded` ready; JSON `NumericValidator` pattern to mirror |
| VALD-02 | String fields validated against pattern (regex) from contract | `ValidationRules.Pattern` populated at load time; compile `Regex` at load time on node; error code `PatternMismatch` ready |
| VALD-03 | String fields validated against minLength/maxLength from contract | `ValidationRules.MinLength/MaxLength` populated; error codes `MinLengthExceeded`/`MaxLengthExceeded` ready |
| VALD-04 | Payload too short for fixed-size contract returns null | Already implemented in `Parse()` line 221-222: `if (TotalFixedSize >= 0 && data.Length < TotalFixedSize) return null;` -- needs test coverage only |
| VALD-05 | Multiple validation errors collected (not fail-fast), using ErrorCollector | `ErrorCollector` already rented in `Parse()`, passed to `ParseResult`. Just needs `Add()` calls -- no new infrastructure |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.RegularExpressions | built-in | Pattern validation regex | .NET BCL, matches JSON StringValidator pattern |
| NUnit | 4.3.1 | Test framework | Already in test project |
| FluentAssertions | 8.0.1 | Test assertions | Already in test project |

No new packages needed. All validation uses existing .NET BCL types and project infrastructure.

## Architecture Patterns

### Recommended Validation Helper Structure

Create a single static helper class in the Binary project, mirroring the JSON validators but adapted for binary field types:

```
src/Gluey.Contract.Binary/
  Schema/
    BinaryContractSchema.cs      # Parse() method -- add validation calls inline
    BinaryContractNode.cs        # Add CompiledPattern property
    BinaryContractLoader.cs      # Compile Regex at load time
    BinaryFieldValidator.cs      # NEW: static validation helper methods
```

### Pattern 1: Inline Validation After ParsedProperty Creation

**What:** After each field's `ParsedProperty` is created and stored in `OffsetTable`, check `node.Validation` and call validation helpers.
**When to use:** Every field type that supports validation (numeric scalars, strings).
**Why after OffsetTable.Set:** D-02 says the parsed value stays in the result regardless of validation errors. Setting in OffsetTable first, then validating, makes this natural.

```csharp
// In Parse() switch default case (scalars):
var scalarProp = new ParsedProperty(...);
offsetTable.Set(i, scalarProp);
if (node.Validation is not null)
    BinaryFieldValidator.ValidateNumeric(scalarProp, node, errors);
```

### Pattern 2: Compile Regex at Load Time

**What:** Add a `Regex? CompiledPattern` property to `BinaryContractNode`. In `BinaryContractLoader.MapValidation()`, if `Pattern` is non-null, compile the Regex with `RegexOptions.Compiled`.
**Why:** Regex compilation is expensive. The contract is loaded once, parsed many times. This is exactly the pattern the JSON `StringValidator.ValidatePattern` uses (it receives a pre-compiled `Regex`).

```csharp
// On BinaryContractNode:
internal Regex? CompiledPattern { get; init; }

// In BinaryContractLoader, after creating ValidationRules:
Regex? compiledPattern = dto.Pattern is not null
    ? new Regex(dto.Pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(100))
    : null;
```

### Pattern 3: Value Extraction for Validation

**What:** Read the numeric value from `ParsedProperty` for comparison against min/max, rather than re-reading raw bytes.
**Why:** `ParsedProperty.GetInt32()`, `GetFloat64()`, etc. already handle endianness and size. Reusing them avoids duplicating byte-reading logic.

For numeric validation, the value needs to be compared as `double` (since `ValidationRules.Min/Max` are `double?`). The approach:
- Extract value via the appropriate `GetXxx()` method based on `fieldType`
- Cast to `double` for comparison

```csharp
internal static void ValidateNumeric(ParsedProperty prop, BinaryContractNode node, ErrorCollector errors)
{
    var rules = node.Validation!;
    if (rules.Min is null && rules.Max is null) return;

    double value = GetNumericValue(prop, node.Type);

    if (rules.Min is not null && value < rules.Min.Value)
        errors.Add(new ValidationError("/" + node.Name, ValidationErrorCode.MinimumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinimumExceeded)));

    if (rules.Max is not null && value > rules.Max.Value)
        errors.Add(new ValidationError("/" + node.Name, ValidationErrorCode.MaximumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaximumExceeded)));
}
```

### Pattern 4: Array Element Validation

**What:** After parsing each array element, validate it against the element type's constraints.
**When:** For scalar arrays, the element type inherits the parent array node's Validation rules. For struct arrays, each sub-field has its own Validation from the contract.

The path must include the array index: `"/readings/2"` for scalar, `"/errors/0/code"` for struct sub-fields.

The path is already on the `ParsedProperty.Path` -- reuse `prop.Path` directly as the validation error path.

### Anti-Patterns to Avoid
- **Separate validation pass:** D-01 explicitly forbids this. Validate inline in the parse loop.
- **Short-circuiting on first error:** D-02 + VALD-05 require collect-all behavior. Never break/return on validation failure.
- **Recompiling Regex per parse:** Pattern strings don't change. Compile once at load time.
- **Blocking the parsed value on validation failure:** D-02 says the value stays. `offsetTable.Set()` must happen before or independently of validation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Regex compilation | Manual string matching | `new Regex(pattern, RegexOptions.Compiled)` | Correctness, performance, edge cases |
| Error message lookup | String interpolation per error | `ValidationErrorMessages.Get(code)` | Pre-allocated strings, zero allocation |
| Error collection | Custom list or manual array | `ErrorCollector.Add()` | ArrayPool-backed, sentinel overflow, already integrated |
| String length counting | `string.Length` | Character count after `GetString()` or byte-level counting | Fixed-size binary strings may have null padding; `minLength`/`maxLength` should measure meaningful content |

**Key insight:** All validation infrastructure already exists in `Gluey.Contract`. The binary validator just needs to call into it with the right values and paths.

## Common Pitfalls

### Pitfall 1: Numeric Type Dispatch for Validation
**What goes wrong:** Using `GetInt32()` on a `uint16` field throws due to type strictness.
**Why it happens:** `ParsedProperty` enforces type matching -- you can't call `GetInt32()` on a field stored as `UInt16`.
**How to avoid:** Dispatch by `fieldType` constant to call the matching `GetXxx()` method, then widen to `double` for min/max comparison.
**Warning signs:** `InvalidOperationException` in tests when accessing wrong-typed getter.

### Pitfall 2: String Length Measurement
**What goes wrong:** Using byte length instead of character/codepoint count for minLength/maxLength validation.
**Why it happens:** Binary strings are fixed-size byte buffers. A 10-byte field may contain "HELLO\0\0\0\0\0" (5 meaningful chars in 10 bytes).
**How to avoid:** Call `GetString()` first (which handles trimming), then measure `string.Length`. The ADR says minLength/maxLength are "byte length constraints" but the CONTEXT uses the same pattern as JSON (which counts codepoints). Follow the ADR wording: validate against byte length of the raw field, not trimmed string length.
**Warning signs:** Tests passing with ASCII but failing with multi-byte UTF-8 or padded strings.

**Clarification on ADR:** The ADR says `minLength`/`maxLength` are "Byte length constraints." This differs from JSON Schema which uses codepoint count. For binary fields with fixed byte sizes, byte length is the natural measure. However, since the field size is always fixed (declared in contract), minLength/maxLength on binary strings would compare against the meaningful content length (after null-byte trimming) rather than the raw buffer size. Use `GetString().Length` (character count) as the practical measure, matching the JSON validator behavior for consistency.

### Pitfall 3: Array Element Validation Paths
**What goes wrong:** Validation errors for array elements all report the same path (the array container path).
**Why it happens:** Forgetting to include the element index in the error path.
**How to avoid:** Use the `ParsedProperty.Path` which already includes the full path (e.g., `"/readings/2"`).
**Warning signs:** Multiple errors with identical paths pointing to the array container.

### Pitfall 4: Struct Sub-Field Validation Rules
**What goes wrong:** Struct sub-fields within arrays don't get validated because validation only checks top-level nodes.
**Why it happens:** Struct sub-fields are stored on `ArrayElementInfo.StructFields` (which are `BinaryContractNode[]`), not in the top-level `OrderedFields`.
**How to avoid:** When parsing struct array elements, check each sub-field's `Validation` property and validate accordingly.
**Warning signs:** Tests for nested struct validation pass but sub-field constraints are silently ignored.

### Pitfall 5: Pass 2 (Dynamic Fields) Validation
**What goes wrong:** Validation only added in Pass 1, not in Pass 2 (dynamic-offset fields).
**Why it happens:** Pass 2 is a separate code block that duplicates the field-type switch. Easy to forget validation there.
**How to avoid:** Add validation calls in both Pass 1 and Pass 2 switch cases, or extract a shared validation helper that both passes call.
**Warning signs:** Fields after a semi-dynamic array are never validated.

## Code Examples

### Numeric Validation Helper
```csharp
// Source: Pattern derived from src/Gluey.Contract.Json/Validators/NumericValidator.cs
internal static class BinaryFieldValidator
{
    internal static void ValidateNumeric(
        double value, string path, ValidationRules rules, ErrorCollector errors)
    {
        if (rules.Min is not null && value < rules.Min.Value)
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.MinimumExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MinimumExceeded)));
        }

        if (rules.Max is not null && value > rules.Max.Value)
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.MaximumExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MaximumExceeded)));
        }
    }
}
```

### String Validation Helper
```csharp
// Source: Pattern derived from src/Gluey.Contract.Json/Validators/StringValidator.cs
internal static void ValidateString(
    string value, string path, ValidationRules rules, Regex? compiledPattern, ErrorCollector errors)
{
    if (rules.MinLength is not null && value.Length < rules.MinLength.Value)
    {
        errors.Add(new ValidationError(
            path,
            ValidationErrorCode.MinLengthExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinLengthExceeded)));
    }

    if (rules.MaxLength is not null && value.Length > rules.MaxLength.Value)
    {
        errors.Add(new ValidationError(
            path,
            ValidationErrorCode.MaxLengthExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxLengthExceeded)));
    }

    if (compiledPattern is not null && !compiledPattern.IsMatch(value))
    {
        errors.Add(new ValidationError(
            path,
            ValidationErrorCode.PatternMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.PatternMismatch)));
    }
}
```

### Inline Validation in Parse()
```csharp
// After creating and setting a scalar ParsedProperty:
var scalarProp = new ParsedProperty(
    data, node.AbsoluteOffset, node.Size,
    "/" + node.Name, 1, node.ResolvedEndianness, fieldType);
offsetTable.Set(i, scalarProp);

// Validate if rules exist
if (node.Validation is not null)
{
    double numValue = ExtractNumericAsDouble(scalarProp, fieldType);
    BinaryFieldValidator.ValidateNumeric(numValue, "/" + node.Name, node.Validation, errors);
}
```

### Test Pattern
```csharp
// Source: Pattern from existing tests (ScalarParsingTests, LeafTypeParsingTests)
private const string ValidationContractJson = """
    {
      "kind": "binary",
      "endianness": "little",
      "fields": {
        "temperature": {
          "type": "int16", "size": 2,
          "validation": { "min": -40, "max": 85 }
        },
        "humidity": {
          "dependsOn": "temperature",
          "type": "uint8", "size": 1,
          "validation": { "min": 0, "max": 100 }
        }
      }
    }
    """;

[Test]
public void Parse_NumericOutOfRange_CollectsValidationError()
{
    var schema = BinaryContractSchema.Load(ValidationContractJson)!;
    // temperature = 100 (above max 85), humidity = 50 (valid)
    var payload = new byte[] { 0x64, 0x00, 0x32 };

    using var result = schema.Parse(payload)!.Value;

    result.IsValid.Should().BeFalse();
    result.Errors.Count.Should().Be(1);
    result.Errors[0].Path.Should().Be("/temperature");
    result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);

    // Value is still accessible (D-02)
    result["temperature"].GetInt16().Should().Be(100);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate validation pass after parsing | Inline validation during parse | D-01 (this phase) | Single-pass, no extra allocation |
| Regex compiled per validation call | Regex compiled at contract load time | This phase (recommended) | Amortized cost over many parses |

**Already implemented (no work needed):**
- Payload-too-short null return (VALD-04) -- `Parse()` line 221-222
- ErrorCollector rented and passed to ParseResult -- `Parse()` lines 254, 698

## Open Questions

1. **String length semantics for binary fields**
   - What we know: ADR says "byte length constraints." JSON Schema uses codepoint count. Binary strings are fixed-size buffers with null padding.
   - What's unclear: Should minLength/maxLength compare against raw byte size, trimmed byte length, or character count of the trimmed string?
   - Recommendation: Use `GetString().Length` (character count of trimmed string) for consistency with JSON validator and practical usefulness. A fixed-size field always has the same raw byte length, so validating raw bytes against minLength/maxLength would be pointless. The meaningful length is after trimming.

2. **Regex timeout protection**
   - What we know: The JSON `StringValidator` receives a pre-compiled `Regex` but doesn't set a timeout.
   - What's unclear: Whether malicious patterns in contract JSON could cause catastrophic backtracking.
   - Recommendation: Set `TimeSpan.FromMilliseconds(100)` as the match timeout when compiling at load time. This is defense-in-depth; contract JSON is trusted input, but the cost is negligible.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| Quick run command | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests" --no-build -q` |
| Full suite command | `dotnet test tests/Gluey.Contract.Binary.Tests --no-build -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VALD-01 | Numeric min/max validation | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests&Name~Numeric" -q` | No -- Wave 0 |
| VALD-02 | String pattern validation | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests&Name~Pattern" -q` | No -- Wave 0 |
| VALD-03 | String minLength/maxLength validation | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests&Name~Length" -q` | No -- Wave 0 |
| VALD-04 | Payload too short returns null | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests&Name~TooShort" -q` | No -- Wave 0 |
| VALD-05 | Multiple errors collected | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests&Name~Multiple" -q` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Binary.Tests --no-build -q`
- **Per wave merge:** `dotnet test tests/Gluey.Contract.Binary.Tests -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Binary.Tests/ValidationTests.cs` -- covers VALD-01 through VALD-05
- [ ] `src/Gluey.Contract.Binary/Schema/BinaryFieldValidator.cs` -- validation helper methods

*(No framework gaps -- NUnit + FluentAssertions already configured and working)*

## Sources

### Primary (HIGH confidence)
- `src/Gluey.Contract/Validation/ValidationError.cs` -- readonly struct with Path, Code, Message
- `src/Gluey.Contract/Validation/ValidationErrorCode.cs` -- all needed codes already defined (MinimumExceeded, MaximumExceeded, MinLengthExceeded, MaxLengthExceeded, PatternMismatch)
- `src/Gluey.Contract/Validation/ValidationErrorMessages.cs` -- pre-allocated static messages per code
- `src/Gluey.Contract/Validation/ErrorCollector.cs` -- ArrayPool-backed, Add/HasErrors/Count, sentinel overflow
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` -- `ValidationRules?` property already populated
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` -- Parse() method where validation hooks in
- `src/Gluey.Contract.Json/Validators/NumericValidator.cs` -- reference pattern for min/max validation
- `src/Gluey.Contract.Json/Validators/StringValidator.cs` -- reference pattern for string validation with compiled Regex
- `docs/adr/16-binary-format-contract.md` -- validation rules specification (min/max, pattern, minLength/maxLength)

### Secondary (MEDIUM confidence)
- None needed -- all sources are project code

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new packages, all existing infrastructure
- Architecture: HIGH -- clear pattern from JSON validators, well-defined integration points in Parse()
- Pitfalls: HIGH -- identified from code analysis of type strictness, dual-pass structure, and string handling

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable -- internal project, no external dependencies changing)
