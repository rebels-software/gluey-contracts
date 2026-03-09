# Phase 7: Composition and Conditionals - Research

**Researched:** 2026-03-09
**Domain:** JSON Schema Draft 2020-12 composition, conditional, and dependency keywords
**Confidence:** HIGH

## Summary

Phase 7 implements three categories of validators: composition (allOf, anyOf, oneOf, not), conditional (if/then/else), and dependency (dependentRequired, dependentSchemas). All follow the established `internal static class` / `internal static bool ValidateX()` pattern. The key architectural distinction is that these validators do NOT evaluate subschemas themselves -- they receive pre-computed boolean results from the walker (Phase 9) and apply only the composition/conditional logic.

All infrastructure is already in place: SchemaNode has all fields (AllOf, AnyOf, OneOf, Not, If, Then, Else, DependentRequired, DependentSchemas), JsonSchemaLoader parses all keywords, ValidationErrorCode has all codes (including the new IfElseInvalid and DependentSchemaInvalid), and ValidationErrorMessages has all message strings. This phase is purely about writing the three validator classes and their tests.

**Primary recommendation:** Create three new validator classes (CompositionValidator, ConditionalValidator, DependencyValidator) in Gluey.Contract.Json following the exact pattern of NumericValidator/ArrayValidator -- stateless static methods that receive pre-computed results and push errors to ErrorCollector.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Walker drives all subschema evaluation -- composition validators do NOT call subschema evaluation themselves
- Walker evaluates subschemas, passes pre-computed results (bool pass/fail per subschema) to validator methods
- Validators apply composition logic only: "all passed?" / "at least one?" / "exactly one?" / "none passed?"
- Silent (bool-only) evaluation mode: walker evaluates subschemas without collecting errors into the main ErrorCollector -- used for not, anyOf, oneOf, and if-schema evaluation
- All inner subschema errors are suppressed for all composition, conditional, and dependency keywords
- Only top-level errors reported: AllOfInvalid, AnyOfInvalid, OneOfInvalid, NotInvalid, IfThenInvalid, IfElseInvalid, DependentRequiredMissing, DependentSchemaInvalid
- Generic static messages only (already defined in ValidationErrorMessages)
- OneOfInvalid used for both "zero matched" and "multiple matched" -- single error code
- dependentRequired errors use root object path (SchemaNode.Path), consistent with RequiredMissing
- 'if' subschema evaluated in same bool-only silent mode as composition keywords
- If 'if' passes and 'then' exists -> evaluate 'then' (errors suppressed, report IfThenInvalid on failure)
- If 'if' fails and 'else' exists -> evaluate 'else' (errors suppressed, report IfElseInvalid on failure)
- If 'then'/'else' missing for the relevant branch -> no-op
- If 'if' present without both 'then' and 'else' -> no validation effect
- Three new internal static classes: CompositionValidator, ConditionalValidator, DependencyValidator
- Each follows: `internal static bool ValidateX(..., ErrorCollector collector)`
- DependencyValidator receives property name set (HashSet<string> or similar)

### Claude's Discretion
- Exact method signatures for composition validators (how bool results are passed -- bool[], int passCount, etc.)
- Internal helpers for property name collection in dependency validation
- Test organization and test helper utilities
- Whether CompositionValidator methods take individual bools or arrays

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| VALD-09 | allOf, anyOf, oneOf, not composition | CompositionValidator with four methods; all error codes and messages already exist |
| VALD-10 | if / then / else conditional validation | ConditionalValidator with ValidateIfThen and ValidateIfElse; IfElseInvalid error code already added |
| VALD-11 | dependentRequired and dependentSchemas | DependencyValidator with two methods; reuses HashSet<string> pattern from ValidateRequired |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET | 9.0 | Runtime | Project target framework |
| NUnit | 4.3.1 | Test framework | Already in use across project |
| FluentAssertions | 8.0.1 | Test assertions | Already in use across project |

### Supporting
No new libraries needed. All infrastructure exists.

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract.Json/
    CompositionValidator.cs      # NEW - allOf, anyOf, oneOf, not
    ConditionalValidator.cs      # NEW - if/then/else
    DependencyValidator.cs       # NEW - dependentRequired, dependentSchemas
