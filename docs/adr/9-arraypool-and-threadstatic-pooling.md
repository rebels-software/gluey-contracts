# ADR 9: ArrayPool and ThreadStatic Pooling for Zero-Allocation Hot Path

## Status
Accepted

## Context
Benchmark profiling revealed that the parse/validation hot path allocated heap memory on every call:

1. **ErrorCollector** — `new int[1]` for the mutable count holder in a `readonly struct` (~32B per call).
2. **ArrayBuffer** — a `class` instance with 6 fields (~56B per call) plus internal `Dictionary<int, (int,int)>` for region tracking.
3. **OffsetTable** — `ArrayPool<ParsedProperty>.Shared.Rent()` (pool-backed, but first calls allocate).

These per-call allocations accumulated to 1,336B for a small payload and 234,521B for a large payload — defeating the zero-allocation guarantee.

## Decision

### ErrorCollector: ArrayPool for count holder
Replace `new int[1]` with `ArrayPool<int>.Shared.Rent(1)`. The rented array is returned in `Dispose()`. After pool warmup, this is allocation-free.

### ArrayBuffer: ThreadStatic pooling + ArrayPool region tracking
1. **Replace Dictionary with ArrayPool arrays** — Region tracking uses `int[] _regionStarts` and `int[] _regionCounts` rented from `ArrayPool<int>`, eliminating the `Dictionary<int, (int,int)>` allocation.
2. **ThreadStatic instance cache** — `ArrayBuffer` constructor is private. Callers use `ArrayBuffer.Rent()` which returns a cached instance from `[ThreadStatic]` storage, or creates a new one on first use. `ArrayBuffer.Return()` clears logical state (preserving rented arrays) and stores the instance back in the thread-static field.
3. **Reset instead of reallocate** — `Reset()` clears counts and region markers without returning arrays to the pool. Arrays only grow, never shrink, so repeated calls reuse the same buffers.

### OffsetTable: ArrayPool (unchanged)
Already used `ArrayPool<ParsedProperty>.Shared.Rent()`. After pool warmup, allocation-free. No changes needed.

## Consequences
- **Zero measured allocations** — BenchmarkDotNet reports 0B allocated for TryParse (small/medium) and ValidateOnly (all sizes).
- **Thread safety** — `[ThreadStatic]` means each thread gets its own cached `ArrayBuffer`. No contention, no locks, but no cross-thread reuse.
- **Memory retention** — The cached `ArrayBuffer` holds onto its rented arrays between calls. This is the intended tradeoff: retain ~1KB of pooled memory per thread to avoid allocation on every call.
- **Dispose semantics** — `ArrayBuffer.Dispose()` attempts `Return()` first (cache for reuse). If the cache is occupied, falls back to `DisposeCore()` which returns arrays to the pool.
- **ArrayPool warmup** — The very first call per thread allocates. All subsequent calls are allocation-free. BenchmarkDotNet's warmup phase absorbs this.
