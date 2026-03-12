# Benchmark Results

## Overview

This document provides benchmark evidence for Gluey.Contract.Json's allocation characteristics.
The library targets minimal allocations on the hot parse/validate path.

## Known Allocations

Per TryParse call, the following managed allocations occur:

| Source | Bytes (approx.) | Path | Notes |
|--------|-----------------|------|-------|
| ErrorCollector int[1] | ~32 | All paths | Mutable count holder for readonly struct |
| ArrayBuffer instance | ~48 | byte[] path only | Class instance for array element storage |
| OffsetTable ArrayPool.Rent | 0 (pooled) | byte[] path | Rented from ArrayPool; zero-alloc after warmup |
| ErrorCollector ArrayPool.Rent | 0 (pooled) | All paths | Rented from ArrayPool; zero-alloc after warmup |

**Validate-only path** (ReadOnlySpan overload): ~336 bytes per call
**Full parse path** (byte[] overload): ~672 bytes per call

Property access via ordinal indexer: **zero allocation**
Dispose: **zero allocation** (ArrayPool return only)
Format assertion: bounded allocation budget (~2KB) for string conversions

## Running Benchmarks Locally

Run all scenarios:
```bash
cd benchmarks/Gluey.Contract.Benchmarks
dotnet run -c Release -- --filter "*" --exporters markdown
```

Run a specific scenario:
```bash
dotnet run -c Release -- --filter "*FlatObject*" --exporters markdown
```

Results will be written to `BenchmarkDotNet.Artifacts/results/`.

**Note:** Disable antivirus real-time protection for accurate results. Windows Defender
can interfere with BenchmarkDotNet's process isolation. Alternatively, add
`[InProcessEmitToolchain]` to benchmark classes to avoid new process creation.

## Scenarios

| Scenario | Schema Complexity | What It Tests |
|----------|------------------|---------------|
| FlatObjectBenchmark | 5 properties, required, additionalProperties | Simple flat object validation |
| NestedObjectBenchmark | 2-3 level nesting (address/geo, contact) | Hierarchical object traversal |
| ArrayPayloadBenchmark | Array of item objects | Array validation + ArrayBuffer |
| FullSchemaBenchmark | allOf, if/then/else, pattern, min/max | Full validation pipeline |

Each scenario benchmarks three payload sizes (small ~100B, medium ~5KB, large ~50KB)
across three methods: TryParse (byte[]), ValidateOnly (ReadOnlySpan), and STJ baseline.

## Allocation Regression Tests

CI enforces allocation budgets via NUnit tests in `tests/Gluey.Contract.Json.Tests/AllocationTests/`:

| Test Class | Assertion |
|-----------|-----------|
| TryParseAllocationTests | byte[] path < 1024B, span path < 512B |
| PropertyAccessAllocationTests | Ordinal indexer = 0B, string indexer < 256B |
| DisposeAllocationTests | Dispose = 0B, double-dispose = 0B |
| FormatAssertionAllocationTests | Format assertion < 2000B |
