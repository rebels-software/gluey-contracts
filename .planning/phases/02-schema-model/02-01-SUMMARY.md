---
phase: 02-schema-model
plan: 01
subsystem: schema
tags: [json-schema, draft-2020-12, immutable, flags-enum, rfc-6901]

# Dependency graph
requires:
  - phase: 01-core-types
    provides: "Core value types (OffsetTable, ErrorCollector, ParseResult) that SchemaNode tree will feed"
provides:
  - "SchemaType flags enum for JSON Schema type keyword (7 types, byte-sized)"
  - "SchemaNode immutable class with all Draft 2020-12 keyword fields (~45 properties)"
  - "Boolean schema sentinels (SchemaNode.True / SchemaNode.False)"
  - "RFC 6901 path building helper (BuildChildPath)"
affects: [02-02-json-loader, 03-ref-resolution, 05-validation, 09-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Immutable tree node with constructor-only initialization", "Flags enum for O(1) bitwise type matching"]

key-files:
  created:
    - src/Gluey.Contract/SchemaType.cs
    - src/Gluey.Contract/SchemaNode.cs
  modified:
    - src/Gluey.Contract/Gluey.Contract.csproj

key-decisions:
  - "enum/const stored as raw UTF-8 byte[] to avoid JsonDocument lifetime management"
  - "SchemaNode is a class (not struct) because tree nodes reference children; allocated once at load time"
  - "C# @-prefixed parameter names for reserved keywords (ref, enum, const, if, else)"

patterns-established:
  - "Immutable internal sealed class pattern: all properties get-only, single constructor with named optional params"
  - "Static sentinel pattern: SchemaNode.True and SchemaNode.False for boolean schemas"

requirements-completed: [SCHM-02]

# Metrics
duration: 2min
completed: 2026-03-09
---

# Phase 2 Plan 01: Schema Node Model Summary

**SchemaType flags enum and SchemaNode immutable tree class with all Draft 2020-12 keyword fields, RFC 6901 path building, and boolean schema sentinels**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-09T11:35:27Z
- **Completed:** 2026-03-09T11:37:01Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- SchemaType [Flags] byte enum covering all 7 JSON Schema types with bitwise OR support
- SchemaNode internal sealed class with ~45 get-only properties covering all Draft 2020-12 keywords
- Static True/False sentinels for boolean schema representation
- BuildChildPath helper implementing RFC 6901 JSON Pointer escaping

## Task Commits

Each task was committed atomically:

1. **Task 1: SchemaType flags enum** - `12c854a` (feat)
2. **Task 2: SchemaNode immutable class with all Draft 2020-12 keyword fields** - `2688d6f` (feat)

## Files Created/Modified
- `src/Gluey.Contract/SchemaType.cs` - Flags enum for JSON Schema type keyword (7 types, byte-sized)
- `src/Gluey.Contract/SchemaNode.cs` - Immutable tree node with all keyword fields, constructor, sentinels, path helper
- `src/Gluey.Contract/Gluey.Contract.csproj` - Added InternalsVisibleTo for Gluey.Contract.Json.Tests

## Decisions Made
- Used `byte[]` / `byte[][]` for const/enum values to avoid JsonDocument lifetime management
- Used `@`-prefixed parameter names (`@ref`, `@enum`, `@const`, `@if`, `@else`) to handle C# reserved keywords cleanly
- SchemaNode is a class (not struct) per context decisions -- tree nodes reference children, allocated once at load time

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- SchemaType and SchemaNode ready for JSON Schema loader in Plan 02
- All keyword fields in place for validation phases (5-8) to consume
- InternalsVisibleTo configured for both test projects

## Self-Check: PASSED

All files exist. All commits verified.

---
*Phase: 02-schema-model*
*Completed: 2026-03-09*
