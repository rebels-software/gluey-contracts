---
phase: 05-basic-validation
plan: 02
subsystem: validation
tags: [json-schema, required, additionalProperties, items, prefixItems, object-validation, array-validation]

# Dependency graph
requires:
  - phase: 01-core-types
    provides: ErrorCollector, ValidationError, ValidationErrorCode, ValidationErrorMessages
  - phase: 02-schema-model
    provides: SchemaNode with Required/Properties/AdditionalProperties/Items/PrefixItems fields, BuildChildPath
  - phase: 05-basic-validation/01
    provides: KeywordValidator static class foundation
provides:
  - KeywordValidator.ValidateRequired for per-property required checking with RFC 6901 paths
  - KeywordValidator.ValidateAdditionalProperty for spec-compliant additionalProperties enforcement
  - KeywordValidator.GetItemSchema for positional (prefixItems) vs uniform (items) schema resolution
affects: [09-single-pass-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: [error-accumulation-not-fail-fast, spec-default-allow-semantics, pure-lookup-function]

key-files:
  created:
    - tests/Gluey.Contract.Json.Tests/KeywordValidatorObjectTests.cs
    - tests/Gluey.Contract.Json.Tests/KeywordValidatorArrayTests.cs
  modified:
    - src/Gluey.Contract.Json/KeywordValidator.cs

key-decisions:
  - "ValidateRequired collects all missing property errors (not fail-fast) for better developer diagnostics"
  - "AdditionalProperties null means allow-all per JSON Schema spec default -- only BooleanSchema==false rejects"
  - "GetItemSchema is a pure lookup with no error collection -- walker handles element validation"

patterns-established:
  - "Error accumulation pattern: ValidateRequired iterates all required properties, collecting every miss"
  - "Spec-default semantics: null additionalProperties = allow all (not reject)"
  - "Pure lookup helper: GetItemSchema returns schema reference without side effects"

requirements-completed: [VALD-03, VALD-04, VALD-05]

# Metrics
duration: 3min
completed: 2026-03-09
---

# Phase 5 Plan 2: Required/AdditionalProperties/Items Validators Summary

**ValidateRequired with per-property error accumulation and RFC 6901 paths, ValidateAdditionalProperty with spec-default allow semantics, and GetItemSchema for positional vs uniform array element schema resolution**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-09T20:35:00Z
- **Completed:** 2026-03-09T20:38:00Z
- **Tasks:** 2 (both TDD: RED + GREEN)
- **Files modified:** 3

## Accomplishments
- ValidateRequired reports each missing property individually with correct RFC 6901 child paths via BuildChildPath
- ValidateAdditionalProperty implements spec-compliant defaults: null=allow, false=reject, true/schema=allow
- GetItemSchema resolves prefixItems positional schemas vs items uniform schema with correct boundary handling
- 22 new tests, 226 total suite green (147 JSON + 79 core)

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: Object validator tests** - `adbb953` (test)
2. **Task 1 GREEN: ValidateRequired + ValidateAdditionalProperty** - `e701951` (feat)
3. **Task 2 RED: Array validator tests** - `b291725` (test)
4. **Task 2 GREEN: GetItemSchema** - `bd00f92` (feat)

_TDD tasks: RED committed failing tests, GREEN committed passing implementation._

## Files Created/Modified
- `src/Gluey.Contract.Json/KeywordValidator.cs` - Added ValidateRequired, ValidateAdditionalProperty, GetItemSchema methods
- `tests/Gluey.Contract.Json.Tests/KeywordValidatorObjectTests.cs` - 13 tests for required and additionalProperties validation
- `tests/Gluey.Contract.Json.Tests/KeywordValidatorArrayTests.cs` - 9 tests for items/prefixItems schema resolution

## Decisions Made
- ValidateRequired collects ALL missing property errors (not fail-fast) so developers see every missing field in one pass
- AdditionalProperties null means allow-all per JSON Schema Draft 2020-12 spec default -- only BooleanSchema==false triggers rejection
- GetItemSchema is a pure lookup function with no ErrorCollector parameter -- element validation is the walker's responsibility in Phase 9

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All basic validation keywords implemented: type, enum, const (plan 01) + required, additionalProperties, items/prefixItems (plan 02)
- Phase 5 complete -- ready for Phase 6 numeric/string constraints
- Walker (Phase 9) will call these validators during single-pass traversal

---
*Phase: 05-basic-validation*
*Completed: 2026-03-09*
