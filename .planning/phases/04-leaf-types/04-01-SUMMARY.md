---
phase: 04-leaf-types
plan: 01
subsystem: api
tags: [binary-parsing, string-encoding, enum, trim-modes, readonly-struct]

# Dependency graph
requires:
  - phase: 03-scalar-parsing
    provides: "ParsedProperty with _fieldType, GetFieldType() mapper, Parse() loop"
provides:
  - "_encoding and _enumValues fields on ParsedProperty"
  - "GetString() with ASCII/UTF-8 encoding, 4 trim modes, and enum label lookup"
  - "StringMode on BinaryContractNode"
  - "Mode on FieldDto"
  - "GetFieldType() returns FieldTypes for string/enum/bits/padding"
affects: [04-leaf-types plan 02, 04-leaf-types plan 03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Encoding byte packs both encoding type (bit 0) and trim mode (bits 2-3) for zero-overhead dispatch"
    - "Enum label lookup via lazy Dictionary reference on ParsedProperty"
    - "MapStringMode helper centralizes mode string-to-byte mapping"

key-files:
  created: []
  modified:
    - "docs/adr/16-binary-format-contract.md"
    - "src/Gluey.Contract/Parsing/ParsedProperty.cs"
    - "src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs"
    - "src/Gluey.Contract.Binary/Dto/FieldDto.cs"
    - "src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs"
    - "src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs"
    - "tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs"

key-decisions:
  - "Encoding byte packs encoding type in bit 0 and trim mode in bits 2-3 for single-byte storage"
  - "Enum label lookup deferred to GetString() call via Dictionary reference (D-07)"
  - "String fields now parsed by main loop (no longer skipped as non-scalar)"

patterns-established:
  - "Encoding byte bit-packing: bit 0 = charset (0=UTF-8, 1=ASCII), bits 2-3 = trim mode (0=plain, 1=trimStart, 2=trimEnd, 3=trim)"
  - "MapStringMode: centralized mode string-to-byte conversion with trimEnd default"

requirements-completed: [STRE-01, STRE-02, STRE-03, STRE-04, BITS-01, BITS-02, BITS-03, BITS-04, COMP-04]

# Metrics
duration: 4min
completed: 2026-03-20
---

# Phase 04 Plan 01: Data Model Extensions Summary

**Extended ParsedProperty with encoding/enum fields, added string trim modes to ADR-16, and mapped all leaf types in GetFieldType()**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-20T19:43:47Z
- **Completed:** 2026-03-20T19:47:40Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- ADR-16 updated with string mode field (4 trim modes, trimEnd default) and corrected enum accessor convention
- ParsedProperty extended with _encoding byte and _enumValues dictionary, plus two new constructors
- GetString() now handles ASCII/UTF-8 encoding, 4 trim modes, and enum lazy label lookup with D-08 unmapped fallback
- GetFieldType() returns correct FieldTypes for string, enum, bits, and padding
- BinaryContractNode.StringMode and FieldDto.Mode added with MapStringMode loader helper

## Task Commits

Each task was committed atomically:

1. **Task 1: Update ADR-16 with string mode field and corrected enum accessor convention** - `ad792be` (docs)
2. **Task 2: Extend ParsedProperty, BinaryContractNode, FieldDto, Loader, and GetFieldType** - `36feaa7` (feat)

## Files Created/Modified
- `docs/adr/16-binary-format-contract.md` - Added string modes section, corrected enum accessor convention
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` - Added _encoding, _enumValues fields; new constructors; updated GetString()
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` - Added StringMode property
- `src/Gluey.Contract.Binary/Dto/FieldDto.cs` - Added Mode property with JsonPropertyName
- `src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs` - Added MapStringMode helper, wired StringMode in MapField
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Extended GetFieldType() with string/enum/bits/padding cases
- `tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs` - Updated test: string fields now parsed as leaf types

## Decisions Made
- Encoding byte packs both charset (bit 0: 0=UTF-8, 1=ASCII) and trim mode (bits 2-3) into a single byte for minimal struct growth
- Enum label lookup is deferred to GetString() via Dictionary reference stored on ParsedProperty (D-07)
- String fields are now parsed in the main Parse() loop instead of skipped; existing test updated accordingly

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated test expecting string fields to be skipped**
- **Found during:** Task 2
- **Issue:** Parse_NonScalarFieldSlot_ReturnsEmptyParsedProperty test expected string fields to return empty, but GetFieldType() now returns FieldTypes.String so they are parsed
- **Fix:** Updated test to assert string field HasValue is true and GetString() returns expected "ABCD"
- **Files modified:** tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs
- **Verification:** All 109 binary tests pass on both net9.0 and net10.0
- **Committed in:** 36feaa7 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug fix)
**Impact on plan:** Test update was necessary consequence of extending GetFieldType(). No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Data model fully extended for all leaf types
- Plan 02 (parse loop) can now create correct ParsedProperty entries using the new constructors
- Plan 03 (tests) can verify string encoding, trim modes, enum lookup, bits, and padding

---
*Phase: 04-leaf-types*
*Completed: 2026-03-20*
