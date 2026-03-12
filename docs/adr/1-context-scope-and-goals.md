# ADR 1: Context, Scope and Goals

## Status
Accepted

## Context
Modern .NET applications deserialize incoming bytes (JSON, Protobuf, etc.) into objects, then validate those objects in a separate pass. This approach has two fundamental costs:

1. **Allocation pressure** — every property becomes a heap-allocated object, causing GC overhead.
2. **Lost context** — after deserialization, the original byte positions are gone. Validation errors reference C# property names, not the paths in the original payload.

## Decision
Gluey.Contract is a schema-driven, zero-allocation library that validates and indexes raw byte buffers in a single pass. It does not deserialize — it builds an offset table into the original bytes.

### Scope
- Core schema model and parsed data interface (`Gluey.Contract`)
- Format-specific byte parsers as separate packages (`Gluey.Contract.Json`, etc.)
- Validation errors with exact wire-format paths ([RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) JSON Pointers for JSON)

### Out of scope
- ASP.NET integration (separate package: `Gluey.Contract.AspNetCore`)
- Schema generation from Gluey DSL (belongs in the Gluey compiler)
- Business logic or domain validation

## Consequences
- The library must work standalone — no dependency on Gluey DSL
- Each wire format requires its own package with format-specific byte parsing
- The core package has zero external dependencies
