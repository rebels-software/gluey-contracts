# Invariant 1: Zero Allocations in Parse Path

## Rule
No heap allocations occur during the parse pass — from the moment bytes enter the parser until the offset table is built.

## Rationale
Allocation-free parsing is the core value proposition. Any allocation in the hot path undermines the library's reason to exist.

## Verification
- Benchmark tests using `BenchmarkDotNet` with `[MemoryDiagnoser]` — `Allocated` column must be `0 B` for parse operations.
- Code review: no `new`, `string` creation, `List<T>`, boxing, or LINQ in the parse path.
