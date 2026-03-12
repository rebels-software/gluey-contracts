# ADR 5: Format-Agnostic Core

## Status
Accepted

## Context
The library must support multiple wire formats (JSON, Protobuf, PostgreSQL wire protocol, Redis RESP). The consuming code should not need to know which format it's working with.

## Decision
The core package (`Gluey.Contract`) defines format-agnostic types:

- `ParsedProperty` — struct with offset, length, and path
- Schema model — describes expected structure independent of wire format
- `Result<T>` and validation error types

Each wire format gets its own package (`Gluey.Contract.Json`, `Gluey.Contract.Protobuf`, etc.) that implements byte-reading against its specific format.

Consumer code interacts with `ParsedProperty` regardless of the underlying format.

## Consequences
- Core package has zero dependencies — no System.Text.Json, no protobuf libraries.
- Format packages depend on core, never on each other.
- Adding a new format means adding a new package, not modifying core.
