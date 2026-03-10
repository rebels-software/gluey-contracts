---
phase: 09-single-pass-walker
plan: 02
subsystem: validation
tags: [array-buffer, parsed-property, hierarchical-access, array-indexing, offset-table]

# Dependency graph
requires:
  - phase: 09-single-pass-walker
    provides: "SchemaWalker single-pass validation + OffsetTable population"
provides:
  - "ArrayBuffer class for array element storage by (ordinal, index)"
  - "ParsedProperty string/int indexers for hierarchical chained access"
  - "ParseResult ArrayBuffer disposal cascade"
  - "Walker child-resolution wiring for objects and arrays"
affects: [10-quality-benchmarks]

# Tech tracking
tech-stack:
  added: []
  patterns: ["direct children dictionary for array element object children", "capture mechanism to avoid OffsetTable collision across array elements"]

key-files:
  created:
    - src/Gluey.Contract/ArrayBuffer.cs
    - tests/Gluey.Contract.Json.Tests/NestedPropertyAccessTests.cs
    - tests/Gluey.Contract.Json.Tests/ArrayElementAccessTests.cs
  modified:
    - src/Gluey.Contract/ParsedProperty.cs
    - src/Gluey.Contract/ParseResult.cs
    - src/Gluey.Contract.Json/SchemaWalker.cs
    - src/Gluey.Contract.Json/JsonContractSchema.cs

key-decisions:
  - "ArrayBuffer is a class (not struct) to avoid copy semantics when shared across ParsedProperty instances"
  - "Array element object children use direct Dictionary<string, ParsedProperty> instead of OffsetTable to avoid ordinal collision across elements"
  - "Walker uses _capturedChildren field to snapshot child properties during WalkObject for array elements"

patterns-established:
  - "Capture mechanism: walker sets _capturedChildren before walking array element objects, WalkObject populates it, WalkArray consumes it"
  - "Direct children resolution: ParsedProperty checks _directChildren before _childOrdinals for string indexer"

requirements-completed: [INTG-02, INTG-03]

# Metrics
duration: 10min
completed: 2026-03-10
---

# Phase 9 Plan 02: Hierarchical Property Access Summary

**ArrayBuffer + ParsedProperty string/int indexers enabling result["address"]["street"] and result["tags"][0] chained access patterns**

## Performance

- **Duration:** 10 min
- **Started:** 2026-03-10T16:13:57Z
- **Completed:** 2026-03-10T16:24:31Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- ArrayBuffer (ArrayPool-backed class) stores array elements by (arrayOrdinal, elementIndex) with region tracking
- ParsedProperty extended with string indexer (child properties via OffsetTable) and int indexer (array elements via ArrayBuffer)
- Direct children mechanism for array element objects (avoids OffsetTable ordinal collision across elements)
- SchemaWalker wired to populate ArrayBuffer and create ParsedProperty with child-resolution references
- 11 new integration tests covering nested access, array indexing, mixed patterns, and error cases

## Task Commits

Each task was committed atomically:

1. **Task 1: ArrayBuffer + ParsedProperty indexers + ParseResult disposal (RED)** - `24e6f86` (test)
2. **Task 1+2: GREEN - implement hierarchical access + wire into SchemaWalker** - `fdb9efd` (feat)

_Note: TDD GREEN phase combined Task 1 implementation with Task 2 walker wiring since tests required end-to-end integration._

## Files Created/Modified
- `src/Gluey.Contract/ArrayBuffer.cs` - Internal class: ArrayPool-backed array element storage by (ordinal, elementIndex)
- `src/Gluey.Contract/ParsedProperty.cs` - Added string/int indexers, childTable/childOrdinals/directChildren/arrayBuffer fields, new constructors
- `src/Gluey.Contract/ParseResult.cs` - Added ArrayBuffer field, 4-param constructor, disposal cascade
- `src/Gluey.Contract.Json/SchemaWalker.cs` - ArrayBuffer field, WalkResult.ArrayBuffer, child-resolution in WalkObject, ArrayBuffer population in WalkArray, capture mechanism
- `src/Gluey.Contract.Json/JsonContractSchema.cs` - Passes ArrayBuffer from WalkResult to ParseResult constructor
- `tests/Gluey.Contract.Json.Tests/NestedPropertyAccessTests.cs` - 5 tests: nested object access, missing child, non-object string indexer, 3-level deep nesting
- `tests/Gluey.Contract.Json.Tests/ArrayElementAccessTests.cs` - 6 tests: first/second element, out-of-bounds, non-array int indexer, array-of-objects nested access, negative index

## Decisions Made
- **ArrayBuffer as class:** Shared by reference across multiple ParsedProperty instances and ParseResult; struct would cause copy semantics issues
- **Direct children for array elements:** OffsetTable has one slot per schema path (e.g., `/items/name`), but array elements at `/items/0/name` and `/items/1/name` both write to the same slot. Direct Dictionary<string, ParsedProperty> avoids collision.
- **Capture mechanism:** Walker sets `_capturedChildren` dict before walking array element objects. WalkObject populates it as a side-channel. WalkArray consumes it into element ParsedProperty. Clean lifecycle with null/create/consume pattern.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Array element object children use direct dictionary instead of OffsetTable lookup**
- **Found during:** Task 1 GREEN phase
- **Issue:** Schema paths (e.g., `/items/name`) don't include array indices, but walker builds paths with indices (e.g., `/items/0/name`). OffsetTable slots for element children collide across elements.
- **Fix:** Added `_directChildren` field to ParsedProperty and capture mechanism in SchemaWalker to snapshot child properties from WalkObject into per-element dictionaries
- **Files modified:** src/Gluey.Contract/ParsedProperty.cs, src/Gluey.Contract.Json/SchemaWalker.cs
- **Committed in:** fdb9efd

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential correctness fix for array-of-objects access. Plan's approach of using OffsetTable ordinals for element children would have caused cross-element data corruption.

## Issues Encountered
- Array element object children path mismatch: SchemaIndexer assigns ordinals using schema paths without array indices, while walker builds paths with indices. Solved by direct dictionary snapshot approach.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All hierarchical and array access patterns working end-to-end
- All 409 tests passing (330 Json + 79 Contract)
- Phase 9 complete, ready for Phase 10 quality/benchmarks

---
*Phase: 09-single-pass-walker*
*Completed: 2026-03-10*

## Self-Check: PASSED
- All 7 key files exist (ArrayBuffer.cs, ParsedProperty.cs, ParseResult.cs, SchemaWalker.cs, JsonContractSchema.cs, NestedPropertyAccessTests.cs, ArrayElementAccessTests.cs)
- Both commits verified (24e6f86, fdb9efd)
- ArrayBuffer.cs 107 lines (min 50)
- NestedPropertyAccessTests.cs 144 lines (min 50)
- ArrayElementAccessTests.cs 161 lines (min 50)
- All 409 tests passing (330 Json + 79 Contract)
