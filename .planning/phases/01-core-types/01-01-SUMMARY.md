---
phase: 01-core-types
plan: 01
subsystem: api
tags: [readonly-struct, utf8-parser, zero-allocation, json-schema, validation]

# Dependency graph
requires: []
provides:
  - "ParsedProperty readonly struct with zero-allocation value materialization from byte buffers"
  - "ValidationErrorCode byte-backed enum covering all JSON Schema Draft 2020-12 keywords (~35 values)"
  - "ValidationError readonly struct with RFC 6901 path, error code, and static message"
  - "ValidationErrorMessages internal static lookup for all error codes"
affects: [02-core-types, 03-schema-model, 04-walker, 05-validation, 06-validation, 07-validation, 08-validation, 09-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [zero-allocation-utf8-parsing, readonly-struct-value-types, static-message-lookup]

key-files:
  created:
    - src/Gluey.Contract/ParsedProperty.cs
    - src/Gluey.Contract/ValidationErrorCode.cs
    - src/Gluey.Contract/ValidationError.cs
    - src/Gluey.Contract/ValidationErrorMessages.cs
    - tests/Gluey.Contract.Tests/ParsedPropertyTests.cs
    - tests/Gluey.Contract.Tests/ValidationErrorTests.cs
  modified: []

key-decisions:
  - "ParsedProperty offset/length points to content inside quotes (no quotes) -- contract with Phase 4 tokenizer"
  - "ValidationErrorMessages uses case-insensitive test assertion for sentinel message verification"

patterns-established:
  - "Zero-allocation value materialization: Utf8Parser.TryParse for numerics, Encoding.UTF8.GetString for strings"
  - "Byte-backed enum for error codes with static message lookup array indexed by (int)code"
  - "TDD workflow: RED (failing tests) -> GREEN (implementation) -> commit"

requirements-completed: [CORE-01, CORE-02, CORE-04]

# Metrics
duration: 4min
completed: 2026-03-08
---

# Phase 1 Plan 1: Leaf Types Summary

**ParsedProperty zero-allocation byte buffer accessor with 6 GetX() materializers, plus ValidationError types with 35 byte-backed error codes and static message lookup**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-08T22:08:44Z
- **Completed:** 2026-03-08T22:12:32Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- ParsedProperty readonly struct with internal constructor, HasValue, Path, RawBytes, and 6 GetX() methods using Utf8Parser for zero-allocation parsing
- ValidationErrorCode byte-backed enum with 35 values covering all JSON Schema Draft 2020-12 keywords plus TooManyErrors sentinel
- ValidationError readonly struct with Path, Code, Message fields
- ValidationErrorMessages internal static class with pre-allocated message strings for every code
- 33 unit tests covering construction, materialization, default behavior, and full enum message coverage

## Task Commits

Each task was committed atomically:

1. **Task 1: ParsedProperty tests (RED)** - `f0ec42c` (test)
2. **Task 1: ParsedProperty implementation (GREEN)** - `780325a` (feat)
3. **Task 2: ValidationError tests (RED)** - `c9545af` (test)
4. **Task 2: ValidationError types implementation (GREEN)** - `5d70811` (feat)

_Note: TDD tasks have separate test and implementation commits._

## Files Created/Modified
- `src/Gluey.Contract/ParsedProperty.cs` - Zero-allocation byte buffer accessor with value materialization via Utf8Parser
- `src/Gluey.Contract/ValidationErrorCode.cs` - Byte-backed enum with 35 JSON Schema error codes
- `src/Gluey.Contract/ValidationError.cs` - Readonly struct carrying path, code, and message
- `src/Gluey.Contract/ValidationErrorMessages.cs` - Static message lookup array indexed by error code
- `tests/Gluey.Contract.Tests/ParsedPropertyTests.cs` - 21 unit tests for ParsedProperty
- `tests/Gluey.Contract.Tests/ValidationErrorTests.cs` - 12 unit tests for ValidationError types

## Decisions Made
- ParsedProperty offset/length contract: points to value content inside quotes, not raw JSON token. JSON unescaping belongs to the tokenizer (Phase 4).
- Used ContainEquivalentOf (case-insensitive) for sentinel message assertion to be resilient to message casing changes.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed case-sensitive string assertion in TooManyErrors test**
- **Found during:** Task 2 (ValidationError types GREEN phase)
- **Issue:** Test used `Contain("too many")` but message starts with "Too many" (capital T)
- **Fix:** Changed to `ContainEquivalentOf("too many")` for case-insensitive matching
- **Files modified:** tests/Gluey.Contract.Tests/ValidationErrorTests.cs
- **Verification:** All 12 validation tests pass
- **Committed in:** 5d70811 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug fix in test)
**Impact on plan:** Minor test assertion fix. No scope creep.

## Issues Encountered
- MSBuild AssemblyInfoInputs.cache corruption required a full clean (rm obj/bin) before builds would succeed. Resolved with clean + restore.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ParsedProperty and ValidationError leaf types are locked down and tested
- Ready for Plan 01-02 (OffsetTable, ErrorCollector, ParseResult) which consume these types
- No blockers

---
*Phase: 01-core-types*
*Completed: 2026-03-08*
