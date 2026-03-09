---
phase: 05-basic-validation
plan: 01
subsystem: validation
tags: [json-schema, type-validation, enum, const, utf8, zero-allocation, bitwise]

# Dependency graph
requires:
  - phase: 01-core-types
    provides: ErrorCollector, ValidationError, ValidationErrorCode, ValidationErrorMessages, SchemaType
  - phase: 02-schema-model
    provides: SchemaNode with Type/Enum/Const fields
  - phase: 04-json-byte-reader
    provides: JsonByteTokenType enum
provides:
  - KeywordValidator static class with ValidateType, ValidateEnum, ValidateConst
  - MapTokenToSchemaType token-to-schema-type mapping
  - IsInteger mathematical integer detection via Utf8JsonReader
  - TryNumericEqual private helper for decimal numeric comparison
affects: [06-numeric-string-constraints, 09-single-pass-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: [static-validator-method, byte-first-numeric-fallback, bitwise-type-matching]

key-files:
  created:
    - src/Gluey.Contract.Json/KeywordValidator.cs
    - tests/Gluey.Contract.Json.Tests/KeywordValidatorTypeTests.cs
    - tests/Gluey.Contract.Json.Tests/KeywordValidatorEnumConstTests.cs
  modified: []

key-decisions:
  - "IsInteger uses TryGetInt64 fast path + TryGetDecimal fallback for mathematical integer detection (1.0, 1e2, 1.5e1)"
  - "Integer tokens map to SchemaType.Integer | SchemaType.Number for spec-compliant subset semantics"

patterns-established:
  - "Static validator pattern: internal static bool ValidateX(..., ErrorCollector collector) -- returns bool, pushes errors directly"
  - "Byte-first with numeric fallback: SequenceEqual first, TryGetDecimal comparison for number tokens only"

requirements-completed: [VALD-01, VALD-02, VALD-17]

# Metrics
duration: 3min
completed: 2026-03-09
---

# Phase 5 Plan 1: Type/Enum/Const Validators Summary

**KeywordValidator with ValidateType (7 JSON Schema types, bitwise matching), ValidateEnum/ValidateConst (byte-exact with decimal numeric fallback), and IsInteger (mathematical integer detection via Utf8JsonReader)**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-09T20:29:06Z
- **Completed:** 2026-03-09T20:32:08Z
- **Tasks:** 1 (TDD: RED + GREEN)
- **Files modified:** 3

## Accomplishments
- ValidateType handles all 7 JSON Schema types with bitwise SchemaType matching
- IsInteger correctly identifies mathematical integers including 1.0, 1e2, 1.5e1 via TryGetInt64 + TryGetDecimal fallback
- ValidateEnum and ValidateConst implement byte-first comparison with decimal numeric fallback for JSON Schema value equality
- 36 new tests, 125 total suite green

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: KeywordValidator tests** - `86da804` (test)
2. **Task 1 GREEN: KeywordValidator implementation** - `315afe3` (feat)

_TDD task: RED committed failing tests, GREEN committed passing implementation._

## Files Created/Modified
- `src/Gluey.Contract.Json/KeywordValidator.cs` - Static validator class with ValidateType, ValidateEnum, ValidateConst, MapTokenToSchemaType, IsInteger, TryNumericEqual
- `tests/Gluey.Contract.Json.Tests/KeywordValidatorTypeTests.cs` - 22 tests for type validation and integer detection
- `tests/Gluey.Contract.Json.Tests/KeywordValidatorEnumConstTests.cs` - 14 tests for enum/const validation including numeric fallback

## Decisions Made
- IsInteger uses TryGetInt64 as fast path (handles plain integers like "42") then falls back to TryGetDecimal + truncation check for mathematical integers (1.0, 1e2, 1.5e1). TryGetInt64 alone rejects decimal-point and scientific notation forms.
- Integer tokens map to `SchemaType.Integer | SchemaType.Number` so bitwise AND matches either type constraint per JSON Schema spec.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IsInteger TryGetInt64 insufficient for mathematical integers**
- **Found during:** Task 1 GREEN phase
- **Issue:** `Utf8JsonReader.TryGetInt64()` only parses plain integer literals -- rejects `1.0`, `1e2`, `1.5e1` which are mathematical integers per JSON Schema Draft 2020-12
- **Fix:** Added TryGetDecimal fallback: if TryGetInt64 fails, parse as decimal and check `value == decimal.Truncate(value)` within Int64 range
- **Files modified:** src/Gluey.Contract.Json/KeywordValidator.cs
- **Verification:** All 36 tests pass including 1.0, 1e2, 1.5e1 cases
- **Committed in:** 315afe3

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Bug in assumed TryGetInt64 behavior caught by TDD RED tests. Fix is correct per JSON Schema spec.

## Issues Encountered
None beyond the TryGetInt64 behavior documented in Deviations above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- KeywordValidator foundation established for subsequent plans to add required/properties/additionalProperties/items/prefixItems validators
- Static validator pattern proven and ready for Phase 6 NumericValidator and StringValidator classes
- Error collection pipeline verified with accumulation semantics (not fail-fast)

---
*Phase: 05-basic-validation*
*Completed: 2026-03-09*
