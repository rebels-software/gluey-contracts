---
phase: 02-contract-model
plan: 03
subsystem: binary-contract
tags: [binary, chain-resolution, endianness, offsets, schema-api, tryload]

# Dependency graph
requires:
  - phase: 02-contract-model plan 01
    provides: "BinaryContractNode model, BinaryContractLoader, ContractMetadata, ArrayElementInfo"
  - phase: 02-contract-model plan 02
    provides: "BinaryContractValidator multi-phase validation pipeline"
provides:
  - "BinaryChainResolver: dependency chain -> ordered field array with absolute byte offsets"
  - "BinaryContractSchema: public TryLoad/Load API wiring loader, validator, and resolver"
  - "Endianness resolution per field (field override > contract default > little fallback)"
  - "Semi-dynamic array handling with IsDynamicOffset propagation"
  - "Struct sub-field relative offset resolution"
affects: [03-scalars, 04-leaf-types, 05-composites]

# Tech tracking
tech-stack:
  added: []
  patterns: [reverse-map chain walking, load-time offset precomputation, dual TryLoad/Load API pattern]

key-files:
  created:
    - src/Gluey.Contract.Binary/Schema/BinaryChainResolver.cs
    - src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs
    - tests/Gluey.Contract.Binary.Tests/ChainResolutionTests.cs
    - tests/Gluey.Contract.Binary.Tests/EndiannessResolutionTests.cs
  modified:
    - tests/Gluey.Contract.Binary.Tests/ContractLoadingTests.cs
    - src/Gluey.Contract.Binary/Schema/BinaryContractValidator.cs

key-decisions:
  - "Reverse map (parent->child) for chain walking -- avoids O(n) child lookups per step"
  - "Semi-dynamic arrays return -1 from ComputeFieldSize, triggering dynamicMode for all subsequent fields"
  - "Struct sub-fields resolved in separate scope with relative offsets (not absolute)"
  - "ErrorCollector disposed explicitly in TryLoad to return ArrayPool buffers"

patterns-established:
  - "Chain resolution via reverse map: build childOf[parentName]=childName, walk from root"
  - "Load-time offset precomputation: all graph work at load time, parser gets flat array"
  - "BinaryContractSchema mirrors JsonContractSchema API exactly: TryLoad/Load with SchemaRegistry? and SchemaOptions?"

requirements-completed: [CNTR-02, CNTR-09, CORE-03]

# Metrics
duration: 6min
completed: 2026-03-20
---

# Phase 02 Plan 03: Chain Resolution and Schema API Summary

**Dependency chain resolver producing ordered field array with precomputed byte offsets and endianness, plus BinaryContractSchema TryLoad/Load public API wiring the full load-validate-resolve pipeline**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-19T23:15:35Z
- **Completed:** 2026-03-19T23:21:05Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- BinaryChainResolver resolves dependency chain into ordered array with absolute byte offsets via parent->child reverse map
- Endianness resolved per field with correct fallback chain (field > contract > little)
- Semi-dynamic arrays mark subsequent fields as IsDynamicOffset=true
- Struct sub-fields get relative offsets within their own scoped chain
- BinaryContractSchema.TryLoad/Load wires full pipeline (load -> validate -> resolve) end-to-end
- ADR-16 battery example loads and resolves with correct offsets for all 10 fields

## Task Commits

Each task was committed atomically (TDD):

1. **Task 1 RED: Failing tests for chain resolution and endianness** - `09dbb59` (test)
2. **Task 1 GREEN: Implement BinaryChainResolver** - `5f55f50` (feat)
3. **Task 2 RED: Failing tests for BinaryContractSchema API** - `b0148b2` (test)
4. **Task 2 GREEN: Implement BinaryContractSchema** - `b447a50` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Schema/BinaryChainResolver.cs` - Dependency chain -> ordered array with offsets and endianness
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Public TryLoad/Load API, metadata properties, pipeline wiring
- `tests/Gluey.Contract.Binary.Tests/ChainResolutionTests.cs` - 10 tests: ordering, offsets, fixed/semi-dynamic arrays, ADR battery
- `tests/Gluey.Contract.Binary.Tests/EndiannessResolutionTests.cs` - 6 tests: contract default, field override, fallback, struct sub-fields
- `tests/Gluey.Contract.Binary.Tests/ContractLoadingTests.cs` - 15 new tests for BinaryContractSchema API (TryLoad/Load, metadata, OrderedFields)
- `src/Gluey.Contract.Binary/Schema/BinaryContractValidator.cs` - Fixed: skip array fields in size validation

## Decisions Made
- Reverse map (parent->child) for chain walking avoids O(n) child lookup per step
- Semi-dynamic arrays return -1 from ComputeFieldSize, triggering dynamicMode flag propagation
- Struct sub-fields resolved in separate scope with relative offsets
- ErrorCollector explicitly disposed in TryLoad to return ArrayPool buffers promptly

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed validator rejecting array fields with Size=0**
- **Found during:** Task 2 (BinaryContractSchema implementation)
- **Issue:** ValidateTypesAndSizes required Size > 0 for all fields, but array fields have Size=0 in the loaded node (their size is computed from count * element.size)
- **Fix:** Added `if (node.Type == "array") continue;` to skip array fields in size validation
- **Files modified:** src/Gluey.Contract.Binary/Schema/BinaryContractValidator.cs
- **Verification:** All 69 binary tests pass, full suite (1486 tests) green
- **Committed in:** b447a50 (Task 2 GREEN commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential fix for correctness -- array fields never declare explicit size in contract JSON.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Complete binary contract model pipeline: load -> validate -> resolve -> schema object
- BinaryContractSchema.OrderedFields provides flat array with precomputed offsets for Phase 3 parser
- BinaryContractSchema.NameToOrdinal provides field lookup for ParsedObject construction
- TotalFixedSize enables buffer pre-allocation for fully-fixed contracts
- Ready for Phase 3 (scalar parsing) to consume OrderedFields

## Self-Check: PASSED

All 6 files verified present. Commits 09dbb59, 5f55f50, b0148b2, b447a50 verified in git log.

---
*Phase: 02-contract-model*
*Completed: 2026-03-20*
