# ADR 12: Bool-Only Validators for Composition and Conditionals

## Status
Accepted

## Context
JSON Schema composition keywords (`allOf`, `anyOf`, `oneOf`, `not`) and conditionals (`if`/`then`/`else`) require evaluating subschemas and counting how many pass. The naive approach creates a temporary `ErrorCollector` per subschema evaluation to capture errors, then discards it — wasting allocation on the hot path.

For composition semantics, only the **pass count** matters:
- `allOf`: all must pass
- `anyOf`: at least one must pass
- `oneOf`: exactly one must pass
- `not`: must fail

Individual subschema error details are not reported to the user — only the top-level composition failure.

## Decision
Add bool-returning validation methods that skip error collection entirely:

### For scalars
- `KeywordValidator.CheckType()` — returns `bool` (no error reporting)
- `KeywordValidator.CheckEnum()` — returns `bool`
- `KeywordValidator.CheckConst()` — returns `bool`
- `FormatValidator.Check()` — returns `bool`
- `ValidateScalarAgainstSchema()` — orchestrates all checks, returns `bool`

### For objects (composition at object level)
- `ValidateObjectSubschema()` — checks `required`, `minProperties`, `maxProperties` against captured state (`seenPropertyNames`, `propertyCount`), returns `bool`

### For arrays (composition at array level)
- `ValidateArraySubschema()` — checks `type`, `minItems`, `maxItems` against captured element count, returns `bool`

### Pattern
```
int passCount = 0;
foreach (var sub in node.AllOf)
{
    if (ValidateScalarAgainstSchema(sub, ...))  // Bool only, no ErrorCollector
        passCount++;
}
if (!CompositionValidator.ValidateAllOf(passCount, node.AllOf.Length, path, _errors))
    valid = false;  // Single error: "allOf failed" with path
```

Only the composition-level failure is reported to the main `ErrorCollector`, not per-subschema details.

## Consequences
- **Zero temporary allocation** — no `ErrorCollector` created per subschema evaluation.
- **Reduced error granularity** — cannot report "which allOf subschema failed" or "why". The error says "allOf validation failed" with the path. This matches common JSON Schema validator behavior and is sufficient for API error responses.
- **Duplicate validation logic** — `Check*` methods duplicate parts of `Validate*` methods but without error reporting. This is intentional: the alternative (a shared method with an optional error collector) would add branching to every validation call.
- **Object/array subschema validation uses captured state** — re-validates structural constraints (required, min/maxProperties, min/maxItems) against already-collected state rather than re-walking the JSON. This avoids re-reading tokens but limits subschema validation to structural keywords only.
