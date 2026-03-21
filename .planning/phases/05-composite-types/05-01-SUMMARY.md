---
phase: 05-composite-types
plan: 01
subsystem: api
tags: [binary-parsing, arrays, composite-types, arraybuffer]

# Dependency graph
requires:
  - phase: 04-leaf-types
    provides: "Parse() with scalar, string, enum, bits, padding handling; NameToOrdinal synthetic entries pattern"
provides:
  - "Parse() handles fixed-count array fields (scalar and struct element types)"
  - "Per-element ParsedProperty creation with ArrayBuffer storage"
  - "Parse-local NameToOrdinal clone with element path entries"
  - "Container ParsedProperty wired to ArrayBuffer for GetEnumerator()"
  - "OffsetTable capacity expansion for array element ordinals"
affects: [05-02-semi-dynamic-arrays, 05-03-composite-tests]

# Tech tracking
tech-stack:
  added: []
  patterns: [parse-time-nametoordinal-clone, arraybuffer-element-expansion, container-parsedproperty]

key-files:
  created: []
  modified:
    - src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs

key-decisions:
  - "NameToOrdinal cloned at parse start to prevent schema mutation across calls"
  - "ArrayBuffer rented from pool for array element storage"
  - "OffsetTable capacity pre-expanded at parse time for fixed array element ordinals"
  - "Struct array elements: sub-fields get individual OffsetTable/NameToOrdinal entries for O(1) path access, plus one ArrayBuffer entry per struct element for enumeration"

patterns-established:
  - "Parse-time NameToOrdinal clone: always clone schema dictionary before adding element paths"
  - "Array element expansion: per-element ParsedProperty with offset = base + (index * elementSize)"
  - "Container ParsedProperty: wired to ArrayBuffer with region index for GetEnumerator() support"

requirements-completed: [COMP-01, COMP-03, COMP-05]

# Metrics
duration: 2min
completed: 2026-03-21
---

# Phase 05 Plan 01: Fixed Array Parsing Summary

**Fixed-count array element expansion in Parse() with NameToOrdinal clone, ArrayBuffer storage, and container enumeration support for both scalar and struct element types**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-21T10:43:12Z
- **Completed:** 2026-03-21T10:44:43Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Parse() handles "array" type nodes with fixed count for both scalar and struct element types
- Per-element ParsedProperties created with correct offsets, registered in ArrayBuffer and OffsetTable
- Parse-local NameToOrdinal clone prevents schema mutation (Pitfall 1), has element path entries for O(1) lookup
- Container ParsedProperty wired to ArrayBuffer supports GetEnumerator() yielding child elements
- Graceful degradation: array element count clamped by available payload bytes (D-05)
- All 131 existing tests pass with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add fixed array element expansion to Parse() with NameToOrdinal clone and ArrayBuffer** - `61b6d52` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Extended Parse() with array handling: NameToOrdinal clone, ArrayBuffer allocation, expanded OffsetTable capacity, scalar/struct element expansion, container ParsedProperty creation

## Decisions Made
- NameToOrdinal cloned at parse start using copy constructor with StringComparer.Ordinal -- prevents mutation of shared schema dictionary
- ArrayBuffer rented from pool (not newly allocated) per existing Rent/Return pattern
- OffsetTable capacity pre-computed by scanning OrderedFields for fixed arrays at parse start
- Struct array elements: each sub-field gets its own OffsetTable slot and NameToOrdinal path entry for O(1) access; one ArrayBuffer entry per struct element (not per sub-field) for enumeration
- Struct element ParsedProperty uses childOrdinals pointing to parseNameToOrdinal for `element["fieldName"]` resolution

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Fixed array parsing complete, ready for Plan 02 (semi-dynamic arrays with two-pass parse)
- ArrayBuffer and NameToOrdinal clone patterns established for reuse in Pass 2

---
*Phase: 05-composite-types*
*Completed: 2026-03-21*
