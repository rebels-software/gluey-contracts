---
phase: 03-schema-references
plan: 01
subsystem: api
tags: [schema-registry, schema-ref, validation-errors, json-schema]

# Dependency graph
requires:
  - phase: 02-schema-model
    provides: "SchemaNode tree model, ValidationErrorCode enum"
provides:
  - "SchemaRegistry: URI-keyed store for cross-schema $ref resolution"
  - "SchemaNode.ResolvedRef: internal settable link to resolved $ref target"
  - "ValidationErrorCode ref-related values: RefCycle, RefUnresolved, AnchorUnresolved, AnchorDuplicate"
affects: [03-schema-references plan 02, schema-ref-resolver]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Public class with internal mutation methods (SchemaNode is internal)", "URI normalization via trailing slash trim"]

key-files:
  created:
    - src/Gluey.Contract/SchemaRegistry.cs
    - tests/Gluey.Contract.Tests/SchemaRegistryTests.cs
  modified:
    - src/Gluey.Contract/SchemaNode.cs
    - src/Gluey.Contract/ValidationErrorCode.cs

key-decisions:
  - "SchemaRegistry.Add overwrites on duplicate URI (no exception)"
  - "URI normalization: trim trailing slashes only, ordinal comparison"
  - "ResolvedRef uses plain set accessor (property is already internal)"

patterns-established:
  - "SchemaRegistry: public class with internal methods for friend-assembly-only mutation"

requirements-completed: [SCHM-06]

# Metrics
duration: 2min
completed: 2026-03-09
---

# Phase 3 Plan 1: Schema Reference Foundation Types Summary

**SchemaRegistry with URI-keyed Add/TryGet API, SchemaNode.ResolvedRef property, and 4 ref-related ValidationErrorCode values**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-09T15:18:03Z
- **Completed:** 2026-03-09T15:20:28Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- SchemaRegistry class with public Count, internal Add/TryGet, URI normalization
- SchemaNode.ResolvedRef settable property for post-load reference linking
- Four new ValidationErrorCode values for ref resolution failures
- 7 new tests, 79 total tests passing

## Task Commits

Each task was committed atomically:

1. **Task 1: SchemaRegistry class + SchemaRegistryTests** - `ff08af6` (test: RED) + `3dfc9f9` (feat: GREEN)
2. **Task 2: SchemaNode.ResolvedRef + ValidationErrorCode additions** - `c2e0640` (feat)

_Note: Task 1 used TDD with RED/GREEN commits_

## Files Created/Modified
- `src/Gluey.Contract/SchemaRegistry.cs` - Public sealed class, URI-keyed registry for schema root nodes
- `tests/Gluey.Contract.Tests/SchemaRegistryTests.cs` - 7 tests covering Add/TryGet, null args, count, URI normalization
- `src/Gluey.Contract/SchemaNode.cs` - Added ResolvedRef internal settable property
- `src/Gluey.Contract/ValidationErrorCode.cs` - Added RefCycle, RefUnresolved, AnchorUnresolved, AnchorDuplicate

## Decisions Made
- SchemaRegistry.Add overwrites on duplicate URI rather than throwing (simpler for reload scenarios)
- URI normalization limited to trailing slash trimming with ordinal comparison (minimal, correct)
- ResolvedRef uses `{ get; set; }` since the property itself is `internal` (no redundant accessor modifier)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed ResolvedRef accessor modifier**
- **Found during:** Task 2
- **Issue:** Plan specified `internal set` but property is already `internal`, causing CS0273 compiler error
- **Fix:** Changed to plain `set` accessor
- **Files modified:** src/Gluey.Contract/SchemaNode.cs
- **Verification:** Build succeeds cleanly
- **Committed in:** c2e0640 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Trivial C# accessor syntax correction. No scope change.

## Issues Encountered
- Intermittent MSBuild cache file lock errors (MSB3492) resolved by clean rebuild

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- SchemaRegistry, ResolvedRef, and error codes ready for Plan 02 (SchemaRefResolver)
- No blockers

## Self-Check: PASSED

- FOUND: src/Gluey.Contract/SchemaRegistry.cs
- FOUND: tests/Gluey.Contract.Tests/SchemaRegistryTests.cs
- FOUND: commit ff08af6 (test RED)
- FOUND: commit 3dfc9f9 (feat GREEN)
- FOUND: commit c2e0640 (feat task 2)

---
*Phase: 03-schema-references*
*Completed: 2026-03-09*