tests/Gluey.Contract.Json.Tests/
    CompositionValidatorTests.cs # NEW
    ConditionalValidatorTests.cs # NEW
    DependencyValidatorTests.cs  # NEW
```

### Pattern 1: Stateless Static Validator
**What:** Each validator is an `internal static class` with `internal static bool ValidateX(...)` methods. Methods return `true` on success, `false` on failure (after pushing error to collector).
**When to use:** All validation methods in this project.
**Example (from existing code):**
```csharp
// Source: src/Gluey.Contract.Json/ArrayValidator.cs
internal static class ArrayValidator
{
    internal static bool ValidateMinItems(int itemCount, int minItems, string path, ErrorCollector collector)
    {
        if (itemCount >= minItems)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinItemsExceeded)));
        return false;
    }
}
```

### Pattern 2: Composition Validator Method Signatures
**What:** Composition methods receive pre-computed pass/fail counts or arrays, not raw data.
**Recommendation:** Use `int passCount, int totalCount` for allOf/anyOf/oneOf (simpler than bool[]), and `bool passed` for not.

```csharp
internal static class CompositionValidator
{
    // allOf: all must pass
    internal static bool ValidateAllOf(int passCount, int totalCount, string path, ErrorCollector collector)
    {
        if (passCount == totalCount)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.AllOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.AllOfInvalid)));
        return false;
    }

    // anyOf: at least one must pass
    internal static bool ValidateAnyOf(int passCount, string path, ErrorCollector collector)
    {
        if (passCount > 0)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.AnyOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.AnyOfInvalid)));
        return false;
    }

    // oneOf: exactly one must pass
    internal static bool ValidateOneOf(int passCount, string path, ErrorCollector collector)
    {
        if (passCount == 1)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.OneOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.OneOfInvalid)));
        return false;
    }

    // not: must fail
    internal static bool ValidateNot(bool subschemaResult, string path, ErrorCollector collector)
    {
        if (!subschemaResult)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.NotInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.NotInvalid)));
        return false;
    }
}
```

**Rationale for `int passCount` over `bool[]`:** The walker already iterates subschemas to evaluate them. Passing a count is simpler, avoids array allocation, and the validator only needs aggregate results. For `not` there is a single subschema so a plain `bool` suffices.

### Pattern 3: Conditional Validator
**What:** Walker evaluates if-subschema silently, then evaluates then/else as needed. Validator receives the boolean result of then/else evaluation.

```csharp
internal static class ConditionalValidator
{
    // Called when if-schema passed and then-schema was evaluated
    internal static bool ValidateIfThen(bool thenResult, string path, ErrorCollector collector)
    {
        if (thenResult)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.IfThenInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.IfThenInvalid)));
        return false;
    }

    // Called when if-schema failed and else-schema was evaluated
    internal static bool ValidateIfElse(bool elseResult, string path, ErrorCollector collector)
    {
        if (elseResult)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.IfElseInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.IfElseInvalid)));
        return false;
    }
}
```

### Pattern 4: Dependency Validator
**What:** dependentRequired checks property co-occurrence. dependentSchemas checks that when a trigger property is present, its dependent schema passes.

```csharp
internal static class DependencyValidator
{
    // dependentRequired: if trigger property present, all dependent properties must also be present
    internal static bool ValidateDependentRequired(
        Dictionary<string, string[]> dependentRequired,
        HashSet<string> presentProperties,
        string path,
        ErrorCollector collector)
    {
        bool valid = true;
        foreach (var entry in dependentRequired)
        {
            if (!presentProperties.Contains(entry.Key))
                continue; // trigger property absent, skip

            for (int i = 0; i < entry.Value.Length; i++)
            {
                if (!presentProperties.Contains(entry.Value[i]))
                {
                    collector.Add(new ValidationError(
                        path,
                        ValidationErrorCode.DependentRequiredMissing,
                        ValidationErrorMessages.Get(ValidationErrorCode.DependentRequiredMissing)));
                    valid = false;
                }
            }
        }
        return valid;
    }

