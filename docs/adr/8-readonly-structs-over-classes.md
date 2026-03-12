# ADR 8: Readonly Structs Over Classes

## Status
Accepted

## Context
The core data types in Gluey.Contract (`ParsedProperty`, `ValidationError`, `Result<T>`) are accessed frequently during parsing and validation. Choosing between `class` and `struct` directly impacts allocation, GC pressure, and cache locality.

## Decision
All core value types are `readonly struct`.

### Why not class?
- Every `class` instance is heap-allocated — one allocation per property per request.
- Heap objects add GC pressure. Under high throughput, Gen0 collections become measurable.
- Object headers cost 16 bytes per instance (sync block + method table) before any actual data.

### Why not regular struct?
- Regular structs allow mutation after creation, which makes reasoning about state harder.
- The compiler cannot optimize defensive copies away for mutable structs passed as `in` parameters.
- `readonly struct` guarantees immutability, enabling the compiler to skip defensive copies entirely.

### Why not ref struct?
- `ref struct` (stack-only) was considered for `ParsedProperty` since it can hold `Span<byte>`.
- Rejected because `ref struct` cannot be used in async methods, stored in fields, returned from properties, or used in collections.
- ASP.NET controllers are async — `ref struct` would make the library unusable in its primary target scenario.

### Why not record struct?
- `record struct` generates `Equals`, `GetHashCode`, `ToString` — none of which are needed in the hot path.
- The generated `ToString()` allocates strings — violates the zero-allocation invariant if called accidentally (e.g., by a debugger or logger).
- Explicit `readonly struct` keeps the type minimal and predictable.

## Consequences
- `ParsedProperty` holds a `byte[]` reference + offset + length + schema node. Copied by value (4 fields, ~24 bytes) — cheap.
- No boxing as long as callers avoid casting to interfaces. Generic constraints (`where T : struct, IParsedProperty`) prevent boxing.
- Cannot use `Span<byte>` directly in the struct. Use `byte[]` with offset/length instead and slice on access.
