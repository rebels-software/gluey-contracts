---
phase: 04-json-byte-reader
plan: 01
subsystem: json
tags: [utf8jsonreader, tokenizer, ref-struct, zero-alloc, byte-offset]

# Dependency graph
requires:
  - phase: 01-core-types
    provides: ParsedProperty contract (offset/length inside quotes)
provides:
  - JsonByteReader ref struct wrapping Utf8JsonReader
  - JsonByteTokenType enum decoupled from BCL
  - JsonReadError and JsonReadErrorKind for structural error reporting
affects: [05-type-validation, 09-single-pass-walker]

# Tech tracking
tech-stack:
  added: []
  patterns: [ref-struct-composition, content-inside-quotes-offset]

key-files:
  created:
    - src/Gluey.Contract.Json/JsonByteReader.cs
    - src/Gluey.Contract.Json/JsonByteTokenType.cs
    - src/Gluey.Contract.Json/JsonReadErrorKind.cs
    - src/Gluey.Contract.Json/JsonReadError.cs
    - tests/Gluey.Contract.Json.Tests/JsonByteReaderTests.cs
  modified: []

key-decisions:
  - "Single Number token type (no Integer/Number split) -- tokenizer does not interpret numeric type"
  - "AllowTrailingCommas=true and CommentHandling=Skip for lenient structural parsing"
  - "UnexpectedEndOfData vs InvalidJson classified by comparing BytesConsumed to input length"

patterns-established:
  - "Ref struct composition: wrap BCL ref struct (Utf8JsonReader) in own ref struct for decoupled API"
  - "Content-inside-quotes offset: TokenStartIndex+1 for String/PropertyName, matching ParsedProperty contract"

requirements-completed: [READ-01, READ-02, READ-03]

# Metrics
duration: 5min
completed: 2026-03-09
---

# Phase 4 Plan 01: JSON Byte Reader Summary

**JsonByteReader ref struct wrapping Utf8JsonReader with native byte offset tracking and content-inside-quotes offset calculation for zero-allocation tokenization**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-09T17:32:19Z
- **Completed:** 2026-03-09T17:37:19Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- JsonByteTokenType enum with 10 values decoupled from BCL JsonTokenType
- JsonReadError readonly struct with Kind, ByteOffset, Message for structural error reporting
- JsonByteReader ref struct with forward-only Read() loop, MapTokenType translation, and content-inside-quotes offset calculation
- 17 tests covering token sequence, offset correctness, multi-input support, and error detection

## Task Commits

Each task was committed atomically:

1. **Task 1: Create supporting types** - `d9e3bdd` (feat)
2. **Task 2: RED - Failing tests for JsonByteReader** - `0477559` (test)
3. **Task 2: GREEN - Implement JsonByteReader** - `e8dcc65` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Json/JsonByteTokenType.cs` - Token type enum (11 values including None)
- `src/Gluey.Contract.Json/JsonReadErrorKind.cs` - Structural error kind enum
- `src/Gluey.Contract.Json/JsonReadError.cs` - Structural error readonly struct
- `src/Gluey.Contract.Json/JsonByteReader.cs` - Ref struct wrapping Utf8JsonReader with offset tracking
- `tests/Gluey.Contract.Json.Tests/JsonByteReaderTests.cs` - 17 tests for reader behavior

## Decisions Made
- Single Number token type -- tokenizer does not interpret numeric subtype (integer vs float is validator's job in Phase 5)
- AllowTrailingCommas=true and CommentHandling=Skip for lenient structural parsing
- UnexpectedEndOfData vs InvalidJson error classification: compare BytesConsumed to input length at catch time
- No CurrentDepth exposure -- depth tracking is the walker's job (Phase 9)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `dotnet build -q` flag triggers "Question build" diagnostic mode on this SDK version (10.0.103), causing false build failures. Removed `-q` flag for verification commands. No impact on code correctness.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- JsonByteReader is complete and tested, ready for consumption by Phase 5 (type validation) and Phase 9 (single-pass walker)
- All 168 tests pass across both test projects (no regressions)
- Phase 4 blocker resolved: decision to wrap Utf8JsonReader confirmed and implemented

## Self-Check: PASSED

- All 5 created files exist on disk
- All 3 commits verified: d9e3bdd, 0477559, e8dcc65

---
*Phase: 04-json-byte-reader*
*Completed: 2026-03-09*
