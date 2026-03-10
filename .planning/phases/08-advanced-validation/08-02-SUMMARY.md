---
phase: 08-advanced-validation
plan: 02
subsystem: validation
tags: [json-schema, format, schema-options, date-time, email, uuid, uri, ipv4, ipv6, json-pointer]

# Dependency graph
requires:
  - phase: 01-core-types
    provides: ValidationErrorCode.FormatInvalid, ErrorCollector, ValidationError
  - phase: 02-schema-model
    provides: SchemaNode.Format property, JsonContractSchema TryLoad/Load
provides:
  - SchemaOptions public API type with AssertFormat flag
  - FormatValidator with 9 format implementations
  - TryLoad/Load accept optional SchemaOptions parameter
affects: [09-single-pass-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: [opt-in format assertion with documented allocation exception]

key-files:
  created:
    - src/Gluey.Contract/SchemaOptions.cs
    - src/Gluey.Contract.Json/FormatValidator.cs
    - tests/Gluey.Contract.Json.Tests/FormatValidatorTests.cs
    - tests/Gluey.Contract.Json.Tests/SchemaOptionsTests.cs
  modified:
    - src/Gluey.Contract.Json/JsonContractSchema.cs

key-decisions:
  - "Format assertion is opt-in via SchemaOptions.AssertFormat, documented exception to zero-allocation guarantee"
  - "Simplified email validation (structural check only: one @, non-empty local/domain, no spaces)"
  - "RFC 3339 time format requires offset indicator (Z, +, -); bare times rejected"

patterns-established:
  - "SchemaOptions pattern: optional parameter on TryLoad/Load, stored as internal property on schema instance"

requirements-completed: [VALD-15, VALD-16]

# Metrics
duration: 3min
completed: 2026-03-10
---

# Phase 8 Plan 2: Format Validation Summary

**SchemaOptions with AssertFormat flag and FormatValidator covering 9 formats (date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer)**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-10T12:01:21Z
- **Completed:** 2026-03-10T12:04:01Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- SchemaOptions public sealed class with AssertFormat init property (default false)
- FormatValidator with switch dispatcher and 9 private format validators using .NET standard library APIs
- TryLoad/Load accept optional SchemaOptions parameter (fully backward compatible)
- AssertFormat flag stored on JsonContractSchema instance for use by future walker phase
- 41 new tests (31 format + 10 schema options integration), all 372 total tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: SchemaOptions + FormatValidator with 9 format implementations** - `1992fb0` (feat)
2. **Task 2: Wire SchemaOptions into TryLoad/Load** - `db5388c` (feat)

## Files Created/Modified
- `src/Gluey.Contract/SchemaOptions.cs` - Public sealed class with AssertFormat init property
- `src/Gluey.Contract.Json/FormatValidator.cs` - Static format validation dispatcher with 9 private validators
- `src/Gluey.Contract.Json/JsonContractSchema.cs` - Added SchemaOptions parameter to TryLoad/Load, internal AssertFormat property
- `tests/Gluey.Contract.Json.Tests/FormatValidatorTests.cs` - 31 tests for all 9 formats and unknown format passthrough
- `tests/Gluey.Contract.Json.Tests/SchemaOptionsTests.cs` - 10 tests for defaults and TryLoad/Load integration

## Decisions Made
- Format assertion is opt-in via SchemaOptions.AssertFormat; documented as exception to zero-allocation guarantee (string conversions needed for .NET parser APIs)
- Simplified email validation: structural check (one @, non-empty local/domain, no spaces) rather than full RFC 5322
- RFC 3339 time validation requires offset indicator (Z, +HH:MM, -HH:MM); bare times without offset are rejected
- Unknown format strings pass silently (return true) per JSON Schema Draft 2020-12 spec

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- FormatValidator ready for integration in Phase 9 single-pass walker
- Walker needs to check schema.AssertFormat and call FormatValidator.Validate on string values with format keyword
- All 9 format validators tested and working

## Self-Check: PASSED

All 5 files verified present. Both task commits (1992fb0, db5388c) verified in git log.

---
*Phase: 08-advanced-validation*
*Completed: 2026-03-10*
