---
phase: 01-format-flag
plan: 01
subsystem: parsing
tags: [binary-primitives, readonly-struct, format-dispatch, endianness]

# Dependency graph
requires: []
provides:
  - "Format-aware ParsedProperty struct with _format and _endianness byte fields"
  - "Binary dispatch in all six GetXxx() methods (GetString, GetInt32, GetInt64, GetDouble, GetBoolean, GetDecimal)"
  - "Two new internal constructor overloads (6-param leaf, 10-param with children)"
  - "InternalsVisibleTo entries for Gluey.Contract.Binary and Gluey.Contract.Binary.Tests"
affects: [02-contract-model, 03-binary-walker, 04-string-encoding]

# Tech tracking
tech-stack:
  added: [System.Buffers.Binary.BinaryPrimitives]
  patterns: [format-discriminator-branching, default-zero-backward-compatibility, constructor-overload-extension]

key-files:
  created:
    - tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs
  modified:
    - src/Gluey.Contract/Parsing/ParsedProperty.cs
    - src/Gluey.Contract/Gluey.Contract.csproj

key-decisions:
  - "Added _endianness byte alongside _format in Phase 1 to avoid second struct layout change in Phase 3"
  - "Implemented real binary paths for standard sizes (1/2/4/8 byte reads) rather than throwing NotSupportedException stubs"
  - "Binary GetDecimal throws NotSupportedException per ADR-16 (no binary decimal type)"

patterns-established:
  - "Format branching: if (_format == 0) { JSON path } else { binary path } in every GetXxx() method"
  - "Endianness dispatch: if (_endianness == 0) { LE } else { BE } with length-based switch expressions"
  - "Constructor overloading: existing signatures unchanged, new overloads add format+endianness params"

requirements-completed: [CORE-01, CORE-02]

# Metrics
duration: 3min
completed: 2026-03-19
---

# Phase 01 Plan 01: Format Flag Summary

**Format-aware ParsedProperty with binary dispatch in all GetXxx() methods using BinaryPrimitives, zero regression across 1496 existing tests**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-19T21:44:04Z
- **Completed:** 2026-03-19T21:46:55Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- ParsedProperty struct extended with _format and _endianness byte fields while preserving all existing constructor signatures
- All six GetXxx() methods branch between JSON (format=0) and binary (format=1) paths with endianness awareness
- 14 new unit tests covering binary boolean, int32 (LE/BE/2-byte/1-byte), int64 (LE/BE), double (LE/BE), string, decimal (throws), regression, and unsupported-length error cases
- Zero regression: all 109 Gluey.Contract.Tests + 639 Gluey.Contract.Json.Tests pass on both net9.0 and net10.0

## Task Commits

Each task was committed atomically:

1. **Task 1: Write binary format dispatch tests** - `059ef09` (test) - TDD RED phase
2. **Task 2: Add format flag and binary GetXxx() branches** - `c9da6e9` (feat) - TDD GREEN phase

## Files Created/Modified
- `tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs` - 14 unit tests defining binary format dispatch contract
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` - Added _format/_endianness fields, binary constructor overloads, GetXxx() branching
- `src/Gluey.Contract/Gluey.Contract.csproj` - InternalsVisibleTo for Gluey.Contract.Binary and Gluey.Contract.Binary.Tests

## Decisions Made
- Added _endianness byte alongside _format in Phase 1 to avoid a second struct layout change in Phase 3 (costs ~0 bytes due to alignment padding)
- Implemented real binary read paths (BinaryPrimitives) rather than NotSupportedException stubs, since the APIs are stable and trivially testable
- Binary GetDecimal throws NotSupportedException per ADR-16 specification (no binary decimal type exists)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ParsedProperty is ready for Phase 2 (contract model) and Phase 3 (binary walker) to consume the format-aware constructors
- InternalsVisibleTo entries pre-configured for Gluey.Contract.Binary package

---
*Phase: 01-format-flag*
*Completed: 2026-03-19*
