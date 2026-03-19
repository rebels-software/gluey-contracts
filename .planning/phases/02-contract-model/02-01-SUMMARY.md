---
phase: 02-contract-model
plan: 01
subsystem: binary-contract
tags: [binary, system-text-json, dto, contract-loading, validation-codes]

# Dependency graph
requires:
  - phase: 01-format-flag
    provides: "Format flag byte and endianness byte in ParsedProperty, InternalsVisibleTo for Binary"
provides:
  - "Gluey.Contract.Binary project (net9.0;net10.0) with solution integration"
  - "DTO classes for binary contract JSON deserialization (ContractDto, FieldDto, BitFieldDto, ValidationDto)"
  - "BinaryContractNode internal model covering all ADR-16 field types"
  - "BinaryContractLoader for JSON-to-node mapping with error reporting"
  - "7 new ValidationErrorCode values for binary contract validation"
  - "ADR-16 binary format contract specification"
affects: [02-contract-model plan 02, 02-contract-model plan 03, 03-scalars]

# Tech tracking
tech-stack:
  added: [System.Text.Json (already in SDK, used for contract deserialization)]
  patterns: [DTO-to-node mapping, polymorphic JsonElement deserialization for struct/bit fields]

key-files:
  created:
    - src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj
    - src/Gluey.Contract.Binary/Dto/ContractDto.cs
    - src/Gluey.Contract.Binary/Dto/FieldDto.cs
    - src/Gluey.Contract.Binary/Dto/BitFieldDto.cs
    - src/Gluey.Contract.Binary/Dto/ValidationDto.cs
    - src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs
    - src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs
    - tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj
    - tests/Gluey.Contract.Binary.Tests/GlobalUsings.cs
    - tests/Gluey.Contract.Binary.Tests/ContractLoadingTests.cs
    - docs/adr/16-binary-format-contract.md
  modified:
    - Gluey.Contract.sln
    - src/Gluey.Contract/Validation/ValidationErrorCode.cs
    - src/Gluey.Contract/Validation/ValidationErrorMessages.cs

key-decisions:
  - "Used JsonElement for FieldDto.Fields to handle polymorphic sub-fields (BitFieldDto for bits, FieldDto for struct)"
  - "Struct sub-fields stored on both ArrayElement.StructFields and parent node StructFields for flexible access"

patterns-established:
  - "DTO-to-node mapping: Deserialize contract JSON into DTOs, then map to internal model"
  - "Polymorphic JsonElement: Store ambiguous JSON as JsonElement, deserialize on demand based on parent type"

requirements-completed: [CNTR-01]

# Metrics
duration: 5min
completed: 2026-03-20
---

# Phase 02 Plan 01: Contract Model Loading Summary

**Binary contract JSON loading pipeline with DTOs, BinaryContractNode model, and BinaryContractLoader mapping all ADR-16 field types (scalars, bits, enum, array, struct, string, padding)**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-19T23:02:39Z
- **Completed:** 2026-03-19T23:08:00Z
- **Tasks:** 1
- **Files modified:** 14

## Accomplishments
- Created Gluey.Contract.Binary project with full solution integration (src + test projects)
- Implemented BinaryContractLoader that deserializes contract JSON via DTOs and maps to BinaryContractNode tree
- Extended ValidationErrorCode with 7 binary-specific codes (InvalidKind, CyclicDependency, MissingRoot, SharedParent, OverlappingBits, MissingSize, InvalidReference)
- 21 tests covering all field types, error cases, validation rules, extensions, and metadata

## Task Commits

Each task was committed atomically:

1. **Task 1: Scaffold projects, DTOs, BinaryContractNode, new error codes, and loading tests** - `3e53a9d` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj` - Binary project targeting net9.0;net10.0
- `src/Gluey.Contract.Binary/Dto/ContractDto.cs` - Top-level contract DTO with JsonPropertyName attributes
- `src/Gluey.Contract.Binary/Dto/FieldDto.cs` - Field DTO with polymorphic Fields (JsonElement)
- `src/Gluey.Contract.Binary/Dto/BitFieldDto.cs` - Bit sub-field DTO (bit, bits, type)
- `src/Gluey.Contract.Binary/Dto/ValidationDto.cs` - Per-field validation DTO (min, max, pattern, etc.)
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` - Internal sealed class with nullable fields per type
- `src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs` - JSON deserialization and DTO-to-node mapping
- `tests/Gluey.Contract.Binary.Tests/ContractLoadingTests.cs` - 21 tests for contract loading
- `src/Gluey.Contract/Validation/ValidationErrorCode.cs` - Added 7 binary-specific error codes
- `src/Gluey.Contract/Validation/ValidationErrorMessages.cs` - Added messages for new codes
- `docs/adr/16-binary-format-contract.md` - ADR-16 binary format contract specification

## Decisions Made
- Used JsonElement for FieldDto.Fields instead of typed Dictionary to handle polymorphic sub-fields (BitFieldDto for bits containers vs FieldDto for struct elements)
- Struct sub-fields stored on both ArrayElementInfo.StructFields and parent BinaryContractNode.StructFields for access flexibility

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Changed FieldDto.Fields from Dictionary<string, BitFieldDto> to JsonElement**
- **Found during:** Task 1 (DTO design)
- **Issue:** FieldDto.Fields is polymorphic: for bits containers it holds BitFieldDto, for struct elements it holds full FieldDto. A single typed Dictionary cannot handle both.
- **Fix:** Changed Fields to JsonElement? and added manual deserialization in BinaryContractLoader (DeserializeBitFields and DeserializeStructFields)
- **Files modified:** src/Gluey.Contract.Binary/Dto/FieldDto.cs, src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs
- **Verification:** All 21 tests pass including struct sub-field mapping
- **Committed in:** 3e53a9d

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Essential for correct struct field deserialization. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- BinaryContractNode tree is loaded and ready for chain resolution (Plan 02)
- All ADR-16 field types representable in the node model
- Validation error codes ready for use in contract validation (Plan 02)

## Self-Check: PASSED

All 12 created/modified files verified present. Commit 3e53a9d verified in git log.

---
*Phase: 02-contract-model*
*Completed: 2026-03-20*
