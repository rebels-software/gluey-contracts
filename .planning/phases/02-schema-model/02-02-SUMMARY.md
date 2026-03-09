---
phase: 02-schema-model
plan: 02
subsystem: schema
tags: [json-schema, utf8jsonreader, recursive-descent, ordinals, rfc-6901]

# Dependency graph
requires:
  - phase: 02-01
    provides: "SchemaNode immutable class and SchemaType flags enum"
  - phase: 01-core-types
    provides: "ParseResult, OffsetTable, ErrorCollector core types"
provides:
  - "JsonSchemaLoader: recursive-descent Utf8JsonReader parser producing SchemaNode tree"
  - "SchemaIndexer: depth-first ordinal assignment for named properties"
  - "TryLoad/Load factory API on JsonContractSchema (byte[] and string overloads)"
  - "PropertyCount for OffsetTable sizing"
affects: [03-ref-resolution, 04-tokenizer, 09-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: [recursive-descent-parser, utf8-keyword-matching, raw-byte-serialization]

key-files:
  created:
    - src/Gluey.Contract.Json/JsonSchemaLoader.cs
    - src/Gluey.Contract.Json/SchemaIndexer.cs
    - tests/Gluey.Contract.Json.Tests/SchemaNodeTests.cs
    - tests/Gluey.Contract.Json.Tests/JsonSchemaLoadingTests.cs
  modified:
    - src/Gluey.Contract.Json/JsonContractSchema.cs
    - tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs

key-decisions:
  - "JsonSchemaLoader uses ValueTextEquals with u8 literals for zero-alloc keyword matching"
  - "enum/const values serialized to byte[] via ArrayBufferWriter + Utf8JsonWriter"
  - "SchemaIndexer assigns ordinals only to named properties (Properties dict children), not array items"
  - "JsonContractSchema constructor is private; only accessible via static TryLoad/Load factory methods"

patterns-established:
  - "Zero-alloc keyword matching: reader.ValueTextEquals(\"keyword\"u8) pattern for all JSON keywords"
  - "TryLoad/Load dual API mirrors TryParse/Parse (bool+out vs nullable return)"
  - "Depth-first ordinal assignment uses SchemaNode.Path as dictionary key"

requirements-completed: [SCHM-01, SCHM-05]

# Metrics
duration: 6min
completed: 2026-03-09
---

# Phase 2 Plan 02: JSON Schema Loader Summary

**Recursive-descent Utf8JsonReader parser loading Draft 2020-12 schemas into SchemaNode tree with depth-first ordinal assignment and TryLoad/Load public API**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-09T11:39:47Z
- **Completed:** 2026-03-09T11:46:13Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- JsonSchemaLoader parses all Draft 2020-12 keywords using zero-alloc Utf8JsonReader with u8 literal matching
- SchemaIndexer assigns stable depth-first ordinals to named properties for OffsetTable sizing
- TryLoad/Load factory API on JsonContractSchema with both byte[] and string overloads
- 131 total tests passing (59 Json + 72 Core), all Phase 1 tests unbroken

## Task Commits

Each task was committed atomically:

1. **Task 1: JsonSchemaLoader (RED)** - `52bed09` (test)
2. **Task 1: JsonSchemaLoader (GREEN)** - `7e64a0d` (feat)
3. **Task 2: SchemaIndexer + TryLoad/Load (RED)** - `9ab11b5` (test)
4. **Task 2: SchemaIndexer + TryLoad/Load (GREEN)** - `a1a61f7` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Json/JsonSchemaLoader.cs` - Recursive-descent parser for all Draft 2020-12 keywords
- `src/Gluey.Contract.Json/SchemaIndexer.cs` - Depth-first ordinal assignment for named properties
- `src/Gluey.Contract.Json/JsonContractSchema.cs` - TryLoad/Load factory API + PropertyCount
- `tests/Gluey.Contract.Json.Tests/SchemaNodeTests.cs` - 36 tests for parser (paths, types, keywords, booleans)
- `tests/Gluey.Contract.Json.Tests/JsonSchemaLoadingTests.cs` - 18 tests for API and ordinals
- `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` - Updated to use factory method

## Decisions Made
- JsonSchemaLoader uses `reader.ValueTextEquals("keyword"u8)` for zero-allocation keyword matching
- enum/const values serialized to raw byte[] via ArrayBufferWriter + Utf8JsonWriter (consistent with 02-01 decision)
- SchemaIndexer only assigns ordinals to named properties from Properties dictionaries, not array items
- JsonContractSchema constructor is private -- only TryLoad/Load factory methods expose construction
- Utf8JsonWriter passed by value (not ref) to WriteComplexValue since it's a reference type

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated Phase 1 API tests to use factory method**
- **Found during:** Task 2 (TryLoad/Load API implementation)
- **Issue:** Existing JsonContractSchemaApiTests used `new JsonContractSchema()` which no longer compiles with private constructor
- **Fix:** Replaced direct construction with `JsonContractSchema.Load(...)` factory method
- **Files modified:** tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs
- **Verification:** All 5 existing API tests still pass with same assertions
- **Committed in:** a1a61f7 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary update to keep Phase 1 tests compiling. No scope creep.

## Issues Encountered
- Build cache issue (MSB3492 on AssemblyInfoInputs.cache) required `dotnet clean` to resolve -- transient filesystem lock, not code-related

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Schema loading pipeline complete: JSON bytes -> SchemaNode tree -> ordinal mapping -> JsonContractSchema
- Ready for Phase 3 ($ref resolution) which walks the SchemaNode tree to resolve references
- Ready for Phase 4 (tokenizer) which uses PropertyCount for OffsetTable sizing
- Ready for Phase 9 (walker) which uses the full schema + ordinals for single-pass validation

## Self-Check: PASSED

All 5 created/modified source files verified on disk. All 4 task commits (52bed09, 7e64a0d, 9ab11b5, a1a61f7) verified in git log.

---
*Phase: 02-schema-model*
*Completed: 2026-03-09*
