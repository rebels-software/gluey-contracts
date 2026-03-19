# Gluey.Contract.Binary

## What This Is

A new format package for Gluey.Contract that parses custom binary protocol payloads using JSON-based contract definitions. Follows the same zero-allocation, single-pass philosophy as Gluey.Contract.Json. Targets IoT and embedded systems where every byte matters and traffic costs are critical.

## Core Value

A consumer calls `parsed["fieldName"].GetInt32()` and gets the value — without knowing or caring whether the backing data is JSON or a custom binary protocol.

## Requirements

### Validated

- [x] Format flag in ParsedProperty: 1-byte `_format` field, branch in GetXxx() methods for binary vs UTF-8 reading — *Validated in Phase 1: format-flag*

### Active

- [ ] Binary contract JSON loaded via TryLoad/Load matching JsonContractSchema API pattern
- [ ] Dependency chain model: fields linked via `dependsOn`, parser computes offsets by walking chain
- [ ] All ADR-16 field types: scalars (uint8/16/32, int8/16/32, float32/64, boolean), strings (ASCII/UTF-8), enums, bit fields, arrays (fixed + semi-dynamic), structs, padding
- [ ] Truncated numerics: int32 stored in 3 bytes, sign-extended correctly
- [ ] Endianness: contract-level default with per-field override (big/little)
- [ ] Enum dual-access: `parsed["mode"]` → string, `parsed["modes"]` → raw numeric
- [ ] Bit fields: multi-byte containers (up to 16 bits), sub-fields at bit positions
- [ ] Fixed arrays: `count` as number, known at contract time
- [ ] Semi-dynamic arrays: `count` as string referencing another field
- [ ] Struct elements inside arrays with scoped dependency chains
- [ ] Path-based access: `parsed["recentErrors/0/code"]` matching JSON Pointer style
- [ ] Validation: min/max for numerics, pattern/minLength/maxLength for strings
- [ ] Contract-load validation: single root, no cycles, no shared parents, valid references
- [ ] Payload too short returns null (structurally invalid)
- [ ] Zero-allocation parse path (ArrayPool, same patterns as JSON package)
- [ ] High code coverage (unit + integration tests)
- [ ] NuGet package with CI pipeline matching Gluey.Contract.Json
- [ ] Published to NuGet, CI green, README with usage examples

### Out of Scope

- Serialization (object → byte[]) — parse only for v1
- Fully dynamic arrays (no count, no terminator) — impossible to parse reliably
- Nested structs outside of array elements — future extension
- Schema generation from Gluey DSL — belongs in Gluey compiler
- Protobuf/MessagePack compatibility — this is for custom binary formats

## Context

- Gluey.Contract core provides format-agnostic types: ParsedProperty, ParseResult, OffsetTable, ErrorCollector, ArrayBuffer
- Gluey.Contract.Json is the reference implementation — same API surface, loading pattern, and zero-allocation approach
- ADR-16 (`docs/adr/16-binary-format-contract.md`) defines the full contract JSON format
- ParsedProperty is a readonly struct — no inheritance. A 1-byte format flag will be added to dispatch between UTF-8 (JSON) and raw binary reading in GetXxx() methods
- Existing codebase targets net9.0 and net10.0, LangVersion 13
- The codebase map exists at `.planning/codebase/` with architecture, stack, conventions, testing docs

## Constraints

- **Zero allocation**: Parse path must not allocate. Use ArrayPool, structs, same patterns as JSON package.
- **No external dependencies**: Core package has zero deps. Binary package depends only on Gluey.Contract core.
- **API parity**: Load/TryLoad/Parse API surface must mirror JsonContractSchema for consistency.
- **Backward compatible**: Adding format flag to ParsedProperty must not break existing JSON consumers.
- **Target frameworks**: net9.0 and net10.0, matching existing packages.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| JSON-based contract definition | Simplicity, no custom DSL parser needed, tooling-friendly | — Pending |
| Dependency chain (no absolute offsets) | JSON key order doesn't matter, enables struct composition in arrays | — Pending |
| Format flag in ParsedProperty (1 byte) | Zero alloc, smallest struct growth, best cache perf, branch predictor handles same-format payloads | ✓ Phase 1 |
| Parse-only (no serialization) | Reduces scope, serialization needs contract for encoding which is a different concern | — Pending |
| Payload too short → null | Mirrors JSON malformed input behavior, consistent API | — Pending |
| count: number = fixed, count: string = ref | Clean discrimination, no wrapper objects, Gluey validates ref exists | — Pending |
| Enum source accessor = name + "s" | Convention for accessing raw byte value alongside mapped string | — Pending |

---
*Last updated: 2026-03-19 after Phase 1 completion*
