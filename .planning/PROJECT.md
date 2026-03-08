# Gluey.Contract

## What This Is

A high-performance, zero-allocation .NET library for schema-driven validation and indexing of raw byte buffers. Instead of deserializing bytes into objects, it validates and indexes the original byte buffer in a single pass — giving direct access to values via offset references with full path tracking. Think FlatBuffers philosophy applied to JSON (and future formats).

## Core Value

Zero-allocation, single-pass validation and indexing of raw bytes against a schema — values accessed on demand from the original buffer, never deserialized into objects.

## Requirements

### Validated

- ✓ Project structure with core + JSON driver packages — existing
- ✓ Architecture decisions documented (8 ADRs) — existing
- ✓ System invariants documented (6 invariants) — existing
- ✓ Glossary with precise term definitions — existing

### Active

- [ ] ParsedProperty readonly struct with offset/length into byte buffer
- [ ] Offset table mapping property names to byte positions
- [ ] Result<T> type for success/failure with validation errors
- [ ] Validation error type with RFC 6901 JSON Pointer path, code, and message
- [ ] Schema model describing expected structure (types, constraints, paths)
- [ ] JsonContractSchema supporting full JSON Schema Draft 2020-12
- [ ] Single-pass validation + indexing of JSON bytes
- [ ] Dual API surface: TryParse (idiomatic) and Parse (Result pattern)
- [ ] Accept byte[], ReadOnlySpan<byte>, and ReadOnlyMemory<byte> inputs
- [ ] All errors collected per parse (not fail-fast), default max 64
- [ ] Zero heap allocations on the parse path
- [ ] BenchmarkDotNet allocation benchmarks
- [ ] Allocation regression tests (GC.GetAllocatedBytesForCurrentThread)
- [ ] NuGet packages ready for publishing

### Out of Scope

- Gluey.Contract.AspNetCore — separate package, future milestone
- Gluey.Contract.Protobuf — separate format driver, future milestone
- Gluey.Contract.Postgres — separate format driver, future milestone
- Gluey.Contract.Redis — separate format driver, future milestone
- Stream input support — deferring to keep single-pass simpler
- Gluey DSL integration — belongs in Gluey compiler
- Business logic / domain validation — application layer concern

## Context

- Part of the Gluey ecosystem but works standalone with standard JSON Schema
- Existing codebase has extensive documentation (ADRs, invariants, glossary) but only empty stubs for implementation
- Two packages: `Gluey.Contract` (core, zero dependencies) and `Gluey.Contract.Json` (JSON driver, references core)
- Test infrastructure set up with NUnit 4.3.1, FluentAssertions 8.0.1, coverlet
- Targeting .NET 9.0 with C# 13, nullable enabled
- CI/CD with GitHub Actions and Codecov (badges in README, workflows not yet created)

## Constraints

- **Zero allocation**: No heap allocations on parse path — enforced by design, benchmarks, and tests
- **No external dependencies in core**: Gluey.Contract has zero NuGet dependencies (ADR 7)
- **Readonly structs**: All core value types are `readonly struct` (ADR 8) — no classes, no record structs, no ref structs
- **Parse never throws**: Validation failures return errors, never exceptions (invariant 6)
- **Buffer ownership**: Caller owns the byte buffer; library never copies it (invariant 2)
- **JSON Schema Draft 2020-12**: Full spec compliance including $ref, allOf/anyOf/oneOf, conditionals

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| readonly struct over class/ref struct/record struct | Zero GC pressure, async-compatible, immutable (ADR 8) | — Pending |
| byte[] with offset/length over Span<byte> in structs | ref struct can't be used in async, collections, or fields (ADR 8) | — Pending |
| Result pattern over exceptions | Expected failures should not use exceptions (invariant 6) | — Pending |
| Format-agnostic core | New formats = new packages, not core changes (ADR 5) | — Pending |
| Paths precomputed from schema | Avoids string allocation during parse (invariant 3) | — Pending |
| Default max 64 validation errors | Generous for typical API payloads, prevents unbounded allocation on malicious input | — Pending |

---
*Last updated: 2026-03-08 after initialization*
