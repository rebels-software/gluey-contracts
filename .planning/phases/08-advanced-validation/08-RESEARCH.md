# Phase 8: Advanced Validation - Research

**Researched:** 2026-03-10
**Domain:** JSON Schema Draft 2020-12 advanced validation keywords (patternProperties, propertyNames, contains, uniqueItems, format)
**Confidence:** HIGH

## Summary

Phase 8 completes the validation keyword coverage for JSON Schema Draft 2020-12 v1. The five requirements (VALD-12 through VALD-16) cover four distinct keyword groups: object property pattern/name validation, array containment with count constraints, array uniqueness with zero-allocation hashing, and format annotation/assertion with 9 built-in format validators.

The infrastructure is fully in place. SchemaNode already carries all Phase 8 fields (PatternProperties, PropertyNames, Contains, MinContains, MaxContains, UniqueItems, Format). JsonSchemaLoader already parses all these keywords. ValidationErrorCode and ValidationErrorMessages already have all needed entries. The work is purely implementing static validator classes following the established pattern.

**Primary recommendation:** Split into two plans: (1) patternProperties + propertyNames + contains/minContains/maxContains + uniqueItems as object/array validators, (2) SchemaOptions + FormatValidator with all 9 format implementations. Plan 1 follows the zero-alloc constraint; Plan 2 has the documented allocation exception for format assertion.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Format Assertion API: Opt-in via `SchemaOptions` sealed class passed to `TryLoad`/`Load` -- e.g., `TryLoad(json, new SchemaOptions { AssertFormat = true })`
- `SchemaOptions` is a public sealed class with sensible defaults
- All-or-nothing: `AssertFormat = true` enables assertion for ALL recognized formats
- Unrecognized format strings pass silently (spec-compliant)
- Implement all 9 formats for v1: date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer
- Email validation: simplified structural check (has @, non-empty local/domain, valid characters) -- not full RFC 5321
- Date/time validation: use `DateTimeOffset.TryParse`
- Organized as a single static `FormatValidator` class with dispatcher and private per-format methods
- Accept allocations in format assertion path; zero-alloc guarantee applies to core validation path only
- Document allocation behavior in XML doc comment on `AssertFormat` property AND project docs
- uniqueItems: Hybrid FNV-1a hash of raw bytes into stackalloc'd hash set for arrays <= 128 items; O(n^2) byte comparison fallback for collisions
- Stack threshold: 128 items (128 x sizeof(int) = 512 bytes on stack)
- Spec-compliant numeric equivalence: 1 and 1.0 are duplicates -- byte-level first, then numeric decimal fallback (same pattern as enum/const in KeywordValidator)

### Claude's Discretion
- patternProperties and propertyNames implementation details (well-defined by spec)
- contains/minContains/maxContains implementation approach
- Hash function choice and collision handling details for uniqueItems
- Internal method signatures and parameter ordering

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| VALD-12 | patternProperties and propertyNames | Static methods on ObjectValidator or new PatternPropertyValidator; regex patterns already compiled at load time via ReadSchemaMap; propertyNames validates property name strings against a subschema |
| VALD-13 | contains, minContains, maxContains | ArrayValidator or ContainsValidator; walker counts matching elements, validator checks count against min/max bounds; minContains defaults to 1 per spec |
| VALD-14 | uniqueItems with zero-allocation hashing | UniqueItemsValidator with FNV-1a hash into stackalloc'd int[128]; TryNumericEqual reusable from KeywordValidator for numeric equivalence |
| VALD-15 | Format annotation by default, opt-in assertion | SchemaOptions public sealed class; AssertFormat bool property; propagated from TryLoad/Load to validation |
| VALD-16 | Common format validators (9 formats) | FormatValidator static class with dispatcher; uses .NET built-in parsers (DateTimeOffset, Guid, IPAddress, Uri) |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9 | net9.0 | Target framework | Project-established |
| System.Text.Json | built-in | Utf8JsonReader for number parsing in uniqueItems | Already used throughout |
| System.Text.RegularExpressions | built-in | Compiled regex for patternProperties matching | Already used for pattern keyword |
| System.Net | built-in | IPAddress.TryParse for ipv4/ipv6 format validation | Zero-dependency approach |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework | All test files |
| FluentAssertions | 8.0.1 | Assertion library | All test assertions |

