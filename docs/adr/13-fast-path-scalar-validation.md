# ADR 13: Fast-Path Scalar Validation for Unknown Properties

## Status
Accepted

## Context
Schemas with `additionalProperties` constrained to a type (e.g., `{"additionalProperties": {"type": "string"}}`) are common. When the JSON payload contains many unknown properties (not defined in `properties`), each one requires:

1. Decoding the property name from UTF-8 to string — heap allocation.
2. Building a child path via `BuildChildPath` — string concatenation, heap allocation.
3. Full `WalkValue` dispatch — general-purpose, handles all schema features.

For the common case where the additional property schema is "type-only" (no `enum`, `const`, numeric/string constraints, composition, conditionals, or format), all three steps can be avoided if the value passes the type check.

## Decision
Add `TryWalkValueFastPath()` — a method that attempts to validate a scalar value against a schema without allocating path strings.

### How it works
1. Check if the schema node is "simple" — only has a `Type` constraint, no other keywords.
2. Check if the current token is a scalar (not StartObject or StartArray).
3. Call `KeywordValidator.CheckType()` (bool-returning, no error reporting).
4. If type matches: return `true` (validation passed, no path allocated).
5. If type mismatches or schema is complex: return `false`, caller falls back to full `WalkValue` with path allocation.

### When it's used
- Unknown properties being validated against `additionalProperties` schema.
- Unknown properties being validated against matched `patternProperties` schema (after pattern match).

### When it's skipped
- Known properties (already have pre-computed paths from `PropertyEntry`).
- Object or array values (require recursive walking).
- Schemas with any keyword beyond `type`.

## Consequences
- **Dramatic allocation savings for large payloads** — a payload with 100 unknown string properties against `additionalProperties: {type: "string"}` saves 100 path string allocations.
- **Behavioral difference on validation failure** — when fast path fails (type mismatch), the caller builds the path and re-validates via full `WalkValue`, which will report the error with correct path. The type check runs twice in the failure case — acceptable since failures are rare.
- **Limited scope** — only applies to scalar values with type-only schemas. Does not help with complex additional property schemas, nested objects, or arrays. This is by design: the optimization targets the most common case.
