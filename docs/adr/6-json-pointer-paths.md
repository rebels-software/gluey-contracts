# ADR 6: JSON Pointer Paths (RFC 6901)

## Status
Accepted

## Context
Validation errors need to tell the API consumer exactly where in their payload the problem is. .NET conventions use C# property names (`Devices[0].SerialNumber`), which don't match the original JSON structure.

## Decision
Validation errors use [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) JSON Pointer paths: `/devices/0/serialNumber`.

- Paths are precomputed from the schema — the parser knows the path before it reads the value.
- Array elements use numeric indices: `/tags/0`, `/tags/1`.
- Nested objects use slashes: `/device/name`.

For non-JSON formats, paths follow the same convention adapted to the format's structure.

## Consequences
- API consumers can programmatically locate the exact field that failed validation.
- Paths match the wire format, not the C# model — this is intentional.
- ProblemDetails integration (in `Gluey.Contract.AspNetCore`) produces errors that clients can act on without guessing property name mappings.
