---
phase: 08-advanced-validation
plan: 01
subsystem: validation
tags: [json-schema, patternProperties, propertyNames, contains, uniqueItems, fnv-1a, regex]

requires:
  - phase: 06-string-numeric
    provides: "CompiledPattern regex pattern, KeywordValidator.TryNumericEqual"
  - phase: 05-keyword-validators
    provides: "ObjectValidator, ArrayValidator, ErrorCollector, ValidationErrorCode"
provides:
  - "ValidatePatternProperty and ValidatePropertyName on ObjectValidator"
  - "ValidateContains with minContains/maxContains on ArrayValidator"
  - "ValidateUniqueItems with FNV-1a hashing and numeric equivalence on ArrayValidator"
  - "CompiledPatternProperties field on SchemaNode compiled at load time"
  - "KeywordValidator.TryNumericEqual accessible as internal"
affects: [09-walker-integration, 10-public-api]

tech-stack:
  added: []
  patterns: [FNV-1a hashing with stackalloc for zero-alloc duplicate detection]

key-files:
  created:
    - tests/Gluey.Contract.Json.Tests/PatternPropertyValidatorTests.cs
    - tests/Gluey.Contract.Json.Tests/ContainsValidatorTests.cs
    - tests/Gluey.Contract.Json.Tests/UniqueItemsValidatorTests.cs
  modified:
    - src/Gluey.Contract/SchemaNode.cs
    - src/Gluey.Contract.Json/ObjectValidator.cs
    - src/Gluey.Contract.Json/ArrayValidator.cs
    - src/Gluey.Contract.Json/KeywordValidator.cs
    - src/Gluey.Contract.Json/JsonSchemaLoader.cs

key-decisions:
  - "FNV-1a hash with stackalloc for <= 128 items, heap fallback for larger arrays"
  - "Numeric equivalence check always runs for number pairs regardless of hash match (different representations like 1 vs 1.0)"
  - "patternProperties regex compiled at load time with fail-fast on invalid patterns"

patterns-established:
  - "FNV-1a hash pre-filter with O(n^2) comparison for uniqueItems duplicate detection"

requirements-completed: [VALD-12, VALD-13, VALD-14]

duration: 4min
completed: 2026-03-10
---

# Phase 8 Plan 1: Advanced Validation Summary

**patternProperties/propertyNames validators, contains with min/maxContains, and uniqueItems with FNV-1a zero-alloc hashing and numeric equivalence**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-10T11:53:46Z
- **Completed:** 2026-03-10T11:57:54Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Added ValidatePatternProperty and ValidatePropertyName to ObjectValidator for object pattern matching
- Added ValidateContains with minContains/maxContains bounds to ArrayValidator
- Added ValidateUniqueItems with FNV-1a hash pre-filter and numeric equivalence detection
- Added CompiledPatternProperties to SchemaNode with load-time regex compilation
- Changed KeywordValidator.TryNumericEqual to internal for cross-validator reuse
- 28 new tests covering all validator behaviors, all 331 total tests passing

## Task Commits

Each task was committed atomically:

1. **Task 1: patternProperties/propertyNames + contains + CompiledPatternProperties** - `65d1fe0` (feat)
2. **Task 2: uniqueItems with FNV-1a hashing** - `0cb6beb` (feat)

_Note: TDD tasks with RED/GREEN phases_

## Files Created/Modified
- `src/Gluey.Contract/SchemaNode.cs` - Added CompiledPatternProperties property and constructor param
- `src/Gluey.Contract.Json/ObjectValidator.cs` - Added ValidatePatternProperty and ValidatePropertyName
- `src/Gluey.Contract.Json/ArrayValidator.cs` - Added ValidateContains and ValidateUniqueItems with Fnv1aHash
- `src/Gluey.Contract.Json/KeywordValidator.cs` - Changed TryNumericEqual from private to internal
- `src/Gluey.Contract.Json/JsonSchemaLoader.cs` - Added patternProperties regex compilation at load time
- `tests/Gluey.Contract.Json.Tests/PatternPropertyValidatorTests.cs` - 11 tests for pattern/property validators
- `tests/Gluey.Contract.Json.Tests/ContainsValidatorTests.cs` - 8 tests for contains validator
- `tests/Gluey.Contract.Json.Tests/UniqueItemsValidatorTests.cs` - 10 tests for uniqueItems validator

## Decisions Made
- FNV-1a hash with stackalloc for <= 128 items provides zero-allocation duplicate detection for typical arrays
- Numeric equivalence check always runs for number pairs regardless of hash match -- different representations (1 vs 1.0, 1e2 vs 100) have different hashes but are semantically equal
- patternProperties regex compiled at load time consistent with Phase 6 CompiledPattern behavior

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Numeric equivalence skipped by hash pre-filter**
- **Found during:** Task 2 (uniqueItems implementation)
- **Issue:** Hash pre-filter caused numeric equivalence to be skipped when hashes differed (1 vs 1.0 have different bytes/hashes)
- **Fix:** Moved numeric equivalence check outside hash equality guard so it always runs for number pairs
- **Files modified:** src/Gluey.Contract.Json/ArrayValidator.cs
- **Verification:** Tests for 1==1.0 and 1e2==100 pass
- **Committed in:** 0cb6beb (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential fix for correctness. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All advanced validators ready for walker integration in Phase 9
- Format validation (VALD-15) deferred to Phase 8 Plan 2 if applicable

## Self-Check: PASSED

All 8 key files verified present. Both task commits (65d1fe0, 0cb6beb) verified in git log.

---
*Phase: 08-advanced-validation*
*Completed: 2026-03-10*