    // dependentSchemas: if trigger property present and schema evaluated, report result
    internal static bool ValidateDependentSchema(
        bool schemaResult,
        string path,
        ErrorCollector collector)
    {
        if (schemaResult)
            return true;
        collector.Add(new ValidationError(path, ValidationErrorCode.DependentSchemaInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.DependentSchemaInvalid)));
        return false;
    }
}
```

**Note on dependentRequired path:** Uses root object path (SchemaNode.Path) per CONTEXT.md decision, consistent with ValidateRequired which uses `SchemaNode.BuildChildPath(path, ...)` for each missing property. For dependentRequired, the decision says to use the root path directly -- this differs from required but is the locked decision.

### Anti-Patterns to Avoid
- **Subschema evaluation in validators:** Validators MUST NOT evaluate subschemas. They receive pre-computed results only. Subschema evaluation is Phase 9 (walker).
- **Inner error propagation:** Never collect or forward inner subschema errors. Only emit the top-level error code.
- **Instance state:** Validators are stateless. No fields, no constructors, no caching.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Error codes/messages | New enum values or message strings | Existing ValidationErrorCode + ValidationErrorMessages | All codes already defined including IfElseInvalid and DependentSchemaInvalid |
| Schema keyword fields | New schema node properties | Existing SchemaNode fields | AllOf, AnyOf, OneOf, Not, If, Then, Else, DependentRequired, DependentSchemas all exist |
| JSON parsing of keywords | New parsing logic | Existing JsonSchemaLoader | Already parses all Phase 7 keywords |
| Subschema evaluation | Recursive validation calls | Walker (Phase 9) provides results | Locked architecture decision |

**Key insight:** This phase adds only the composition/conditional logic layer. All supporting infrastructure (schema model, parsing, error codes) is complete.

## Common Pitfalls

### Pitfall 1: oneOf Zero vs Multiple Match Confusion
**What goes wrong:** Implementing separate error codes or messages for "zero matched" vs "two or more matched."
**Why it happens:** Intuition says these are different failures.
**How to avoid:** Locked decision: single OneOfInvalid error code for both cases. `passCount == 1` is the only success condition.
**Warning signs:** Any branching on zero vs multiple in error reporting.

### Pitfall 2: dependentRequired Path Inconsistency
**What goes wrong:** Using BuildChildPath for the missing dependent property name (like ValidateRequired does).
**Why it happens:** ValidateRequired uses `BuildChildPath(path, missingPropName)` for each missing property.
**How to avoid:** Locked decision: dependentRequired uses root object path (SchemaNode.Path) directly. Each error gets the same path.
**Warning signs:** Calling `SchemaNode.BuildChildPath` inside DependencyValidator.

### Pitfall 3: if Without then AND else
**What goes wrong:** Evaluating the if-schema when neither then nor else exists, wasting effort.
**Why it happens:** The schema has an if keyword, so code tries to process it.
**How to avoid:** Per spec and locked decision: if present without both then and else has no validation effect. Walker should skip entirely. Validators should never be called in this case.
**Warning signs:** Validator methods being called with default/null parameters for missing branches.

### Pitfall 4: Error Collection in Silent Mode
**What goes wrong:** Collecting subschema errors into the main ErrorCollector during composition evaluation.
**Why it happens:** Reusing the same ErrorCollector for subschema evaluation.
**How to avoid:** This is a Phase 9 (walker) concern -- the walker must implement silent bool-only evaluation. Phase 7 validators simply receive bools and never see inner errors.
**Warning signs:** N/A for Phase 7 validators; they don't have access to subschema evaluation.

### Pitfall 5: dependentSchemas Granularity
**What goes wrong:** Creating a single method that loops over all dependent schemas.
**Why it happens:** Trying to mirror dependentRequired's loop-based approach.
**How to avoid:** The walker iterates dependent schemas (checking which trigger properties are present), evaluates each schema silently, and calls ValidateDependentSchema per schema. The validator method handles one schema at a time, not the full dictionary.
**Warning signs:** DependencyValidator receiving a Dictionary<string, SchemaNode> and trying to evaluate schemas.

## Code Examples

Verified patterns from the existing codebase:

### Error Push Pattern
```csharp
// Source: src/Gluey.Contract.Json/ArrayValidator.cs
collector.Add(new ValidationError(
    path,
    ValidationErrorCode.MinItemsExceeded,
    ValidationErrorMessages.Get(ValidationErrorCode.MinItemsExceeded)));
