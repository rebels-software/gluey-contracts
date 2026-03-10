---
phase: 09-single-pass-walker
plan: 01
subsystem: validation
tags: [schema-walker, ref-struct, single-pass, json-validation, offset-table]

# Dependency graph
requires:
  - phase: 05-keyword-validators
    provides: "KeywordValidator type/enum/const/required/additionalProperties dispatch"
  - phase: 06-advanced-validators
    provides: "Numeric/String/Array/Object validators"
  - phase: 07-composition-validators
    provides: "Composition/Conditional/Dependency validators"
  - phase: 08-advanced-validation
    provides: "UniqueItems/PatternProperties/FormatValidator"
provides:
  - "SchemaWalker ref struct with single-pass Walk/WalkValue/WalkObject/WalkArray"
  - "Working TryParse/Parse on JsonContractSchema (replaces stubs)"
  - "OffsetTable population for byte[] overloads"
  - "InvalidJson error code for structural errors"
affects: [10-quality-benchmarks]

# Tech tracking
tech-stack:
  added: []
  patterns: ["ref struct walker with ReadOnlySpan for zero-alloc validation", "WalkResult struct to escape ref struct scope", "raw JSON bytes vs content-inside-quotes distinction for enum/const"]

key-files:
  created:
    - src/Gluey.Contract.Json/SchemaWalker.cs
    - tests/Gluey.Contract.Json.Tests/SchemaWalkerTests.cs
  modified:
    - src/Gluey.Contract/ValidationErrorCode.cs
    - src/Gluey.Contract/ValidationErrorMessages.cs
    - src/Gluey.Contract.Json/JsonContractSchema.cs
    - tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs

key-decisions:
  - "Enum/const comparison uses raw JSON bytes (including quotes for strings) to match schema-stored values"
  - "OffsetTable ordinals keyed by RFC 6901 path (from SchemaIndexer), accessed via result[\"/name\"]"
  - "WalkResult is a non-ref struct to allow escaping the SchemaWalker ref struct scope"
  - "Walker stores ReadOnlySpan<byte> field for value byte access regardless of byte[]/span input"

patterns-established:
  - "Walker reads token first, then WalkValue processes current token -- callers advance reader"
  - "Composition/conditional validation at scalar level uses captured token data without re-reading"
  - "Object/array level composition validates captured structural state (seenProperties, counts)"

requirements-completed: [INTG-01]

# Metrics
duration: 11min
completed: 2026-03-10
---

# Phase 9 Plan 01: Single-Pass Walker Summary

**SchemaWalker ref struct orchestrating all 17 validator categories in one forward pass through JSON bytes, with OffsetTable population for property access**

## Performance

- **Duration:** 11 min
- **Started:** 2026-03-10T15:58:15Z
- **Completed:** 2026-03-10T16:09:33Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- SchemaWalker validates all JSON Schema keywords in a single forward pass with no token re-reading
- TryParse/Parse fully implemented: TryParse returns true for valid JSON+schema, Parse returns null only for malformed JSON
- OffsetTable populated for byte[] overloads enabling result["/name"] property access
- 26 new integration tests covering all validator dispatch categories

## Task Commits

Each task was committed atomically:

1. **Task 1: Add InvalidJson error code + SchemaWalker core implementation** - `34bca14` (feat)
2. **Task 2: Wire TryParse/Parse to SchemaWalker + cleanup** - `876b7cb` (chore)

## Files Created/Modified
- `src/Gluey.Contract.Json/SchemaWalker.cs` - Internal ref struct walker with Walk/WalkValue/WalkObject/WalkArray + WalkResult
- `src/Gluey.Contract/ValidationErrorCode.cs` - Added InvalidJson error code before TooManyErrors sentinel
- `src/Gluey.Contract/ValidationErrorMessages.cs` - Added "JSON is structurally invalid." message for InvalidJson
- `src/Gluey.Contract.Json/JsonContractSchema.cs` - Replaced stub TryParse/Parse with SchemaWalker.Walk delegation, added byte[] overloads
- `tests/Gluey.Contract.Json.Tests/SchemaWalkerTests.cs` - 26 integration tests for walker + TryParse/Parse
- `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` - Updated from stub expectations to real behavior

## Decisions Made
- Enum/const stored as raw JSON bytes (strings with quotes) so walker must compare raw JSON representation, not content-inside-quotes
- OffsetTable ordinals are keyed by RFC 6901 path in the SchemaIndexer, so property access uses `result["/name"]` not `result["name"]`
- WalkResult is a plain struct (not ref struct) to allow returning from the ref struct walker
- Walker stores `ReadOnlySpan<byte> _span` alongside nullable `byte[]? _data` so both overloads can access value bytes for validation

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated existing API tests for non-stub behavior**
- **Found during:** Task 1
- **Issue:** JsonContractSchemaApiTests expected stub TryParse=false/Parse=null, but real implementation returns true/non-null for valid input
- **Fix:** Updated 4 test assertions to match real implementation behavior
- **Files modified:** tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs
- **Committed in:** 34bca14

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary correction -- old tests asserted stub behavior that no longer applies.

## Issues Encountered
- Raw string literals with 4-quote delimiters (`""""hello""""`) produce content without quotes, not JSON strings. Fixed by using escaped strings (`"\"hello\""`) for JSON string test data.
- Enum/const bytes stored with JSON quoting (via Utf8JsonWriter.WriteStringValue) required GetRawJsonBytes helper to reconstruct full JSON representation from content-inside-quotes offsets.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Single-pass walker complete, ready for Phase 10 quality/benchmarks
- All 398 tests passing (319 Json + 79 Contract)
- No stubs remain in the codebase

---
*Phase: 09-single-pass-walker*
*Completed: 2026-03-10*

## Self-Check: PASSED
- All key files exist (SchemaWalker.cs 1037 lines, SchemaWalkerTests.cs 386 lines)
- Both commits verified (34bca14, 876b7cb)
- InvalidJson error code present in ValidationErrorCode.cs
- SchemaWalker.Walk called 4 times in JsonContractSchema.cs
- All 398 tests passing
