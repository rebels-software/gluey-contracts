---
phase: 01-core-types
plan: 02
subsystem: api
tags: [readonly-struct, arraypool, zero-allocation, idisposable, sentinel-overflow]

# Dependency graph
requires:
  - phase: 01-core-types-plan-01
    provides: "ParsedProperty readonly struct, ValidationError readonly struct, ValidationErrorCode enum, ValidationErrorMessages lookup"
provides:
  - "OffsetTable ArrayPool-backed ordinal-to-ParsedProperty mapping with IDisposable"
  - "ErrorCollector pre-allocated error buffer with sentinel overflow and IDisposable"
affects: [03-schema-model, 04-walker, 05-validation, 06-validation, 07-validation, 08-validation, 09-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [arraypool-rental-with-dispose, int-array-count-holder-for-readonly-struct, struct-enumerator-for-foreach]

key-files:
  created:
    - src/Gluey.Contract/OffsetTable.cs
    - src/Gluey.Contract/ErrorCollector.cs
    - tests/Gluey.Contract.Tests/OffsetTableTests.cs
    - tests/Gluey.Contract.Tests/ErrorCollectorTests.cs
  modified: []

key-decisions:
  - "OffsetTable.Count represents capacity (schema-determined), not number of set entries"
  - "ErrorCollector uses int[1] count holder for mutable count in readonly struct (one small allocation at construction, outside per-parse path)"
  - "ErrorCollector parameterless constructor is public (C# requirement for struct parameterless constructors), capacity constructor remains internal"

patterns-established:
  - "ArrayPool rental with null-checked Dispose and clearArray:true for zero-allocation container types"
  - "int[1] count holder pattern for mutable state in readonly structs"
  - "Custom struct Enumerator with GetEnumerator() for allocation-free foreach"

requirements-completed: [CORE-03, CORE-05]

# Metrics
duration: 5min
completed: 2026-03-08
---

# Phase 1 Plan 2: Container Types Summary

**OffsetTable and ErrorCollector ArrayPool-backed readonly structs with ordinal indexing and sentinel overflow for zero-allocation parse/validation containers**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-08T22:14:54Z
- **Completed:** 2026-03-08T22:19:45Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- OffsetTable readonly struct with ArrayPool<ParsedProperty> backing, ordinal-based get/set, safe out-of-range access returning Empty, and null-checked Dispose
- ErrorCollector readonly struct with ArrayPool<ValidationError> backing, Add() with sentinel overflow at capacity, struct enumerator for foreach, and null-checked Dispose
- Both types handle default/uninitialized state gracefully (no NullReferenceException)
- 24 unit tests covering construction, indexing, overflow, disposal, default state, and enumeration

## Task Commits

Each task was committed atomically:

1. **Task 1: OffsetTable tests (RED)** - `c25cf5b` (test)
2. **Task 1: OffsetTable implementation (GREEN)** - `ba68e92` (feat)
3. **Task 2: ErrorCollector tests (RED)** - `1669ba4` (test)
4. **Task 2: ErrorCollector implementation (GREEN)** - `441e636` (feat)

_Note: TDD tasks have separate test and implementation commits._

## Files Created/Modified
- `src/Gluey.Contract/OffsetTable.cs` - ArrayPool-backed ordinal-to-ParsedProperty mapping with IDisposable
- `src/Gluey.Contract/ErrorCollector.cs` - Pre-allocated error buffer with sentinel overflow, struct enumerator, IDisposable
- `tests/Gluey.Contract.Tests/OffsetTableTests.cs` - 9 unit tests for OffsetTable
- `tests/Gluey.Contract.Tests/ErrorCollectorTests.cs` - 15 unit tests for ErrorCollector

## Decisions Made
- OffsetTable.Count represents capacity (schema-determined slots), not a count of populated entries. The single-pass walker fills entries by ordinal; consumers check HasValue on individual ParsedProperty entries.
- ErrorCollector uses int[1] array as a count holder to achieve mutable count within a readonly struct. The single allocation at construction time is acceptable since ErrorCollector is created once per parse call, outside the zero-allocation hot path.
- ErrorCollector parameterless constructor made public because C# requires struct parameterless constructors to be public. The capacity-accepting constructor remains internal.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed parameterless constructor visibility for ErrorCollector**
- **Found during:** Task 2 (ErrorCollector GREEN phase)
- **Issue:** Plan specified `internal` constructor, but C# CS8958 requires parameterless struct constructors to be `public`
- **Fix:** Made parameterless constructor public, added explicit delegation to `this(DefaultCapacity)`, kept capacity constructor internal
- **Files modified:** src/Gluey.Contract/ErrorCollector.cs
- **Verification:** All 15 ErrorCollector tests pass
- **Committed in:** 441e636 (Task 2 commit)

**2. [Rule 1 - Bug] Fixed default parameter constructor vs parameterless constructor ambiguity**
- **Found during:** Task 2 (ErrorCollector GREEN phase)
- **Issue:** `new ErrorCollector()` called implicit struct zeroing instead of `ErrorCollector(int capacity = DefaultCapacity)`, causing Add() to silently no-op on null arrays
- **Fix:** Split into explicit parameterless constructor and capacity constructor (no default parameter)
- **Files modified:** src/Gluey.Contract/ErrorCollector.cs
- **Verification:** All 15 ErrorCollector tests pass
- **Committed in:** 441e636 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bug fixes in constructor design)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
- MSBuild AssemblyInfoInputs.cache corruption continues from Plan 01. Requires `rm -rf obj/bin` before builds succeed after file changes. Known .NET SDK 10 issue.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- OffsetTable and ErrorCollector container types are locked down and tested
- All Phase 1 leaf types and container types complete
- Ready for Phase 2 (Schema Model) or Phase 3+ which consume these types
- No blockers

## Self-Check: PASSED

- All 4 source/test files exist
- All 4 task commits verified (c25cf5b, ba68e92, 1669ba4, 441e636)
- Line counts: OffsetTable.cs=84 (min 40), ErrorCollector.cs=149 (min 50), tests exceed minimums
- 24 tests pass (9 OffsetTable + 15 ErrorCollector)

---
*Phase: 01-core-types*
*Completed: 2026-03-08*
