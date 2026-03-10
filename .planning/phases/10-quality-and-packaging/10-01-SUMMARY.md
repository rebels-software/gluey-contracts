---
phase: 10-quality-and-packaging
plan: 01
subsystem: core
tags: [zero-allocation, nuget, benchmarkdotnet, packaging, dispose]

# Dependency graph
requires:
  - phase: 09-single-pass-walker
    provides: ParseResult with int[1] dispose guard, OffsetTable, ErrorCollector, ArrayBuffer
provides:
  - Zero-allocation ParseResult dispose (no int[1] heap allocation)
  - Full NuGet package metadata for Gluey.Contract and Gluey.Contract.Json
  - BenchmarkDotNet project scaffold for allocation profiling
affects: [10-02]

# Tech tracking
tech-stack:
  added: [BenchmarkDotNet 0.14.0]
  patterns: [idempotent-dispose-without-guard, ci-conditional-build-properties]

key-files:
  created:
    - benchmarks/Gluey.Contract.Benchmarks/Gluey.Contract.Benchmarks.csproj
    - benchmarks/Gluey.Contract.Benchmarks/Program.cs
  modified:
    - src/Gluey.Contract/ParseResult.cs
    - src/Gluey.Contract/Gluey.Contract.csproj
    - src/Gluey.Contract.Json/Gluey.Contract.Json.csproj
    - Gluey.Contract.sln

key-decisions:
  - "Removed double-dispose guard entirely rather than replacing with bool field -- underlying Dispose methods are safe to call multiple times"
  - "BenchmarkDotNet 0.14.0 selected as latest stable supporting .NET 9"

patterns-established:
  - "CI-conditional ContinuousIntegrationBuild property group in csproj"
  - "Benchmark projects in benchmarks/ solution folder"

requirements-completed: [QUAL-03, QUAL-01]

# Metrics
duration: 2min
completed: 2026-03-10
---

# Phase 10 Plan 01: Quality Foundation Summary

**Zero-allocation ParseResult dispose, full NuGet metadata (Apache-2.0, 1.0.0-preview.1), and BenchmarkDotNet scaffold**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-10T21:48:45Z
- **Completed:** 2026-03-10T21:51:12Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Eliminated the int[1] heap allocation from ParseResult constructors, achieving zero-allocation dispose
- Configured both NuGet packages with full metadata: Apache-2.0 license, version 1.0.0-preview.1, repo URL, CI build support
- Scaffolded BenchmarkDotNet project referencing Gluey.Contract.Json for full-stack allocation profiling

## Task Commits

Each task was committed atomically:

1. **Task 1: Refactor ParseResult dispose guard and configure NuGet metadata** - `0df4520` (feat)
2. **Task 2: Scaffold benchmark project and add to solution** - `e57dd9f` (feat)

## Files Created/Modified
- `src/Gluey.Contract/ParseResult.cs` - Removed _disposedHolder field, simplified Dispose() to direct calls
- `src/Gluey.Contract/Gluey.Contract.csproj` - Full NuGet metadata, version 1.0.0-preview.1, Apache-2.0
- `src/Gluey.Contract.Json/Gluey.Contract.Json.csproj` - Full NuGet metadata, version 1.0.0-preview.1, Apache-2.0
- `benchmarks/Gluey.Contract.Benchmarks/Gluey.Contract.Benchmarks.csproj` - BenchmarkDotNet 0.14.0 project with Json reference
- `benchmarks/Gluey.Contract.Benchmarks/Program.cs` - BenchmarkSwitcher entry point
- `Gluey.Contract.sln` - Added benchmark project under benchmarks solution folder

## Decisions Made
- Removed double-dispose guard entirely rather than replacing with a bool field -- OffsetTable/ErrorCollector Dispose checks for null arrays, ArrayBuffer nulls out on dispose; calling Return twice on ArrayPool is safe
- BenchmarkDotNet 0.14.0 selected (latest stable supporting .NET 9)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Benchmark project ready for allocation profiling benchmarks in Plan 02
- Both packages packable; ready for publish workflow
- All 418 existing tests continue to pass

## Self-Check: PASSED

All 5 created/modified files verified on disk. Both task commits (0df4520, e57dd9f) verified in git log.

---
*Phase: 10-quality-and-packaging*
*Completed: 2026-03-10*