return false;
```

### Property Presence Check Pattern (reused by DependencyValidator)
```csharp
// Source: src/Gluey.Contract.Json/KeywordValidator.cs - ValidateRequired
internal static bool ValidateRequired(
    string[] required,
    HashSet<string> seenProperties,
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

### Test Pattern
```csharp
// Source: tests/Gluey.Contract.Json.Tests/NumericValidatorTests.cs
[Test]
public void ValidateMinimum_ValueBelowMinimum_ReturnsFalse()
{
    using var collector = new ErrorCollector();
    NumericValidator.ValidateMinimum(2m, 3m, "/age", collector).Should().BeFalse();
    collector.Count.Should().Be(1);
    collector[0].Code.Should().Be(ValidationErrorCode.MinimumExceeded);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| dependencies keyword (Draft 4/7) | dependentRequired + dependentSchemas (Draft 2020-12) | JSON Schema 2019-09 | Split into two distinct keywords |
| definitions | $defs (Draft 2020-12) | JSON Schema 2019-09 | Already handled by SchemaNode |

**Deprecated/outdated:**
- `dependencies` (Draft 4/7): Split into `dependentRequired` (property co-occurrence) and `dependentSchemas` (conditional schema application) in Draft 2019-09. This project targets Draft 2020-12, so only the split keywords are implemented.

## Open Questions

1. **dependentRequired error path granularity**
   - What we know: Decision says use root object path (SchemaNode.Path)
   - What's unclear: Whether to emit one error per missing dependent property or one error per trigger property with missing dependencies
   - Recommendation: One error per missing dependent property (consistent with ValidateRequired collecting all missing), but using the root path instead of child path. This gives better diagnostics while honoring the path decision.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~CompositionValidator\|ClassName~ConditionalValidator\|ClassName~DependencyValidator" --no-build -q` |
| Full suite command | `dotnet test tests/Gluey.Contract.Json.Tests -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VALD-09 | allOf all pass/fail, anyOf at least one, oneOf exactly one, not inverts | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~CompositionValidator" -q` | No - Wave 0 |
| VALD-10 | if/then pass/fail, if/else pass/fail, if without then/else | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ConditionalValidator" -q` | No - Wave 0 |
| VALD-11 | dependentRequired co-occurrence, dependentSchemas conditional | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~DependencyValidator" -q` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~CompositionValidator|ClassName~ConditionalValidator|ClassName~DependencyValidator" -q`
- **Per wave merge:** `dotnet test tests/Gluey.Contract.Json.Tests -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/CompositionValidatorTests.cs` -- covers VALD-09
- [ ] `tests/Gluey.Contract.Json.Tests/ConditionalValidatorTests.cs` -- covers VALD-10
- [ ] `tests/Gluey.Contract.Json.Tests/DependencyValidatorTests.cs` -- covers VALD-11

No framework install needed -- NUnit and FluentAssertions already configured.

## Sources

### Primary (HIGH confidence)
- Existing codebase: SchemaNode.cs, ValidationErrorCode.cs, ValidationErrorMessages.cs, KeywordValidator.cs, NumericValidator.cs, ArrayValidator.cs -- all patterns verified by direct code inspection
- JSON Schema Draft 2020-12 spec for composition/conditional semantics -- verified against CONTEXT.md decisions

### Secondary (MEDIUM confidence)
- JSON Schema Draft 2020-12 migration notes for dependencies -> dependentRequired/dependentSchemas split

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new libraries, all infrastructure exists
- Architecture: HIGH - pattern is well-established across 5 existing validator classes, CONTEXT.md provides explicit method organization
- Pitfalls: HIGH - decisions are locked and specific, edge cases are documented

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable -- no external dependencies changing)
