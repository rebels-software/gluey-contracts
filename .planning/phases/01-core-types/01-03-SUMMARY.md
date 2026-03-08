---
phase: 01-core-types
plan: 03
subsystem: api
tags: [readonly-struct, idisposable, dual-indexer, tryparse, parse-result, zero-allocation]

# Dependency graph
requires:
  - phase: 01-core-types-plan-01
    provides: "ParsedProperty readonly struct with value materialization"
  - phase: 01-core-types-plan-02
    provides: "OffsetTable ArrayPool-backed storage, ErrorCollector with sentinel overflow"
provides:
  - "ParseResult composite readonly struct with dual indexers (ordinal + string), IsValid, Errors, foreach, IDisposable"
  - "JsonContractSchema dual API surface: TryParse(ReadOnlySpan<byte>, out ParseResult) and Parse(ReadOnlySpan<byte>)"
affects: [03-schema-model, 04-walker, 05-validation, 06-validation, 07-validation, 08-validation, 09-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [composite-readonly-struct-wrapping-containers, dual-api-surface-tryparse-parse, struct-enumerator-with-hasvalue-skip]

key-files:
  created:
    - src/Gluey.Contract/ParseResult.cs
    - tests/Gluey.Contract.Tests/ParseResultTests.cs
    - tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs
  modified:
    - src/Gluey.Contract.Json/JsonContractSchema.cs

key-decisions:
  - "ParseResult uses Dictionary<string, int> for name-to-ordinal mapping (passed from schema at construction)"
  - "ParseResult.Enumerator skips empty slots (HasValue == false) during foreach enumeration"
  - "JsonContractSchema TryParse/Parse are stub implementations returning false/null until Phase 9"

patterns-established:
  - "Composite readonly struct wrapping multiple IDisposable containers with cascading Dispose"
  - "Dual API surface: TryParse (bool + out) and Parse (nullable return, never throws)"
  - "Struct enumerator that filters entries by HasValue during iteration"

requirements-completed: [CORE-06, CORE-07]

# Metrics
duration: 3min
completed: 2026-03-08
---

# Phase 1 Plan 3: ParseResult and Dual API Summary

**ParseResult composite readonly struct with dual indexers (ordinal + string) wrapping OffsetTable/ErrorCollector, plus TryParse/Parse stub API on JsonContractSchema**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-08T22:22:29Z
- **Completed:** 2026-03-08T22:25:47Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- ParseResult readonly struct wrapping OffsetTable + ErrorCollector with Dictionary-based name-to-ordinal mapping
- Dual indexers: result[0] (ordinal) and result["name"] (string) both resolving through OffsetTable
- Missing/out-of-range access returns ParsedProperty.Empty (no exceptions, uniform API)
- Struct enumerator for foreach that skips empty slots, cascading IDisposable, safe default state handling
- JsonContractSchema dual API: TryParse (bool + out ParseResult) and Parse (ParseResult?) with stub implementations
- 20 new tests (15 ParseResult + 5 API surface), 77 total across solution

## Task Commits

Each task was committed atomically:

1. **Task 1: ParseResult tests (RED)** - `182f6e0` (test)
2. **Task 1: ParseResult implementation (GREEN)** - `a3acc7a` (feat)
3. **Task 2: JsonContractSchema API tests (RED)** - `216efe9` (test)
4. **Task 2: JsonContractSchema API implementation (GREEN)** - `b686479` (feat)

_Note: TDD tasks have separate test and implementation commits._

## Files Created/Modified
- `src/Gluey.Contract/ParseResult.cs` - Composite result struct with dual indexers, error access, IDisposable, and enumerator (128 lines)
- `src/Gluey.Contract.Json/JsonContractSchema.cs` - Dual API surface with TryParse and Parse method signatures (57 lines)
- `tests/Gluey.Contract.Tests/ParseResultTests.cs` - 15 unit tests for ParseResult (198 lines)
- `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` - 5 compilation/contract tests for API surface (73 lines)

## Decisions Made
- ParseResult receives Dictionary<string, int> at construction for name-to-ordinal lookup. The mapping belongs to the schema but ParseResult needs it for the string indexer. This avoids coupling ParseResult to schema internals while enabling ergonomic access.
- ParseResult.Enumerator skips empty (unset) slots by checking HasValue during MoveNext(), so foreach only yields properties that were actually parsed.
- TryParse/Parse are stub implementations (false/null) -- Phase 9 will provide the single-pass walker logic.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- MSBuild `-q` flag triggers AssemblyInfoInputs.cache corruption on .NET SDK 10. Workaround: build without `-q` then run tests with `--no-build`. Known issue from Plans 01 and 02.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All Phase 1 core types complete: ParsedProperty, ValidationError, ValidationErrorCode, OffsetTable, ErrorCollector, ParseResult
- JsonContractSchema has its public API surface defined (TryParse/Parse)
- CORE-01 through CORE-07 all addressed across Plans 01-03
- Ready for Phase 2 (Schema Model) and beyond
- No blockers

## Self-Check: PASSED

- All 4 source/test files exist
- All 4 task commits verified (182f6e0, a3acc7a, 216efe9, b686479)
- Line counts: ParseResult.cs=128 (min 80), JsonContractSchema.cs=57 (min 20), tests exceed minimums
- 20 new tests pass (15 ParseResult + 5 API surface), 77 total across solution

---
*Phase: 01-core-types*
*Completed: 2026-03-08*
