# Architecture

## Pattern

**Schema-driven, zero-allocation byte parser with format-agnostic core.**

The core library defines format-independent types (`ParsedProperty`, schema model, validation errors). Format-specific packages implement byte-reading for their wire format (JSON, Protobuf, etc.).

```
Gluey.Contract (core)
  |
  +-- Schema model         — types, constraints, paths
  +-- ParsedProperty       — readonly struct: offset + length into byte buffer
  +-- Offset table         — maps property names to byte positions
  +-- Result<T>            — success/failure with validation errors
  +-- Validation errors    — code + path + message
        |
        +-- Gluey.Contract.Json       — JSON byte parser (JSON Schema)
        +-- Gluey.Contract.Protobuf   — Protobuf parser (planned)
        +-- Gluey.Contract.Postgres   — PG wire protocol (planned)
```

## Key Abstractions (Designed, Not Yet Implemented)

### ParsedProperty (`src/Gluey.Contract/ParsedProperty.cs`)
- `readonly struct` — zero allocation, stack-allocated
- Holds offset + length into original byte buffer
- Values materialized on demand: `GetString()`, `GetInt32()`, etc.
- Includes RFC 6901 JSON Pointer path

### JsonContractSchema (`src/Gluey.Contract.Json/JsonContractSchema.cs`)
- Schema-driven JSON parser
- Accepts JSON Schema to describe expected structure
- Single-pass: validate + index simultaneously
- Dual API: `TryParse()` (.NET-idiomatic) and `Parse()` (Result pattern)

## Data Flow (Designed)

```
Raw bytes (caller-owned)
    |
    v
JsonContractSchema.Parse(bytes)
    |
    +-- Walk bytes once
    +-- Validate against schema
    +-- Build offset table
    |
    v
ParsedData (offset table into original bytes)
    |
    v
data["field"].GetString()  -- reads from byte buffer on demand
```

## Design Decisions (ADRs)

| # | Decision | File |
|---|----------|------|
| 1 | Context, scope, goals | `docs/adr/1-context-scope-and-goals.md` |
| 2 | Zero-allocation design | `docs/adr/2-zero-allocation-design.md` |
| 3 | Single-pass validation | `docs/adr/3-single-pass-validation.md` |
| 4 | Dual API surface (TryParse + Result) | `docs/adr/4-dual-api-surface.md` |
| 5 | Format-agnostic core | `docs/adr/5-format-agnostic-core.md` |
| 6 | JSON Pointer paths (RFC 6901) | `docs/adr/6-json-pointer-paths.md` |
| 7 | No external dependencies in core | `docs/adr/7-no-external-dependencies.md` |
| 8 | Readonly structs over classes | `docs/adr/8-readonly-structs-over-classes.md` |

## Invariants

| # | Rule | File |
|---|------|------|
| 1 | Zero allocations in parse path | `docs/invariants/1-zero-allocations-in-parse-path.md` |
| 2 | Buffer ownership by caller | `docs/invariants/2-buffer-ownership-by-caller.md` |
| 3 | Paths precomputed from schema | `docs/invariants/3-paths-precomputed-from-schema.md` |
| 4 | No external dependencies in core | `docs/invariants/4-no-external-dependencies-in-core.md` |
| 5 | All errors collected per parse | `docs/invariants/5-all-errors-collected-per-parse.md` |
| 6 | Parse never throws | `docs/invariants/6-parse-never-throws.md` |

## Current Implementation Status

**Extremely early stage.** Both `ParsedProperty` and `JsonContractSchema` are empty stubs. All architecture exists as documentation (ADRs, invariants, README) with no runtime code yet.