### Alternatives Considered
None -- all decisions are locked or follow established project patterns.

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract.Json/
    ObjectValidator.cs           # Add ValidatePatternProperty, ValidatePropertyName
    ArrayValidator.cs            # Add ValidateContains, ValidateUniqueItems
    FormatValidator.cs           # NEW: format dispatch + 9 private validators
    JsonContractSchema.cs        # Add SchemaOptions parameter to TryLoad/Load
src/Gluey.Contract/
    SchemaOptions.cs             # NEW: public sealed class
    SchemaNode.cs                # No changes needed (fields exist)
tests/Gluey.Contract.Json.Tests/
    PatternPropertyValidatorTests.cs  # NEW
    ContainsValidatorTests.cs         # NEW
    UniqueItemsValidatorTests.cs      # NEW
    FormatValidatorTests.cs           # NEW
    SchemaOptionsTests.cs             # NEW
```

### Pattern 1: Static Validator Method (established)
**What:** Static methods on validator classes that return bool and push errors to ErrorCollector
**When to use:** All new validation keywords
**Example:**
```csharp
// Follows exact pattern from ObjectValidator, ArrayValidator, etc.
internal static bool ValidatePatternProperty(
    bool schemaResult,
    string propertyName,
    string path,
    ErrorCollector collector)
{
    if (schemaResult)
        return true;

    collector.Add(new ValidationError(
        SchemaNode.BuildChildPath(path, propertyName),
        ValidationErrorCode.PatternPropertyInvalid,
        ValidationErrorMessages.Get(ValidationErrorCode.PatternPropertyInvalid)));
    return false;
}
```

### Pattern 2: Walker-Delegated Validation (established)
**What:** Validator receives pre-computed results (bool or count), not raw data. Walker handles traversal.
**When to use:** patternProperties (walker matches property names against regex, calls validator per match), contains (walker evaluates contains schema per element, passes match count), propertyNames (walker evaluates propertyNames schema per name, passes result)
**Example:**
```csharp
// Same pattern as CompositionValidator.ValidateAllOf receiving passCount
internal static bool ValidateContains(
    int matchCount,
    int? minContains,
    int? maxContains,
    string path,
    ErrorCollector collector)
{
    int effectiveMin = minContains ?? 1; // spec default
    // ...
}
```

### Pattern 3: Zero-Allocation Hash Set (new for this phase)
**What:** stackalloc'd int array used as hash buckets for uniqueItems duplicate detection
**When to use:** uniqueItems validation with arrays <= 128 items
**Example:**
```csharp
// FNV-1a hash of raw bytes, linear probing in stackalloc'd array
Span<int> buckets = stackalloc int[128];
buckets.Clear();
// Hash each element's raw bytes, detect collisions, then byte-compare
```

### Anti-Patterns to Avoid
- **Allocating in core validation path:** uniqueItems must use stackalloc, not new int[] or HashSet<T>
- **Regex recompilation:** patternProperties patterns are already compiled at load time (stored in SchemaNode.PatternProperties dict keys -- but note the keys are strings, not compiled Regex; the regex compilation for patternProperties needs to happen at load time)
- **Failing on unknown format:** Unrecognized format strings MUST pass silently per spec
- **Format assertion by default:** Format is annotation-only unless SchemaOptions.AssertFormat = true

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IPv4/IPv6 parsing | Custom parser | `IPAddress.TryParse` | Edge cases (leading zeros, mapped addresses) |
| UUID parsing | Custom parser | `Guid.TryParse` | Handles all UUID formats |
| URI parsing | Custom validator | `Uri.TryCreate` with `UriKind.Absolute` | RFC 3986 compliance |
| Date/time parsing | Custom ISO 8601 parser | `DateTimeOffset.TryParse` | Handles timezone offsets, leap seconds |
| Regex matching | Manual string scanning | `Regex.IsMatch` with pre-compiled regex | Already established pattern |

**Key insight:** .NET has high-quality built-in parsers for most format types. Using them avoids thousands of lines of spec-compliance code and edge case handling.

## Common Pitfalls

### Pitfall 1: patternProperties Regex Not Compiled at Load Time
**What goes wrong:** patternProperties keys are regex pattern strings, but the current loader stores them as Dictionary<string, SchemaNode> where keys are raw strings. The regex needs to be compiled at load time.
**Why it happens:** JsonSchemaLoader.ReadSchemaMap doesn't compile regex for patternProperties keys (unlike the `pattern` keyword which gets CompiledPattern).
**How to avoid:** Either (a) add a parallel Dictionary<Regex, SchemaNode> CompiledPatternProperties on SchemaNode, or (b) compile regex on first use and cache. Option (a) is consistent with the CompiledPattern precedent. The loader must compile each patternProperties key as Regex at schema load time and fail-fast on invalid patterns.
**Warning signs:** Performance regression if regex is compiled per-validation-call.

### Pitfall 2: additionalProperties Must Account for patternProperties
**What goes wrong:** A property matched by patternProperties but not in `properties` is NOT an "additional property" per the spec.
**Why it happens:** The existing ValidateAdditionalProperty only checks against `properties` dict.
**How to avoid:** The walker must check patternProperties matches before calling additionalProperty validation. Properties matched by ANY patternProperties pattern are not "additional." This is a walker concern (Phase 9), but the validator API may need awareness.
**Warning signs:** False rejections when additionalProperties: false + patternProperties both present.

### Pitfall 3: minContains Default is 1, Not 0
**What goes wrong:** With `contains` present and no `minContains`, at least 1 matching element is required.
**Why it happens:** Developers assume default is 0 (no minimum).
**How to avoid:** Explicitly default to 1 in the validator: `int effectiveMin = minContains ?? 1;`
**Warning signs:** Arrays with 0 matching elements incorrectly passing validation.

### Pitfall 4: maxContains Without contains Has No Effect
**What goes wrong:** `maxContains` alone (without `contains`) should be ignored per spec.
**Why it happens:** Validator checks maxContains independently of contains presence.
**How to avoid:** Only evaluate minContains/maxContains when contains schema is present on the SchemaNode.
**Warning signs:** Validation errors on schemas that only specify maxContains without contains.

### Pitfall 5: uniqueItems Numeric Equivalence
**What goes wrong:** `[1, 1.0]` should be detected as duplicates, but byte comparison sees them as different.
**Why it happens:** JSON allows different representations of the same numeric value.
**How to avoid:** Use the same two-phase approach as enum/const: byte comparison first, then TryNumericEqual fallback for number tokens. The existing `KeywordValidator.TryNumericEqual` is private -- it needs to become internal to be reusable.
**Warning signs:** `[1, 1.0]` or `[1e2, 100]` not detected as duplicates.

### Pitfall 6: Format "time" Requires Offset
**What goes wrong:** RFC 3339 time format requires a timezone offset (e.g., "14:30:00Z" or "14:30:00+05:00"), but DateTimeOffset.TryParse may accept offset-less times.
**Why it happens:** .NET's parser is more permissive than RFC 3339.
**How to avoid:** After successful parse, verify the string contains 'Z', '+', or '-' offset indicator.
**Warning signs:** "14:30:00" (no offset) incorrectly passing time format validation.

### Pitfall 7: SchemaOptions Propagation
**What goes wrong:** SchemaOptions is passed at load time but needs to influence validation behavior at parse time.
**Why it happens:** The schema is loaded once and parsed many times; options must persist.
**How to avoid:** Store AssertFormat flag on JsonContractSchema instance (or on SchemaNode root). The flag is set during TryLoad/Load and consulted during validation.
**Warning signs:** Format assertion not working because the flag isn't accessible during validation.

## Code Examples

### patternProperties Validation (walker calls per matching property)
```csharp
// ObjectValidator addition -- called by walker for each property that matches a pattern
internal static bool ValidatePatternProperty(
    bool schemaResult,
    string propertyName,
    string path,
    ErrorCollector collector)
{
    if (schemaResult)
        return true;

    collector.Add(new ValidationError(
        SchemaNode.BuildChildPath(path, propertyName),
        ValidationErrorCode.PatternPropertyInvalid,
        ValidationErrorMessages.Get(ValidationErrorCode.PatternPropertyInvalid)));
    return false;
}
```

### propertyNames Validation (walker calls per property name)
```csharp
// ObjectValidator addition -- called by walker for each property name
internal static bool ValidatePropertyName(
    bool nameSchemaResult,
    string propertyName,
    string path,
    ErrorCollector collector)
{
    if (nameSchemaResult)
        return true;

    collector.Add(new ValidationError(
        SchemaNode.BuildChildPath(path, propertyName),
        ValidationErrorCode.PropertyNameInvalid,
        ValidationErrorMessages.Get(ValidationErrorCode.PropertyNameInvalid)));
    return false;
}
```

### contains/minContains/maxContains Validation
```csharp
// ArrayValidator addition -- receives match count from walker
internal static bool ValidateContains(
    int matchCount,
    int? minContains,
    int? maxContains,
    string path,
    ErrorCollector collector)
{
    bool valid = true;
    int effectiveMin = minContains ?? 1; // spec default

    if (matchCount < effectiveMin)
    {
        var code = minContains.HasValue
            ? ValidationErrorCode.MinContainsExceeded
            : ValidationErrorCode.ContainsInvalid;
        collector.Add(new ValidationError(path, code, ValidationErrorMessages.Get(code)));
        valid = false;
    }

    if (maxContains.HasValue && matchCount > maxContains.Value)
    {
        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MaxContainsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxContainsExceeded)));
        valid = false;
    }

    return valid;
}
```

### uniqueItems with FNV-1a Hash (zero-allocation)
```csharp
internal static bool ValidateUniqueItems(
    ReadOnlySpan<byte>[] elementBytes,
    bool[] isNumber,
    string path,
    ErrorCollector collector)
{
    int count = elementBytes.Length;
    if (count <= 1) return true;

    // Phase 1: FNV-1a hash into stackalloc'd buckets
    Span<int> hashes = count <= 128
        ? stackalloc int[count]
        : new int[count]; // fallback for huge arrays

    for (int i = 0; i < count; i++)
        hashes[i] = Fnv1aHash(elementBytes[i]);

    // Phase 2: compare hashes, byte-compare on collision, numeric fallback
    for (int i = 0; i < count; i++)
    {
        for (int j = i + 1; j < count; j++)
        {
            if (hashes[i] != hashes[j]) continue;

            // Hash collision -- byte-exact check
            if (elementBytes[i].SequenceEqual(elementBytes[j]))
            {
                collector.Add(new ValidationError(path,
                    ValidationErrorCode.UniqueItemsViolation,
                    ValidationErrorMessages.Get(ValidationErrorCode.UniqueItemsViolation)));
                return false;
            }

            // Numeric equivalence fallback
            if (isNumber[i] && isNumber[j] &&
                TryNumericEqual(elementBytes[i], elementBytes[j], out bool equal) && equal)
            {
                collector.Add(new ValidationError(path,
                    ValidationErrorCode.UniqueItemsViolation,
                    ValidationErrorMessages.Get(ValidationErrorCode.UniqueItemsViolation)));
                return false;
            }
        }
    }
    return true;
}

