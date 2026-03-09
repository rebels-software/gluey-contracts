# Phase 6: Constraint Validation - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Numeric, string, and collection size constraint validators enforce value-level rules on validated JSON data. Covers VALD-06 (minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf), VALD-07 (minLength, maxLength, pattern), and VALD-08 (minItems, maxItems, minProperties, maxProperties). Composition/conditionals are Phase 7. Advanced keywords are Phase 8. The single-pass walker that orchestrates these validators is Phase 9.

</domain>

<decisions>
## Implementation Decisions

### Numeric Precision
- Decimal arithmetic throughout — SchemaNode already stores constraints as `decimal?`, validators parse token bytes to `decimal` via `Utf8JsonReader.TryGetDecimal()`
- Parsing: wrap raw UTF-8 bytes in `Utf8JsonReader`, call `TryGetDecimal()` — same pattern as existing `IsInteger()` and `TryNumericEqual()` in KeywordValidator
- multipleOf uses decimal modulo: `value % multipleOf == 0m`
- Overflow handling: if `TryGetDecimal()` fails (number exceeds ~±7.9×10²⁸), skip constraint and pass validation — pragmatic limit consistent with Phase 5's Int64 approach

### String Length & Pattern
- minLength/maxLength count Unicode codepoints using `System.Text.Rune` enumeration over UTF-8 span — zero-alloc, handles multi-byte sequences correctly
- Pattern regex compiled at schema load time with `RegexOptions.Compiled` — schema loading is not on the zero-alloc parse path, validation calls `regex.IsMatch()` which is zero-alloc for simple patterns
- Compiled `Regex` stored as a new `internal Regex? CompiledPattern` field on `SchemaNode` — co-located with the pattern string, no external cache
- Invalid regex patterns (e.g., `[invalid`) reported as schema loading error — fail-fast, caught at load time not validation time
- Pattern validator accepts a `string` parameter — walker (Phase 9) materializes the string once and passes it

### Collection Size Tracking
- Walker (Phase 9) tracks item/property counts during traversal; after processing an array/object, passes the count to validators
- Validators are pure check functions: `ValidateMinItems(int count, int? minItems, ...)` — consistent with Phase 5's stateless validator pattern

### Validator Class Organization
- 4 new internal static classes in Gluey.Contract.Json:
  - `NumericValidator` — ValidateMinimum, ValidateMaximum, ValidateExclusiveMinimum, ValidateExclusiveMaximum, ValidateMultipleOf
  - `StringValidator` — ValidateMinLength, ValidateMaxLength, ValidatePattern
  - `ArrayValidator` — ValidateMinItems, ValidateMaxItems
  - `ObjectValidator` — ValidateMinProperties, ValidateMaxProperties
- Each follows the Phase 5 pattern: `internal static bool ValidateX(..., ErrorCollector collector)`
- KeywordValidator (Phase 5) stays focused on type/enum/const/required/additionalProperties — not expanded

### Claude's Discretion
- Exact method signatures beyond the established pattern (parameter ordering, whether to combine min/max into single methods)
- Internal helpers for UTF-8 byte parsing and Rune counting
- Test organization and test helper utilities
- Whether to move GetItemSchema from KeywordValidator to ArrayValidator as a cleanup

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `KeywordValidator.IsInteger()` and `TryNumericEqual()`: establish the Utf8JsonReader-wrapping pattern for parsing numeric bytes — Phase 6 numeric validators follow the same approach
- `ValidationErrorCode.cs`: All Phase 6 error codes already defined (MinimumExceeded, MaximumExceeded, ExclusiveMinimumExceeded, ExclusiveMaximumExceeded, MultipleOfInvalid, MinLengthExceeded, MaxLengthExceeded, PatternMismatch, MinItemsExceeded, MaxItemsExceeded, MinPropertiesExceeded, MaxPropertiesExceeded)
- `ValidationErrorMessages.cs`: Static message lookup — needs entries for Phase 6 error codes
- `ErrorCollector.cs`: ArrayPool-backed, Add() method — validators push errors directly
- `SchemaNode.cs`: All constraint fields present — Minimum, Maximum, ExclusiveMinimum, ExclusiveMaximum, MultipleOf (decimal?), MinLength, MaxLength (int?), Pattern (string?), MinItems, MaxItems, MinProperties, MaxProperties (int?)

### Established Patterns
- `internal static class` with `internal static bool ValidateX()` methods returning pass/fail
- Utf8JsonReader wrapping for zero-alloc byte parsing (IsInteger, TryNumericEqual)
- Direct error push to ErrorCollector — validators never check IsFull
- SchemaNode.BuildChildPath() for constructing JSON Pointer paths

### Integration Points
- `JsonSchemaLoader.cs`: Needs to compile Regex when loading `pattern` keyword — new CompiledPattern field on SchemaNode
- Phase 9 (Single-Pass Walker): primary consumer — calls validator methods with parsed values and counts
- ErrorCollector passed through from walker to validators — same instance for entire parse

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 06-constraint-validation*
*Context gathered: 2026-03-09*
