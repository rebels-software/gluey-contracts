# ADR 2: Zero-Allocation Design

## Status
Accepted

## Context
Traditional JSON parsing in .NET allocates a heap object for every string, array, and nested object in the payload. For a request with 20 properties, that's 20+ allocations per request — multiplied by thousands of requests per second.

## Decision
Gluey.Contract performs zero heap allocations during parsing:

- `ParsedProperty` is a `readonly struct` — stack-allocated, no GC pressure.
- Values are not materialized during parsing. `GetString()`, `GetInt32()`, etc. read from the byte buffer on demand.
- The offset table maps property names to byte positions. It can be stack-allocated or pooled when the schema is known (max property count is deterministic).
- The caller owns the byte buffer. Gluey.Contract never copies it.

## Consequences
- `ParsedProperty` cannot implement interfaces directly (boxing). Use generic constraints instead: `where T : struct, IParsedProperty`.
- The byte buffer must outlive all `ParsedProperty` instances. Caller is responsible for buffer lifetime.
- Values are re-parsed from bytes on every access. For hot-path repeated access of the same property, callers should cache the result in a local variable.