private static int Fnv1aHash(ReadOnlySpan<byte> bytes)
{
    unchecked
    {
        int hash = (int)2166136261u;
        for (int i = 0; i < bytes.Length; i++)
            hash = (hash ^ bytes[i]) * 16777619;
        return hash;
    }
}
```

### FormatValidator Dispatcher
```csharp
internal static class FormatValidator
{
    internal static bool Validate(string format, ReadOnlySpan<byte> valueBytes, string path, ErrorCollector collector)
    {
        bool valid = format switch
        {
            "date-time" => ValidateDateTime(valueBytes),
            "date" => ValidateDate(valueBytes),
            "time" => ValidateTime(valueBytes),
            "email" => ValidateEmail(valueBytes),
            "uuid" => ValidateUuid(valueBytes),
            "uri" => ValidateUri(valueBytes),
            "ipv4" => ValidateIpv4(valueBytes),
            "ipv6" => ValidateIpv6(valueBytes),
            "json-pointer" => ValidateJsonPointer(valueBytes),
            _ => true, // unknown formats pass silently
        };

        if (!valid)
        {
            collector.Add(new ValidationError(path,
                ValidationErrorCode.FormatInvalid,
                ValidationErrorMessages.Get(ValidationErrorCode.FormatInvalid)));
        }
        return valid;
    }
}
```

### SchemaOptions
```csharp
// In Gluey.Contract namespace (public API)
public sealed class SchemaOptions
{
    /// <summary>
    /// When true, the "format" keyword is treated as an assertion (validated).
    /// When false (default), "format" is treated as an annotation only.
    /// Note: format assertion may allocate (string conversions for .NET parser APIs).
    /// </summary>
    public bool AssertFormat { get; init; } = false;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| format always validates | format is annotation by default (Draft 2020-12) | JSON Schema 2019-09 | Must be opt-in assertion |
| additionalItems keyword | items keyword (Draft 2020-12) | 2020-12 | Already handled in Phase 5 |
| contains alone sufficient | minContains/maxContains added | 2019-09 | Need count-based contains validation |

**Deprecated/outdated:**
- Draft-04 `additionalItems` replaced by `items`/`prefixItems` split (already handled)

## Open Questions

1. **patternProperties regex compilation storage**
   - What we know: Keys in PatternProperties dictionary are regex strings. CompiledPattern on SchemaNode is for the `pattern` keyword only.
   - What's unclear: Best storage approach -- new field `CompiledPatternProperties` as `Dictionary<Regex, SchemaNode>` or `(Regex, SchemaNode)[]` on SchemaNode?
   - Recommendation: Add `internal (Regex Pattern, SchemaNode Schema)[]? CompiledPatternProperties` to SchemaNode. Compile in JsonSchemaLoader alongside the existing pattern compilation. Fail-fast on invalid regex. Array instead of dictionary since we iterate all patterns per property name.

2. **TryNumericEqual accessibility**
   - What we know: `KeywordValidator.TryNumericEqual` is currently private.
   - What's unclear: Should it move to a shared utility or become internal?
   - Recommendation: Make it `internal` on KeywordValidator so uniqueItems can reuse it. This is the minimal change.

3. **uniqueItems element collection**
   - What we know: The walker will need to collect element byte spans for uniqueItems comparison.
   - What's unclear: How elements are presented to the validator (array of spans vs. callback).
   - Recommendation: Validator receives `ReadOnlySpan<byte>[]` of raw element bytes plus `bool[]` isNumber flags. Walker collects these during array traversal. For Phase 8, test with pre-built arrays; Phase 9 walker handles collection.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~PatternProperty\|ClassName~Contains\|ClassName~UniqueItems\|ClassName~Format\|ClassName~SchemaOptions" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VALD-12 | patternProperties matches property name regex and validates values | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~PatternProperty" -x` | No -- Wave 0 |
| VALD-12 | propertyNames validates all property name strings | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~PatternProperty" -x` | No -- Wave 0 |
| VALD-13 | contains validates at least one element matches | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Contains" -x` | No -- Wave 0 |
| VALD-13 | minContains/maxContains count control | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Contains" -x` | No -- Wave 0 |
| VALD-14 | uniqueItems detects duplicates via FNV-1a hashing | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~UniqueItems" -x` | No -- Wave 0 |
| VALD-14 | numeric equivalence (1 vs 1.0) detected as duplicate | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~UniqueItems" -x` | No -- Wave 0 |
| VALD-15 | format treated as annotation by default (no error) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Format" -x` | No -- Wave 0 |
| VALD-15 | format asserts when AssertFormat=true | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Format" -x` | No -- Wave 0 |
| VALD-16 | 9 format validators produce correct results | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Format" -x` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --no-build -x`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/PatternPropertyValidatorTests.cs` -- covers VALD-12
- [ ] `tests/Gluey.Contract.Json.Tests/ContainsValidatorTests.cs` -- covers VALD-13
- [ ] `tests/Gluey.Contract.Json.Tests/UniqueItemsValidatorTests.cs` -- covers VALD-14
- [ ] `tests/Gluey.Contract.Json.Tests/FormatValidatorTests.cs` -- covers VALD-15, VALD-16
- [ ] `tests/Gluey.Contract.Json.Tests/SchemaOptionsTests.cs` -- covers VALD-15 (SchemaOptions loading)
- No framework install needed -- NUnit 4.3.1 and FluentAssertions 8.0.1 already configured

## Sources

### Primary (HIGH confidence)
- Project codebase -- SchemaNode.cs, JsonSchemaLoader.cs, KeywordValidator.cs, ObjectValidator.cs, ArrayValidator.cs (established patterns)
- JSON Schema Draft 2020-12 Validation specification (https://json-schema.org/draft/2020-12/json-schema-validation) -- format, uniqueItems, minContains/maxContains semantics
- JSON Schema Draft 2020-12 Core specification (https://json-schema.org/draft/2020-12/json-schema-core) -- patternProperties, additionalProperties interaction

### Secondary (MEDIUM confidence)
- Learn JSON Schema (https://www.learnjsonschema.com/2020-12/applicator/patternproperties/) -- patternProperties/additionalProperties interaction verified against spec

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all .NET built-in APIs, no new dependencies
- Architecture: HIGH -- follows 7 phases of established validator patterns exactly
- Pitfalls: HIGH -- spec semantics verified against official JSON Schema docs, code patterns observed directly

**Research date:** 2026-03-10
**Valid until:** 2026-04-10 (stable -- JSON Schema 2020-12 is finalized spec)
