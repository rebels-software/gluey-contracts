---
phase: 03-schema-references
plan: 02
subsystem: api
tags: [schema-ref, json-pointer, anchor, cycle-detection, json-schema]

# Dependency graph
requires:
  - phase: 03-schema-references plan 01
    provides: "SchemaRegistry, SchemaNode.ResolvedRef, ref error codes"
  - phase: 02-schema-model
    provides: "SchemaNode tree model, JsonSchemaLoader, SchemaIndexer"
provides:
  - "SchemaRefResolver: two-pass resolver (anchors then refs) with cycle detection"
  - "JsonContractSchema TryLoad/Load with optional SchemaRegistry parameter"
  - "JSON Pointer navigation for $ref resolution within schema trees"
  - "$anchor resolution via anchor lookup table"
  - "Cross-schema $ref resolution via SchemaRegistry"
affects: [04-tokenizer, 05-validation]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Two-pass tree resolution (collect then resolve)", "Per-chain cycle detection via HashSet", "Container keyword two-step JSON Pointer navigation"]

key-files:
  created:
    - src/Gluey.Contract.Json/SchemaRefResolver.cs
    - tests/Gluey.Contract.Json.Tests/SchemaRefResolutionTests.cs
  modified:
    - src/Gluey.Contract.Json/JsonContractSchema.cs
    - src/Gluey.Contract/ValidationErrorMessages.cs

key-decisions:
  - "Two-pass algorithm: collect anchors first, then resolve refs in second pass"
  - "Per-chain cycle detection using HashSet of paths (not global visited set)"
  - "Container keywords ($defs, properties, etc.) use two-step JSON Pointer lookup"

patterns-established:
  - "SchemaRefResolver: generic WalkChildren helper with Func visitor pattern"
  - "JSON Pointer navigation with RFC 6901 unescaping (~0/~1)"

requirements-completed: [SCHM-03, SCHM-04, SCHM-06]

# Metrics
duration: 4min
completed: 2026-03-09
---

# Phase 3 Plan 2: Schema Ref Resolution Summary

**Two-pass SchemaRefResolver with JSON Pointer navigation, $anchor lookup, cycle detection, and cross-schema $ref via SchemaRegistry**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-09T15:23:17Z
- **Completed:** 2026-03-09T15:28:01Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- SchemaRefResolver with two-pass algorithm: anchor collection then ref resolution
- Full JSON Pointer navigation with RFC 6901 unescaping and container keyword handling
- Per-chain cycle detection for direct, mutual, and transitive cycles
- Cross-schema $ref resolution via SchemaRegistry
- 13 new ref resolution tests, 151 total tests passing (72 Json + 79 Core)

## Task Commits

Each task was committed atomically:

1. **Task 1: SchemaRefResolver two-pass algorithm** - `18a29be` (feat)
2. **Task 2: Integrate resolver + tests + fix messages** - `27dc968` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Json/SchemaRefResolver.cs` - Two-pass ref resolver with JSON Pointer nav, anchor lookup, cycle detection
- `src/Gluey.Contract.Json/JsonContractSchema.cs` - Optional SchemaRegistry param on TryLoad/Load, resolver integration, Root property
- `tests/Gluey.Contract.Json.Tests/SchemaRefResolutionTests.cs` - 13 tests covering all ref resolution behaviors
- `src/Gluey.Contract/ValidationErrorMessages.cs` - Added messages for ref-related error codes

## Decisions Made
- Two-pass algorithm: collect all $anchor declarations in pass 1, resolve all $ref in pass 2
- Per-chain cycle detection (HashSet per resolution chain, not global visited) to allow non-cyclic diamond references
- Container keywords ($defs, properties, patternProperties, dependentSchemas) use two-step JSON Pointer navigation: keyword segment selects dictionary, next segment selects entry
- Generic WalkChildren helper with Func visitor pattern for type-safe state passing

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added missing ValidationErrorMessages for ref error codes**
- **Found during:** Task 2 (full test suite regression check)
- **Issue:** Plan 01 added RefCycle, RefUnresolved, AnchorUnresolved, AnchorDuplicate to ValidationErrorCode enum but did not add corresponding messages to ValidationErrorMessages, causing pre-existing test failure
- **Fix:** Added 4 message strings for the ref-related error codes
- **Files modified:** src/Gluey.Contract/ValidationErrorMessages.cs
- **Verification:** ValidationErrorTests.ValidationErrorMessages_EveryCodeExceptNone_HasNonEmptyMessage passes
- **Committed in:** 27dc968 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary fix for pre-existing issue from Plan 01. No scope creep.

## Issues Encountered
- MSBuild cache lock errors (MSB3492) with `-q` quiet mode on .NET 10 SDK -- resolved by using normal verbosity for builds

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All $ref/$defs/$anchor resolution complete
- Phase 3 (Schema References) is fully complete
- Ready for Phase 4 (Tokenizer)
- No blockers

## Self-Check: PASSED

- FOUND: src/Gluey.Contract.Json/SchemaRefResolver.cs
- FOUND: tests/Gluey.Contract.Json.Tests/SchemaRefResolutionTests.cs
- FOUND: src/Gluey.Contract.Json/JsonContractSchema.cs
- FOUND: src/Gluey.Contract/ValidationErrorMessages.cs
- FOUND: commit 18a29be (Task 1)
- FOUND: commit 27dc968 (Task 2)

---
*Phase: 03-schema-references*
*Completed: 2026-03-09*
