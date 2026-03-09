---
phase: 06-constraint-validation
plan: 01
subsystem: validation
tags: [numeric-validation, string-validation, regex, unicode, codepoints, decimal]

# Dependency graph
requires:
  - phase: 05-basic-validation
    provides: "ErrorCollector, ValidationError, ValidationErrorCode, KeywordValidator pattern"
provides:
  - "NumericValidator: TryParseDecimal, ValidateMinimum, ValidateMaximum, ValidateExclusiveMinimum, ValidateExclusiveMaximum, ValidateMultipleOf"
  - "StringValidator: CountCodepoints, ValidateMinLength, ValidateMaxLength, ValidatePattern"
  - "SchemaNode.CompiledPattern property"
  - "Regex compilation at schema load time in JsonSchemaLoader"
affects: [07-composition-conditionals, 09-single-pass-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: [Rune.DecodeFromUtf8 for zero-alloc codepoint counting, RegexOptions.Compiled at load time]

key-files:
  created:
    - src/Gluey.Contract.Json/NumericValidator.cs
    - src/Gluey.Contract.Json/StringValidator.cs
    - tests/Gluey.Contract.Json.Tests/NumericValidatorTests.cs
    - tests/Gluey.Contract.Json.Tests/StringValidatorTests.cs
  modified:
    - src/Gluey.Contract/SchemaNode.cs
    - src/Gluey.Contract.Json/JsonSchemaLoader.cs

key-decisions:
  - "Regex compiled at schema load time with RegexOptions.Compiled, stored as CompiledPattern on SchemaNode"
  - "Invalid regex patterns cause schema load failure (return null) -- fail-fast at load time"
  - "multipleOf guards against zero divisor by returning true (silently pass)"
  - "Emoji test uses Encoding.UTF8.GetBytes with escape sequence rather than u8 literal with hex escapes"

patterns-established:
  - "Stateless numeric validator: internal static bool ValidateX(decimal value, decimal constraint, string path, ErrorCollector collector)"
  - "Stateless string validator: length checked via codepoint count, pattern via pre-compiled Regex"
  - "Rune.DecodeFromUtf8 loop for zero-allocation Unicode codepoint counting over UTF-8 spans"

requirements-completed: [VALD-06, VALD-07]

# Metrics
duration: 3min
completed: 2026-03-09
---

# Phase 6 Plan 01: Numeric and String Constraint Validators Summary

**NumericValidator (min/max/exclusiveMin/exclusiveMax/multipleOf) and StringValidator (minLength/maxLength/pattern) with CompiledPattern on SchemaNode and Rune-based codepoint counting**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-09T21:42:16Z
- **Completed:** 2026-03-09T21:45:30Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- NumericValidator with 6 methods covering all Draft 2020-12 numeric constraints
- StringValidator with 4 methods including zero-alloc Unicode codepoint counting via Rune
- CompiledPattern property on SchemaNode populated at schema load time
- Invalid regex patterns detected at load time (fail-fast, return null)
- 39 new tests (22 numeric + 17 string), full suite at 265 tests

## Task Commits

Each task was committed atomically:

1. **Task 1: NumericValidator with TDD** - `3ac8189` (feat)
2. **Task 2: StringValidator + CompiledPattern with TDD** - `ef0306b` (feat)

_Note: TDD tasks -- RED phase confirmed compilation failure, GREEN phase confirmed all tests pass._

## Files Created/Modified
- `src/Gluey.Contract.Json/NumericValidator.cs` - TryParseDecimal + 5 numeric constraint validators
- `src/Gluey.Contract.Json/StringValidator.cs` - CountCodepoints + 3 string constraint validators
- `src/Gluey.Contract/SchemaNode.cs` - Added CompiledPattern property and constructor parameter
- `src/Gluey.Contract.Json/JsonSchemaLoader.cs` - Regex compilation at load time, invalid pattern returns null
- `tests/Gluey.Contract.Json.Tests/NumericValidatorTests.cs` - 22 tests for numeric validators
- `tests/Gluey.Contract.Json.Tests/StringValidatorTests.cs` - 17 tests for string validators + CompiledPattern

## Decisions Made
- Regex compiled at schema load time with `RegexOptions.Compiled`, stored as `CompiledPattern` on SchemaNode -- avoids runtime compilation during validation
- Invalid regex patterns cause `JsonSchemaLoader.Load` to return null (fail-fast at load time, not at validation time)
- `multipleOf == 0m` guard returns true to avoid DivideByZeroException (silently pass)
- Emoji test fixed to use `Encoding.UTF8.GetBytes("\U0001F600")` instead of `u8` literal with hex escapes which produced individual bytes not a valid UTF-8 sequence

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed emoji test byte representation**
- **Found during:** Task 2 (StringValidator tests)
- **Issue:** Plan suggested `"\xF0\x9F\x98\x80"u8` which produced 4 separate characters (Latin-1 interpretation) not a single UTF-8 emoji sequence
- **Fix:** Changed to `Encoding.UTF8.GetBytes("\U0001F600")` for correct 4-byte UTF-8 encoding
- **Files modified:** tests/Gluey.Contract.Json.Tests/StringValidatorTests.cs
- **Verification:** Test passes, CountCodepoints correctly returns 1
- **Committed in:** ef0306b (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Minimal -- test encoding fix only. No scope creep.

## Issues Encountered
None beyond the emoji byte encoding fix documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Numeric and string constraint validators ready for Phase 9 walker integration
- Phase 06 Plan 02 (array/object size validators) can proceed independently
- All 265 tests green, no regressions

## Self-Check: PASSED

All 5 created/modified source files verified on disk. Both task commits (3ac8189, ef0306b) verified in git log.

---
*Phase: 06-constraint-validation*
*Completed: 2026-03-09*
